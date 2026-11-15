using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace PulseGlassWidget
{
    public partial class App : Application
    {
        private static Mutex _singleInstanceMutex;
        private static bool _ownsMutex;

        public static void ReleaseSingleInstanceMutex()
        {
            if (_singleInstanceMutex != null)
            {
                if (_ownsMutex)
                {
                    try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                    _ownsMutex = false;
                }
                try { _singleInstanceMutex.Dispose(); } catch { }
                _singleInstanceMutex = null;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "PulseGlassWidget_SingleInstance_Mutex", out createdNew);
            if (!createdNew)
            {
                // Brief wait in case the previous non-elevated instance is handing off after UAC elevation
                try { createdNew = _singleInstanceMutex.WaitOne(1500); } catch { createdNew = false; }
            }
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                MessageBox.Show("PulseGlass zaten çalışıyor! Sistem tepsisinden (sağ alt) açabilirsiniz.",
                    "PulseGlass", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"),
                        args.Exception.ToString());
                }
                catch { }
                MessageBox.Show("Uygulama başlatılırken hata oluştu:\n" + args.Exception.Message,
                    "PulseGlass Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                Shutdown();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ReleaseSingleInstanceMutex();
            base.OnExit(e);
        }
    }
}
