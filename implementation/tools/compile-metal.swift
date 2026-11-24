// SPDX-License-Identifier: AGPL-3.0-only
import Foundation
import Metal

guard let device = MTLCreateSystemDefaultDevice() else {
    fatalError("Metal unavailable")
}

print("GPU: \(device.name)")

var passed = 0
var failed = 0

let arguments = Array(CommandLine.arguments.dropFirst())
let paths: [String]
if arguments.first == "--manifest", arguments.count == 2 {
    paths = try String(contentsOfFile: arguments[1], encoding: .utf8).split(separator: "\n").map(String.init)
} else {
    paths = arguments
}
for path in paths {
    let url = URL(fileURLWithPath: path)

    print("\n=== \(url.lastPathComponent) ===")

    do {
        let source = try String(
            contentsOf: url,
            encoding: .utf8
        )

        let library = try device.makeLibrary(
            source: source,
            options: nil
        )

        print("Functions:", library.functionNames)
        print("COMPILATION SUCCESS")

        passed += 1
    } catch {
        print("COMPILATION FAILED:", error)
        failed += 1
    }
}

print("\n=== FINAL RESULTS ===")
print("Compiled:", passed)
print("Failed:", failed)

exit(failed == 0 ? 0 : 1)
