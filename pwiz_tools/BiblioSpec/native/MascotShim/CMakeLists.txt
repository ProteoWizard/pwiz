# MascotShim — native C wrapper around Matrix Science's msparser.

cmake_minimum_required(VERSION 3.20)
project(MascotShim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Required. Defaults match the cpp Jamfile's `msparser-path` resolver
# (Jamroot.jam:1273/1287) — the parent csproj passes the platform-specific
# default in via -DMASCOT_PARSER_PATH=...
set(MASCOT_PARSER_PATH "" CACHE PATH
    "Root of the msparser distribution (vs2015/ for Windows, gnu/ for Linux). \
     Must contain include/ and lib/ subdirs.")

if (NOT MASCOT_PARSER_PATH)
    message(FATAL_ERROR
        "MASCOT_PARSER_PATH is required. Pass -DMASCOT_PARSER_PATH=... or build "
        "BiblioSpec with -p:MascotSupport=false to skip the Mascot wrapper.")
endif()

if (NOT EXISTS "${MASCOT_PARSER_PATH}/include/ms_mascotresfile_dat.hpp")
    message(FATAL_ERROR
        "MASCOT_PARSER_PATH='${MASCOT_PARSER_PATH}' does not look like an msparser "
        "distribution (include/ms_mascotresfile_dat.hpp missing).")
endif()

add_library(MascotShim SHARED
    MascotShim.cpp
    MascotShim.h
)

target_compile_definitions(MascotShim PRIVATE MASCOT_SHIM_EXPORTS)
target_include_directories(MascotShim PRIVATE "${MASCOT_PARSER_PATH}/include")

if (MSVC)
    # Match msparser's CRT per configuration: msparser.lib (Release) was
    # built with /MD, msparserD.lib (Debug) with /MDd. STL types like
    # std::string/std::vector returned by value have allocator layouts that
    # match across the DLL boundary only when both sides use the same CRT.
    # A /MD shim linked against msparserD silently corrupted std::string
    # bytes on every other call. Same rule cpp's Jamfile follows via
    # <runtime-debugging> picking msparserD only in Debug.
    set_property(TARGET MascotShim PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")

    # Pin the export list.
    target_sources(MascotShim PRIVATE MascotShim.def)

    # /EHa instead of /EHs: msparser sometimes surfaces failures (missing
    # files, malformed input) as SEH access violations rather than C++
    # exceptions. /EHa lets our catch(...) in MascotShim.cpp intercept those
    # so the C# side gets a clean SdkException instead of a crashed test
    # host. The small perf cost is irrelevant for the shim's call sites.
    target_compile_options(MascotShim PRIVATE /W4 /permissive- /EHa)
    # Silence msparser-internal warnings inside its header set (C4100
    # 'unreferenced parameter' is everywhere; we don't control msparser
    # sources). Leave our own /W4 active for MascotShim.cpp.
    target_compile_options(MascotShim PRIVATE /wd4100)

    # msparser ships separate Debug / Release import libs; the .dll names
    # match (msparserD.dll vs msparser.dll). The .dll itself has to land
    # next to MascotShim.dll on the load path — the csproj handles the
    # second copy.
    target_link_libraries(MascotShim PRIVATE
        $<IF:$<CONFIG:Debug>,${MASCOT_PARSER_PATH}/lib/msparserD.lib,${MASCOT_PARSER_PATH}/lib/msparser.lib>
    )
else()
    target_compile_options(MascotShim PRIVATE -Wall -Wextra -fvisibility=hidden)
    # Linux msparser ships as a static archive (libmsparser.a). Static-link
    # it into our .so so consumers only see libMascotShim.so as a runtime
    # dependency.
    target_link_libraries(MascotShim PRIVATE
        "${MASCOT_PARSER_PATH}/lib/libmsparser.a"
        pthread
    )
endif()

# Default PREFIX keeps the platform-native convention: MascotShim.dll on
# Windows, libMascotShim.so on Linux. .NET's `[DllImport("MascotShim")]`
# resolves both forms automatically.
set_target_properties(MascotShim PROPERTIES
    OUTPUT_NAME MascotShim
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
)
