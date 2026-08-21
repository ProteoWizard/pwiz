cmake_minimum_required(VERSION 3.21)
project(cpp_nethost_reader CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Anchor derived paths on PWIZ_SHARP_ROOT, same convention as ../cpp-aot-reader. This file
# sits at pwiz-sharp/examples/cpp-nethost-reader/CMakeLists.txt, so two parents up.
if(NOT DEFINED PWIZ_SHARP_ROOT)
    get_filename_component(PWIZ_SHARP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../.." ABSOLUTE
        CACHE PATH "pwiz-sharp/ checkout root")
endif()
if(NOT EXISTS "${PWIZ_SHARP_ROOT}/Pwiz.sln")
    message(FATAL_ERROR
        "PWIZ_SHARP_ROOT='${PWIZ_SHARP_ROOT}' doesn't look like a pwiz-sharp checkout "
        "(no Pwiz.sln inside). Pass -DPWIZ_SHARP_ROOT=<path> when invoking cmake.")
endif()

# Build output of pwiz/src/MsData.Hosted — the managed assembly, its runtimeconfig.json,
# and the vendor assemblies. Unlike the AOT example we don't LINK anything from here; the
# path is only baked in as a runtime default so the exe can be run without arguments.
if(NOT DEFINED PWIZ_MSDATA_HOSTED_DIR)
    set(PWIZ_MSDATA_HOSTED_DIR
        "${PWIZ_SHARP_ROOT}/pwiz/src/MsData.Hosted/bin/Release/net8.0"
        CACHE PATH "Build output of MsData.Hosted (managed assembly + runtimeconfig.json)")
endif()
if(NOT EXISTS "${PWIZ_MSDATA_HOSTED_DIR}/Pwiz.MsData.Hosted.runtimeconfig.json")
    message(FATAL_ERROR
        "No Pwiz.MsData.Hosted.runtimeconfig.json under:\n  ${PWIZ_MSDATA_HOSTED_DIR}\n"
        "Build the hosted shim first:\n"
        "  dotnet build pwiz/src/MsData.Hosted/MsData.Hosted.csproj -c Release -p:IAgreeToVendorLicenses=true\n"
        "(If the .dll is there but the .json isn't, the csproj is missing EnableDynamicLoading.)")
endif()

# ---- nethost ----
# libnethost is the ONLY piece that must be linked: it resolves where hostfxr lives. It ships
# in the targeting pack, not the shared runtime, so locate it under dotnet/packs. Everything
# past get_hostfxr_path() is looked up by name at runtime.
if(NOT DEFINED PWIZ_NETHOST_DIR)
    if(WIN32)
        set(_host_pack_root "$ENV{ProgramFiles}/dotnet/packs/Microsoft.NETCore.App.Host.win-x64")
        set(_rid_native "runtimes/win-x64/native")
    elseif(APPLE)
        set(_host_pack_root "/usr/local/share/dotnet/packs/Microsoft.NETCore.App.Host.osx-arm64")
        set(_rid_native "runtimes/osx-arm64/native")
    else()
        set(_host_pack_root "/usr/share/dotnet/packs/Microsoft.NETCore.App.Host.linux-x64")
        set(_rid_native "runtimes/linux-x64/native")
    endif()
    # Prefer the newest 8.x pack: the shim targets net8.0, and the hosting ABI is stable
    # across patch versions.
    file(GLOB _host_packs "${_host_pack_root}/8.*")
    if(NOT _host_packs)
        # Fall back to any version present — hostfxr's C API is versioned by contract, not
        # by pack version, so a newer pack still hosts a net8.0 assembly correctly.
        file(GLOB _host_packs "${_host_pack_root}/*")
    endif()
    if(NOT _host_packs)
        message(FATAL_ERROR
            "No Microsoft.NETCore.App.Host pack under ${_host_pack_root}.\n"
            "Install the .NET SDK, or pass -DPWIZ_NETHOST_DIR=<dir containing nethost.h>.")
    endif()
    list(SORT _host_packs)
    list(GET _host_packs -1 _newest_pack)
    set(PWIZ_NETHOST_DIR "${_newest_pack}/${_rid_native}"
        CACHE PATH "Directory holding nethost.h / hostfxr.h / libnethost")
endif()

# The pack ships nethost two ways and the choice is not free:
#
#   nethost.lib     1.7 KB import library -> nethost.dll must sit beside the .exe
#   libnethost.lib  1.5 MB static library, but built /MT
#
# The static one is tempting (single self-contained .exe) but it is compiled against the
# STATIC CRT, so linking it forces /MT on the entire consuming application — mixing that
# with the default /MD gives LNK2038 "RuntimeLibrary mismatch" on every object. Most real
# C++ hosts use /MD, and changing an existing application's CRT to satisfy a hosting shim
# is not a reasonable ask. So the DLL variant is the default here and static is opt-in.
option(PWIZ_NETHOST_STATIC "Link libnethost statically (forces the static /MT CRT on this target)" OFF)

if(PWIZ_NETHOST_STATIC)
    find_library(PWIZ_NETHOST_LIB NAMES libnethost HINTS "${PWIZ_NETHOST_DIR}" REQUIRED)
else()
    find_library(PWIZ_NETHOST_LIB NAMES nethost HINTS "${PWIZ_NETHOST_DIR}" REQUIRED)
endif()
message(STATUS "nethost: ${PWIZ_NETHOST_LIB}")
message(STATUS "hosted assemblies: ${PWIZ_MSDATA_HOSTED_DIR}")

add_executable(cpp_nethost_reader main.cpp)
target_include_directories(cpp_nethost_reader PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}"
    "${PWIZ_NETHOST_DIR}")
