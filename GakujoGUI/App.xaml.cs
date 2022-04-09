using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MessageBox = ModernWpf.MessageBox;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoggingConfiguration loggingConfiguration = new();
            FileTarget fileTarget = new();
            loggingConfiguration.AddTarget("file", fileTarget);
            fileTarget.Name = "fileTarget";
            fileTarget.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @"GakujoGUI\Logs\${shortdate}.log");
            fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}"; ;
            LoggingRule loggingRule = new("*", LogLevel.Debug, fileTarget);
            if (Environment.GetCommandLineArgs().Contains("-trace"))
            { loggingRule = new("*", LogLevel.Trace, fileTarget); }
            loggingConfiguration.LoggingRules.Add(loggingRule);
            LogManager.Configuration = loggingConfiguration;
            logger.Info("Start Logging.");
            Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Where(x => x.Id != Environment.ProcessId).ToArray();
            if (processes.Length != 0 && !Environment.GetCommandLineArgs().Contains("-force"))
            {
                foreach (Process process in processes)
                {
                    MessageBox.Show("GakujoGUIはすでに起動しています．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                    Current.Shutdown();
                    return;
                }
            }
            foreach (Process process in processes)
            {
                process.Kill();
                logger.Warn($"Kill other GakujoGUI process processId={process.Id}.");
            }
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            //TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Error DispatcherUnhandledException.");
            MessageBox.Show($"エラーが発生しまいた．\n{e.Exception.Message}", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception.InnerException, "Error UnobservedTaskException.");
            MessageBox.Show($"エラーが発生しまいた．\n{e.Exception.InnerException?.Message}", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error((Exception)e.ExceptionObject, "Error UnhandledException.");
            MessageBox.Show($"エラーが発生しまいた．\n{((Exception)e.ExceptionObject).Message}", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
