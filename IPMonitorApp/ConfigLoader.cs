using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IPMonitorCore;

namespace IPMonitorApp
{
    public class AppConfig
    {
        public Settings settings { get; set; } = new Settings();
        public List<Target>? targets { get; set; }
        public WebshareConfig webshare { get; set; } = new WebshareConfig();
    }

    public class Settings
    {
        public bool debug { get; set; } = false;
        public string apiKey { get; set; } = string.Empty;
        public int checkIntervalSeconds { get; set; } = 30;
        public int pingIntervalSeconds { get; set; } = 5;
        public int ipCheckIntervalSeconds { get; set; } = 60;
        public int maxLogSizeKB { get; set; } = 500;
        public int maxLogFiles { get; set; } = 5;
        public int MaxFailures { get; set; } = 5;
        public int heartbeatIntervalMinutes { get; set; } = 60;
        public bool firstRunComplete { get; set; } = false;
        public bool startMinimizedToTray { get; set; } = false;
        public List<string>? pingTargets { get; set; } = new List<string>
        {
            "8.8.8.8",
            "1.1.1.1",
            "github.com",
            "api.ipify.org"
        };
    }

    public class WebshareConfig
    {
        public bool Enabled { get; set; } = true;
        public string Url { get; set; } = "https://proxy.webshare.io/api/v2/proxy/ipauthorization/whatsmyip/";
        public int IntervalSeconds { get; set; } = 60;
        public string ApiKeyCredentialTarget { get; set; } = "WebshareProxyUtil:WebshareAPIKey";
    }

    public class Target
    {
        public string? host { get; set; }
        public string? note { get; set; }
    }

    public static class ConfigLoader
    {
        private const string AppFolderName = "WebshareProxyUtil";
        private const string ConfigFileName = "MonitorConfig.json";
        private static bool _configPathLogged = false;
        private static readonly object _configPathLogSync = new object();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string ConfigDirectory
        {
            get
            {
                var overridePath = Environment.GetEnvironmentVariable("WEBSHARE_PROXY_UTIL_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(overridePath))
                    return overridePath;

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    AppFolderName);
            }
        }

        public static string ConfigFile => Path.Combine(ConfigDirectory, ConfigFileName);
        private static string LegacyExeConfigFile => Path.Combine(Logger.ExecutableDirectory, ConfigFileName);
        private static string LegacyAppDataConfigFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPMonitorSuite", ConfigFileName);

        public static AppConfig Load()
        {
            try
            {
                EnsureConfigExists();
                LogConfigPathOnce();
                Logger.Debug("Config", $"Loading config from '{ConfigFile}'.");

                var text = File.ReadAllText(ConfigFile);
                var cfg = JsonSerializer.Deserialize<AppConfig>(text, JsonOptions) ?? CreateDefaultConfig();
                Normalize(cfg);
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Error("Config", $"Failed to load config from '{ConfigFile}': {ex.Message}");
                var cfg = CreateDefaultConfig();
                Normalize(cfg);
                return cfg;
            }
        }


        private static void LogConfigPathOnce()
        {
            if (_configPathLogged)
                return;

            lock (_configPathLogSync)
            {
                if (_configPathLogged)
                    return;

                Logger.Info("Config", $"Using config file '{ConfigFile}'.");
                _configPathLogged = true;
            }
        }

        public static void Save(AppConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                Normalize(cfg);

                var json = JsonSerializer.Serialize(cfg, JsonOptions);
                File.WriteAllText(ConfigFile, json);
                Logger.Info("Config", $"Saved config to '{ConfigFile}'.");
            }
            catch (Exception ex)
            {
                Logger.Error("Config", $"Failed to save config to '{ConfigFile}': {ex.Message}");
                throw;
            }
        }

