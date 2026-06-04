
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace IPMonitorApp
{
    public static class CredentialStore
    {
        // Minimal wrapper for storing generic credential using CredWrite/CredRead
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern void CredFree([In] IntPtr cred);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CredDelete(string target, uint type, uint flags);

        const uint CRED_TYPE_GENERIC = 1;
        const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        public static bool SaveCredential(string target, string secret)
        {
            try
            {
                var bytes = Encoding.Unicode.GetBytes(secret);
                var credential = new CREDENTIAL
                {
                    Flags = 0,
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    CredentialBlobSize = (uint)bytes.Length,
                    CredentialBlob = Marshal.AllocHGlobal(bytes.Length),
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    AttributeCount = 0,
                    Attributes = IntPtr.Zero,
                    TargetAlias = null,
                    UserName = null,
                    Comment = "Stored by IPMonitor"
                };
                Marshal.Copy(bytes, 0, credential.CredentialBlob, bytes.Length);
                var written = CredWrite(ref credential, 0);
                Marshal.FreeHGlobal(credential.CredentialBlob);
                return written;
            }
            catch { return false; }
        }

        public static string? GetCredential(string target)
        {
            try
            {
                if (!CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr credPtr)) return null;
                var cred = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
                if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                {
                    var bytes = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
                    var secret = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                    CredFree(credPtr);
                    return secret;
                }
                CredFree(credPtr);
                return null;
            }
            catch { return null; }
        }

        public static bool DeleteCredential(string target)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(target))
                    return false;

                return CredDelete(target, CRED_TYPE_GENERIC, 0);
            }
            catch
            {
                return false;
            }
        }
    }
}
