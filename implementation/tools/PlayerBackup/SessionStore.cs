// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BeatSaberMarkupLanguage.Attributes;

namespace PlayerBackup {
    public sealed partial class Plugin {
        const string Security = "/System/Library/Frameworks/Security.framework/Security";
        const string KeychainService = "it.xcenter.beatsaber.player-sync";
        const string KeychainAccount = "session";
        static string StayPath => Path.Combine(Root, "player-sync", "stay-signed-in");
        [UIValue("stay-signed-in")] public bool StaySignedIn {
            get => File.Exists(StayPath);
            set {
                try {
                    if (value) {
                        File.WriteAllText(StayPath, "");
                        SaveSession();
                    } else {
                        File.Delete(StayPath);
                        DeleteSession();
                    }
                } catch (IOException) {
                    SetAccountStatus("Could not save sign-in preference");
                } catch (UnauthorizedAccessException) {
                    SetAccountStatus("Could not save sign-in preference");
                }
            }
        }

        [DllImport(Security)]
        static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, string service,
            uint accountLength, string account, out uint length, out IntPtr data, out IntPtr item);
        [DllImport(Security)]
        static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, string service,
            uint accountLength, string account, uint length, byte[] data, IntPtr item);
        [DllImport(Security)]
        static extern int SecKeychainItemFreeContent(IntPtr list, IntPtr data);
        [DllImport(Security)]
        static extern int SecKeychainItemDelete(IntPtr item);
        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        static extern void CFRelease(IntPtr item);

        static bool FindSession(out uint length, out IntPtr data, out IntPtr item) =>
            SecKeychainFindGenericPassword(IntPtr.Zero, (uint)KeychainService.Length, KeychainService,
                (uint)KeychainAccount.Length, KeychainAccount, out length, out data, out item) == 0;

        static void DeleteSession() {
            if (!FindSession(out _, out var data, out var item)) return;
            try { SecKeychainItemDelete(item); }
            finally { SecKeychainItemFreeContent(IntPtr.Zero, data); CFRelease(item); }
        }

        void SaveSession() {
            if (!StaySignedIn || !HasSession) return;
            DeleteSession();
            byte[] bytes = Encoding.UTF8.GetBytes(session + "\n" + sessionExpiry.Ticks);
            try {
                if (SecKeychainAddGenericPassword(IntPtr.Zero, (uint)KeychainService.Length, KeychainService,
                    (uint)KeychainAccount.Length, KeychainAccount, (uint)bytes.Length, bytes, IntPtr.Zero) != 0)
                    log.Warn("Could not save the session in Keychain.");
            } finally {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        async System.Threading.Tasks.Task ResumeAccount() {
            if (!StaySignedIn || !FindSession(out uint length, out var data, out var item)) return;
            try {
                if (length == 0 || length > 100) return;
                var bytes = new byte[(int)length];
                Marshal.Copy(data, bytes, 0, bytes.Length);
                var fields = Encoding.UTF8.GetString(bytes).Split('\n');
                Array.Clear(bytes, 0, bytes.Length);
                if (fields.Length != 2 || !IsToken(fields[0]) || !long.TryParse(fields[1], out long expiry) ||
                    expiry <= DateTime.UtcNow.Ticks || expiry > DateTime.UtcNow.AddHours(8).Ticks) return;
                session = fields[0];
                sessionExpiry = new DateTime(expiry, DateTimeKind.Utc);
            } finally { SecKeychainItemFreeContent(IntPtr.Zero, data); CFRelease(item); }

            try {
                using (var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    await RefreshCloud(timeout.Token);
                if (!HasSession) return;
                SetAccountStatus("Signed in to X-Center ID");
                RunScheduledBackup();
            } catch (System.Net.Http.HttpRequestException) {
                session = null;
                SetAccountStatus("Could not resume X-Center ID. Sign in again.");
            } catch (OperationCanceledException) {
                session = null;
                SetAccountStatus("Cloud connection timed out. Try signing in again.");
            }
        }
    }
}