        private static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(ConfigFile))
                return;

            if (TryMigrateConfig(LegacyExeConfigFile))
                return;

            if (TryMigrateConfig(LegacyAppDataConfigFile))
                return;

            var cfg = CreateDefaultConfig();
            var json = JsonSerializer.Serialize(cfg, JsonOptions);
            File.WriteAllText(ConfigFile, json);
            Logger.Info("Config", $"Created default config at '{ConfigFile}'.");
        }

        private static bool TryMigrateConfig(string source)
        {
            if (!File.Exists(source))
                return false;

            try
            {
                File.Copy(source, ConfigFile, false);
                Logger.Info("Config", $"Migrated config from '{source}' to '{ConfigFile}'.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn("Config", $"Failed to migrate config from '{source}': {ex.Message}");
                return false;
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                settings = new Settings
                {
                    debug = false,
                    apiKey = string.Empty,
                    checkIntervalSeconds = 30,
                    pingIntervalSeconds = 5,
                    ipCheckIntervalSeconds = 60,
                    maxLogSizeKB = 500,
                    maxLogFiles = 5,
                    MaxFailures = 5,
                    heartbeatIntervalMinutes = 60,
                    firstRunComplete = false,
                    startMinimizedToTray = false,
                    pingTargets = new List<string>
                    {
                        "8.8.8.8",
                        "1.1.1.1",
                        "github.com",
                        "api.ipify.org"
                    }
                },
                targets = new List<Target>(),
                webshare = new WebshareConfig
                {
                    Enabled = true,
                    Url = "https://proxy.webshare.io/api/v2/proxy/ipauthorization/whatsmyip/",
                    IntervalSeconds = 60,
                    ApiKeyCredentialTarget = "WebshareProxyUtil:WebshareAPIKey"
                }
            };
        }

        private static void Normalize(AppConfig cfg)
        {
            cfg.settings ??= new Settings();
            cfg.webshare ??= new WebshareConfig();
            cfg.targets ??= new List<Target>();

            if (!string.IsNullOrWhiteSpace(cfg.settings.apiKey) && !ConfigSecretProtector.LooksEncrypted(cfg.settings.apiKey))
            {
                var encrypted = ConfigSecretProtector.Protect(cfg.settings.apiKey);
                if (!string.IsNullOrWhiteSpace(encrypted))
                    cfg.settings.apiKey = encrypted;
            }

            if (cfg.settings.pingTargets == null || cfg.settings.pingTargets.Count == 0)
            {
                cfg.settings.pingTargets = new List<string>
                {
                    "8.8.8.8",
                    "1.1.1.1",
                    "github.com",
                    "api.ipify.org"
                };
            }

            cfg.settings.pingTargets = CleanTargets(cfg.settings.pingTargets);

            if (cfg.settings.checkIntervalSeconds <= 0)
                cfg.settings.checkIntervalSeconds = 30;

            if (cfg.settings.pingIntervalSeconds <= 0)
                cfg.settings.pingIntervalSeconds = 5;

            if (cfg.settings.ipCheckIntervalSeconds <= 0)
                cfg.settings.ipCheckIntervalSeconds = 60;

            if (cfg.settings.maxLogSizeKB <= 0)
                cfg.settings.maxLogSizeKB = 500;

            if (cfg.settings.maxLogFiles <= 0)
                cfg.settings.maxLogFiles = 5;

            if (cfg.settings.MaxFailures <= 0)
                cfg.settings.MaxFailures = 5;

            if (cfg.settings.heartbeatIntervalMinutes <= 0)
                cfg.settings.heartbeatIntervalMinutes = 60;

            if (string.IsNullOrWhiteSpace(cfg.webshare.Url))
                cfg.webshare.Url = "https://proxy.webshare.io/api/v2/proxy/ipauthorization/whatsmyip/";

            if (cfg.webshare.IntervalSeconds <= 0)
                cfg.webshare.IntervalSeconds = 60;

            if (string.IsNullOrWhiteSpace(cfg.webshare.ApiKeyCredentialTarget))
                cfg.webshare.ApiKeyCredentialTarget = "WebshareProxyUtil:WebshareAPIKey";
        }

        public static List<string> CleanTargets(IEnumerable<string> targets)
        {
            var clean = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in targets)
            {
                var item = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                if (item.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (seen.Add(item))
                    clean.Add(item);
            }

            return clean;
        }
    }
}
