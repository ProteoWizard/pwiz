// MobilionShim.cpp — implementation of the C wrapper around MBISDK.
//
// Each export catches all exceptions, stashes the message in a per-thread
// buffer (mbi_last_error_message), and returns an MBI_ERR_* code so the C#
// side can surface a clean InvalidOperationException without crossing the
// ABI with C++ exceptions.

#define MOBILION_SHIM_EXPORTS
#include "MobilionShim.h"

#include "MBIFile.h"
#include "MBIFrame.h"
#include "MBICalibration.h"
#include "MBIMetadata.h"

#include <cstring>
#include <memory>
#include <string>
#include <vector>
#include <stdexcept>

namespace {

// Per-thread last-error string. Lazily initialized on the first failure;
// every successful call clears it so the C# side can rely on emptiness =
// "no error from the previous call".
thread_local std::string g_last_error;

void set_error(const char* msg) {
    g_last_error = msg ? msg : "(unknown)";
}
void clear_error() {
    g_last_error.clear();
}

// Wrapper around MBIFile so the handle owns the unique_ptr and any cached
// frame metadata pointer. MBIFile must be heap-allocated because its ctor
// touches I/O.
struct mbi_file_impl {
    std::unique_ptr<MBISDK::MBIFile> file;
};

// Wrapper around shared_ptr<Frame>. The shared_ptr keeps the frame alive
// past the MBIFile's internal cache invalidation.
struct mbi_frame_impl {
    std::shared_ptr<MBISDK::Frame> frame;
};

// Helper: copy a std::string into the caller's buffer following the
// two-call pattern used across the API.
int copy_string_out(const std::string& s, char* out_buf, int out_buf_size, int* out_required) {
    int required = static_cast<int>(s.size()) + 1;  // include NUL
    if (out_required) *out_required = required;
    if (out_buf == nullptr || out_buf_size <= 0) {
        return required <= 1 ? MBI_ERR_NO_DATA : MBI_ERR_NOT_ENOUGH_SPACE;
    }
    if (out_buf_size < required) return MBI_ERR_NOT_ENOUGH_SPACE;
    std::memcpy(out_buf, s.data(), s.size());
    out_buf[s.size()] = '\0';
    return MBI_OK;
}

}  // namespace

extern "C" {

/* ------------------------------------------------------------------------ */
MBI_API const char* mbi_last_error_message(void) {
    return g_last_error.c_str();
}

/* ------------------------------------------------------------------------ */
MBI_API mbi_file_t* mbi_file_open(const char* path) {
    if (path == nullptr) { set_error("path is null"); return nullptr; }
    try {
        clear_error();
        auto wrapper = new mbi_file_impl;
        wrapper->file.reset(new MBISDK::MBIFile(path));
        return reinterpret_cast<mbi_file_t*>(wrapper);
    }
    catch (const std::exception& e) { set_error(e.what()); return nullptr; }
    catch (...) { set_error("unknown C++ exception in mbi_file_open"); return nullptr; }
}

MBI_API void mbi_file_free(mbi_file_t* h) {
    if (h == nullptr) return;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try { if (impl->file) impl->file->Close(); } catch (...) {}
    delete impl;
}

MBI_API int mbi_file_init(mbi_file_t* h) {
    if (h == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        // MBIFile::Init returns bool; cpp pwiz ignores it but we surface failure.
        bool ok = impl->file->Init();
        return ok ? MBI_OK : MBI_ERR_SDK_EXCEPTION;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mbi_file_init"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API void mbi_file_close(mbi_file_t* h) {
    if (h == nullptr) return;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try { if (impl->file) impl->file->Close(); } catch (...) {}
}

MBI_API int mbi_file_num_frames(mbi_file_t* h) {
    if (h == nullptr) return -1;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        return impl->file->NumFrames();
    }
    catch (const std::exception& e) { set_error(e.what()); return -1; }
    catch (...) { set_error("unknown C++ exception in mbi_file_num_frames"); return -1; }
}

MBI_API mbi_frame_t* mbi_file_get_frame(mbi_file_t* h, int frame_index_1based) {
    if (h == nullptr) return nullptr;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        auto sp = impl->file->GetFrame(frame_index_1based);
        if (!sp) { set_error("MBIFile::GetFrame returned null"); return nullptr; }
        auto* frame = new mbi_frame_impl;
        frame->frame = sp;
        return reinterpret_cast<mbi_frame_t*>(frame);
    }
    catch (const std::exception& e) { set_error(e.what()); return nullptr; }
    catch (...) { set_error("unknown C++ exception in mbi_file_get_frame"); return nullptr; }
}

