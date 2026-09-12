// Minimal C++ example that loads an MS data file via pwiz-sharp's Native AOT shim.
// Uses the header-only RAII wrapper in pwiz_msdata.hpp; see pwiz_msdata.h for the
// underlying flat-C API the wrapper builds on.
//
// Build (from this directory, after publishing the AOT library — see README.md):
//   cmake -S . -B build
//   cmake --build build --config Release
//
// Run:
//   build/Release/cpp_aot_reader path/to/sample.mzML
#include <cstdio>
#include <exception>
#include <string>

#include "pwiz_msdata.hpp"

namespace ms = pwiz::msdata;

int main(int argc, char** argv)
{
    if (argc != 2)
    {
        std::fprintf(stderr, "usage: %s <ms-data-file>\n", argv[0]);
        return 2;
    }

    try
    {
        ms::File file(argv[1]);

        const int count = file.spectrumCount();
        std::printf("source id:       %s\n", file.sourceId().c_str());
        std::printf("spectrum count:  %d\n", count);

        // Probe a few spectra from the front, middle, and end of the file. Only ask
        // for peakCount() on the first one — that's the call that actually reads
        // binary data, and we don't want to triple the work for a quick sniff.
        if (count > 0)
        {
            for (int idx : { 0, count / 2, count - 1 })
            {
                auto spectrum = file[idx];
                if (idx == 0)
                    std::printf("  [%6d] id=%s  peaks=%d\n", idx, spectrum.id().c_str(), spectrum.peakCount());
                else
                    std::printf("  [%6d] id=%s\n", idx, spectrum.id().c_str());
            }
        }

        // Range-for also works on the file — uncomment to walk every spectrum:
        // for (auto spectrum : file) { ... }
        return 0;
    }
    catch (const ms::Error& e)
    {
        std::fprintf(stderr, "pwiz error (rc=%d): %s\n", e.code(), e.what());
        return 1;
    }
    catch (const std::exception& e)
    {
        std::fprintf(stderr, "error: %s\n", e.what());
        return 1;
    }
}
