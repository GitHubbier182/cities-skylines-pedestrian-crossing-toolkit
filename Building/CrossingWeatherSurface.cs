using UnityEngine;
using UnityEngine.Rendering;

namespace PedestrianCrossingToolkit
{
    public static partial class CrossingPathBuilder
    {
        private enum WeatherSurfaceProfile
        {
            General,
            Marking,
            Metal,
            Roof
        }

        private sealed class CrossingWeatherSurface : MonoBehaviour
        {
            private const int WeatherRefreshFrames = 12;
            private const float RoofDryingRatePerSecond = 0.004f;
            private static int _lastWeatherFrame = -WeatherRefreshFrames;
            private static float _cachedWetness;
            private static float _cachedSnow;
            private static float _lastWeatherTime = -1f;

            private Renderer _renderer;
            private MaterialPropertyBlock _properties;
            private Color _dryColor = Color.white;
            private WeatherSurfaceProfile _profile;
            private bool _snowExposed;
            private float _lastWetness = -1f;
            private float _lastSnow = -1f;

            internal void Initialize(Renderer renderer, WeatherSurfaceProfile profile, bool snowExposed)
            {
                _renderer = renderer;
                _profile = profile;
                _snowExposed = snowExposed;
                Material material = renderer == null ? null : renderer.sharedMaterial;
                if (material != null)
                    _dryColor = material.color;
                ApplyWeather(true);
            }

            private void Awake()
            {
                _renderer = GetComponent<Renderer>();
                if (_properties == null)
                    _properties = new MaterialPropertyBlock();
            }

            private void OnEnable()
            {
                ApplyWeather(true);
            }

            private void Update()
            {
                ApplyWeather(false);
            }

            private void ApplyWeather(bool force)
            {
                if (_renderer == null)
                    _renderer = GetComponent<Renderer>();
                if (_renderer == null || _renderer.sharedMaterial == null)
                    return;

                UpdateWeatherCache();
                float wetness = _cachedWetness;
                float snow = _snowExposed ? _cachedSnow : 0f;
                if (!force
                    && Mathf.Abs(wetness - _lastWetness) < 0.01f
                    && Mathf.Abs(snow - _lastSnow) < 0.01f)
                {
                    return;
                }

                _lastWetness = wetness;
                _lastSnow = snow;
                Material material = _renderer.sharedMaterial;
                float wetMultiplier = _profile == WeatherSurfaceProfile.Marking ? 0.82f : 0.70f;
                Color wetColor = new Color(
                    _dryColor.r * wetMultiplier,
                    _dryColor.g * wetMultiplier,
                    _dryColor.b * wetMultiplier,
                    _dryColor.a);
                Color tint = Color.Lerp(_dryColor, wetColor, wetness);
                if (snow > 0f)
                {
                    Color snowColor = new Color(0.88f, 0.90f, 0.92f, _dryColor.a);
                    tint = Color.Lerp(tint, snowColor, snow * 0.86f);
                }

                float dryMetallic = _profile == WeatherSurfaceProfile.Roof
                    ? 0.88f
                    : (_profile == WeatherSurfaceProfile.Metal ? 0.58f : 0f);
                float dryGloss = _profile == WeatherSurfaceProfile.Roof
                    ? 0.82f
                    : (_profile == WeatherSurfaceProfile.Metal
                        ? 0.48f
                        : (_profile == WeatherSurfaceProfile.Marking ? 0.16f : 0.10f));
                float gloss = Mathf.Lerp(dryGloss, 0.94f, wetness);
                if (snow > 0f)
                    gloss = Mathf.Lerp(gloss, 0.22f, snow);

                if (_properties == null)
                    _properties = new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_properties);
                if (material.HasProperty("_Color"))
                    _properties.SetColor("_Color", tint);
                if (material.HasProperty("_TintColor"))
                    _properties.SetColor("_TintColor", tint);
                if (material.HasProperty("_Metallic"))
                    _properties.SetFloat("_Metallic", dryMetallic);
                if (material.HasProperty("_Glossiness"))
                    _properties.SetFloat("_Glossiness", gloss);
                if (material.HasProperty("_GlossMapScale"))
                    _properties.SetFloat("_GlossMapScale", gloss);
                _renderer.SetPropertyBlock(_properties);
            }

