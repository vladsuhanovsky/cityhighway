using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Observable capture conditions. Does not verify a manually labelled Magnate seed or camera.
    /// </summary>
    [Serializable]
    public sealed class City3DPerformanceEnvironment
    {
        public string unityVersion;
        public string platform;
        public string graphicsDevice;
        public int width;
        public int height;
        public int vSyncCount;
        public int targetFrameRate;

        public static City3DPerformanceEnvironment Capture() =>
            new City3DPerformanceEnvironment
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                width = Screen.width,
                height = Screen.height,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate
            };

        public bool IsValid() =>
            !string.IsNullOrWhiteSpace(unityVersion) &&
            !string.IsNullOrWhiteSpace(platform) &&
            !string.IsNullOrWhiteSpace(graphicsDevice) &&
            width > 0 && height > 0 && vSyncCount >= 0 &&
            vSyncCount <= 4 && targetFrameRate >= -1;

        public enum Compatibility { BothLegacy, Incomplete, Match, Different }

        public static Compatibility Compare(City3DPerformanceEnvironment before,
            City3DPerformanceEnvironment after)
        {
            if (before == null && after == null) return Compatibility.BothLegacy;
            if (before == null || after == null ||
                !before.IsValid() || !after.IsValid()) return Compatibility.Incomplete;
            return string.Equals(before.unityVersion, after.unityVersion, StringComparison.Ordinal) &&
                string.Equals(before.platform, after.platform, StringComparison.Ordinal) &&
                string.Equals(before.graphicsDevice, after.graphicsDevice, StringComparison.Ordinal) &&
                before.width == after.width && before.height == after.height &&
                before.vSyncCount == after.vSyncCount &&
                before.targetFrameRate == after.targetFrameRate
                ? Compatibility.Match : Compatibility.Different;
        }

        public string Describe() => unityVersion + " / " + platform + " / " +
            graphicsDevice + " / " + width + "x" + height + " / VSync " +
            vSyncCount + " / cap " + targetFrameRate;
    }
}
