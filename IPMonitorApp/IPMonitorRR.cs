using System;
using System.Threading;
using System.Threading.Tasks;
using IPMonitorCore;

namespace IPMonitorApp
{
    public class IPMonitorRR
    {
        private readonly AppConfig _cfg;
        private readonly object _sync = new object();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event Action<MonitorStatus>? OnStatusChanged;

        public IPMonitorRR(AppConfig cfg) { _cfg = cfg; }

        public bool IsRunning
        {
            get
            {
                lock (_sync)
                    return _isRunning;
            }
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_isRunning)
                {
                    Logger.Debug("IPMonitorRR", "Start requested while monitor is already running; ignoring duplicate start.");
                    return;
                }

                _cts = new CancellationTokenSource();
                _isRunning = true;
            }

            Logger.Info("IPMonitorRR", "Monitor loop starting.");
            OnStatusChanged?.Invoke(MonitorStatus.Ok);
            _ = Loop(_cts.Token);
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? ctsToCancel = null;

            lock (_sync)
            {
                if (!_isRunning && _cts == null)
                {
                    OnStatusChanged?.Invoke(MonitorStatus.Paused);
                    return;
                }

                ctsToCancel = _cts;
                _cts = null;
                _isRunning = false;
            }

            try
            {
                ctsToCancel?.Cancel();
            }
            catch
            {
            }

            await Task.Delay(100);
            Logger.Info("IPMonitorRR", "Monitor loop paused by request.");
            OnStatusChanged?.Invoke(MonitorStatus.Paused);
        }

        private async Task Loop(CancellationToken tok)
        {
            int failures = 0;

            try
            {
                while (!tok.IsCancellationRequested)
                {
                    try
                    {
                        failures = 0;
                        OnStatusChanged?.Invoke(MonitorStatus.Ok);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        Logger.Warn("IPMonitorRR", $"Monitor tick failed: {ex.Message}");

                        if (failures >= _cfg.settings.MaxFailures)
                            OnStatusChanged?.Invoke(MonitorStatus.Error);
                        else
                            OnStatusChanged?.Invoke(MonitorStatus.Warning);
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _cfg.settings.checkIntervalSeconds)), tok);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error("IPMonitorRR", $"Monitor loop stopped unexpectedly: {ex}");
            }
            finally
            {
                lock (_sync)
                {
                    if (_cts != null && _cts.IsCancellationRequested)
                    {
                        _cts = null;
                        _isRunning = false;
                    }
                }

                Logger.Info("IPMonitorRR", "Monitor loop stopped.");
            }
        }

        public async Task ForceRecheckAsync()
        {
            Logger.Log("force recheck");
            await Task.CompletedTask;
        }
    }
}
