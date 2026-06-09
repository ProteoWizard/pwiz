cmake_minimum_required(VERSION 3.21)
project(cpp_aot_reader CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Where the AOT-published pwiz_msdata native artifacts live. Override via cmake -D
# when invoking from a different layout.
if(NOT DEFINED PWIZ_MSDATA_NATIVE_DIR)
    if(WIN32)
        set(PWIZ_MSDATA_RID "win-x64")
    elseif(APPLE)
        set(PWIZ_MSDATA_RID "osx-arm64")
    else()
        set(PWIZ_MSDATA_RID "linux-x64")
    endif()
    set(PWIZ_MSDATA_NATIVE_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../pwiz/pwiz/src/MsData.NativeAot/bin/Release/net8.0/${PWIZ_MSDATA_RID}/native"
        CACHE PATH "AOT-published pwiz_msdata native artifacts (.dll + .lib on Windows, .so on POSIX)")
endif()

if(NOT EXISTS "${PWIZ_MSDATA_NATIVE_DIR}")
    message(FATAL_ERROR
        "PWIZ_MSDATA_NATIVE_DIR does not exist: ${PWIZ_MSDATA_NATIVE_DIR}\n"
        "Publish the AOT shim first:\n"
        "  dotnet publish pwiz/pwiz/src/MsData.NativeAot/MsData.NativeAot.csproj -c Release -r <rid>")
endif()

add_executable(cpp_aot_reader main.cpp)
target_include_directories(cpp_aot_reader PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}")

if(WIN32)
    target_link_libraries(cpp_aot_reader PRIVATE "${PWIZ_MSDATA_NATIVE_DIR}/pwiz_msdata.lib")
    # Copy pwiz_msdata.dll next to the .exe so we don't have to put the native dir on PATH.
    add_custom_command(TARGET cpp_aot_reader POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${PWIZ_MSDATA_NATIVE_DIR}/pwiz_msdata.dll"
            "$<TARGET_FILE_DIR:cpp_aot_reader>/pwiz_msdata.dll")
else()
    # On POSIX the AOT output is libpwiz_msdata.{so,dylib} — link with -l + a hint dir.
    target_link_directories(cpp_aot_reader PRIVATE "${PWIZ_MSDATA_NATIVE_DIR}")
    target_link_libraries(cpp_aot_reader PRIVATE pwiz_msdata)
    set_target_properties(cpp_aot_reader PROPERTIES
        INSTALL_RPATH_USE_LINK_PATH TRUE
        BUILD_RPATH "${PWIZ_MSDATA_NATIVE_DIR}")
endif()

# ----- Tests (CTest) -----
# Each test runs the cpp_aot_reader binary and matches its output against a regex.
# The wrapper at run-tests.ps1 invokes `ctest --output-junit` and emits a TeamCity
# `##teamcity[importData type='junit']` service message so per-test results show
# up in TC's UI when these tests run under a TC build.
enable_testing()

set(TINY_MZML "${CMAKE_CURRENT_SOURCE_DIR}/../../example_data/tiny.pwiz.1.1.mzML")

# Happy path: open the reference fixture, expect every documented metadata line.
add_test(NAME tiny_mzml_smoke
         COMMAND cpp_aot_reader "${TINY_MZML}")
set_tests_properties(tiny_mzml_smoke PROPERTIES
    PASS_REGULAR_EXPRESSION
    "source id: +urn:lsid:psidev\\.info:mzML\\.instanceDocuments\\.tiny\\.pwiz.*spectrum count: +4.*id=scan=19  peaks=15"
    TIMEOUT 30)

# Error path: file that doesn't exist should exit non-zero. WILL_FAIL flips the
# default "exit 0 = pass" check; FAIL_REGULAR_EXPRESSION makes sure we don't accept
# a crash (FATAL on stderr) — only a clean "I couldn't open that" error.
add_test(NAME missing_file_errors
         COMMAND cpp_aot_reader "${CMAKE_CURRENT_SOURCE_DIR}/no-such-file.mzML")
set_tests_properties(missing_file_errors PROPERTIES
    WILL_FAIL TRUE
    FAIL_REGULAR_EXPRESSION "FATAL"
    TIMEOUT 10)
