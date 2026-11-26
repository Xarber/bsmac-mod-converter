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
        CancellationTokenSource transfer;
        [DllImport("libc", EntryPoint = "chmod")]
        static extern int ChangeFileMode(string path, uint mode);
        const int MaxBackup = 32 * 1024 * 1024;
        [UIValue("account-status")]
        public string AccountStatus { get; private set; } = "Signed out";

        void SetAccountStatus(string value) {
            AccountStatus = value;
            Changed(nameof(AccountStatus));
        }

        [Serializable] sealed class StartReply { public string handle = null; public string authorization = null; }
        [Serializable] sealed class PickupReply { public string session = null; public int expiresIn = 0; }

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
                        reply.EnsureSuccessStatusCode();
                        var pickup = JsonUtility.FromJson<PickupReply>(await reply.Content.ReadAsStringAsync());
                        if (!IsToken(pickup.session) || pickup.expiresIn <= 0 || pickup.expiresIn > 28800)
                            throw new InvalidOperationException("Invalid session");
                        attempt.Token.ThrowIfCancellationRequested();
                        session = pickup.session;
                        sessionExpiry = DateTime.UtcNow.AddSeconds(pickup.expiresIn);
                        SetAccountStatus("Signed in to X-Center ID");
                        break;
                    }
                }
            } catch (OperationCanceledException) {
                if (signIn == attempt) SetAccountStatus("Sign-in expired or was canceled");
            } catch (Exception) {
                if (signIn == attempt) SetAccountStatus("Sign-in failed. Try again.");
            } finally {
                if (signIn == attempt) signIn = null;
                attempt.Dispose();
            }
        }

        [Serializable] sealed class BackupManifest { public int schema = 0; public string gameVersion = null; }

        [UIAction("upload")] public async void Upload() => await Transfer(true);
        [UIAction("download")] public async void Download() => await Transfer(false);

        string CurrentVersion() {
            var contents = Directory.GetParent(Application.dataPath)?.Parent;
            if (contents?.Name != "Contents") throw new InvalidOperationException();
            var instance = contents.Parent?.Parent?.FullName;
            var version = File.ReadAllText(Path.Combine(instance, "BeatSaberVersion.txt")).Trim();
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

        async Task Transfer(bool upload) {
            if (transfer != null) return;
            var ownerSession = session;
            if (ownerSession == null || sessionExpiry <= DateTime.UtcNow) {
                SetAccountStatus("Sign in to transfer backups");
                return;
            }

            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            transfer = operation;
            string staging = null;
            try {
                var version = CurrentVersion();
                var path = Path.Combine(Backups, version + ".zip");
                if (upload && !File.Exists(path)) {
                    SetAccountStatus("Create a local backup first");
                    return;
                }
                if (!upload && File.Exists(path)) {
                    SetAccountStatus("A local backup already exists for " + version);
                    return;
                }
                SetAccountStatus(upload ? "Uploading backup…" : "Downloading backup…");
                using (var request = new HttpRequestMessage(upload ? HttpMethod.Put : HttpMethod.Get, "/api/backups/" + version)) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerSession);
                    if (upload) {
                        using (var input = File.OpenRead(path)) {
                            if (input.Length > MaxBackup) throw new InvalidDataException();
                            CheckBackup(input, version);
                            request.Content = new StreamContent(input);
                            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                            request.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
                            using (var response = await AccountClient.SendAsync(request, operation.Token)) {
                                if (session != ownerSession) return;
                                if (response.StatusCode == HttpStatusCode.PreconditionFailed) {
                                    SetAccountStatus("A cloud backup already exists for " + version);
                                    return;
                                }
                                response.EnsureSuccessStatusCode();
                            }
                        }
                    } else {
                        using (var response = await AccountClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operation.Token)) {
                            if (session != ownerSession) return;
                            if (response.StatusCode == HttpStatusCode.NotFound) {
                                SetAccountStatus("No cloud backup for " + version);
                                return;
                            }
                            response.EnsureSuccessStatusCode();
                            Directory.CreateDirectory(Backups);
                            staging = Path.Combine(Backups, "." + Guid.NewGuid().ToString("N") + ".tmp");
                            using (var input = await response.Content.ReadAsStreamAsync())
                            using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.ReadWrite)) {
                                if (ChangeFileMode(staging, 384) != 0) throw new IOException();
                                var buffer = new byte[65536];
                                int read;
                                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, operation.Token)) > 0) {
                                    if (session != ownerSession) return;
                                    if (output.Length + read > MaxBackup) throw new InvalidDataException();
                                    await output.WriteAsync(buffer, 0, read, operation.Token);
                                }
                                output.Position = 0;
                                CheckBackup(output, version);
                            }
                            if (session != ownerSession) return;
                            File.Move(staging, path);
                            staging = null;
                            RefreshHistory();
                        }
                    }
                }
                if (session == ownerSession) SetAccountStatus(upload ? "Cloud backup saved" : "Cloud backup downloaded");
            } catch (Exception) {
                if (session == ownerSession) SetAccountStatus("Backup transfer failed. Check the file and sign-in.");
            } finally {
                if (staging != null) {
                    try { File.Delete(staging); } catch (IOException) { }
                }
                transfer = null;
                operation.Dispose();
            }
        }

        static bool IsToken(string value) => value != null && value.Length == 43 &&
            value.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                           (c >= '0' && c <= '9') || c == '_' || c == '-');

        [UIAction("sign-out")]
        public void ClearAccount() {
            var attempt = signIn;
            signIn = null;
            attempt?.Cancel();
            transfer?.Cancel();
            var previous = session;
            session = null;
            sessionExpiry = default;
            SetAccountStatus("Signed out");
            if (previous != null) _ = Revoke(previous);
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
