// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bsmac {
    public static class AutomaticPreparation {
        static readonly string RuntimeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "bsmac", "mod-converter", "automatic");
        static readonly string EnabledFile = Path.Combine(RuntimeDirectory, "enabled");
        static readonly string Python = Path.Combine(RuntimeDirectory, "venv", "bin", "python");
        static readonly string Compiler = Path.Combine(RuntimeDirectory, "compile-metal");
        static readonly Dictionary<(string, uint), Task<string>> Pending = new();
        static readonly ConditionalWeakTable<object, DownloadView> Views = new();

        internal static bool WorkerAvailable => File.Exists(EnabledFile) && File.Exists(Python) && File.Exists(Compiler);
        internal static bool Enabled => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal
            && WorkerAvailable;

        // Called at Vivify's existing-bundle branch, after its requirement check.
        public static bool PrepareExisting(object controller, string original) {
            if (!Enabled) return false;
            try {
                var view = Views.GetValue(controller, key => new DownloadView(key));
                view.StartExisting(original);
                return true;
            } catch (Exception error) {
                Debug.LogError("[BsmacAuto] Could not start preparation: " + error.Message);
                return false;
            }
        }

        // Drive the real downloader to completion under its normal consent,
        // network and cancellation rules. Intercept completion within the same
        // MoveNext call, before the separate PlayView waiter sees the flag.
        public static IEnumerator PrepareAfterDownload(IEnumerator download, object controller, string original, uint crc) {
            if (!Enabled) return download;
            try {
                return DownloadThenPrepare(download, Views.GetValue(controller, key => new DownloadView(key)), original, crc);
            } catch (Exception error) {
                Debug.LogError("[BsmacAuto] Could not attach to the Vivify download: " + error.Message);
                return download;
            }
        }

        public static bool SetDownloadConsentView(bool allowed, object controller) {
            if (!Enabled) return allowed;
            try {
                Views.GetValue(controller, key => new DownloadView(key)).ResetDownloadView(allowed);
            } catch (Exception error) {
                Debug.LogError("[BsmacAuto] Could not update download view: " + error.Message);
            }
            return allowed;
        }

        public static void RememberWaiter(Coroutine waiter, object controller) {
            try {
                Views.GetValue(controller, key => new DownloadView(key)).RememberWaiter(waiter);
            } catch (Exception error) {
                Debug.LogWarning("[BsmacAuto] Could not track download waiter: " + error.Message);
            }
        }

        static IEnumerator DownloadThenPrepare(IEnumerator download, DownloadView view, string original, uint crc) {
            int ticket = view.BeginDownload();
            try {
                while (view.Current(ticket) && download.MoveNext()) {
                    if (view.Finished) break;
                    yield return download.Current;
                }
            } finally {
                (download as IDisposable)?.Dispose();
            }
            if (!view.Current(ticket) || !view.Finished) yield break;
            view.ShowPreparing();
            var prepare = view.Prepare(original, crc, ticket);
            while (prepare.MoveNext()) yield return prepare.Current;
        }

        static Task<string> Request(string original, uint crc) {
            var key = (original, crc);
            lock (Pending) {
                if (Pending.TryGetValue(key, out var task)) return task;
                if (Pending.Count >= 2)
                    return Task.FromResult("Another Vivify bundle is preparing. Try again shortly.");
                task = Task.Run(() => RunWorker(original, crc));
                Pending.Add(key, task);
                _ = task.ContinueWith(_ => { lock (Pending) Pending.Remove(key); });
                return task;
            }
        }

        static string RunWorker(string original, uint crc) {
            return RunLocalWorker("prepare-map", original, crc.ToString(CultureInfo.InvariantCulture)).Error;
        }

        internal static (string Output, string Error) RunLocalWorker(string command, string original, string checksum = null) {
            if (original.IndexOfAny(new[] {'\n', '\r'}) >= 0)
                return (null, "The bundle path contains a line break.");
            try {
                var start = new ProcessStartInfo(Python, "-I -m bsmac_converter.cli " + command + " --request-stdin") {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = RuntimeDirectory
                };
                start.EnvironmentVariables["BSMAC_METAL_COMPILER"] = Compiler;
                using (var process = Process.Start(start)) {
                    if (process == null) return (null, "The local conversion worker could not start.");
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    using (var input = process.StandardInput) {
                        input.WriteLine(original);
                        if (checksum != null) input.WriteLine(checksum);
                    }
                    if (!process.WaitForExit(590000)) {
                        process.Kill();
                        return (null, "Conversion exceeded its time limit. Review the converter log before retrying.");
                    }
                    if (!Task.WaitAll(new Task[] {stdout, stderr}, 2000))
                        return (null, "The conversion worker did not close its output.");
                    if (process.ExitCode == 0) return (stdout.Result.Trim(), null);
                    return (null, !string.IsNullOrWhiteSpace(stderr.Result)
                        ? stderr.Result.Trim() : "The local conversion worker stopped. Check the converter log.");
                }
            } catch (Exception error) {
                return (null, "Worker error: " + error.Message);
            }
        }

        sealed class DownloadView {
            readonly object controller;
            readonly MonoBehaviour runner;
            readonly FieldInfo aborted, finished, newView, progress, lastError, waiter;
            readonly object preparing, error, consent;
            int sequence;

            internal DownloadView(object controller) {
                this.controller = controller;
                Type type = controller.GetType();
                FieldInfo Field(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(type.FullName, name);
                runner = (MonoBehaviour)Field("_assetDownloader").GetValue(controller);
                aborted = Field("_doAbort");
                finished = Field("_downloadFinished");
                progress = Field("_downloadProgress");
                newView = Field("_newView");
                lastError = Field("_lastError");
                waiter = Field("_downloadWaiter");
                preparing = Enum.Parse(newView.FieldType, "Downloading");
                error = Enum.Parse(newView.FieldType, "Error");
                consent = Enum.Parse(newView.FieldType, "Tos");
            }

            internal bool Current(int ticket) => ticket == sequence && !(bool)aborted.GetValue(controller);
            internal bool Finished => (bool)finished.GetValue(controller);
            internal int BeginDownload() => ++sequence;

            internal void ResetDownloadView(bool allowed) {
                progress.SetValue(controller, 0f);
                newView.SetValue(controller, allowed ? preparing : consent);
            }

            internal void RememberWaiter(Coroutine coroutine) => waiter.SetValue(controller, coroutine);

            internal void StartExisting(string original) {
                if (runner == null) throw new InvalidOperationException("Vivify download UI is unavailable");
                int ticket = ++sequence;
                aborted.SetValue(controller, false);
                ShowPreparing();
                runner.StartCoroutine(Prepare(original, 0, ticket));
            }

            internal void ShowPreparing() {
                finished.SetValue(controller, false);
                progress.SetValue(controller, 0f);
                newView.SetValue(controller, preparing);
            }

            internal IEnumerator Prepare(string original, uint crc, int ticket) {
                Debug.Log("[BsmacAuto] Preparing Vivify assets for Metal");
                var task = Request(original, crc);
                while (Current(ticket) && !task.IsCompleted) yield return null;
                if (!Current(ticket)) yield break;
                string message = task.IsFaulted ? "The local worker failed unexpectedly." : task.Result;
                if (message != null) {
                    lastError.SetValue(controller, "Metal asset preparation failed.\n" + message);
                    newView.SetValue(controller, error);
                    Debug.LogError("[BsmacAuto] " + message);
                    yield break;
                }
                progress.SetValue(controller, 1f);
                finished.SetValue(controller, true);
                Debug.Log("[BsmacAuto] Vivify preparation completed");
            }
        }
    }
}
