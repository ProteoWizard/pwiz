// MobilionShim.h — flat C surface around MBISDK's C++ class API.
// Built into MobilionShim.dll; consumed via P/Invoke from
// pwiz-sharp/src/Vendor/Mobilion/MobilionShimNative.cs.
//
// Naming: every exported symbol starts with `mbi_` for clarity in stack
// traces / map files. All getters that may fail return an int status
// (0 = success, non-zero = error code; see MBI_OK / MBI_ERR_*) and write the
// real value through an out-pointer.
//
// Ownership:
//   - mbi_file_t* / mbi_frame_t* are opaque handles that own a wrapper struct
//     holding a shared_ptr / unique_ptr to the underlying C++ object. Callers
//     must call mbi_file_free() / mbi_frame_free() exactly once per handle.
//   - Array getters use the two-call pattern: call once with `out_buf=NULL,
//     buf_size=0` to query the required size, then call again with the
//     allocated buffer. The "actual size returned" is written through
//     `*out_count`.
//   - String getters: same two-call pattern; pass `out_buf` + `out_buf_size`.
//     Return value is the required size including the NUL terminator. Zero
//     is returned only on hard failure.

#ifndef MOBILION_SHIM_H_INCLUDED
#define MOBILION_SHIM_H_INCLUDED

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#  ifdef MOBILION_SHIM_EXPORTS
#    define MBI_API __declspec(dllexport)
#  else
#    define MBI_API __declspec(dllimport)
#  endif
#else
#  define MBI_API __attribute__((visibility("default")))
#endif

#include <stddef.h>
#include <stdint.h>

/* ---- Opaque handles ----------------------------------------------------- */
typedef struct mbi_file_   mbi_file_t;
typedef struct mbi_frame_  mbi_frame_t;

/* ---- Result codes ------------------------------------------------------- */
#define MBI_OK                       0
#define MBI_ERR_INVALID_HANDLE      -1
#define MBI_ERR_NULL_BUFFER         -2
#define MBI_ERR_NOT_ENOUGH_SPACE    -3   /* caller buffer too small */
#define MBI_ERR_SDK_EXCEPTION       -4   /* MBI SDK call threw — message via mbi_last_error_message */
#define MBI_ERR_NO_DATA             -5   /* metadata key missing / array empty */

/* ---- Diagnostics -------------------------------------------------------- */
/* Returns the last std::exception::what() message captured by the shim,
 * or empty string if the last call succeeded. Buffer is per-thread. */
MBI_API const char* mbi_last_error_message(void);

/* ---- MBIFile lifecycle -------------------------------------------------- */
MBI_API mbi_file_t* mbi_file_open(const char* path);
MBI_API void        mbi_file_free(mbi_file_t* h);

/* Calls MBIFile::Init(). Returns MBI_OK on success. */
MBI_API int  mbi_file_init(mbi_file_t* h);
MBI_API void mbi_file_close(mbi_file_t* h);  /* idempotent */

/* MBIFile::NumFrames(). Returns -1 on error. */
MBI_API int  mbi_file_num_frames(mbi_file_t* h);

/* Borrows a Frame handle (1-based index, matching the SDK). The handle owns
 * its own shared_ptr<Frame>; caller must mbi_frame_free() to release. */
MBI_API mbi_frame_t* mbi_file_get_frame(mbi_file_t* h, int frame_index_1based);

/* ---- Global metadata (MBIFile::GetGlobalMetaData) ----------------------- */
/* Read a string key from global metadata into `out_buf`. `*out_required`
 * receives the size including NUL (so callers can grow + retry).
 *   `out_buf == NULL` is allowed for size-only queries; `*out_required` is
 *   still set. Returns MBI_OK / MBI_ERR_NOT_ENOUGH_SPACE / MBI_ERR_NO_DATA. */
MBI_API int mbi_file_global_read_string(mbi_file_t* h, const char* key,
                                        char* out_buf, int out_buf_size,
                                        int* out_required);
MBI_API int mbi_file_global_read_double(mbi_file_t* h, const char* key,
                                        double* out_value);

/* ---- CCS calibration (MBIFile::GetEyeOnCCSCalibration) ------------------ */
/* Probes whether GetEyeOnCCSCalibration().GetAtSurf() succeeds. Returns 1
 * for yes, 0 for no, -1 for hard error. */
MBI_API int    mbi_file_can_convert_ccs(mbi_file_t* h);
MBI_API double mbi_file_ccs_min(mbi_file_t* h);
MBI_API double mbi_file_ccs_max(mbi_file_t* h);
MBI_API double mbi_file_arrival_time_to_ccs(mbi_file_t* h, double drift_time, double abs_mz_charge);
MBI_API double mbi_file_ccs_to_arrival_time(mbi_file_t* h, double ccs, double abs_mz_charge);

