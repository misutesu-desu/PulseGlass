using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseGlassWidget.Models
{
    public class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public class CpuInfo : NotifyBase
    {
        public string Name { get; set; }
        public int PhysicalCores { get; set; }
        public int LogicalProcessors { get; set; }
        public double BaseClockGHz { get; set; }
        public string TemperatureNote { get; set; }

        public CpuInfo()
        {
            Name = "İşlemci Yükleniyor...";
            TemperatureNote = "";
            _coreUsages = new List<double>();
        }

        private double _usagePercent;
        public double UsagePercent { get { return _usagePercent; } set { SetField(ref _usagePercent, value); } }

        private double _currentClockGHz;
        public double CurrentClockGHz { get { return _currentClockGHz; } set { SetField(ref _currentClockGHz, value); } }

        private double? _temperatureC;
        public double? TemperatureC
        {
            get { return _temperatureC; }
            set { if (SetField(ref _temperatureC, value)) OnPropertyChanged("TemperatureText"); }
        }

        public string TemperatureText { get { return TemperatureC.HasValue ? string.Format("{0:0}°C", TemperatureC.Value) : "N/A"; } }

        private List<double> _coreUsages;
        public List<double> CoreUsages { get { return _coreUsages; } set { SetField(ref _coreUsages, value); } }
    }

    public class GpuInfo : NotifyBase
    {
        public string Name { get; set; }
        public double TotalVramGB { get; set; }
        public string DriverVersion { get; set; }
        public string SensorNote { get; set; }
        public bool IsIntegrated { get; set; }
        public bool IsAvailable { get; set; }

        public GpuInfo()
        {
            Name = "Ekran Kartı Yükleniyor...";
            DriverVersion = "";
            SensorNote = "";
        }

        private double _usagePercent;
        public double UsagePercent { get { return _usagePercent; } set { SetField(ref _usagePercent, value); } }

        private double _usedVramGB;
        public double UsedVramGB
        {
            get { return _usedVramGB; }
            set { if (SetField(ref _usedVramGB, value)) OnPropertyChanged("VramText"); }
        }

        private double? _temperatureC;
        public double? TemperatureC
        {
            get { return _temperatureC; }
            set { if (SetField(ref _temperatureC, value)) OnPropertyChanged("TemperatureText"); }
        }

        private double? _clockMHz;
        public double? ClockMHz { get { return _clockMHz; } set { SetField(ref _clockMHz, value); } }

        private double? _fanPercent;
        public double? FanPercent { get { return _fanPercent; } set { SetField(ref _fanPercent, value); } }

        public string TemperatureText { get { return TemperatureC.HasValue ? string.Format("{0:0}°C", TemperatureC.Value) : "N/A"; } }
        public string VramText { get { return TotalVramGB > 0 ? string.Format("{0:0.0} / {1:0.0} GB", UsedVramGB, TotalVramGB) : "N/A"; } }
    }

    public class RamSlotInfo
    {
        public string Locator { get; set; }
        public double CapacityGB { get; set; }
        public int SpeedMHz { get; set; }
        public string MemoryType { get; set; }
        public string Manufacturer { get; set; }
    }

    public class RamInfo : NotifyBase
    {
        public double InstalledGB { get; set; }
        public double TotalGB { get; set; }
        public int SpeedMHz { get; set; }
        public string MemoryType { get; set; }
        public List<RamSlotInfo> Slots { get; set; }

        public RamInfo()
        {
            MemoryType = "";
            Slots = new List<RamSlotInfo>();
        }

        private double _usedGB;
        public double UsedGB
        {
            get { return _usedGB; }
            set
            {
                if (SetField(ref _usedGB, value))
                {
                    OnPropertyChanged("FreeGB");
                    OnPropertyChanged("SummaryText");
                }
            }
        }

        private double _usagePercent;
        public double UsagePercent { get { return _usagePercent; } set { SetField(ref _usagePercent, value); } }

        public double FreeGB { get { return Math.Max(0, TotalGB - UsedGB); } }
        public string SummaryText { get { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} / {1:0.0} GB", UsedGB, TotalGB); } }
    }

    public class DiskDetailInfo
    {
        public string Model { get; set; }
        public string DriveLetters { get; set; }
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double? TemperatureC { get; set; }
        public string MediaType { get; set; }
    }

    public class StorageInfo : NotifyBase
    {
        public string PrimaryModel { get; set; }
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }

        public StorageInfo()
        {
            PrimaryModel = "Disk Yükleniyor...";
            _disks = new List<DiskDetailInfo>();
        }

        private double _usagePercent;
        public double UsagePercent { get { return _usagePercent; } set { SetField(ref _usagePercent, value); } }

        private double _readMBps;
        public double ReadMBps
        {
            get { return _readMBps; }
            set { if (SetField(ref _readMBps, value)) OnPropertyChanged("IoText"); }
        }

        private double _writeMBps;
        public double WriteMBps
        {
            get { return _writeMBps; }
            set { if (SetField(ref _writeMBps, value)) OnPropertyChanged("IoText"); }
        }

        private double? _temperatureC;
        public double? TemperatureC
        {
            get { return _temperatureC; }
            set { if (SetField(ref _temperatureC, value)) OnPropertyChanged("TemperatureText"); }
        }

        private List<DiskDetailInfo> _disks;
        public List<DiskDetailInfo> Disks { get { return _disks; } set { SetField(ref _disks, value); } }

        public string TemperatureText { get { return TemperatureC.HasValue ? string.Format("{0:0}°C", TemperatureC.Value) : "N/A"; } }
        public string IoText { get { return string.Format("R: {0:0.0} | W: {1:0.0} MB/s", ReadMBps, WriteMBps); } }
    }

    public class NetworkInfo : NotifyBase
    {
        public string AdapterName { get; set; }

        public NetworkInfo()
        {
            AdapterName = "Ağ Bağdaştırıcısı";
        }

        private double _downloadMBps;
        public double DownloadMBps { get { return _downloadMBps; } set { SetField(ref _downloadMBps, value); } }

        private double _uploadMBps;
        public double UploadMBps { get { return _uploadMBps; } set { SetField(ref _uploadMBps, value); } }
    }

    public class ProcessUsageItem
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public int InstanceCount { get; set; }
        public double CpuPercent { get; set; }
        public double RamMB { get; set; }
        public double DiskMBps { get; set; }
        public double? GpuPercent { get; set; }

        public ProcessUsageItem()
        {
            InstanceCount = 1;
        }

        public string DisplayName { get { return InstanceCount > 1 ? string.Format("{0} ({1})", Name, InstanceCount) : Name; } }
        public string CpuText { get { return string.Format("{0:0.0}%", CpuPercent); } }
        public string RamText { get { return RamMB >= 1024 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00} GB", RamMB / 1024.0) : string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0} MB", RamMB); } }
        public string DiskText { get { return string.Format("{0:0.0} MB/s", DiskMBps); } }
        public string GpuText { get { return GpuPercent.HasValue ? string.Format("{0:0.0}%", GpuPercent.Value) : "—"; } }
    }

    public class SystemModeProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int CpuMinPercent { get; set; }
        public int CpuMaxPercent { get; set; }
        public bool ActiveCooling { get; set; }
        public bool BoostEnabled { get; set; }
        public bool AutoFan { get; set; }
        public int CpuFanPercent { get; set; }
        public int GpuFanPercent { get; set; }
    }

    public class HardwareSnapshot
    {
        public CpuInfo Cpu { get; set; }
        public GpuInfo Gpu { get; set; }
        public GpuInfo IntegratedGpu { get; set; }
        public RamInfo Ram { get; set; }
        public StorageInfo Storage { get; set; }
        public NetworkInfo Network { get; set; }
        public List<ProcessUsageItem> Processes { get; set; }
        public List<string> Alerts { get; set; }
        public int? Fps { get; set; }

        public HardwareSnapshot()
        {
            Cpu = new CpuInfo();
            Gpu = new GpuInfo();
            IntegratedGpu = new GpuInfo { IsIntegrated = true, IsAvailable = false, Name = "Dahili GPU Bulunamadı" };
            Ram = new RamInfo();
            Storage = new StorageInfo();
            Network = new NetworkInfo();
            Processes = new List<ProcessUsageItem>();
            Alerts = new List<string>();
        }
    }
}
