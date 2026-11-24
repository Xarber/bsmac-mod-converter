// SPDX-License-Identifier: AGPL-3.0-only
// Host synthetic tests using the already installed game's Mono runtime.
// No game startup, game asset loading or SDK installation is involved.
#include <dlfcn.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
int main(int argc, char **argv) {
    if (argc != 5) { fprintf(stderr, "usage: runner libmono managed-directory test-assembly search-path\n"); return 2; }
    void *lib = dlopen(argv[1], RTLD_NOW | RTLD_GLOBAL);
    if (!lib) { fprintf(stderr, "%s\n", dlerror()); return 2; }
    void (*paths)(const char *) = dlsym(lib, "mono_set_assemblies_path");
    void *(*init)(const char *, const char *) = dlsym(lib, "mono_jit_init_version");
    void *(*open_assembly)(void *, const char *) = dlsym(lib, "mono_domain_assembly_open");
    int (*exec)(void *, void *, int, char **) = dlsym(lib, "mono_jit_exec");
    void (*cleanup)(void *) = dlsym(lib, "mono_jit_cleanup");
    void (*map)(void *, const char *, const char *, const char *, const char *) = dlsym(lib, "mono_dllmap_insert");
    if (!paths || !init || !open_assembly || !exec || !cleanup) return 2;
    setenv("MONO_PATH", argv[4], 1);
    if (map) {
        char native[4096];
        snprintf(native, sizeof native, "%s", argv[1]);
        char *slash = strrchr(native, '/');
        if (!slash || (size_t)(slash-native)+22 >= sizeof native) return 2;
        strcpy(slash+1, "libmono-native.dylib");
        map(NULL, "System.Native", NULL, native, NULL);
        map(NULL, "System.Security.Cryptography.Native.Apple", NULL, native, NULL);
    }
    paths(argv[2]);
    void *domain = init("bsmac-regression-tests", "v4.0.30319");
    if (!domain) return 2;
    void *assembly = open_assembly(domain, argv[3]);
    if (!assembly) { fprintf(stderr, "Test assembly did not load\n"); cleanup(domain); return 2; }
    char *arguments[] = { argv[3] };
    int result = exec(domain, assembly, 1, arguments);
    cleanup(domain);
    return result;
}
