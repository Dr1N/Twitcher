using MultiWatcher.Interfaces;
using MultiWatcher.Utils;
using OeBrowser.Interfaces;
using OeBrowser.Utils;
using System;
using System.Threading;
using System.Windows;

namespace MultiWatcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static ILogWriter logger;
        private Thread closeEOThread;
        private int eOSleepTime = 50;

        public static ILogWriter LogWriter => logger;

        private static ILogger fileLogger;
        private static ILogger debugLogger;

        static App()
        {
            logger = new LoggerManager();
        }

        public App()
        {
            string fileName = DateTime.Now.Ticks.ToString() + ".log";
            fileLogger = new FileLogger(fileName);
            debugLogger = new SimpleLogger();
            logger.AddLogger(debugLogger);
            logger.AddLogger(fileLogger);

            Startup += App_Startup;
            Exit += App_Exit;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            ClearFileLogger();
        }

        private static void ClearFileLogger()
        {
            LogWriter.RemoveLogger(fileLogger);
            (fileLogger as IDisposable)?.Dispose();
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            closeEOThread = new Thread(CloseEO)
            {
                IsBackground = true
            };
            closeEOThread.Start();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogWriter.WriteLog("Dispatcher Exception: " + e.Exception.Message);
            MessageBox.Show(e.Exception.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogWriter.WriteLog("Domain Exception: " + ex.Message);
            MessageBox.Show(ex?.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CloseEO()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(eOSleepTime);
                    EOWindowDestroyer.CloseEOWindow();
                }
                catch (Exception ex)
                {
                    LogWriter.WriteLog($"Close Window Error: {ex.Message}");
                }
            }
        }
    }
}