/* ---- global metadata --------------------------------------------------- */
MBI_API int mbi_file_global_read_string(mbi_file_t* h, const char* key,
                                        char* out_buf, int out_buf_size,
                                        int* out_required) {
    if (h == nullptr || key == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        auto md = impl->file->GetGlobalMetaData();
        if (!md) return MBI_ERR_NO_DATA;
        // ReadString returns std::string by value in the SDK; rely on auto +
        // implicit conversion in copy_string_out so this compiles whether the
        // SDK widens to std::string_view or returns const char* in the future.
        std::string value = md->ReadString(key);
        return copy_string_out(value, out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mbi_file_global_read_string"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_file_global_read_double(mbi_file_t* h, const char* key, double* out_value) {
    if (h == nullptr || key == nullptr || out_value == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        auto md = impl->file->GetGlobalMetaData();
        if (!md) return MBI_ERR_NO_DATA;
        *out_value = md->ReadDouble(key);
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mbi_file_global_read_double"); return MBI_ERR_SDK_EXCEPTION; }
}

/* ---- CCS calibration --------------------------------------------------- */
MBI_API int mbi_file_can_convert_ccs(mbi_file_t* h) {
    if (h == nullptr) return -1;
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        // cpp pwiz pattern: try GetAtSurf(); success ⇒ true, throw ⇒ false.
        impl->file->GetEyeOnCCSCalibration().GetAtSurf();
        return 1;
    }
    catch (const std::exception& e) { set_error(e.what()); return 0; }
    catch (...) { set_error("unknown C++ exception"); return 0; }
}

MBI_API double mbi_file_ccs_min(mbi_file_t* h) {
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try { clear_error(); return impl->file->GetEyeOnCCSCalibration().GetCCSMinimum(); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API double mbi_file_ccs_max(mbi_file_t* h) {
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try { clear_error(); return impl->file->GetEyeOnCCSCalibration().GetCCSMaximum(); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API double mbi_file_arrival_time_to_ccs(mbi_file_t* h, double drift_time, double abs_mz_charge) {
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        return impl->file->GetEyeOnCCSCalibration().ArrivalTimeToCCS(drift_time, abs_mz_charge);
    }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API double mbi_file_ccs_to_arrival_time(mbi_file_t* h, double ccs, double abs_mz_charge) {
    auto* impl = reinterpret_cast<mbi_file_impl*>(h);
    try {
        clear_error();
        return impl->file->GetEyeOnCCSCalibration().CCSToArrivalTime(ccs, abs_mz_charge);
    }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

/* ------------------------------------------------------------------------ */
MBI_API void mbi_frame_free(mbi_frame_t* h) {
    if (h == nullptr) return;
    delete reinterpret_cast<mbi_frame_impl*>(h);
}

MBI_API double mbi_frame_get_ce_at(mbi_frame_t* h, int64_t index) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try { clear_error(); return impl->frame->GetCE(index); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API double mbi_frame_collision_energy(mbi_frame_t* h) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try { clear_error(); return impl->frame->GetCollisionEnergy(); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API double mbi_frame_time(mbi_frame_t* h) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try { clear_error(); return impl->frame->Time(); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API int64_t mbi_frame_total_intensity(mbi_frame_t* h) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try { clear_error(); return impl->frame->GetFrameTotalIntensity(); }
    catch (const std::exception& e) { set_error(e.what()); return 0; }
    catch (...) { set_error("unknown C++ exception"); return 0; }
}

MBI_API double mbi_frame_arrival_bin_time_offset(mbi_frame_t* h, size_t bin_index) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try { clear_error(); return impl->frame->GetArrivalBinTimeOffset(bin_index); }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API int mbi_frame_arrival_bin_time_offsets_batch(mbi_frame_t* h,
                                                     const int64_t* scan_indices,
                                                     int count,
                                                     double* out_drift) {
    if (h == nullptr) return MBI_ERR_INVALID_HANDLE;
    if (count > 0 && (scan_indices == nullptr || out_drift == nullptr)) return MBI_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        for (int i = 0; i < count; ++i) {
            out_drift[i] = impl->frame->GetArrivalBinTimeOffset(static_cast<size_t>(scan_indices[i]));
        }
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mbi_frame_arrival_bin_time_offsets_batch"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_metadata_read_string(mbi_frame_t* h, const char* key,
                                           char* out_buf, int out_buf_size,
                                           int* out_required) {
    if (h == nullptr || key == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        auto md = impl->frame->GetFrameMetaData();
        if (!md) return MBI_ERR_NO_DATA;
        std::string value = md->ReadString(key);
        return copy_string_out(value, out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_metadata_read_double(mbi_frame_t* h, const char* key, double* out_value) {
    if (h == nullptr || key == nullptr || out_value == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        auto md = impl->frame->GetFrameMetaData();
        if (!md) return MBI_ERR_NO_DATA;
        *out_value = md->ReadDouble(key);
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_get_nonzero_scan_indices(mbi_frame_t* h,
                                               int64_t* out_buf, int out_buf_size,
                                               int* out_count) {
    if (h == nullptr || out_count == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        auto vec = impl->frame->GetNonZeroScanIndices();
        int needed = static_cast<int>(vec.size());
        *out_count = needed;
        if (out_buf == nullptr) return needed == 0 ? MBI_OK : MBI_ERR_NOT_ENOUGH_SPACE;
        if (out_buf_size < needed) return MBI_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < needed; ++i) out_buf[i] = static_cast<int64_t>(vec[i]);
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_get_scan_data_mz_sparse(mbi_frame_t* h, size_t scan_index,
                                              double* out_mz, int64_t* out_intens,
                                              int out_buf_size, int* out_count) {
    if (h == nullptr || out_count == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        std::vector<double> mz;
        std::vector<size_t> intens;
        // padWithZeroes=false: hand back the raw non-zero peaks. Reader_Mobilion expands
        // gap-edge zeros client-side to mirror cpp pwiz's per-scan output.
        if (!impl->frame->GetScanDataMzIndexedSparse(scan_index, &mz, &intens, false))
            return MBI_ERR_SDK_EXCEPTION;
        int needed = static_cast<int>(mz.size());
        *out_count = needed;
        if (out_mz == nullptr || out_intens == nullptr)
            return needed == 0 ? MBI_OK : MBI_ERR_NOT_ENOUGH_SPACE;
        if (out_buf_size < needed) return MBI_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < needed; ++i) {
            out_mz[i] = mz[i];
            out_intens[i] = static_cast<int64_t>(intens[i]);
        }
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_get_scan_data_tof_sparse(mbi_frame_t* h, size_t scan_index,
                                               int64_t* out_tof, int64_t* out_intens,
                                               int out_buf_size, int* out_count) {
    if (h == nullptr || out_count == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        std::vector<int64_t> tof;
        std::vector<size_t> intens;
        // padWithZeroes=false: see mbi_frame_get_scan_data_mz_sparse for the rationale.
        impl->frame->GetScanDataToFIndexedSparse(scan_index, &tof, &intens, false);
        int needed = static_cast<int>(tof.size());
        *out_count = needed;
        if (out_tof == nullptr || out_intens == nullptr)
            return needed == 0 ? MBI_OK : MBI_ERR_NOT_ENOUGH_SPACE;
        if (out_buf_size < needed) return MBI_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < needed; ++i) {
            out_tof[i] = tof[i];
            out_intens[i] = static_cast<int64_t>(intens[i]);
        }
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API int mbi_frame_get_coo_array(mbi_frame_t* h,
                                    int64_t* out_data,
                                    int64_t* out_row_scan,
                                    int64_t* out_col_tof,
                                    int out_buf_size,
                                    int* out_count) {
    if (h == nullptr || out_count == nullptr) return MBI_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        // padWithZeroes=false: see mbi_frame_get_scan_data_mz_sparse for the rationale.
        auto coo = impl->frame->GetFrameDataAsCOOArray(false);
        int needed = static_cast<int>(coo.data.size());
        *out_count = needed;
        if (out_data == nullptr || out_row_scan == nullptr || out_col_tof == nullptr)
            return needed == 0 ? MBI_OK : MBI_ERR_NOT_ENOUGH_SPACE;
        if (out_buf_size < needed) return MBI_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < needed; ++i) {
            out_data[i] = static_cast<int64_t>(coo.data[i]);
            out_row_scan[i] = static_cast<int64_t>(coo.rowIndices[i]);
            out_col_tof[i] = static_cast<int64_t>(coo.columnIndices[i]);
        }
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception"); return MBI_ERR_SDK_EXCEPTION; }
}

MBI_API double mbi_frame_index_to_mz(mbi_frame_t* h, int64_t tof_index) {
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        return impl->frame->GetCalibration().IndexToMz(tof_index);
    }
    catch (const std::exception& e) { set_error(e.what()); return 0.0; }
    catch (...) { set_error("unknown C++ exception"); return 0.0; }
}

MBI_API int mbi_frame_index_to_mz_batch(mbi_frame_t* h,
                                        const int64_t* tof_indices,
                                        int count,
                                        double* out_mz) {
    if (h == nullptr) return MBI_ERR_INVALID_HANDLE;
    if (count > 0 && (tof_indices == nullptr || out_mz == nullptr)) return MBI_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mbi_frame_impl*>(h);
    try {
        clear_error();
        if (count == 0) return MBI_OK;
        // The SDK's IndexToMzBuffer takes vector<size_t> in / vector<double> out, so
        // we adapt: copy the C array into a transient size_t vector, run the buffered
        // conversion, then copy the result into the caller's buffer. The N=O(thousands)
        // copy is still vastly cheaper than the N per-point P/Invokes it replaces.
        std::vector<size_t> indices(count);
        for (int i = 0; i < count; ++i) indices[i] = static_cast<size_t>(tof_indices[i]);
        auto cal = impl->frame->GetCalibration();
        std::vector<double> mz;
        cal.IndexToMzBuffer(indices, mz);
        if (static_cast<int>(mz.size()) != count) return MBI_ERR_SDK_EXCEPTION;
        for (int i = 0; i < count; ++i) out_mz[i] = mz[i];
        return MBI_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MBI_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mbi_frame_index_to_mz_batch"); return MBI_ERR_SDK_EXCEPTION; }
}

}  // extern "C"
