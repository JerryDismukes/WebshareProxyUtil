using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IPMonitorCore
{
    public static class Logger
    {
        private static readonly object _sync = new object();
        private static long _maxBytes = 500 * 1024;
        private static int _maxFiles = 5;
        private static bool _setupComplete;

        public static bool DebugEnabled { get; set; }
        public static event Action<string>? OnLog;

        public static string ExecutableDirectory
        {
            get
            {
                try
                {
                    var processPath = Environment.ProcessPath;
                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        var dir = Path.GetDirectoryName(processPath);
                        if (!string.IsNullOrWhiteSpace(dir))
                            return dir;
                    }
                }
                catch
                {
                }

                try
                {
                    var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(mainModulePath))
                    {
                        var dir = Path.GetDirectoryName(mainModulePath);
                        if (!string.IsNullOrWhiteSpace(dir))
                            return dir;
                    }
                }
                catch
                {
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
                        return AppContext.BaseDirectory;
                }
                catch
                {
                }

                return Directory.GetCurrentDirectory();
            }
        }

        public static string LogDir => Path.Combine(ExecutableDirectory, "Logs");
        public static string LogFile => Path.Combine(LogDir, "monitor.log");

        public static void Setup(int maxKb = 500, int maxFiles = 5, bool debug = false)
        {
            DebugEnabled = debug;
            _maxBytes = Math.Max(1, maxKb) * 1024L;
            _maxFiles = Math.Max(1, maxFiles);

            try
            {
                Directory.CreateDirectory(LogDir);
                RotateIfNeeded();

                if (!File.Exists(LogFile))
                {
                    File.WriteAllText(
                        LogFile,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [Logger] Log file created at '{LogFile}'.{Environment.NewLine}",
                        Encoding.UTF8);
                }

                _setupComplete = true;
            }
            catch (Exception ex)
            {
                RaiseUiOnly("ERROR", "Logger", $"Failed to initialize log file '{LogFile}': {ex.Message}");
                return;
            }

            Info("Logger", $"Initialized. Debug={DebugEnabled} MaxKB={maxKb} MaxFiles={_maxFiles}");
            Info("Logger", $"ExecutableDirectory={ExecutableDirectory}");
            Info("Logger", $"LogFile={LogFile}");
            Info("Logger", $"AppContext.BaseDirectory={AppContext.BaseDirectory}");
            Info("Logger", $"CurrentDirectory={Directory.GetCurrentDirectory()}");
            Info("Logger", $"ProcessPath={Environment.ProcessPath}");
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(LogFile))
                return;

            var fi = new FileInfo(LogFile);
            if (fi.Length < _maxBytes)
                return;

            var oldest = Path.Combine(LogDir, $"monitor.log.{_maxFiles}");
            if (File.Exists(oldest))
                File.Delete(oldest);

            for (int i = _maxFiles - 1; i >= 1; i--)
            {
                var src = Path.Combine(LogDir, $"monitor.log.{i}");
                var dst = Path.Combine(LogDir, $"monitor.log.{i + 1}");

                if (File.Exists(dst))
                    File.Delete(dst);

                if (File.Exists(src))
                    File.Move(src, dst);
            }

            var first = Path.Combine(LogDir, "monitor.log.1");
            if (File.Exists(first))
                File.Delete(first);

            File.Move(LogFile, first);
        }

        private static void WriteLine(string level, string component, string message, bool isDebug = false)
        {
            if (isDebug && !DebugEnabled)
                return;

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{component}] {message}";

            lock (_sync)
            {
                try
                {
                    Directory.CreateDirectory(LogDir);
                    RotateIfNeeded();
                    File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
                    _setupComplete = true;
                }
                catch (Exception ex)
                {
                    RaiseUiOnly("ERROR", "Logger", $"Failed to write to log file '{LogFile}': {ex.Message}");
                }

                try
                {
                    OnLog?.Invoke(line);
                }
                catch
                {
                }
            }
        }

        private static void RaiseUiOnly(string level, string component, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{component}] {message}";

            try
            {
                OnLog?.Invoke(line);
            }
            catch
            {
            }

            try
            {
                Console.Error.WriteLine(line);
            }
            catch
            {
            }
        }

        public static void Info(string component, string message) => WriteLine("INFO", component, message);
        public static void Warn(string component, string message) => WriteLine("WARN", component, message);
        public static void Error(string component, string message) => WriteLine("ERROR", component, message);
        public static void Debug(string component, string message) => WriteLine("DEBUG", component, message, true);
        public static void Log(string msg) => Info("General", msg);

        public static string GetStartupDiagnostics()
        {
            return
                $"Logger setup complete: {_setupComplete}{Environment.NewLine}" +
                $"Debug enabled: {DebugEnabled}{Environment.NewLine}" +
                $"Executable directory: {ExecutableDirectory}{Environment.NewLine}" +
                $"Log directory: {LogDir}{Environment.NewLine}" +
                $"Log file: {LogFile}{Environment.NewLine}" +
                $"AppContext.BaseDirectory: {AppContext.BaseDirectory}{Environment.NewLine}" +
                $"CurrentDirectory: {Directory.GetCurrentDirectory()}{Environment.NewLine}" +
                $"Environment.ProcessPath: {Environment.ProcessPath}";
        }
    }
}
