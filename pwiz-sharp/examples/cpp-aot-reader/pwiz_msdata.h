/*
 * Native AOT C API for pwiz-sharp's MsData reader.
 *
 * Build:
 *   1. dotnet publish src/MsData.NativeAot/MsData.NativeAot.csproj -c Release -r win-x64
 *      (or -r linux-x64 / osx-arm64). Requires the C/C++ build tools on PATH so
 *      ILC can invoke the platform linker. Output:
 *        bin/Release/net8.0/win-x64/native/pwiz_msdata.dll (Windows)
 *        bin/Release/net8.0/win-x64/native/pwiz_msdata.lib (import library)
 *        bin/Release/net8.0/linux-x64/native/libpwiz_msdata.so (Linux)
 *
 *   2. Link against pwiz_msdata.lib (Windows) or -lpwiz_msdata (POSIX); include
 *      this header. Ship pwiz_msdata.dll/.so alongside your binary (plus the
 *      HDF5 native dlls from the publish dir if you need mzMLb support).
 *
 * Strings:
 *   All char* are UTF-8 null-terminated.
 *
 * Errors:
 *   Functions return 0 on success or a negative error code (see PWIZ_MSDATA_ERR_*).
 *   Functions that produce a string return the FULL UTF-8 byte length of the
 *   string excluding the terminator; if the caller's buffer was too small, the
 *   buffer holds a truncated, null-terminated prefix and the return value tells
 *   the caller to grow + retry.
 */
#ifndef PWIZ_MSDATA_H
#define PWIZ_MSDATA_H

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#  define PWIZ_MSDATA_API __declspec(dllimport)
#else
#  define PWIZ_MSDATA_API
#endif

typedef void* pwiz_msdata_handle;

/* Success / error codes. */
#define PWIZ_MSDATA_OK                 0
#define PWIZ_MSDATA_ERR_INVALID_HANDLE -1
#define PWIZ_MSDATA_ERR_INVALID_ARG    -2
#define PWIZ_MSDATA_ERR_INDEX          -3
#define PWIZ_MSDATA_ERR_IO             -4

/* Opens an MS data file. Supported formats: mzML, mzXML, MGF, MS1/2, mz5/mzMLb (HDF5).
 * On success, *out_handle receives an opaque handle that the caller must release via
 * pwiz_msdata_close. On failure, the error message is available from
 * pwiz_msdata_last_error. */
PWIZ_MSDATA_API int pwiz_msdata_open(const char* path, pwiz_msdata_handle* out_handle);

/* Returns the number of spectra in the file, or a negative error code. */
PWIZ_MSDATA_API int pwiz_msdata_spectrum_count(pwiz_msdata_handle handle);

/* Writes the spectrum id at `index` into `id_buf` (UTF-8, null-terminated, truncated
 * if needed). Returns the FULL UTF-8 byte length (excluding terminator). */
PWIZ_MSDATA_API int pwiz_msdata_spectrum_id(pwiz_msdata_handle handle, int index,
                                            char* id_buf, int id_buf_len);

/* Returns the number of (m/z, intensity) peaks in the spectrum at `index`, reading
 * binary data lazily. Returns a negative error code on failure. */
PWIZ_MSDATA_API int pwiz_msdata_spectrum_peak_count(pwiz_msdata_handle handle, int index);

/* Writes the file's source id (typically the basename without extension) into `buf`.
 * Same length-probe / truncate convention as pwiz_msdata_spectrum_id. */
PWIZ_MSDATA_API int pwiz_msdata_source_id(pwiz_msdata_handle handle, char* buf, int buf_len);

/* Closes the handle. Safe to call with NULL (no-op). */
PWIZ_MSDATA_API void pwiz_msdata_close(pwiz_msdata_handle handle);

/* Returns the last error message set on the current thread (UTF-8, null-terminated,
 * truncated to buf_len-1 bytes). Returns the FULL byte length of the message. */
PWIZ_MSDATA_API int pwiz_msdata_last_error(char* buf, int buf_len);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* PWIZ_MSDATA_H */
