using UnityEngine;

namespace IsekaiChaos.Core
{
    public enum PerformanceTier { Low = 0, Mid = 1, High = 2 }

    /// <summary>
    /// Classifies the device once at boot. Drives LOD bias, streaming radius, shadow distance,
    /// cloth/hair physics budget and texture mip limits. Players can override in Settings.
    /// </summary>
    public static class DeviceTier
    {
        const string OverrideKey = "perf.tierOverride";
        static PerformanceTier? _cached;

        public static PerformanceTier Current
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;
                int ov = PlayerPrefs.GetInt(OverrideKey, -1);
                _cached = ov >= 0 ? (PerformanceTier)ov : Detect();
                return _cached.Value;
            }
        }

        public static void SetOverride(PerformanceTier? tier)
        {
            PlayerPrefs.SetInt(OverrideKey, tier.HasValue ? (int)tier.Value : -1);
            PlayerPrefs.Save();
            _cached = null;
        }

        static PerformanceTier Detect()
        {
#if UNITY_ANDROID || UNITY_IOS
            int ramMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            bool vulkan = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;

            if (ramMb >= 7500 && cores >= 8 && vulkan) return PerformanceTier.High;
            if (ramMb >= 3500 && cores >= 6) return PerformanceTier.Mid;
            return PerformanceTier.Low;
#else
            return SystemInfo.systemMemorySize >= 12000 ? PerformanceTier.High : PerformanceTier.Mid;
#endif
        }
    }
}
