using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace PulseGlassWidget
{
    public partial class App : Application
    {
        private static Mutex _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "PulseGlassWidget_SingleInstance_Mutex", out createdNew);
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
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
