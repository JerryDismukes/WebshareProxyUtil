using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IPMonitorCore;

namespace IPMonitorApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                try { Logger.Error("UIThread", $"Unhandled UI thread exception: {e.Exception}"); } catch { }
                try
                {
                    MessageBox.Show(e.Exception.Message, "WebshareProxyUtil Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try { Logger.Error("AppDomain", $"Unhandled exception: {e.ExceptionObject}"); } catch { }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try { Logger.Error("TaskScheduler", $"Unobserved task exception: {e.Exception}"); } catch { }
                e.SetObserved();
            };

            if (HasArg(args, "--headless") || HasArg(args, "--service"))
            {
                Task.Run(async () => await HeadlessRunner.RunAsync()).GetAwaiter().GetResult();
                return;
            }

            ApplicationConfiguration.Initialize();

            try
            {
                Logger.Setup();
                Logger.Info("Program", "WebshareProxyUtil interactive startup started.");
                Logger.Info("Program", $"Command line: {Environment.CommandLine}");
                Logger.Info("Program", Logger.GetStartupDiagnostics());

                if (HasArg(args, "--install-startup-task"))
                {
                    var ok = StartupTaskHelper.InstallBootTask(out var details);
                    MessageBox.Show(details, ok ? "Startup Task Installed" : "Startup Task Install Failed", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    return;
                }

                if (HasArg(args, "--uninstall-startup-task"))
                {
                    var ok = StartupTaskHelper.RemoveBootTask(out var details);
                    MessageBox.Show(details, ok ? "Startup Task Removed" : "Startup Task Remove Failed", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    return;
                }

                if (HasArg(args, "--check-startup-task"))
                {
                    var installed = StartupTaskHelper.IsInstalled(out var details);
                    MessageBox.Show(details, installed ? "Startup Task Installed" : "Startup Task Not Installed", MessageBoxButtons.OK, installed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    return;
                }

                Application.Run(new TrayAppContext());
                Logger.Info("Program", "Interactive message loop exited.");
            }
            catch (Exception ex)
            {
                try { Logger.Error("Program", $"Fatal startup error: {ex}"); } catch { }

                try
                {
                    MessageBox.Show(
                        "WebshareProxyUtil failed to start.\r\n\r\n" + ex.Message,
                        "WebshareProxyUtil Startup Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                }
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
