using UnityEngine;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeEnvironmentContext : MonoBehaviour
    {
        private static readonly int EnvironmentTimeId = Shader.PropertyToID("_MazeEnv_Time");
        private static readonly int WindDirectionId = Shader.PropertyToID("_MazeEnv_WindDirection");
        private static readonly int WindSpeedId = Shader.PropertyToID("_MazeEnv_WindSpeed");
        private static readonly int SunDirectionId = Shader.PropertyToID("_MazeEnv_SunDirectionWS");
        private static readonly int SunColorId = Shader.PropertyToID("_MazeEnv_SunColor");
        private static readonly int SunIntensityId = Shader.PropertyToID("_MazeEnv_SunIntensity");

        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private bool assignRenderSettingsSun = true;
        [SerializeField] private bool useRealtimeClock = true;
        [SerializeField] private float manualTime;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.35f);
        [SerializeField, Min(0f)] private float windSpeed = 1f;

        public static MazeEnvironmentContext Active { get; private set; }

        public Light MainDirectionalLight => ResolveDirectionalLight();
        public float EnvironmentTime => (useRealtimeClock ? Time.realtimeSinceStartup : manualTime) * timeScale;
        public Vector2 WindDirection => windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;
        public float WindSpeed => windSpeed;
        public Vector3 SunDirectionWS => MainDirectionalLight != null ? -MainDirectionalLight.transform.forward : new Vector3(0.3f, 0.8f, 0.2f).normalized;
        public Color SunColor => MainDirectionalLight != null ? MainDirectionalLight.color : Color.white;
        public float SunIntensity => MainDirectionalLight != null ? MainDirectionalLight.intensity : 1f;

        private void Reset()
        {
            mainDirectionalLight = FindMainDirectionalLight();
        }

        private void OnEnable()
        {
            Active = this;
            ApplyGlobalShaderState();
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void LateUpdate()
        {
            using var sample = MazeProfiler.Sample("MAZE.Environment.Context");
            ApplyGlobalShaderState();
        }

        private void OnValidate()
        {
            timeScale = Mathf.Max(0f, timeScale);
            windSpeed = Mathf.Max(0f, windSpeed);
            ApplyGlobalShaderState();
        }

        public void ApplyGlobalShaderState()
        {
            var light = ResolveDirectionalLight();
            if (assignRenderSettingsSun && light != null && RenderSettings.sun != light)
            {
                RenderSettings.sun = light;
            }

            Shader.SetGlobalFloat(EnvironmentTimeId, EnvironmentTime);
            var wind = WindDirection;
            Shader.SetGlobalVector(WindDirectionId, new Vector4(wind.x, wind.y, 0f, 0f));
            Shader.SetGlobalFloat(WindSpeedId, windSpeed);

            var sunDirection = SunDirectionWS;
            Shader.SetGlobalVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            var sunColor = SunColor.linear * Mathf.Max(0f, SunIntensity);
            Shader.SetGlobalColor(SunColorId, sunColor);
            Shader.SetGlobalFloat(SunIntensityId, Mathf.Max(0f, SunIntensity));
        }

        private Light ResolveDirectionalLight()
        {
            if (mainDirectionalLight != null)
            {
                return mainDirectionalLight;
            }

            mainDirectionalLight = FindMainDirectionalLight();
            return mainDirectionalLight;
        }

        private static Light FindMainDirectionalLight()
        {
            if (RenderSettings.sun != null)
            {
                return RenderSettings.sun;
            }

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var light in lights)
            {
                if (light != null && light.type == LightType.Directional && light.enabled)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
