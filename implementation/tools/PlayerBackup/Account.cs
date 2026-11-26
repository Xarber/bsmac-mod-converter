// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using UnityEngine;

namespace PlayerBackup {
    public sealed partial class Plugin {
        static readonly HttpClient AccountClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            BaseAddress = new Uri("https://beatsaber.xcenter.it"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        CancellationTokenSource signIn;
        string session;
        DateTime sessionExpiry;
        [DllImport("libc", EntryPoint = "chmod")]
        static extern int ChangeFileMode(string path, uint mode);
        const int MaxBackup = 32 * 1024 * 1024;
        bool HasSession => session != null && sessionExpiry > DateTime.UtcNow;
        [UIValue("show-sign-in")] public bool ShowSignIn => signIn == null && !HasSession;
        [UIValue("show-account-cancel")] public bool ShowAccountCancel => signIn != null;
        [UIValue("show-sign-out")] public bool ShowSignOut => HasSession;
        [UIValue("account-status")]
        public string AccountStatus { get; private set; } = "Signed out";

        void SetAccountStatus(string value) {
            AccountStatus = value;
            Changed(nameof(AccountStatus));
            Changed(nameof(ShowSignIn));
            Changed(nameof(ShowAccountCancel));
            Changed(nameof(ShowSignOut));
        }

        [Serializable] sealed class StartReply { public string handle = null; public string authorization = null; }
        [Serializable] sealed class PickupReply { public string session = null; public int expiresIn = 0; public bool persistent = false; }

        static async Task<HttpResponseMessage> Post(string path, string json, CancellationToken cancellation) {
            using (var request = new HttpRequestMessage(HttpMethod.Post, path)) {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return await AccountClient.SendAsync(request, cancellation);
            }
        }

        [UIAction("sign-in")]
        public async void SignIn() {
            if (signIn != null || (session != null && sessionExpiry > DateTime.UtcNow)) return;
            var attempt = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            signIn = attempt;
            try {
                SetAccountStatus("Opening X-Center ID…");
                byte[] random = new byte[32];
                using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
                string proof = Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                string challenge;
                using (var hash = SHA256.Create())
                    challenge = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(proof)).Select(value => value.ToString("x2")));
                StartReply start;
                using (var reply = await Post("/api/account/start", "{\"challenge\":\"" + challenge + "\"}", attempt.Token)) {
                    reply.EnsureSuccessStatusCode();
                    start = JsonUtility.FromJson<StartReply>(await reply.Content.ReadAsStringAsync());
                }
                var authorization = new Uri(start.authorization);
                if (authorization.Scheme != "https" || authorization.Host != "id.xcenter.it" ||
                    !authorization.IsDefaultPort || authorization.UserInfo != "" || authorization.AbsolutePath != "/auth" ||
                    !IsToken(start.handle)) throw new InvalidOperationException("Invalid browser handoff");
                attempt.Token.ThrowIfCancellationRequested();
                Application.OpenURL(authorization.AbsoluteUri);
                SetAccountStatus("Finish signing in in your browser");
                for (;;) {
                    await Task.Delay(TimeSpan.FromSeconds(3), attempt.Token);
                    using (var reply = await Post("/api/account/pickup",
                        "{\"handle\":\"" + start.handle + "\",\"proof\":\"" + proof + "\"}", attempt.Token)) {
                        if (reply.StatusCode == HttpStatusCode.Accepted) continue;
                        if (reply.StatusCode == HttpStatusCode.Forbidden) {
                            var failure = await reply.Content.ReadAsStringAsync();
                            signIn = null;
                            SetAccountStatus(failure.Contains("client_rejected")
                                ? "X-Center ID rejected the server credentials"
                                : "X-Center ID could not complete sign-in");
                            return;
                        }
                        reply.EnsureSuccessStatusCode();
                        var pickup = JsonUtility.FromJson<PickupReply>(await reply.Content.ReadAsStringAsync());
                        if (!IsToken(pickup.session) || pickup.expiresIn <= 0 || pickup.expiresIn > (pickup.persistent ? MaxPersistentSeconds : 28800))
                            throw new InvalidOperationException("Invalid session");
                        attempt.Token.ThrowIfCancellationRequested();
                        session = pickup.session;
                        sessionExpiry = DateTime.UtcNow.AddSeconds(pickup.expiresIn);
                        signIn = null;
                        persistentSession = pickup.persistent;
                        if (persistentSession) SaveSession();
                        else DeleteSession();
                        SetAccountStatus("Signed in to X-Center ID");
                        try { await RefreshCloud(attempt.Token); }
                        catch (HttpRequestException) { SetAccountStatus("Cloud unavailable · allow app data access"); }
                        await PublishExports(attempt.Token);
                        RunScheduledBackup();
                        break;
                    }
                }
            } catch (OperationCanceledException) {
                if (signIn == attempt) SetAccountStatus("Sign-in expired or was canceled");
            } catch (Exception) {
                if (signIn == attempt) SetAccountStatus("Sign-in failed. Try again.");
            } finally {
                if (signIn == attempt) {
                    signIn = null;
                    SetAccountStatus(AccountStatus);
                }
                attempt.Dispose();
            }
        }

        [Serializable] sealed class BackupManifest { public int schema = 0; public string gameVersion = null; }

        string CurrentVersion() {
            var version = File.ReadAllText(Path.Combine(InstancePath(), "BeatSaberVersion.txt")).Trim();
            if (!Regex.IsMatch(version, @"^[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}$"))
                throw new InvalidOperationException();
            return version;
        }

