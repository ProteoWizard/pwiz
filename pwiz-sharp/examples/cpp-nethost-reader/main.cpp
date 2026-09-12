// Reads an MS data file — including VENDOR formats — from a native C++ process, with
// pwiz-sharp loaded through nethost. See README.md for how this differs from
// ../cpp-aot-reader, which shares the managed C API but cannot carry vendor readers.
//
//   cpp_nethost_reader <assembly-dir> <data-file>
//
// <assembly-dir> is the build output of pwiz/src/MsData.Hosted (holds Pwiz.MsData.Hosted.dll
// plus its .runtimeconfig.json and the vendor assemblies). CMake bakes the configured value
// in as a default, so passing just a data file works for the common case.

#include <cstdio>
#include <cstdlib>
#include <string>

#include "pwiz_msdata_host.hpp"

namespace ms = pwiz::msdata::hosted;

#ifndef PWIZ_MSDATA_HOSTED_DIR
#  define PWIZ_MSDATA_HOSTED_DIR ""
#endif

int main(int argc, char** argv) {
    std::string assembly_dir = PWIZ_MSDATA_HOSTED_DIR;
    std::string data_path;

    if (argc == 2) {
        data_path = argv[1];
    } else if (argc == 3) {
        assembly_dir = argv[1];
        data_path = argv[2];
    } else {
        std::fprintf(stderr, "usage: %s [assembly-dir] <data-file>\n", argv[0]);
        return 2;
    }
    if (assembly_dir.empty()) {
        std::fprintf(stderr, "FATAL: no assembly dir (build-time default empty; pass one)\n");
        return 2;
    }

    try {
        // 1. Boot the runtime and bind the exports.
        ms::HostedRuntime runtime(assembly_dir);

        // 2. Build the reader list. In this build that also installs the vendor SDK
        //    assembly resolver, which MUST happen before any vendor reader is touched —
        //    hence an explicit call rather than lazy initialization inside open().
        runtime.init();

        // 3. From here the code is backend-agnostic: same operations, same semantics as
        //    the AOT example.
        ms::File file(runtime, data_path);

        std::printf("backend:         nethost (vendor readers available)\n");
        std::printf("source id:       %s\n", file.source_id().c_str());

        const int count = file.spectrum_count();
        std::printf("spectrum count:  %d\n", count);

        for (int i = 0; i < count && i < 3; ++i) {
            std::printf("  [%6d] id=%s  peaks=%d\n", i,
                        file.spectrum_id(i).c_str(), file.peak_count(i));
        }
        return 0;
    } catch (const ms::Error& e) {
        std::fprintf(stderr, "FATAL: %s (code %d)\n", e.what(), e.code());
        return 1;
    } catch (const std::exception& e) {
        std::fprintf(stderr, "FATAL: %s\n", e.what());
        return 1;
    }
}
