using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// "Which build, on what machine": the facts a bug report needs before any log line, as ordered key/value
    /// pairs. Used for the session-log header, and by the report collector. Main thread only (Screen,
    /// QualitySettings). Nothing personal: no user name, no paths, no network ids.
    /// </summary>
    public static class SystemSnapshot
    {
        /// <summary>Product, company, version, build GUID, Unity version, platform, build type, system language.</summary>
        public static void AppendApp(List<(string Key, string Value)> into)
        {
            into.Add(("Product", Application.productName));
            into.Add(("Company", Application.companyName));
            into.Add(("Version", Application.version));
            into.Add(("Build GUID", string.IsNullOrEmpty(Application.buildGUID) ? "(editor)" : Application.buildGUID));
            into.Add(("Unity", Application.unityVersion));
            into.Add(("Platform", Application.platform.ToString()));
            into.Add(("Build type", Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development" : "Release"));
            into.Add(("System language", Application.systemLanguage.ToString()));
        }

        /// <summary>OS, CPU, RAM, GPU + VRAM, graphics API, display, window mode, quality level.</summary>
        public static void AppendSystem(List<(string Key, string Value)> into)
        {
            var inv = CultureInfo.InvariantCulture;
            into.Add(("OS", SystemInfo.operatingSystem));
            into.Add(("CPU", string.Format(inv, "{0} ({1} threads, {2} MHz)",
                SystemInfo.processorType, SystemInfo.processorCount, SystemInfo.processorFrequency)));
            into.Add(("RAM", string.Format(inv, "{0} MB", SystemInfo.systemMemorySize)));
            into.Add(("GPU", string.Format(inv, "{0} ({1}), {2} MB VRAM",
                SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceVendor, SystemInfo.graphicsMemorySize)));
            into.Add(("Graphics API", string.Format(inv, "{0} ({1}), shader level {2}",
                SystemInfo.graphicsDeviceType, SystemInfo.graphicsDeviceVersion, SystemInfo.graphicsShaderLevel)));

            var display = Screen.currentResolution;
            into.Add(("Display", string.Format(inv, "{0}x{1} @ {2:0.##} Hz",
                display.width, display.height, display.refreshRateRatio.value)));
            into.Add(("Window", string.Format(inv, "{0}x{1}, {2}", Screen.width, Screen.height, Screen.fullScreenMode)));

            int quality = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            into.Add(("Quality", quality >= 0 && quality < names.Length ? names[quality] : quality.ToString(inv)));
        }
    }
}
