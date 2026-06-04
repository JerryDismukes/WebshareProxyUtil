using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace IPMonitorCore
{
    public static class WebshareUtil
    {
        private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

        private static HttpClient CreateClient(string apiKey)
        {
            var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            if (!string.IsNullOrWhiteSpace(apiKey))
                hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);
            return hc;
        }

        private static void Log(string msg) => Debug.WriteLine("[WebshareUtil] " + msg);

        private static string? lastIp;
        private static int _isCheckingIp = 0; // semaphore to prevent simultaneous IP checks

        public static async Task<string?> WhatsMyIpAsync(string? apiKey = null)
        {
            try
            {
                using var client = new HttpClient();
                var resp = await client.GetAsync("https://proxy.webshare.io/api/v2/proxy/ipauthorization/whatsmyip/");
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                var obj = JsonDocument.Parse(json);
                var ip = obj.RootElement.GetProperty("ip_address").GetString();

                if (!string.IsNullOrEmpty(ip))
                {
                    if (lastIp == null)
                        Logger.Info("WebshareIP", $"CurrentIP={ip} (first run)");
                    else if (ip != lastIp)
                        Logger.Info("WebshareIP", $"CurrentIP={ip} PreviousIP={lastIp}");
                    else
                        Logger.Info("WebshareIP", $"CurrentIP={ip} (no change)");

                    lastIp = ip;
                }

                return ip;
            }
            catch (Exception ex)
            {
                Logger.Error("WebshareIP", $"Error checking IP: {ex.Message}");
                return null;
            }
        }

        public static async Task<PagedResult<AuthorizedIp>> ListAuthorizedIpsAsync(string apiKey, int page = 1, int pageSize = 100)
        {
            var url = $"https://proxy.webshare.io/api/v2/proxy/ipauthorization/";
            Logger.Debug("General", "GET " + url);

            using var client = CreateClient(apiKey);
            var res = await client.GetAsync(url);
            res.EnsureSuccessStatusCode();
            await using var s = await res.Content.ReadAsStreamAsync();
            var doc = await JsonSerializer.DeserializeAsync<PagedResult<AuthorizedIp>>(s, jsonOptions);
            return doc ?? new PagedResult<AuthorizedIp>();
        }

        public static async Task<AuthorizedIp?> CreateAuthorizedIpAsync(string apiKey, string ip)
        {
            var url = "https://proxy.webshare.io/api/v2/proxy/ipauthorization/";
            Logger.Info("WebshareIP", "POST " + url + " ip_address: " + ip);
            using var client = CreateClient(apiKey);
            var payload = JsonSerializer.Serialize(new { ip_address = ip });
            var res = await client.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"));
            res.EnsureSuccessStatusCode();
            await using var s = await res.Content.ReadAsStreamAsync();
            var obj = await JsonSerializer.DeserializeAsync<AuthorizedIp>(s, jsonOptions);
            return obj;
        }

        public static async Task<string?> CheckAndAddIpIfChangedAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            // prevent overlapping checks
            if (Interlocked.CompareExchange(ref _isCheckingIp, 1, 0) != 0)
                return lastIp;

            try
            {
                var currentIp = await WhatsMyIpAsync(apiKey);
                if (string.IsNullOrEmpty(currentIp)) return lastIp;

                var paged = await ListAuthorizedIpsAsync(apiKey);
                var authorizedIps = paged.Results.Select(a => a.IpAddress).ToList();

                Logger.Info("WebshareIP", $"Authorized IPs: {string.Join(", ", authorizedIps)}");
                Logger.Info("WebshareIP", $"Current IP: {currentIp}");

                if (!authorizedIps.Contains(currentIp))
                {
                    var added = await CreateAuthorizedIpAsync(apiKey, currentIp);
                    if (added != null)
                        Logger.Info("WebshareIP", $"Authorized new IP: {currentIp}");
                    else
                        Logger.Warn("WebshareIP", $"Failed to authorize IP: {currentIp}");
                }
                else
                {
                    Logger.Debug("WebshareIP", $"IP {currentIp} already authorized.");
                }

                lastIp = currentIp;
                return currentIp;
            }
            catch (Exception ex)
            {
                Logger.Error("WebshareIP", $"IP authorization failed: {ex.Message}");
                return lastIp;
            }
            finally
            {
                Interlocked.Exchange(ref _isCheckingIp, 0);
            }
        }

        // other methods remain unchanged
        public static async Task DeleteAuthorizedIpAsync(string apiKey, int id)
        {
            var url = $"https://proxy.webshare.io/api/v2/proxy/ipauthorization/{id}/";
            Log("DELETE " + url);
            using var client = CreateClient(apiKey);
            var res = await client.DeleteAsync(url);
            res.EnsureSuccessStatusCode();
        }

        public static async Task<PagedResult<ProxyInfo>> ListProxiesAsync(string apiKey, string mode = "direct", int page = 1, int pageSize = 100, string? search = null, string? countryCodes = null)
        {
            if (mode != "direct" && mode != "backbone") throw new ArgumentException("mode must be 'direct' or 'backbone'");
            var query = new List<string> { $"mode={Uri.EscapeDataString(mode)}", $"page={page}", $"page_size={pageSize}" };
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(countryCodes)) query.Add($"country_code__in={Uri.EscapeDataString(countryCodes)}");
            var url = "https://proxy.webshare.io/api/v2/proxy/list/?" + string.Join("&", query);
            Logger.Log("GET " + url);
            using var client = CreateClient(apiKey);
            var res = await client.GetAsync(url);
            res.EnsureSuccessStatusCode();
            await using var s = await res.Content.ReadAsStreamAsync();
            var doc = await JsonSerializer.DeserializeAsync<PagedResult<ProxyInfo>>(s, jsonOptions);
            return doc ?? new PagedResult<ProxyInfo>();
        }

        public static async Task<SubscriptionPlan?> GetActivePlanAsync(string apiKey)
        {
            var url = "https://proxy.webshare.io/api/v2/subscription/plan/";
            Logger.Log("GET " + url);
            using var client = CreateClient(apiKey);
            var res = await client.GetAsync(url);
            var json = await res.Content.ReadAsStringAsync();
            Logger.Log("Raw-Plan= " + json);
            res.EnsureSuccessStatusCode();
            var doc = JsonSerializer.Deserialize<SubscriptionPlanResponse>(json, jsonOptions);
            return doc?.Results?.FirstOrDefault(r => r.Status == "active");
        }

        // ----------------- Classes -----------------
        private class SubscriptionPlanResponse
        {
            public int Count { get; set; }
            public List<SubscriptionPlan>? Results { get; set; }
        }

        public class PagedResult<T>
        {
            [JsonPropertyName("count")] public int Count { get; set; }
            [JsonPropertyName("next")] public string? Next { get; set; }
            [JsonPropertyName("previous")] public string? Previous { get; set; }
            [JsonPropertyName("results")] public List<T> Results { get; set; } = new();
        }

        public class SubscriptionPlan
        {
            public int Id { get; set; }
            public string Status { get; set; } = "";
            public double Bandwidth_Limit { get; set; }
            public double Monthly_Price { get; set; }
            public double Yearly_Price { get; set; }
            public string Proxy_Type { get; set; } = "";
            public string Proxy_Subtype { get; set; } = "";
            public int Proxy_Count { get; set; }
        }

        public class AuthorizedIp
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("ip_address")] public string IpAddress { get; set; } = "";
            [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
            [JsonPropertyName("last_used_at")] public DateTimeOffset? LastUsedAt { get; set; }
        }

        public class ProxyInfo
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("username")] public string? Username { get; set; }
            [JsonPropertyName("password")] public string? Password { get; set; }
            [JsonPropertyName("proxy_address")] public string? ProxyAddress { get; set; }
            [JsonPropertyName("port")] public int Port { get; set; }
            [JsonPropertyName("valid")] public bool Valid { get; set; }
            [JsonPropertyName("last_verification")] public DateTimeOffset? LastVerification { get; set; }
            [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
            [JsonPropertyName("city_name")] public string? CityName { get; set; }
            [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
        }

        public class ProxyReplacement
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("reason")] public string Reason { get; set; } = "";
            [JsonPropertyName("state")] public string? State { get; set; }
            [JsonPropertyName("proxy")] public string? Proxy { get; set; }
            [JsonPropertyName("proxy_port")] public int ProxyPort { get; set; }
            [JsonPropertyName("proxy_country_code")] public string? ProxyCountryCode { get; set; }
            [JsonPropertyName("replaced_with")] public string? ReplacedWith { get; set; }
            [JsonPropertyName("replaced_with_port")] public int ReplacedWithPort { get; set; }
            [JsonPropertyName("replaced_with_country_code")] public string? ReplacedWithCountryCode { get; set; }
            [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        }

        public class ReplacementTarget
        {
            [JsonPropertyName("type")] public string? Type { get; set; }
            [JsonPropertyName("ip_ranges")] public List<string>? Ip_Ranges { get; set; }
            [JsonPropertyName("country_code")] public string? Country_Code { get; set; }
        }
        public static async Task<PagedResult<ProxyReplacement>> ListProxyReplacementsForActivePlanAsync(string apiKey, int page = 1, int pageSize = 100)
        {
            var plan = await GetActivePlanAsync(apiKey);
            if (plan == null)
            {
                Logger.Log("No active plan found, cannot list replacements.");
                return new PagedResult<ProxyReplacement>();
            }

            var url = $"https://proxy.webshare.io/api/v2/proxy/list/replaced/?page={page}&page_size={pageSize}&plan_id={plan.Id}";
            Logger.Log("GET " + url);
            using var client = CreateClient(apiKey);
            var res = await client.GetAsync(url);
            var json = await res.Content.ReadAsStringAsync();
            Logger.Log("Raw-Replacements= " + json);

            res.EnsureSuccessStatusCode();
            var doc = JsonSerializer.Deserialize<PagedResult<ProxyReplacement>>(json, jsonOptions);
            return doc ?? new PagedResult<ProxyReplacement>();
        }

    }
}
