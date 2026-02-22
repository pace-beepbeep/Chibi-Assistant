using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Chibi_Assistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Tangkap semua exception yang tidak ter-handle di UI thread
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Tangkap exception dari background thread / Task
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[Chibi] UI Exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
            e.Handled = true; // Jangan crash, tandai sebagai sudah di-handle
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine($"[Chibi] Task Exception: {e.Exception?.Message}");
            e.SetObserved(); // Tandai sebagai sudah diobservasi agar tidak crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Debug.WriteLine($"[Chibi] Domain Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
