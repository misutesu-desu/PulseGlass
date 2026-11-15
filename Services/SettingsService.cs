using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace PulseGlassWidget.Services
{
    public class AppSettingsData
    {
        public double WidgetOpacity = 0.78;
        public double WindowOpacity = 0.94;
        public double Scale = 1.0;
        public int RefreshMs = 1000;
        public string ActiveModeId = "Balanced";
        public string PrimaryHex = "#FF4D6D";
        public string SecondaryHex = "#FFB085";
        public string BgHex = "#120F18";
        public double CpuTempThresh = 85;
        public double GpuTempThresh = 85;
        public double CpuUseThresh = 90;
        public double RamUseThresh = 90;
        public double? Left = null;
        public double? Top = null;
        public bool SumCpuPct = true;
        public bool SumCpuTemp = true;
        public bool SumGpuPct = true;
        public bool SumGpuTemp = true;
        public bool SumIGpuPct = false;
        public bool SumRamPct = true;
        public bool SumSsdTemp = false;
        public bool SumFps = true;
        public string Language = "tr";
    }

    public static class SettingsService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PulseGlassWidget");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.cfg");
        private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegName = "PulseGlassWidget";

        public static AppSettingsData Load()
        {
            var data = new AppSettingsData();
            try
            {
                if (!File.Exists(ConfigPath)) return data;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(ConfigPath))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0)
                    {
                        map[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                    }
                }
                double d; int i; bool b;
                if (map.ContainsKey("WidgetOpacity") && double.TryParse(map["WidgetOpacity"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.WidgetOpacity = d;
                if (map.ContainsKey("WindowOpacity") && double.TryParse(map["WindowOpacity"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.WindowOpacity = d;
                if (map.ContainsKey("Scale") && double.TryParse(map["Scale"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.Scale = d;
                if (map.ContainsKey("RefreshMs") && int.TryParse(map["RefreshMs"], out i)) data.RefreshMs = i;
                if (map.ContainsKey("ActiveModeId")) data.ActiveModeId = map["ActiveModeId"];
                if (map.ContainsKey("Language")) data.Language = map["Language"];
                if (map.ContainsKey("PrimaryHex")) data.PrimaryHex = map["PrimaryHex"];
                if (map.ContainsKey("SecondaryHex")) data.SecondaryHex = map["SecondaryHex"];
                if (map.ContainsKey("BgHex")) data.BgHex = map["BgHex"];
                if (map.ContainsKey("CpuTempThresh") && double.TryParse(map["CpuTempThresh"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.CpuTempThresh = d;
                if (map.ContainsKey("GpuTempThresh") && double.TryParse(map["GpuTempThresh"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.GpuTempThresh = d;
                if (map.ContainsKey("CpuUseThresh") && double.TryParse(map["CpuUseThresh"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.CpuUseThresh = d;
                if (map.ContainsKey("RamUseThresh") && double.TryParse(map["RamUseThresh"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.RamUseThresh = d;
                if (map.ContainsKey("Left") && double.TryParse(map["Left"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.Left = d;
                if (map.ContainsKey("Top") && double.TryParse(map["Top"], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) data.Top = d;
                if (map.ContainsKey("SumCpuPct") && bool.TryParse(map["SumCpuPct"], out b)) data.SumCpuPct = b;
                if (map.ContainsKey("SumCpuTemp") && bool.TryParse(map["SumCpuTemp"], out b)) data.SumCpuTemp = b;
                if (map.ContainsKey("SumGpuPct") && bool.TryParse(map["SumGpuPct"], out b)) data.SumGpuPct = b;
                if (map.ContainsKey("SumGpuTemp") && bool.TryParse(map["SumGpuTemp"], out b)) data.SumGpuTemp = b;
                if (map.ContainsKey("SumIGpuPct") && bool.TryParse(map["SumIGpuPct"], out b)) data.SumIGpuPct = b;
                if (map.ContainsKey("SumRamPct") && bool.TryParse(map["SumRamPct"], out b)) data.SumRamPct = b;
                if (map.ContainsKey("SumSsdTemp") && bool.TryParse(map["SumSsdTemp"], out b)) data.SumSsdTemp = b;
                if (map.ContainsKey("SumFps") && bool.TryParse(map["SumFps"], out b)) data.SumFps = b;
            }
            catch { }
            return data;
        }

        public static void Save(AppSettingsData data)
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                var lines = new List<string>
                {
                    "WidgetOpacity=" + data.WidgetOpacity.ToString(CultureInfo.InvariantCulture),
                    "WindowOpacity=" + data.WindowOpacity.ToString(CultureInfo.InvariantCulture),
                    "Scale=" + data.Scale.ToString(CultureInfo.InvariantCulture),
                    "RefreshMs=" + data.RefreshMs,
                    "ActiveModeId=" + data.ActiveModeId,
                    "PrimaryHex=" + data.PrimaryHex,
                    "SecondaryHex=" + data.SecondaryHex,
                    "BgHex=" + data.BgHex,
                    "CpuTempThresh=" + data.CpuTempThresh.ToString(CultureInfo.InvariantCulture),
                    "GpuTempThresh=" + data.GpuTempThresh.ToString(CultureInfo.InvariantCulture),
                    "CpuUseThresh=" + data.CpuUseThresh.ToString(CultureInfo.InvariantCulture),
                    "RamUseThresh=" + data.RamUseThresh.ToString(CultureInfo.InvariantCulture),
                    "SumCpuPct=" + data.SumCpuPct,
                    "SumCpuTemp=" + data.SumCpuTemp,
                    "SumGpuPct=" + data.SumGpuPct,
                    "SumGpuTemp=" + data.SumGpuTemp,
                    "SumIGpuPct=" + data.SumIGpuPct,
                    "SumRamPct=" + data.SumRamPct,
                    "SumSsdTemp=" + data.SumSsdTemp,
                    "SumFps=" + data.SumFps,
                    "Language=" + data.Language
                };
                if (data.Left.HasValue) lines.Add("Left=" + data.Left.Value.ToString(CultureInfo.InvariantCulture));
                if (data.Top.HasValue) lines.Add("Top=" + data.Top.Value.ToString(CultureInfo.InvariantCulture));
                File.WriteAllLines(ConfigPath, lines);
            }
            catch { }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegKey, false))
                {
                    return key != null && key.GetValue(AppRegName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable, string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegKey, true))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(AppRegName, "\"" + exePath + "\"");
                    else key.DeleteValue(AppRegName, false);
                }
            }
            catch { }
        }

        public static bool CreateDesktopShortcut(string exePath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnkPath = Path.Combine(desktop, "PulseGlass Monitor.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.IconLocation = exePath + ",0";
                shortcut.Description = "PulseGlass Donanım İzleyici & Sistem Kontrol";
                shortcut.Save();
                return true;
            }
            catch { return false; }
        }
    }
}
