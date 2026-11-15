using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PulseGlassWidget.Services
{
    /// <summary>
    /// Real-time ETW FPS monitor listening to Microsoft-Windows-DXGI Present events.
    /// Requires Administrator privileges (or Performance Log Users membership).
    /// </summary>
    public class EtwFpsMonitor : IDisposable
    {
        private const string SessionName = "PulseGlass_DXGI_FPS";
        private static readonly Guid DxgiProviderGuid = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

        private const uint WNODE_FLAG_TRACED_GUID = 0x00020000;
        private const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
        private const uint EVENT_TRACE_CONTROL_STOP = 1;
        private const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
        private const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
        private const uint EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
        private const byte TRACE_LEVEL_INFORMATION = 4;
        private const byte TRACE_LEVEL_VERBOSE = 5;

        private ulong _sessionHandle;
        private ulong _traceHandle = ulong.MaxValue;
        private Thread _thread;
        private volatile bool _running;
        private EventRecordCallback _callbackDelegate;

        private readonly ConcurrentDictionary<uint, ConcurrentQueue<long>> _pidFrames =
            new ConcurrentDictionary<uint, ConcurrentQueue<long>>();

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint StartTrace(out ulong sessionHandle, string sessionName, IntPtr properties);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ControlTrace(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

        [DllImport("advapi32.dll")]
        private static extern uint EnableTraceEx2(
            ulong traceHandle,
            ref Guid providerId,
            uint controlCode,
            byte level,
            ulong matchAnyKeyword,
            ulong matchAllKeyword,
            uint timeout,
            IntPtr enableParameters);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ulong OpenTrace(ref EVENT_TRACE_LOGFILE logfile);

        [DllImport("advapi32.dll")]
        private static extern uint ProcessTrace(ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

        [DllImport("advapi32.dll")]
        private static extern uint CloseTrace(ulong traceHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate void EventRecordCallback(ref EVENT_RECORD eventRecord);

        [StructLayout(LayoutKind.Sequential)]
        private struct WNODE_HEADER
        {
            public uint BufferSize;
            public uint ProviderId;
            public ulong HistoricalContext;
            public long TimeStamp;
            public Guid Guid;
            public uint ClientContext;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EVENT_TRACE_PROPERTIES
        {
            public WNODE_HEADER Wnode;
            public uint BufferSize;
            public uint MinimumBuffers;
            public uint MaximumBuffers;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint FlushTimer;
            public uint EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers;
            public uint FreeBuffers;
            public uint EventsLost;
            public uint BuffersWritten;
            public uint LogBuffersLost;
            public uint RealTimeBuffersLost;
            public IntPtr LoggerThreadId;
            public uint LogFileNameOffset;
            public uint LoggerNameOffset;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct EVENT_TRACE_LOGFILE
        {
            public string LogFileName;
            public string LoggerName;
            public long CurrentTime;
            public uint BuffersRead;
            public uint ProcessTraceMode;
            public EVENT_TRACE CurrentEvent;
            public TRACE_LOGFILE_HEADER LogfileHeader;
            public IntPtr BufferCallback;
            public uint BufferSize;
            public uint Filled;
            public uint EventsLost;
            public EventRecordCallback EventRecordCallback;
            public uint IsKernelTrace;
            public IntPtr Context;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EVENT_TRACE_HEADER_DUMMY
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EVENT_TRACE
        {
            public EVENT_TRACE_HEADER_DUMMY Header;
            public uint InstanceId;
            public uint ParentInstanceId;
            public Guid ParentGuid;
            public IntPtr MofData;
            public uint MofLength;
            public uint ClientContext;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TIME_ZONE_INFORMATION_DUMMY
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 172)]
            public byte[] Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TRACE_LOGFILE_HEADER
        {
            public uint BufferSize;
            public uint Version;
            public uint ProviderVersion;
            public uint NumberOfProcessors;
            public long EndTime;
            public uint TimerResolution;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint BuffersWritten;
            public Guid LogInstanceGuid;
            public IntPtr LoggerName;
            public IntPtr LogFileName;
            public TIME_ZONE_INFORMATION_DUMMY TimeZone;
            public long BootTime;
            public long PerfFreq;
            public long StartTime;
            public uint ReservedFlags;
            public uint BuffersLost;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EVENT_HEADER
        {
            public ushort Size;
            public ushort HeaderType;
            public ushort Flags;
            public ushort EventProperty;
            public uint ThreadId;
            public uint ProcessId;
            public long TimeStamp;
            public Guid ProviderId;
            public ushort Id;
            public byte Version;
            public byte Channel;
            public byte Level;
            public byte Opcode;
            public ushort Task;
            public ulong Keyword;
            public uint KernelTime;
            public uint UserTime;
            public Guid ActivityId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EVENT_RECORD
        {
            public EVENT_HEADER EventHeader;
            public uint BufferContext;
            public ushort ExtendedDataCount;
            public ushort UserDataLength;
            public IntPtr ExtendedData;
            public IntPtr UserData;
            public IntPtr UserContext;
        }

        public bool IsRunning { get { return _running; } }

        public bool Start()
        {
            if (_running) return true;
            try
            {
                StopExistingSession();

                int propsSize = Marshal.SizeOf(typeof(EVENT_TRACE_PROPERTIES));
                int totalSize = propsSize + 512;
                IntPtr propsPtr = Marshal.AllocHGlobal(totalSize);
                try
                {
                    for (int i = 0; i < totalSize; i++) Marshal.WriteByte(propsPtr, i, 0);
                    var props = new EVENT_TRACE_PROPERTIES();
                    props.Wnode.BufferSize = (uint)totalSize;
                    props.Wnode.Guid = Guid.NewGuid();
                    props.Wnode.ClientContext = 1; // QPC
                    props.Wnode.Flags = WNODE_FLAG_TRACED_GUID;
                    props.BufferSize = 64;
                    props.MinimumBuffers = 4;
                    props.MaximumBuffers = 16;
                    props.LogFileMode = EVENT_TRACE_REAL_TIME_MODE;
                    props.FlushTimer = 1;
                    props.LoggerNameOffset = (uint)propsSize;
                    Marshal.StructureToPtr(props, propsPtr, false);

                    uint status = StartTrace(out _sessionHandle, SessionName, propsPtr);
                    if (status != 0)
                    {
                        return false;
                    }

                    Guid dxgi = DxgiProviderGuid;
                    status = EnableTraceEx2(
                        _sessionHandle,
                        ref dxgi,
                        EVENT_CONTROL_CODE_ENABLE_PROVIDER,
                        TRACE_LEVEL_VERBOSE,
                        ulong.MaxValue, 0, 0, IntPtr.Zero);
                    if (status != 0)
                    {
                        StopExistingSession();
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(propsPtr);
                }

                _callbackDelegate = OnEventRecord;
                var logfile = new EVENT_TRACE_LOGFILE
                {
                    LoggerName = SessionName,
                    ProcessTraceMode = PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD,
                    EventRecordCallback = _callbackDelegate
                };

                _traceHandle = OpenTrace(ref logfile);
                if (_traceHandle == ulong.MaxValue)
                {
                    StopExistingSession();
                    return false;
                }

                _running = true;
                _thread = new Thread(ProcessLoop) { IsBackground = true, Name = "PulseGlass_ETW_FPS" };
                _thread.Start();
                return true;
            }
            catch
            {
                _running = false;
                return false;
            }
        }

        private void ProcessLoop()
        {
            try
            {
                ProcessTrace(new[] { _traceHandle }, 1, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
            finally
            {
                _running = false;
            }
        }

        private void OnEventRecord(ref EVENT_RECORD record)
        {
            // DXGI Present_Start event IDs: 42 (Present_Start), 44 (Present1_Start), 55 (Present_Start in newer Win10/11)
            // Opcode 1 = Start
            ushort id = record.EventHeader.Id;
            if ((id == 42 || id == 44 || id == 55) && record.EventHeader.Opcode == 1)
            {
                uint pid = record.EventHeader.ProcessId;
                if (pid == 0) return;
                long now = DateTime.UtcNow.Ticks;
                var q = _pidFrames.GetOrAdd(pid, _ => new ConcurrentQueue<long>());
                q.Enqueue(now);
            }
        }

        public int? GetCurrentFps()
        {
            if (!_running) return null;
            long cutoff = DateTime.UtcNow.AddSeconds(-1).Ticks;

            uint fgPid = 0;
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);
            }
            catch { }

            int fgFps = 0;
            int maxFps = 0;
            foreach (var kvp in _pidFrames)
            {
                long ts;
                while (kvp.Value.TryPeek(out ts) && ts < cutoff)
                {
                    kvp.Value.TryDequeue(out ts);
                }
                int count = kvp.Value.Count;
                if (kvp.Key == fgPid && count > 0) fgFps = count;
                if (count > maxFps) maxFps = count;
            }

            if (fgFps > 0) return fgFps;
            if (maxFps > 0) return maxFps;
            return null;
        }

        private static void StopExistingSession()
        {
            int propsSize = Marshal.SizeOf(typeof(EVENT_TRACE_PROPERTIES));
            int totalSize = propsSize + 512;
            IntPtr propsPtr = Marshal.AllocHGlobal(totalSize);
            try
            {
                for (int i = 0; i < totalSize; i++) Marshal.WriteByte(propsPtr, i, 0);
                var props = new EVENT_TRACE_PROPERTIES();
                props.Wnode.BufferSize = (uint)totalSize;
                props.LoggerNameOffset = (uint)propsSize;
                Marshal.StructureToPtr(props, propsPtr, false);
                ControlTrace(0, SessionName, propsPtr, EVENT_TRACE_CONTROL_STOP);
            }
            catch { }
            finally
            {
                Marshal.FreeHGlobal(propsPtr);
            }
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                if (_traceHandle != ulong.MaxValue)
                {
                    CloseTrace(_traceHandle);
                    _traceHandle = ulong.MaxValue;
                }
            }
            catch { }
            StopExistingSession();
        }
    }
}