            private static void UpdateWeatherCache()
            {
                if (Time.frameCount - _lastWeatherFrame < WeatherRefreshFrames)
                    return;

                _lastWeatherFrame = Time.frameCount;
                WeatherManager weather = WeatherManager.instance;
                if (weather == null)
                {
                    _cachedWetness = 0f;
                    _cachedSnow = 0f;
                    return;
                }

                float rain = Mathf.Clamp01(weather.m_currentRain);
                float groundWetness = Mathf.Clamp01(weather.m_groundWetness);
                float nativeWetness = Mathf.Clamp01(Mathf.Max(groundWetness, rain * 0.85f));
                float now = Time.time;
                float elapsed = _lastWeatherTime < 0f ? 0f : Mathf.Max(0f, now - _lastWeatherTime);
                _lastWeatherTime = now;
                _cachedWetness = nativeWetness >= _cachedWetness
                    ? nativeWetness
                    : Mathf.MoveTowards(_cachedWetness, nativeWetness, elapsed * RoofDryingRatePerSecond);
                bool rainIsSnow = weather.m_properties != null
                                  && weather.m_properties.m_rainIsSnow;
                _cachedSnow = rainIsSnow
                    ? Mathf.Clamp01(Mathf.Max(rain, groundWetness * 0.65f))
                    : 0f;
            }

            internal static void ResetCachedWeather()
            {
                _lastWeatherFrame = -WeatherRefreshFrames;
                _cachedWetness = 0f;
                _cachedSnow = 0f;
                _lastWeatherTime = -1f;
            }
        }

        internal static void ResetWeatherSurfaceState()
        {
            CrossingWeatherSurface.ResetCachedWeather();
        }

        private static void ConfigureWeatherSurfaceRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            Material material = renderer.sharedMaterial;
            string materialName = material.name ?? string.Empty;
            string objectName = renderer.gameObject.name ?? string.Empty;
            WeatherSurfaceProfile profile = WeatherSurfaceProfile.General;
            bool snowExposed = false;
            if (material == _bridgeRoofMaterial
                || material == _subwayEntranceRoofMaterial
                || materialName.Contains("Shiny Metallic Bridge Roof")
                || materialName.Contains("Shiny Metallic Subway Roof")
                || objectName.Contains("pitched roof")
                || objectName.Contains("pitched canopy"))
            {
                profile = WeatherSurfaceProfile.Roof;
                snowExposed = true;
            }
            else if (material == _bridgeTrimMaterial
                     || material == _subwayEntranceCanopyMaterial
                     || material == _signalPoleMaterial)
            {
                profile = WeatherSurfaceProfile.Metal;
            }
            else if (material == _crossingStripeMaterial
                     || material == _vergeCrossingMaterial)
            {
                profile = WeatherSurfaceProfile.Marking;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = profile == WeatherSurfaceProfile.Metal
                                            || profile == WeatherSurfaceProfile.Roof
                ? ReflectionProbeUsage.BlendProbes
                : ReflectionProbeUsage.Off;

            // Wet-weather presentation belongs only on the three exposed pitched roof
            // surfaces: bridge deck, bridge access/stairs and subway canopy. All
            // other PCT structure surfaces are sheltered or already sit on a road.
            if (profile != WeatherSurfaceProfile.Roof)
                return;

            CrossingWeatherSurface surface = renderer.gameObject.GetComponent<CrossingWeatherSurface>();
            if (surface == null)
                surface = renderer.gameObject.AddComponent<CrossingWeatherSurface>();
            surface.Initialize(renderer, profile, snowExposed);
        }
    }
}
