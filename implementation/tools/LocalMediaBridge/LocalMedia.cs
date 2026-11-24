// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;

namespace Bsmac {
    public static class LocalMedia {
        public static string FileUrl(string path) {
            if (path == null) throw new ArgumentNullException(nameof(path));
            // Preserve the existing helper's URL passthrough, including remote
            // service URLs. Only filesystem paths receive file-URI encoding.
            if (path.Contains("://")) return path;
            string absolute = Path.GetFullPath(path);
            if (!absolute.StartsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("Expected a native macOS absolute file path", nameof(path));
            return "file://" + string.Join("/", absolute.Split('/').Select(Uri.EscapeDataString));
        }
    }
}
