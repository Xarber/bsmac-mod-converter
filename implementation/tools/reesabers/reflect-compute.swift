// SPDX-License-Identifier: AGPL-3.0-only
import Foundation
import Metal

guard CommandLine.arguments.count == 2, let device = MTLCreateSystemDefaultDevice() else {
    fatalError("Expected a Metal device and one source file")
}
let source = try String(contentsOfFile: CommandLine.arguments[1], encoding: .utf8)
let library = try device.makeLibrary(source: source, options: nil)
let kernels = library.functionNames.compactMap { library.makeFunction(name: $0) }.filter { $0.functionType == .kernel }
guard kernels.count == 1 else { fatalError("Expected a single compute kernel") }
var reflection: MTLComputePipelineReflection?
_ = try device.makeComputePipelineState(function: kernels[0], options: [.argumentInfo, .bufferTypeInfo], reflection: &reflection)
guard let arguments = reflection?.arguments else { fatalError("Missing compute reflection") }
for argument in arguments where argument.type == .buffer {
    print("Buffer \(argument.index): \(argument.name)")
    print("Size: \(argument.bufferDataSize)")
    for member in argument.bufferStructType?.members ?? [] {
        print("\(member.name): offset \(member.offset)")
    }
}
print("Pipeline compilation: SUCCESS")
