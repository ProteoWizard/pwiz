vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO kpnovoselov/pwiz
    REF 36e8f7098ca05b27c4b65e0ac9646319a77e988b
    SHA512 f5e04f513cac870c099b0179cb079a8e73f9ad0b06abc700f181497e02c65d318cfc99ca926b1880fd2ffb94d0e244e0f66f4615e0aac0791cc02a4489301a22
)

if (NOT VCPKG_BUILD_TYPE OR VCPKG_BUILD_TYPE STREQUAL "release")
    
    vcpkg_execute_required_process(
        COMMAND "${SOURCE_PATH}/mt_build.bat" 
        WORKING_DIRECTORY "${SOURCE_PATH}"
        LOGNAME "build-${TARGET_TRIPLET}-rel"
        SAVE_LOG_FILES pwizbuild/config.log
    )

endif()

if (NOT VCPKG_BUILD_TYPE OR VCPKG_BUILD_TYPE STREQUAL "debug")

    vcpkg_execute_required_process(
        COMMAND "${SOURCE_PATH}/mt_build_debug.bat" 
        WORKING_DIRECTORY "${SOURCE_PATH}"
        LOGNAME "build-${TARGET_TRIPLET}-dbg"
        SAVE_LOG_FILES pwizbuild/config.log
    )

endif()


# copy libs to dlls
set(PWIZ_BUILD_DIR "${SOURCE_PATH}/build-nt-x86")
configure_file("${CMAKE_CURRENT_LIST_DIR}/copy_libs.bat.in" "${SOURCE_PATH}/vcplg_copy_libs.bat" @ONLY)

vcpkg_execute_required_process(
    COMMAND "${SOURCE_PATH}/vcplg_copy_libs.bat" 
    WORKING_DIRECTORY "${SOURCE_PATH}"
    LOGNAME "copy-libs-${TARGET_TRIPLET}"
    SAVE_LOG_FILES pwizbuild/config.log
)

# install headers
set(HEADERS_DEST_DIR "${CURRENT_PACKAGES_DIR}/include/${PORT}")
configure_file("${CMAKE_CURRENT_LIST_DIR}/copy_headers.bat.in" "${SOURCE_PATH}/vcpkg_copy_headers.bat" @ONLY)

vcpkg_execute_required_process(
    COMMAND "${SOURCE_PATH}/vcpkg_copy_headers.bat" 
    WORKING_DIRECTORY "${SOURCE_PATH}"
    LOGNAME "copy-headers-${TARGET_TRIPLET}"
    SAVE_LOG_FILES pwizbuild/config.log
)

# install libs and dlls
configure_file("${CMAKE_CURRENT_LIST_DIR}/copy_binaries.bat.in" "${SOURCE_PATH}/vcpkg_copy_binaries.bat" @ONLY)

vcpkg_execute_required_process(
    COMMAND "${SOURCE_PATH}/vcpkg_copy_binaries.bat" 
    WORKING_DIRECTORY "${SOURCE_PATH}"
    LOGNAME "copy-binaries-${TARGET_TRIPLET}"
    SAVE_LOG_FILES pwizbuild/config.log
)

set(VCPKG_INCLUDE_DIR "../../include/pwiz")
set(VCPKG_DEBUG_LIBS "../../debug/lib/pwiz")
set(VCPKG_RELEASE_LIBS "../../lib/pwiz")
set(VCPKG_DEBUG_BIN "../../debug/bin/pwiz")
set(VCPKG_RELEASE_BIN "../../bin/pwiz")

configure_file("${CMAKE_CURRENT_LIST_DIR}/pwiz-config.cmake.in" "${CURRENT_PACKAGES_DIR}/share/${PORT}/pwiz-config.cmake" @ONLY)

file(INSTALL "${CMAKE_CURRENT_LIST_DIR}/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE")