using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using IPMonitorCore;

namespace IPMonitorApp
{
    public static class PingLoop
    {
        private static CancellationTokenSource? _cts;
        private static Task? _task;

        private static readonly object _ipLock = new();
        private static string? _lastPublicIp;
        private static bool _isCheckingIp = false;
        private static DateTime _lastNetworkEvent = DateTime.MinValue;
        private static readonly TimeSpan NetworkDebounce = TimeSpan.FromSeconds(2);

        public static void Start(AppConfig cfg)
        {
            if (_cts != null)
            {
                Logger.Warn("PingLoop", "Start requested while PingLoop is already running; ignoring duplicate start.");
                return;
            }

            Logger.Info("PingLoop", "Start requested.");

            // Initial IP check
            Task.Run(() => CheckAndAuthorizeIpAsync(cfg));

            // Subscribe to network changes
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;

            _cts = new CancellationTokenSource();
            _task = Task.Run(() => Loop(cfg, _cts.Token));
        }

        private static void OnNetworkChanged(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;

            lock (_ipLock)
            {
                if ((now - _lastNetworkEvent) < NetworkDebounce)
                    return; // debounce rapid events

                _lastNetworkEvent = now;

                if (_isCheckingIp)
                    return; // another check in progress

                _isCheckingIp = true;
            }

            Logger.Info("WebshareIP", "Network address change detected; validating public IP.");

            Task.Run(async () =>
            {
                try
                {
                    var cfg = ConfigLoader.Load();
                    if (!cfg.webshare.Enabled)
                    {
                        Logger.Info("WebshareIP", "Webshare disabled, skipping network-change IP check.");
                        return;
                    }

                    var apiKey = ConfigUtils.GetApiKey();
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        Logger.Warn("WebshareIP", "No API key found, cannot perform network-change IP check.");
                        return;
                    }

                    var currentIp = await WebshareUtil.WhatsMyIpAsync(apiKey);
                    if (string.IsNullOrWhiteSpace(currentIp))
                    {
                        Logger.Warn("WebshareIP", "Network-change IP check could not determine current public IP.");
                        return;
                    }

                    lock (_ipLock)
                    {
                        if (currentIp == _lastPublicIp)
                        {
                            Logger.Info("WebshareIP", $"Public IP unchanged after network change: {currentIp}");
                            return;
                        }

                        _lastPublicIp = currentIp;
                    }

                    Logger.Info("WebshareIP", $"Public IP changed after network event. CurrentIP={currentIp}");
                    await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
                }
                catch (Exception ex)
                {
                    Logger.Error("WebshareIP", $"Error in network-change IP check: {ex.Message}");
                }
                finally
                {
                    lock (_ipLock)
                    {
                        _isCheckingIp = false;
                    }
                }
            });
        }

