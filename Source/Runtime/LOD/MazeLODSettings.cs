using System;
using UnityEngine;

namespace Maze
{
    [Serializable]
    public struct MazeLODSettings
    {
        [Range(1, 4)] public int levelCount;
        [Min(1)] public int standardTriangleTarget;
        [Min(1)] public int mediumTriangleTarget;
        [Min(1)] public int farawayTriangleTarget;
        [Range(0.01f, 1f)] public float firstTransitionHeight;
        [Range(0.05f, 0.95f)] public float transitionScale;
        public bool crossFade;
        [Range(0f, 1f)] public float fadeTransitionWidth;
        public bool cull;
        [Range(0f, 0.5f)] public float cullScreenHeight;

        public static MazeLODSettings Default => new()
        {
            levelCount = 4,
            standardTriangleTarget = 1600,
            mediumTriangleTarget = 800,
            farawayTriangleTarget = 200,
            firstTransitionHeight = 0.6f,
            transitionScale = 0.5f,
            crossFade = true,
            fadeTransitionWidth = 0.15f,
            cull = true,
            cullScreenHeight = 0.01f
        };

        public int LevelCount => Mathf.Max(1, levelCount);

        public int GetTriangleTarget(int generatedLevelIndex)
        {
            return generatedLevelIndex switch
            {
                0 => standardTriangleTarget,
                1 => mediumTriangleTarget,
                2 => farawayTriangleTarget,
                _ => throw new ArgumentOutOfRangeException(nameof(generatedLevelIndex))
            };
        }

        public float GetTransitionHeight(int levelIndex)
        {
            var count = LevelCount;
            if (levelIndex < 0 || levelIndex >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex));
            }

            if (levelIndex == count - 1)
            {
                return cull ? Mathf.Clamp01(cullScreenHeight) : 0f;
            }

            return Mathf.Clamp01(firstTransitionHeight * Mathf.Pow(transitionScale, levelIndex));
        }

        public bool TryValidate(out string error)
        {
            if (levelCount < 1 || levelCount > 4)
            {
                error = "Level Count must be between one and four.";
                return false;
            }

            if (levelCount >= 2 && standardTriangleTarget < 1)
            {
                error = "Standard Target must be at least one triangle.";
                return false;
            }

            if (levelCount >= 3 && (mediumTriangleTarget < 1 || mediumTriangleTarget >= standardTriangleTarget))
            {
                error = "Medium Target must be positive and lower than Standard Target.";
                return false;
            }

            if (levelCount >= 4 && (farawayTriangleTarget < 1 || farawayTriangleTarget >= mediumTriangleTarget))
            {
                error = "Faraway Target must be positive and lower than Medium Target.";
                return false;
            }

            if (firstTransitionHeight <= 0f || firstTransitionHeight > 1f)
            {
                error = "First Transition must be greater than zero and no greater than one.";
                return false;
            }

            if (transitionScale <= 0f || transitionScale >= 1f)
            {
                error = "Transition Scale must be between zero and one.";
                return false;
            }

            if (fadeTransitionWidth < 0f || fadeTransitionWidth > 1f)
            {
                error = "Fade Width must be between zero and one.";
                return false;
            }

            if (cull && (cullScreenHeight < 0f || cullScreenHeight >= firstTransitionHeight))
            {
                error = "Cull Screen Height must be non-negative and lower than First Transition.";
                return false;
            }

            var previous = 1f;
            for (var level = 0; level < LevelCount; level++)
            {
                var current = GetTransitionHeight(level);
                if (current >= previous)
                {
                    error = $"LOD{level} transition must be lower than the preceding level.";
                    return false;
                }

                previous = current;
            }

            error = string.Empty;
            return true;
        }
    }
}