target_link_libraries(cpp_nethost_reader PRIVATE "${PWIZ_NETHOST_LIB}")
target_compile_definitions(cpp_nethost_reader PRIVATE
    PWIZ_MSDATA_HOSTED_DIR="${PWIZ_MSDATA_HOSTED_DIR}")

if(PWIZ_NETHOST_STATIC)
    # NETHOST_USE_AS_STATIC drops the dllimport decoration in nethost.h; without it the
    # link fails looking for __imp_get_hostfxr_path. The CRT switch is what actually makes
    # libnethost.lib link — see the comment on the option above.
    target_compile_definitions(cpp_nethost_reader PRIVATE NETHOST_USE_AS_STATIC)
    set_property(TARGET cpp_nethost_reader PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
elseif(WIN32)
    # Default path: nethost.dll has to sit next to the .exe.
    add_custom_command(TARGET cpp_nethost_reader POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${PWIZ_NETHOST_DIR}/nethost.dll"
            "$<TARGET_FILE_DIR:cpp_nethost_reader>/nethost.dll")
endif()

# ----- Tests (CTest) -----
# Mirrors ../cpp-aot-reader's shape: run the binary, match a regex. The mzML case proves the
# hosting path end to end without needing vendor data, so it runs anywhere. The vendor case
# is opt-in because it needs a real vendor file AND a primed vendor SDK cache — see README.
enable_testing()

set(TINY_MZML "${CMAKE_CURRENT_SOURCE_DIR}/../../example_data/tiny.pwiz.1.1.mzML")

add_test(NAME hosted_tiny_mzml_smoke
         COMMAND cpp_nethost_reader "${TINY_MZML}")
set_tests_properties(hosted_tiny_mzml_smoke PROPERTIES
    PASS_REGULAR_EXPRESSION
    "backend: +nethost.*source id: +urn:lsid:psidev\\.info:mzML\\.instanceDocuments\\.tiny\\.pwiz.*spectrum count: +4"
    TIMEOUT 120)

add_test(NAME hosted_missing_file_errors
         COMMAND cpp_nethost_reader "${CMAKE_CURRENT_SOURCE_DIR}/no-such-file.mzML")
set_tests_properties(hosted_missing_file_errors PROPERTIES
    WILL_FAIL TRUE
    TIMEOUT 120)

# Point this at e.g. a Thermo .raw to assert the thing the AOT backend structurally cannot do.
if(PWIZ_VENDOR_TEST_FILE)
    add_test(NAME hosted_vendor_read
             COMMAND cpp_nethost_reader "${PWIZ_VENDOR_TEST_FILE}")
    set_tests_properties(hosted_vendor_read PROPERTIES
        PASS_REGULAR_EXPRESSION "spectrum count: +[1-9][0-9]*"
        TIMEOUT 300)
endif()
