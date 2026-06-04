using System;
using System.Collections.Generic;
using IPMonitorApp;

public static class ConfigUtils
{
    public const string CurrentCredentialTarget = "WebshareProxyUtil:WebshareAPIKey";
    public const string LegacyCredentialTarget = "IPMonitorApp:WebshareAPIKey";

    public static string GetApiKey()
    {
        try
        {
            var cfg = ConfigLoader.Load();

            var keyFromSettings = GetString(cfg.settings, "apiKey");
            if (!string.IsNullOrWhiteSpace(keyFromSettings))
            {
                var decryptedOrPlain = ConfigSecretProtector.UnprotectOrReturnPlainText(keyFromSettings);
                if (!string.IsNullOrWhiteSpace(decryptedOrPlain))
                    return decryptedOrPlain.Trim();
            }

            var target = GetString(cfg.webshare, "ApiKeyCredentialTarget");
            if (string.IsNullOrWhiteSpace(target))
                target = CurrentCredentialTarget;

            var stored = CredentialStore.GetCredential(target);
            if (!string.IsNullOrWhiteSpace(stored))
                return stored.Trim();

            if (!target.Equals(CurrentCredentialTarget, StringComparison.OrdinalIgnoreCase))
            {
                stored = CredentialStore.GetCredential(CurrentCredentialTarget);
                if (!string.IsNullOrWhiteSpace(stored))
                    return stored.Trim();
            }

            stored = CredentialStore.GetCredential(LegacyCredentialTarget);
            if (!string.IsNullOrWhiteSpace(stored))
                return stored.Trim();

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }



    public static void ClearApiKey()
    {
        try
        {
            var cfg = ConfigLoader.Load();
            var target = GetString(cfg.webshare, "ApiKeyCredentialTarget");
            if (string.IsNullOrWhiteSpace(target))
                target = CurrentCredentialTarget;

            CredentialStore.DeleteCredential(target);
            CredentialStore.DeleteCredential(CurrentCredentialTarget);
            CredentialStore.DeleteCredential(LegacyCredentialTarget);

            cfg.settings.apiKey = string.Empty;
            ConfigLoader.Save(cfg);
        }
        catch
        {
        }
    }

    private static string? GetString(object? obj, string key)
    {
        if (obj == null || string.IsNullOrEmpty(key))
            return null;

        if (obj is IDictionary<string, object> dict)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
                return val.ToString();
        }

        var prop = obj.GetType().GetProperty(key);
        if (prop != null)
        {
            var val = prop.GetValue(obj);
            if (val != null)
                return val.ToString();
        }

        return null;
    }
}
