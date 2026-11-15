using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using PulseGlassWidget.Models;

namespace PulseGlassWidget.Services
{
    public class SystemControlService
    {
        private const string SUB_PROCESSOR = "54533251-82be-4824-96c1-47b60b740d00";
        private const string PROCTHROTTLEMIN = "893dee8e-2bef-41e0-89c6-b55d0929964c";
        private const string PROCTHROTTLEMAX = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        private const string SYSCOOLPOL = "94d3a615-a699-4ab6-9f6a-0a95b3d69c03";
        private const string PERFBOOSTMODE = "be337238-0d82-4146-a960-4f3749d470c7";

        private static readonly Guid OVERLAY_POWER_SAVER = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        private static readonly Guid OVERLAY_BALANCED = new Guid("00000000-0000-0000-0000-000000000000");
        private static readonly Guid OVERLAY_BEST_PERF = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        public List<SystemModeProfile> Profiles { get; private set; }
        public string ActiveModeId { get; private set; }
        public bool HardwareFanControlAvailable { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool SuppressOthers { get; set; }
        public string LastStatusMessage { get; private set; }

        public SystemControlService()
        {
            IsAdmin = CheckIsAdmin();
            Profiles = new List<SystemModeProfile>
            {
                new SystemModeProfile
                {
                    Id = "Quiet",
                    DisplayName = "Sessiz",
                    Description = "Düşük fan sesi, enerji tasarrufu ve pasif soğutma önceliği.",
                    CpuMinPercent = 5,
                    CpuMaxPercent = 75,
                    ActiveCooling = false,
                    BoostEnabled = false,
                    AutoFan = true,
                    CpuFanPercent = 35,
                    GpuFanPercent = 35
                },
                new SystemModeProfile
                {
                    Id = "Balanced",
                    DisplayName = "Dengeli",
                    Description = "Günlük kullanım için dengeli işlemci gücü ve otomatik fan.",
                    CpuMinPercent = 5,
                    CpuMaxPercent = 100,
                    ActiveCooling = true,
                    BoostEnabled = true,
                    AutoFan = true,
                    CpuFanPercent = 55,
                    GpuFanPercent = 55
                },
                new SystemModeProfile
                {
                    Id = "Performance",
                    DisplayName = "Performans",
                    Description = "Oyun ve ağır işler için yüksek işlemci ve aktif soğutma.",
                    CpuMinPercent = 50,
                    CpuMaxPercent = 100,
                    ActiveCooling = true,
                    BoostEnabled = true,
                    AutoFan = false,
                    CpuFanPercent = 80,
                    GpuFanPercent = 80
                },
                new SystemModeProfile
                {
                    Id = "Turbo",
                    DisplayName = "Turbo",
                    Description = "Maksimum CPU frekansı, agresif boost ve tam güç soğutma profili.",
                    CpuMinPercent = 100,
                    CpuMaxPercent = 100,
                    ActiveCooling = true,
                    BoostEnabled = true,
                    AutoFan = false,
                    CpuFanPercent = 100,
                    GpuFanPercent = 100
                }
            };
            ActiveModeId = "Balanced";
            HardwareFanControlAvailable = CheckAcerWmiSupport();
            LastStatusMessage = "Sistem modu: Dengeli";
        }

        private bool CheckIsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private bool CheckAcerWmiSupport()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM meta_class WHERE __Class LIKE '%Acer%' OR __Class LIKE '%Nitro%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public int SuppressConflictingApps()
        {
            int stopped = 0;
            string[] competingNames =
            {
                "NitroSense", "NitroSenseV31", "AcerQuickAccess",
                "PredatorSense", "ArmouryCrate", "OmenCommandCenter"
            };

            foreach (var name in competingNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Kill();
                            stopped++;
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }
                catch { }
            }
            return stopped;
        }

        public string ApplyMode(SystemModeProfile profile)
        {
            if (profile == null) return "Geçersiz profil.";
            ActiveModeId = profile.Id;

            int suppressedCount = 0;
            if (SuppressOthers)
            {
                suppressedCount = SuppressConflictingApps();
            }

            // 1. Apply Windows 10/11 Power Mode Overlay (works immediately on Modern Standby laptops)
            bool overlayOk = false;
            try
            {
                Guid overlay = OVERLAY_BALANCED;
                if (profile.Id == "Quiet") overlay = OVERLAY_POWER_SAVER;
                else if (profile.Id == "Performance" || profile.Id == "Turbo") overlay = OVERLAY_BEST_PERF;

                uint res = NativeMethods.PowerSetActiveOverlayScheme(overlay);
                overlayOk = (res == 0);
            }
            catch { }

            // 1b. Also switch classic power scheme if available (e.g. desktop / non-Modern-Standby)
            if (profile.Id == "Quiet") RunPowerCfg("/setactive a1841308-3541-4fab-bc81-f71556f20b4a");
            else if (profile.Id == "Balanced") RunPowerCfg("/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            else if (profile.Id == "Performance" || profile.Id == "Turbo") RunPowerCfg("/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

            // 2. Apply granular processor throttle, cooling policy, and boost via powercfg
            int minCpu = Math.Max(5, Math.Min(100, profile.CpuMinPercent));
            int maxCpu = Math.Max(minCpu, Math.Min(100, profile.CpuMaxPercent));
            int cooling = profile.ActiveCooling ? 1 : 0;
            int boost = profile.BoostEnabled ? 2 : 0;

            int successCount = 0;
            if (RunPowerCfg(string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PROCTHROTTLEMIN, minCpu))) successCount++;
            if (RunPowerCfg(string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PROCTHROTTLEMAX, maxCpu))) successCount++;
            if (RunPowerCfg(string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PROCTHROTTLEMIN, minCpu))) successCount++;
            if (RunPowerCfg(string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PROCTHROTTLEMAX, maxCpu))) successCount++;
            if (RunPowerCfg(string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, SYSCOOLPOL, cooling))) successCount++;
            if (RunPowerCfg(string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, SYSCOOLPOL, cooling))) successCount++;
            if (RunPowerCfg(string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PERFBOOSTMODE, boost))) successCount++;
            if (RunPowerCfg(string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", SUB_PROCESSOR, PERFBOOSTMODE, boost))) successCount++;
            bool activated = RunPowerCfg("/setactive SCHEME_CURRENT");

            string supNote = suppressedCount > 0 ? string.Format(" ({0} rakip uygulama bastırıldı)", suppressedCount) : "";
            string fanNote = profile.AutoFan
                ? "Fan: Otomatik"
                : string.Format("Hedef Fan: CPU %{0} / GPU %{1}", profile.CpuFanPercent, profile.GpuFanPercent);

            if (overlayOk || (activated && successCount > 0))
            {
                LastStatusMessage = string.Format("{0} aktif{1}! Güç Modu: {2}, CPU %{3}-%{4}, Boost: {5} • {6}",
                    profile.DisplayName,
                    supNote,
                    overlayOk ? "Uygulandı" : "Standart",
                    minCpu, maxCpu,
                    profile.BoostEnabled ? "Açık" : "Kapalı",
                    fanNote);
            }
            else
            {
                LastStatusMessage = "Ayarlar tam uygulanamadı. 'Yönetici Olarak Başlat' butonunu deneyin.";
            }

            return LastStatusMessage;
        }

        private bool RunPowerCfg(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit(1000);
                        return p.ExitCode == 0;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