        private static async Task CheckAndAuthorizeIpAsync(AppConfig cfg)
        {
            lock (_ipLock)
            {
                if (_isCheckingIp) return;
                _isCheckingIp = true;
            }

            try
            {
                if (!cfg.webshare.Enabled)
                {
                    Logger.Info("WebshareIP", "Webshare disabled, skipping IP check.");
                    return;
                }

                var apiKey = ConfigUtils.GetApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Logger.Warn("WebshareIP", "No API key found, cannot check IP.");
                    return;
                }

                Logger.Info("WebshareIP", "Performing startup/periodic IP check.");

                var currentIp = await WebshareUtil.WhatsMyIpAsync(apiKey);
                if (string.IsNullOrWhiteSpace(currentIp))
                {
                    Logger.Warn("WebshareIP", "Failed to get current public IP.");
                    return;
                }

                bool ipChanged;
                lock (_ipLock)
                {
                    ipChanged = currentIp != _lastPublicIp;
                    _lastPublicIp = currentIp;
                }

                if (!ipChanged)
                {
                    Logger.Info("WebshareIP", $"Public IP unchanged: {currentIp}");
                    return;
                }

                Logger.Info("WebshareIP", $"Public IP changed, new IP: {currentIp}");

                await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
            }
            catch (Exception ex)
            {
                Logger.Error("WebshareIP", $"Error checking/authorizing IP: {ex.Message}");
            }
            finally
            {
                lock (_ipLock)
                {
                    _isCheckingIp = false;
                }
            }
        }

        public static void Stop()
        {
            Logger.Info("PingLoop", "Stop requested.");

            try { _cts?.Cancel(); } catch { }
            _cts = null;

            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        }

        private static async Task Loop(AppConfig cfg, CancellationToken tok)
        {
            var settings = cfg?.settings ?? new AppConfig().settings;
            var pingTargets = ConfigLoader.CleanTargets(settings.pingTargets ?? new List<string> { "8.8.8.8", "1.1.1.1", "github.com", "api.ipify.org" });
            if (pingTargets.Count == 0)
                pingTargets = new List<string> { "8.8.8.8", "1.1.1.1", "github.com", "api.ipify.org" };

            int pingInterval = Math.Max(1, settings.pingIntervalSeconds);
            int heartbeatMinutes = Math.Max(1, settings.heartbeatIntervalMinutes);
            var heartbeatInterval = TimeSpan.FromMinutes(heartbeatMinutes);
            var nextHeartbeat = DateTimeOffset.Now.Add(heartbeatInterval);
            var startedAt = DateTimeOffset.Now;
            long checkCount = 0;
            long successCount = 0;
            long failureCount = 0;
            bool? lastConnectionOk = null;
            DateTimeOffset lastStatusChange = DateTimeOffset.Now;
            string lastSuccessfulTarget = "(none)";
            string lastResultDetail = "No checks completed yet.";

            using var http = new HttpClient();

            Logger.Info("PingLoop", $"Starting PingLoop. PingIntervalSeconds={pingInterval}; HeartbeatIntervalMinutes={heartbeatMinutes}; WebshareEnabled={cfg.webshare.Enabled}");
            Logger.Info("PingLoop", "Loaded internet detection targets: " + string.Join(", ", pingTargets));

            while (!tok.IsCancellationRequested)
            {
                bool connectionOk = false;
                string successfulTarget = "(none)";
                string resultSummary = "No target responded.";
                checkCount++;

                foreach (var target in pingTargets)
                {
                    if (tok.IsCancellationRequested) break;

                    bool targetOk = false;
                    string resultDetail;

                    try
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(target, 3000);
                        targetOk = reply.Status == IPStatus.Success;
                        resultDetail = reply.Status == IPStatus.Success
                            ? $"Ping Success RoundtripMs={reply.RoundtripTime}"
                            : $"Ping {reply.Status}";
                    }
                    catch (Exception exPing)
                    {
                        try
                        {
                            var url = target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? target : $"http://{target}/";
                            var resp = await http.GetAsync(url, tok);
                            targetOk = resp.IsSuccessStatusCode;
                            resultDetail = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                        }
                        catch (Exception exHttp)
                        {
                            targetOk = false;
                            resultDetail = $"Failed: {exHttp.Message}";
                        }
                    }

                    if (settings.debug)
                        Logger.Debug("PingLoop", $"Connectivity check #{checkCount}; Target={target}; Status={targetOk}; Result={resultDetail}");

                    if (targetOk)
                    {
                        connectionOk = true;
                        successfulTarget = target;
                        resultSummary = resultDetail;
                        break;
                    }

                    resultSummary = $"Last failed target={target}; {resultDetail}";
                }

                if (connectionOk)
                {
                    successCount++;
                    lastSuccessfulTarget = successfulTarget;
                }
                else
                {
                    failureCount++;
                }

                lastResultDetail = resultSummary;

                if (lastConnectionOk == null || connectionOk != lastConnectionOk.Value)
                {
                    if (connectionOk)
                        Logger.Info("PingLoop", $"Connection restored/confirmed. Target={successfulTarget}; Result={resultSummary}");
                    else
                        Logger.Warn("PingLoop", $"Connection lost. {resultSummary}");

                    lastConnectionOk = connectionOk;
                    lastStatusChange = DateTimeOffset.Now;
                }

                var now = DateTimeOffset.Now;
                if (now >= nextHeartbeat)
                {
                    var uptime = now - startedAt;
                    var stableFor = now - lastStatusChange;
                    Logger.Info(
                        "PingLoop",
                        $"Heartbeat: running. ConnectionOk={connectionOk}; Checks={checkCount}; Success={successCount}; Failures={failureCount}; LastSuccessfulTarget={lastSuccessfulTarget}; LastResult={lastResultDetail}; StableFor={FormatDuration(stableFor)}; Uptime={FormatDuration(uptime)}; PingIntervalSeconds={pingInterval}; TargetCount={pingTargets.Count}.");
                    nextHeartbeat = now.Add(heartbeatInterval);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(pingInterval), tok);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            Logger.Info("PingLoop", $"Stopped PingLoop. Checks={checkCount}; Success={successCount}; Failures={failureCount}; LastConnectionOk={lastConnectionOk?.ToString() ?? "unknown"}.");
        }

        private static string FormatDuration(TimeSpan value)
        {
            if (value.TotalDays >= 1)
                return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";

            if (value.TotalHours >= 1)
                return $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s";

            if (value.TotalMinutes >= 1)
                return $"{(int)value.TotalMinutes}m {value.Seconds}s";

            return $"{Math.Max(0, (int)value.TotalSeconds)}s";
        }
    }
}
