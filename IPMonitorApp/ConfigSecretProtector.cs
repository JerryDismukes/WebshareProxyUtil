using System;
using System.Security.Cryptography;
using System.Text;
using IPMonitorCore;

namespace IPMonitorApp
{
    public static class ConfigSecretProtector
    {
        private const string Prefix = "dpapi-localmachine:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WebshareProxyUtil.ApiKey.v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText.Trim());
                var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.LocalMachine);
                return Prefix + Convert.ToBase64String(protectedBytes);
            }
            catch (Exception ex)
            {
                Logger.Error("ConfigSecret", "Failed to encrypt API key for shared config: " + ex.Message);
                return string.Empty;
            }
        }

        public static string UnprotectOrReturnPlainText(string storedValue)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
                return string.Empty;

            var trimmed = storedValue.Trim();

            if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed;

            try
            {
                var encoded = trimmed.Substring(Prefix.Length);
                var protectedBytes = Convert.FromBase64String(encoded);
                var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(bytes).Trim();
            }
            catch (Exception ex)
            {
                Logger.Error("ConfigSecret", "Failed to decrypt API key from shared config: " + ex.Message);
                return string.Empty;
            }
        }

        public static bool LooksEncrypted(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
