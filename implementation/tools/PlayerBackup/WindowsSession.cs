// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PlayerBackup {
    public sealed partial class Plugin {
        static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        static string WindowsSessionPath => Path.Combine(Root, "player-sync", "session.dpapi");

        [StructLayout(LayoutKind.Sequential)]
        struct Blob { public int Size; public IntPtr Data; }

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr pointer);

        static byte[] ProtectWindows(byte[] bytes, bool decrypt) {
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Blob input = new Blob { Size = bytes.Length, Data = pin.AddrOfPinnedObject() };
            try {
                Blob output;
                bool ok = decrypt
                    ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!ok) throw new IOException("Windows could not protect the account session");
                try {
                    var result = new byte[output.Size];
                    Marshal.Copy(output.Data, result, 0, output.Size);
                    return result;
                } finally {
                    if (decrypt) for (int i = 0; i < output.Size; i++) Marshal.WriteByte(output.Data, i, 0);
                    LocalFree(output.Data);
                }
            } finally { pin.Free(); }
        }
    }
}