        static void CheckBackup(Stream data, string version) {
            using (var archive = new ZipArchive(data, ZipArchiveMode.Read, true)) {
                var entry = archive.GetEntry("manifest.json");
                if (entry == null || entry.Length > 16 * 1024 * 1024) throw new InvalidDataException();
                using (var reader = new StreamReader(entry.Open())) {
                    var manifest = JsonUtility.FromJson<BackupManifest>(reader.ReadToEnd());
                    if (manifest?.schema != 1 || manifest.gameVersion != version) throw new InvalidDataException();
                }
            }
            data.Position = 0;
        }

        [Serializable] sealed class CloudVersion { public string version = null; }
        [Serializable] sealed class CloudHistory { public CloudVersion[] versions = null; }

        async Task RefreshCloud(CancellationToken cancellation) {
            var owner = session;
            using (var reply = await SendBackup(HttpMethod.Get, "/api/backups", owner, cancellation)) {
                if (reply.StatusCode == HttpStatusCode.Forbidden) {
                    ClearAccount();
                    SetAccountStatus("Sign in and allow app data access");
                    throw new HttpRequestException("App data permission required");
                }
                reply.EnsureSuccessStatusCode();
                var history = Newtonsoft.Json.JsonConvert.DeserializeObject<CloudHistory>(await reply.Content.ReadAsStringAsync());
                if (session != owner) return;
                cloudVersions = (history?.versions ?? Array.Empty<CloudVersion>())
                    .Select(item => item.version).Where(IsSnapshot).ToArray();
                RefreshHistory();
            }
        }

        static async Task<HttpResponseMessage> SendBackup(HttpMethod method, string path, string owner,
                CancellationToken cancellation) {
            if (owner == null) throw new InvalidOperationException("Sign in first");
            using (var request = new HttpRequestMessage(method, path)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
                return await AccountClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            }
        }

        async Task PublishExports(CancellationToken cancellation) {
            var owner = session;
            try {
                if (owner == null) return;
                var python = Path.Combine(Root, "player-sync", "venv", "bin", "python");
                var start = new System.Diagnostics.ProcessStartInfo(python) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    Arguments = "-I -m bsmac_converter.player_exports " + Quote(InstancePath()) +
                        " " + Quote(Application.persistentDataPath),
                };
                string payload;
                using (var process = System.Diagnostics.Process.Start(start))
                using (cancellation.Register(() => Stop(process))) {
                    payload = await process.StandardOutput.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode != 0 || session != owner || cancellation.IsCancellationRequested) return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/exports")) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using (var reply = await AccountClient.SendAsync(request, cancellation))
                        reply.EnsureSuccessStatusCode();
                }
            } catch (Exception error) {
                log.Warn("Shared Beat Saber records were not updated: " + error.GetType().Name);
            }
        }

        async Task UploadBackup(string version, CancellationToken cancellation) {
            var owner = session;
            var path = "/api/backups/" + version;
            string revision;
            using (var reply = await SendBackup(HttpMethod.Head, path, owner, cancellation)) {
                if (reply.StatusCode == HttpStatusCode.NotFound) revision = "\"0\"";
                else {
                    reply.EnsureSuccessStatusCode();
                    revision = reply.Headers.ETag?.Tag ?? throw new InvalidDataException();
                }
            }
            if (owner != session) throw new OperationCanceledException();
            using (var input = File.OpenRead(LocalBackup(version)))
            using (var request = new HttpRequestMessage(HttpMethod.Put, path)) {
                if (input.Length > MaxBackup) throw new InvalidDataException("Backup exceeds the 32 MB transfer limit");
                CheckBackup(input, SnapshotVersion(version));
                request.Content = new StreamContent(input);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
                request.Headers.TryAddWithoutValidation("If-Match", revision);
                using (var response = await AccountClient.SendAsync(request, cancellation))
                    response.EnsureSuccessStatusCode();
            }
        }

        async Task DownloadBackup(string version, CancellationToken cancellation) {
            var owner = session;
            Directory.CreateDirectory(Backups);
            var staging = Path.Combine(Backups, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var reply = await SendBackup(HttpMethod.Get, "/api/backups/" + version, owner, cancellation)) {
                    reply.EnsureSuccessStatusCode();
                    using (var input = await reply.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.ReadWrite)) {
                        if (ChangeFileMode(staging, 384) != 0) throw new IOException();
                        var buffer = new byte[65536];
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0) {
                            if (owner != session) throw new OperationCanceledException();
                            if (output.Length + read > MaxBackup) throw new InvalidDataException();
                            await output.WriteAsync(buffer, 0, read, cancellation);
                        }
                        output.Position = 0;
                        CheckBackup(output, SnapshotVersion(version));
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (owner != session) throw new OperationCanceledException();
                File.Move(staging, LocalBackup(version));
            } finally {
                if (File.Exists(staging)) {
                    try { File.Delete(staging); } catch (IOException) { }
                }
            }
        }

        static bool IsToken(string value) => value != null && value.Length == 43 &&
            value.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                           (c >= '0' && c <= '9') || c == '_' || c == '-');

        [UIAction("sign-out")]
        public void ClearAccount() => EndAccount(true);

        void EndAccount(bool revoke) {
            var attempt = signIn;
            signIn = null;
            attempt?.Cancel();
            var previous = session;
            session = null;
            sessionExpiry = default;
            persistentSession = false;
            cloudVersions = Array.Empty<string>();
            SetAccountStatus("Signed out");
            RefreshHistory();
            if (revoke) {
                DeleteSession();
                if (previous != null) _ = Revoke(previous);
            }
        }

        static async Task Revoke(string previous) {
            try {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/logout")) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", previous);
                    using (await AccountClient.SendAsync(request)) { }
                }
            } catch (HttpRequestException) { }
            catch (OperationCanceledException) { }
        }
    }
}
