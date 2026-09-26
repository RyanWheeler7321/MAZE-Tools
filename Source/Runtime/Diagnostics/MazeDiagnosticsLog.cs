using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Maze
{
    public enum MazeDiagnosticLevel
    {
        Info,
        Warn,
        Error
    }

    public static class MazeDiagnosticsLog
    {
        private const string LogFolderName = "MazeDiagnostics";
        private const int MaxMessageChars = 420;
        private const int MaxDataChars = 1600;
        private const int MaxEventsPerFrame = 80;
        private const int MaxRecentEvents = 12;

        private static readonly object FileLock = new();
        private static readonly HashSet<string> OnceKeys = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> LastStateByKey = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> LastThrottleTimeByKey = new(StringComparer.Ordinal);
        private static readonly Queue<string> RecentEvents = new(MaxRecentEvents);

        private static string logPath;
        private static string latestPath;
        private static int sessionFrame = int.MinValue;
        private static int eventsThisFrame;
        private static int pendingDroppedEvents;
        private static bool sessionStarted;
        private static bool writeFailureLogged;

        public static string LogPath
        {
            get
            {
                EnsureSession();
                return logPath;
            }
        }

        public static string LatestPath
        {
            get
            {
                EnsureSession();
                return latestPath;
            }
        }

        public static int InfoCount { get; private set; }
        public static int WarnCount { get; private set; }
        public static int ErrorCount { get; private set; }
        public static int DroppedCount { get; private set; }

        public static string[] Recent
        {
            get
            {
                EnsureSession();
                return RecentEvents.ToArray();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaySession()
        {
            OnceKeys.Clear();
            LastStateByKey.Clear();
            LastThrottleTimeByKey.Clear();
            RecentEvents.Clear();
            logPath = null;
            latestPath = null;
            sessionFrame = int.MinValue;
            eventsThisFrame = 0;
            pendingDroppedEvents = 0;
            sessionStarted = false;
            writeFailureLogged = false;
            InfoCount = 0;
            WarnCount = 0;
            ErrorCount = 0;
            DroppedCount = 0;
        }

        public static void Info(string category, string name, string message, string dataFields = null)
        {
            Log(MazeDiagnosticLevel.Info, category, name, message, dataFields);
        }

        public static void Warn(string category, string name, string message, string dataFields = null)
        {
            Log(MazeDiagnosticLevel.Warn, category, name, message, dataFields);
        }

        public static void Error(string category, string name, string message, string dataFields = null)
        {
            Log(MazeDiagnosticLevel.Error, category, name, message, dataFields);
        }

        public static void InfoOnce(string key, string category, string name, string message, string dataFields = null)
        {
            if (MarkOnce(key))
            {
                Info(category, name, message, dataFields);
            }
        }

        public static void WarnOnce(string key, string category, string name, string message, string dataFields = null)
        {
            if (MarkOnce(key))
            {
                Warn(category, name, message, dataFields);
            }
        }

        public static void ErrorOnce(string key, string category, string name, string message, string dataFields = null)
        {
            if (MarkOnce(key))
            {
                Error(category, name, message, dataFields);
            }
        }

        public static void InfoOnChange(string key, string state, string category, string name, string message, string dataFields = null)
        {
            LogOnChange(MazeDiagnosticLevel.Info, key, state, category, name, message, dataFields);
        }

        public static void WarnOnChange(string key, string state, string category, string name, string message, string dataFields = null)
        {
            LogOnChange(MazeDiagnosticLevel.Warn, key, state, category, name, message, dataFields);
        }

        public static void InfoThrottled(string key, float intervalSeconds, string category, string name, string message, string dataFields = null)
        {
            if (CanLogThrottled(key, intervalSeconds))
            {
                Info(category, name, message, dataFields);
            }
        }

        public static void WarnThrottled(string key, float intervalSeconds, string category, string name, string message, string dataFields = null)
        {
            if (CanLogThrottled(key, intervalSeconds))
            {
                Warn(category, name, message, dataFields);
            }
        }

        public static void ErrorThrottled(string key, float intervalSeconds, string category, string name, string message, string dataFields = null)
        {
            if (CanLogThrottled(key, intervalSeconds))
            {
                Error(category, name, message, dataFields);
            }
        }

        public static void Snapshot(string category, string name, string dataFields)
        {
            Info(category, name, "snapshot", dataFields);
        }

        public static string JsonString(string key, string value)
        {
            return $"\"{Escape(key)}\":\"{Escape(value)}\"";
        }

        public static string JsonNumber(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return $"\"{Escape(key)}\":null";
            }

            return $"\"{Escape(key)}\":{value.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        public static string JsonBool(string key, bool value)
        {
            return $"\"{Escape(key)}\":{(value ? "true" : "false")}";
        }

        public static string JoinData(params string[] fields)
        {
            if (fields == null || fields.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(fields.Length);
            for (var i = 0; i < fields.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(fields[i]))
                {
                    parts.Add(fields[i]);
                }
            }

            return string.Join(",", parts);
        }

        private static void LogOnChange(MazeDiagnosticLevel level, string key, string state, string category, string name, string message, string dataFields)
        {
            key = string.IsNullOrWhiteSpace(key) ? category + ":" + name : key;
            state ??= string.Empty;
            if (LastStateByKey.TryGetValue(key, out var previous) && string.Equals(previous, state, StringComparison.Ordinal))
            {
                return;
            }

            LastStateByKey[key] = state;
            Log(level, category, name, message, dataFields);
        }

        private static bool MarkOnce(string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? "once:missing" : key;
            return OnceKeys.Add(key);
        }

        private static bool CanLogThrottled(string key, float intervalSeconds)
        {
            key = string.IsNullOrWhiteSpace(key) ? "throttle:missing" : key;
            intervalSeconds = Mathf.Max(0.1f, intervalSeconds);
            var now = Time.realtimeSinceStartup;
            if (LastThrottleTimeByKey.TryGetValue(key, out var lastTime) && now - lastTime < intervalSeconds)
            {
                return false;
            }

            LastThrottleTimeByKey[key] = now;
            return true;
        }

        private static void Log(MazeDiagnosticLevel level, string category, string name, string message, string dataFields)
        {
            EnsureSession();
            if (!CanWriteThisFrame())
            {
                return;
            }

            if (pendingDroppedEvents > 0)
            {
                var dropped = pendingDroppedEvents;
                pendingDroppedEvents = 0;
                WriteEvent(MazeDiagnosticLevel.Warn, "Diagnostics", "dropped_events", "diagnostic events dropped by frame budget", JsonNumber("count", dropped));
            }

            WriteEvent(level, category, name, message, dataFields);
        }

        private static bool CanWriteThisFrame()
        {
            var frame = Application.isPlaying ? Time.frameCount : -1;
            if (frame != sessionFrame)
            {
                sessionFrame = frame;
                eventsThisFrame = 0;
            }

            eventsThisFrame++;
            if (eventsThisFrame <= MaxEventsPerFrame)
            {
                return true;
            }

            pendingDroppedEvents++;
            DroppedCount++;
            return false;
        }

        private static void EnsureSession()
        {
            if (sessionStarted)
            {
                return;
            }

            sessionStarted = true;
            var folder = Path.Combine(Application.persistentDataPath, LogFolderName);
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, $"maze_diag_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
            latestPath = Path.Combine(folder, "latest.json");
            WriteEvent(MazeDiagnosticLevel.Info, "Diagnostics", "start", "diagnostics session started", JoinData(
                JsonString("unity", Application.unityVersion),
                JsonString("platform", Application.platform.ToString()),
                JsonString("path", logPath)));
        }

        private static void WriteEvent(MazeDiagnosticLevel level, string category, string name, string message, string dataFields)
        {
            category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            name = string.IsNullOrWhiteSpace(name) ? "event" : name.Trim();
            message = Trim(message, MaxMessageChars);
            dataFields = NormalizeDataFields(dataFields);
            if (dataFields.Length > MaxDataChars)
            {
                var originalChars = dataFields.Length;
                dataFields = JoinData(
                    JsonBool("truncated", true),
                    JsonNumber("originalChars", originalChars),
                    JsonString("text", dataFields.Substring(0, Mathf.Max(0, MaxDataChars - 120))));
            }

            switch (level)
            {
                case MazeDiagnosticLevel.Warn:
                    WarnCount++;
                    break;
                case MazeDiagnosticLevel.Error:
                    ErrorCount++;
                    break;
                default:
                    InfoCount++;
                    break;
            }

            var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var scene = SceneManager.GetActiveScene();
            var frame = Application.isPlaying ? Time.frameCount : -1;
            var line = $"{{\"utc\":\"{timestamp}\",\"frame\":{frame},\"level\":\"{level.ToString().ToLowerInvariant()}\",\"category\":\"{Escape(category)}\",\"name\":\"{Escape(name)}\",\"message\":\"{Escape(message)}\",\"scene\":\"{Escape(scene.name)}\"";
            if (!string.IsNullOrWhiteSpace(dataFields))
            {
                line += $",\"data\":{{{dataFields}}}";
            }

            line += "}";
            try
            {
                lock (FileLock)
                {
                    File.AppendAllText(logPath, line + Environment.NewLine);
                    File.WriteAllText(latestPath, $"{{\"utc\":\"{timestamp}\",\"logPath\":\"{Escape(logPath)}\",\"info\":{InfoCount},\"warn\":{WarnCount},\"error\":{ErrorCount},\"dropped\":{DroppedCount},\"latest\":{line}}}");
                }

                RememberRecent(level, category, name, message);
            }
            catch (Exception exception)
            {
                if (!writeFailureLogged)
                {
                    writeFailureLogged = true;
                    Debug.LogWarning($"[MAZE DIAG] Failed to write diagnostics log. {exception.Message}");
                }
            }
        }

        private static void RememberRecent(MazeDiagnosticLevel level, string category, string name, string message)
        {
            if (RecentEvents.Count >= MaxRecentEvents)
            {
                RecentEvents.Dequeue();
            }

            RecentEvents.Enqueue($"{level.ToString().ToUpperInvariant()} {category}.{name}: {message}");
        }

        private static string NormalizeDataFields(string dataFields)
        {
            if (string.IsNullOrWhiteSpace(dataFields))
            {
                return string.Empty;
            }

            var trimmed = dataFields.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static string Trim(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxChars - 1) + "...";
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal);
        }
    }
}