/* ---- Frame -------------------------------------------------------------- */
MBI_API void   mbi_frame_free(mbi_frame_t* h);

/* Frame::GetCE(0) — used to detect MS1 vs MS2; cpp uses index 0 as a probe. */
MBI_API double mbi_frame_get_ce_at(mbi_frame_t* h, int64_t index);
MBI_API double mbi_frame_collision_energy(mbi_frame_t* h);    /* Frame::GetCollisionEnergy */
MBI_API double mbi_frame_time(mbi_frame_t* h);
MBI_API int64_t mbi_frame_total_intensity(mbi_frame_t* h);    /* Frame::GetFrameTotalIntensity */
MBI_API double mbi_frame_arrival_bin_time_offset(mbi_frame_t* h, size_t bin_index);

/* Batch arrival-bin-time-offset: calls Frame::GetArrivalBinTimeOffset N times
 * inside the shim. The SDK has no batch overload, but bundling the loop in one
 * P/Invoke amortizes the marshaling cost — combine-IMS spectra ask for drift
 * at every COO cell (thousands), which dominated the per-spectrum time before. */
MBI_API int mbi_frame_arrival_bin_time_offsets_batch(mbi_frame_t* h,
                                                     const int64_t* scan_indices,
                                                     int count,
                                                     double* out_drift);

/* Frame metadata via FrameMetadata accessor. */
MBI_API int mbi_frame_metadata_read_string(mbi_frame_t* h, const char* key,
                                           char* out_buf, int out_buf_size,
                                           int* out_required);
MBI_API int mbi_frame_metadata_read_double(mbi_frame_t* h, const char* key,
                                           double* out_value);

/* Frame::GetNonZeroScanIndices(). Two-call pattern. Output values are
 * size_t cast to int64_t for ABI stability. */
MBI_API int mbi_frame_get_nonzero_scan_indices(mbi_frame_t* h,
                                               int64_t* out_buf, int out_buf_size,
                                               int* out_count);

/* All three sparse / COO getters always pass `padWithZeroes=false` to the
 * SDK — cpp pwiz uses the no-arg overload which the diagnostic probe
 * confirmed is identical to `=false`, but the C++ reader expands gaps
 * client-side and we mirror that. Pinning `=false` explicitly insulates
 * us from any future SDK change to the no-arg overload's default. */

/* Frame::GetScanDataMzIndexedSparse — returns parallel mz + intensity arrays
 * for a single drift scan. Two-call pattern. Returns MBI_OK / MBI_ERR_*. */
MBI_API int mbi_frame_get_scan_data_mz_sparse(mbi_frame_t* h, size_t scan_index,
                                              double* out_mz, int64_t* out_intens,
                                              int out_buf_size, int* out_count);

/* Frame::GetScanDataToFIndexedSparse — parallel TOF-index + intensity arrays
 * for a single drift scan. Two-call pattern. */
MBI_API int mbi_frame_get_scan_data_tof_sparse(mbi_frame_t* h, size_t scan_index,
                                               int64_t* out_tof, int64_t* out_intens,
                                               int out_buf_size, int* out_count);

/* Frame::GetFrameDataAsCOOArray — sparse cube (intensity, scanIdx, tofIdx).
 * `data` is int32_t in the SDK; we widen to int64 for one consistent type
 * across the ABI. Two-call pattern. */
MBI_API int mbi_frame_get_coo_array(mbi_frame_t* h,
                                    int64_t* out_data,
                                    int64_t* out_row_scan,
                                    int64_t* out_col_tof,
                                    int out_buf_size,
                                    int* out_count);

/* Calibration::IndexToMz applied to the frame's calibration. cpp pwiz uses
 * this on per-TOF-index basis; expose a per-call function so the C# side
 * doesn't need to materialize a Calibration handle. */
MBI_API double mbi_frame_index_to_mz(mbi_frame_t* h, int64_t tof_index);

/* Batch IndexToMz: routes through TofCalibration::IndexToMzBuffer. The C#
 * combine-IMS / per-scan paths convert thousands of TOF indices per frame;
 * one P/Invoke per call here replaces N per-point P/Invokes through
 * mbi_frame_index_to_mz, which dominated the combine-IMS hot path. Both
 * input and output buffers are caller-allocated and parallel
 * (length == count). */
MBI_API int mbi_frame_index_to_mz_batch(mbi_frame_t* h,
                                        const int64_t* tof_indices,
                                        int count,
                                        double* out_mz);

#ifdef __cplusplus
}
#endif

#endif /* MOBILION_SHIM_H_INCLUDED */
