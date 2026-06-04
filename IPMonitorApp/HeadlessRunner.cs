using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using IPMonitorCore;

namespace IPMonitorApp
{
    public static class HeadlessRunner
    {
        private static CancellationTokenSource? _cts;

        public static async Task RunAsync()
        {
            _cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cts.Cancel();
            };

            var cfg = ConfigLoader.Load();
            Logger.Setup(cfg.settings.maxLogSizeKB, cfg.settings.maxLogFiles, cfg.settings.debug);
            Logger.Info("Headless", "WebshareProxyUtil headless mode starting.");
            Logger.Info("Headless", Logger.GetStartupDiagnostics());
            Logger.Info("Headless", $"ConfigFile={ConfigLoader.ConfigFile}");

            IPMonitorRR? monitor = null;

            try
            {
                PingLoop.Start(cfg);
                Logger.Info("Headless", "PingLoop started.");

                monitor = new IPMonitorRR(cfg);
                monitor.OnStatusChanged += status => Logger.Info("Headless", $"Monitor status changed: {status}");
                monitor.Start();
                Logger.Info("Headless", "Monitor started.");

                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

                await RunPeriodicLoopAsync(cfg, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Headless", "Headless mode cancellation requested.");
            }
            catch (Exception ex)
            {
                Logger.Error("Headless", $"Fatal headless error: {ex}");
            }
            finally
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

                try { PingLoop.Stop(); } catch { }
                try
                {
                    if (monitor != null)
                        await monitor.StopAsync();
                }
                catch { }

                Logger.Info("Headless", "WebshareProxyUtil headless mode stopped.");
            }
        }

        private static async Task RunPeriodicLoopAsync(AppConfig cfg, CancellationToken token)
        {
            var heartbeatMinutes = Math.Max(1, cfg.settings.heartbeatIntervalMinutes);
            var ipIntervalSeconds = Math.Max(30, cfg.webshare.IntervalSeconds > 0 ? cfg.webshare.IntervalSeconds : cfg.settings.ipCheckIntervalSeconds);
            var nextIpCheck = DateTimeOffset.MinValue;
            var nextHeartbeat = DateTimeOffset.MinValue;

            while (!token.IsCancellationRequested)
            {
                var now = DateTimeOffset.Now;

                if (now >= nextHeartbeat)
                {
                    Logger.Info("Headless", "Heartbeat: running.");
                    nextHeartbeat = now.AddMinutes(heartbeatMinutes);
                }

                if (now >= nextIpCheck)
                {
                    await CheckWebshareIpAsync("Headless");
                    nextIpCheck = now.AddSeconds(ipIntervalSeconds);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }

        private static void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            _ = Task.Run(async () => await CheckWebshareIpAsync("NetworkChange"));
        }

        private static async Task CheckWebshareIpAsync(string component)
        {
            try
            {
                var cfg = ConfigLoader.Load();
                if (cfg.webshare?.Enabled != true)
                {
                    Logger.Info(component, "Webshare integration disabled; skipping IP validation.");
                    return;
                }

                var apiKey = ConfigUtils.GetApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Logger.Warn(component, "Webshare enabled but no API key is configured.");
                    return;
                }

                Logger.Info(component, "Running Webshare IP validation.");
                var ip = await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
                Logger.Info(component, $"Webshare IP validation complete. CurrentIP={ip ?? "(unknown)"}");
            }
            catch (Exception ex)
            {
                Logger.Error(component, $"Webshare IP validation failed: {ex.Message}");
            }
        }
    }
}
