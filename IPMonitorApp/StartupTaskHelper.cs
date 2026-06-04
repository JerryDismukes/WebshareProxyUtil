using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using IPMonitorCore;

namespace IPMonitorApp
{
    public static class StartupTaskHelper
    {
        public const string TaskName = "WebshareProxyUtil";

        public static string ExePath
        {
            get
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(path))
                    return path;

                return Process.GetCurrentProcess().MainModule?.FileName ?? ApplicationExecutableFallback;
            }
        }

        private static string ApplicationExecutableFallback => Path.Combine(AppContext.BaseDirectory, "WebshareProxyUtil.exe");

        public static bool IsRunningElevated()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool RelaunchElevated(string arguments, out string details)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ExePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(ExePath) ?? Environment.CurrentDirectory
                };

                Process.Start(psi);
                details = "Elevation was requested. Approve the UAC prompt to continue.";
                return true;
            }
            catch (Exception ex)
            {
                details = "Failed to relaunch elevated: " + ex.Message;
                return false;
            }
        }

        public static bool IsInstalled(out string details)
        {
            var result = RunSchtasks($"/Query /TN \"{TaskName}\"");
            details = result.Output;
            return result.ExitCode == 0;
        }

        public static bool InstallBootTask(out string details)
        {
            if (!IsRunningElevated())
            {
                details = "Administrator rights are required to install the boot-time SYSTEM scheduled task.";
                Logger.Warn("StartupTask", details);
                return false;
            }

            var exe = ExePath;
            var taskRun = $"\\\"{exe}\\\" --headless";
            var args = $"/Create /F /TN \"{TaskName}\" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR \"{taskRun}\"";
            var result = RunSchtasks(args);
            details = result.Output;

            if (result.ExitCode == 0)
                Logger.Info("StartupTask", $"Installed boot-time scheduled task '{TaskName}' pointing to '{exe}'.");
            else
                Logger.Error("StartupTask", $"Failed to install boot-time scheduled task '{TaskName}'. {details}");

            return result.ExitCode == 0;
        }

        public static bool RemoveBootTask(out string details)
        {
            if (!IsRunningElevated())
            {
                details = "Administrator rights are required to remove the boot-time SYSTEM scheduled task.";
                Logger.Warn("StartupTask", details);
                return false;
            }

            var result = RunSchtasks($"/Delete /F /TN \"{TaskName}\"");
            details = result.Output;

            if (result.ExitCode == 0)
                Logger.Info("StartupTask", $"Removed scheduled task '{TaskName}'.");
            else
                Logger.Error("StartupTask", $"Failed to remove scheduled task '{TaskName}'. {details}");

            return result.ExitCode == 0;
        }

        private static CommandResult RunSchtasks(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return new CommandResult(1, "Failed to start schtasks.exe.");

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CommandResult(process.ExitCode, (output + Environment.NewLine + error).Trim());
            }
            catch (Exception ex)
            {
                return new CommandResult(1, ex.Message);
            }
        }

        private readonly struct CommandResult
        {
            public CommandResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }
            public string Output { get; }
        }
    }
}
