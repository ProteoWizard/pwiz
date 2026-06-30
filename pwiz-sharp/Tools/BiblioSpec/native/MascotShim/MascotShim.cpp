// MascotShim.cpp — implementation of the C wrapper around msparser.
//
// Phase 1 scaffolding: msparser is NOT linked yet. Every entry point apart
// from mascot_get_version + mascot_last_error returns MASCOT_ERR_NOT_IMPLEMENTED.
// The thread-local last-error infrastructure is in place so subsequent phases
// only need to flesh out the bodies — the ABI shape is frozen here.
//
// Each export catches all exceptions, stashes the message in a per-thread
// buffer (mascot_last_error), and returns a MASCOT_ERR_* code so the C# side
// can surface a clean exception without C++ exceptions crossing the ABI.

// MASCOT_SHIM_EXPORTS is supplied via the build system (CMake's
// target_compile_definitions). Don't redefine here.
#include "MascotShim.h"

// Matrix Science msparser headers. CMake adds $(MASCOT_PARSER_PATH)/include
// to the include path; on Windows, msparser.lib's import library is linked
// via target_link_libraries.
#include "msparser.hpp"

#include <cstring>
#include <filesystem>
#include <memory>
#include <string>
#include <vector>

namespace {

// Per-thread last-error string. Lazily initialised on the first failure;
// every successful call clears it so the C# side can rely on emptiness =
// "no error from the previous call".
thread_local std::string g_last_error;

void set_error(const char* msg) {
    g_last_error = msg ? msg : "(unknown)";
}
void clear_error() {
    g_last_error.clear();
}

// Per-component isotope-delta table — used internally to satisfy the
// Phase 4c entry points without re-running applyIsotopes on every call.
struct quant_component_diffs {
    std::string name;
    std::vector<mascot_isotope_diff_t> diffs;
};

// Wrapper holding the msparser .dat handle plus anything else we'll cache
// (peptidesummary, searchparams) as later phases add them.
struct mascot_dat_impl {
    std::unique_ptr<matrix_science::ms_mascotresfile_dat> file;
    // PSM acceptance cutoff threaded through from mascot_dat_open. A PSM
    // whose expectation value exceeds this is dropped during iteration.
    double score_cutoff = 0.0;
    // Lazily-constructed search-parameter handle. Used by the modification
    // enumeration entry points (Phase 4); ms_searchparams's ctor parses the
    // .dat header so we defer it until the first caller asks.
    std::unique_ptr<matrix_science::ms_searchparams> params;

    // Phase 4c — quantitation config + per-component isotope diffs. Cached
    // lazily because constructing ms_quant_configfile / ms_umod_configfile
    // is non-trivial and only runs once we know the .dat declares a quant
    // method. quant_initialised == true after first attempt; the vectors
    // stay empty for unlabeled runs.
    bool quant_initialised = false;
    std::string quant_name;
    std::vector<quant_component_diffs> quant_components;
    // Directory containing quantitation_1.xsd / quantitation_2.xsd /
    // unimod_2.xsd. Set via mascot_dat_set_quant_config_dir; empty means
    // the caller hasn't asked for quantitation yet.
    std::string quant_config_dir;

    matrix_science::ms_searchparams& ensure_params() {
        if (!params) params.reset(new matrix_science::ms_searchparams(*file));
        return *params;
    }
};

// Iterator over (queryId, rank) pairs that pass the score threshold. State
// machine: current query, current rank, the rank-1 ion-score we're matching
// (cpp emits every rank with the same top-score), the cached per-query
// expectation value + observed m/z, and a flag for whether the current peptide
// pointer is still valid.
//
// The msparser ms_peptide pointer returned by ms_peptidesummary::getPeptide
// is owned by the summary; we keep a borrowed pointer and re-fetch on each
// advance.
struct mascot_psm_iter_impl {
    mascot_dat_impl* dat = nullptr;
    std::unique_ptr<matrix_science::ms_peptidesummary> summary;
    int num_queries = 0;
    int cur_query = 0;     // 0 == "before first query"; advance() bumps to 1
    int cur_rank = 0;
    double cur_top_score = 0.0;
    double cur_expectation = 0.0;
    double cur_observed_mz = 0.0;
    bool query_active = false;  // we've found a query with matches; iterating ranks
};

// Copy a std::string into a fixed-size UTF-8 buffer, NUL-terminating and
// truncating if necessary. The 256-byte / 8-byte caps in mascot_psm_record_t
// fit every PSM in the BiblioSpec test corpus.
template <size_t N>
void copy_fixed(char (&dst)[N], const std::string& src) {
    const size_t cap = N > 0 ? N - 1 : 0;
    const size_t n = src.size() < cap ? src.size() : cap;
    if (n > 0) std::memcpy(dst, src.data(), n);
    dst[n] = '\0';
}

// Construct the ms_peptidesummary mirroring MascotResultsReader.cpp:104-119.
std::unique_ptr<matrix_science::ms_peptidesummary> build_summary(
        matrix_science::ms_mascotresfile_dat& f) {
    unsigned int flags = matrix_science::ms_mascotresults::MSRES_DUPE_REMOVE_NONE;
    unsigned int flags2 = matrix_science::ms_peptidesummary::MSPEPSUM_USE_HOMOLOGY_THRESH
                        | matrix_science::ms_peptidesummary::MSPEPSUM_NO_PROTEIN_GROUPING;
    if (f.isErrorTolerant()) {
        flags |= matrix_science::ms_mascotresults::MSRES_INTEGRATED_ERR_TOL;
        // MSPEPSUM_NO_PROTEIN_GROUPING is incompatible with error-tolerant
        // searches per cpp comment at MascotResultsReader.cpp:109.
        flags2 &= ~matrix_science::ms_peptidesummary::MSPEPSUM_NO_PROTEIN_GROUPING;
    }
    return std::make_unique<matrix_science::ms_peptidesummary>(
        f,
        flags,
        /*minProbability=*/0.0,
        /*maxHits=*/0,
        /*unigeneIndexFile=*/nullptr,
        /*ignoreIonsScoreBelow=*/0.0,
        /*minPepLenInPepSummary=*/0,
        /*singleHit=*/nullptr,
        flags2);
}

// Helper: pull the (possibly long) error message msparser stashed after the
// most recent failed call into our last-error slot. Falls back to a generic
// message if msparser had nothing to say.
void capture_msparser_error(const matrix_science::ms_mascotresfile_dat& f,
                            const char* fallback) {
    std::string msg = f.getLastErrorString();
    if (msg.empty()) msg = fallback ? fallback : "msparser reported an error";
    g_last_error = std::move(msg);
}

// Helper for the two-call string-getter pattern used across the API.
// Returns MASCOT_OK on success, MASCOT_ERR_NOT_ENOUGH_SPACE if the caller
// buffer is too small (still writes the required size), and MASCOT_ERR_NO_DATA
// for an empty source string.
int copy_string_out(const std::string& s, char* out_buf, int out_buf_size,
                    int* out_required) {
    const int required = static_cast<int>(s.size()) + 1;  // include NUL
    if (out_required) *out_required = required;
    if (s.empty()) return MASCOT_ERR_NO_DATA;
    if (out_buf == nullptr || out_buf_size <= 0) return MASCOT_ERR_NOT_ENOUGH_SPACE;
    if (out_buf_size < required) return MASCOT_ERR_NOT_ENOUGH_SPACE;
    std::memcpy(out_buf, s.data(), s.size());
    out_buf[s.size()] = '\0';
    return MASCOT_OK;
}

}  // namespace

extern "C" {

/* ---- Diagnostics -------------------------------------------------------- */
MASCOT_API int mascot_get_version(int* major, int* minor, int* patch) {
    if (major) *major = 0;
    if (minor) *minor = 1;
    if (patch) *patch = 0;
    clear_error();
    return MASCOT_OK;
}

MASCOT_API const char* mascot_last_error(void) {
    return g_last_error.c_str();
}

/* ---- Lifecycle ---------------------------------------------------------- */
/* Mirrors cpp MascotResultsReader::initParse (MascotResultsReader.cpp:60-98):
 * construct ms_mascotresfile_dat with no keepalive, the default RESFILE_NOFLAG,
 * and "." as the cache directory; on isValid() == false, pull
 * getLastErrorString() into our thread-local slot so the C# side can surface
 * it. score_cutoff is parked here for Phase 3 (it gates PSM acceptance, not
 * file opening) — Phase 2 ignores it. */
MASCOT_API int mascot_dat_open(const char* utf8_path, double score_cutoff,
                               mascot_dat_t** out) {
    if (out) *out = nullptr;
    if (utf8_path == nullptr) {
        set_error("mascot_dat_open: utf8_path is null");
        return MASCOT_ERR_NULL_BUFFER;
    }
    if (out == nullptr) {
        set_error("mascot_dat_open: out is null");
        return MASCOT_ERR_NULL_BUFFER;
    }
    try {
        clear_error();
        // Pre-flight the file. msparser's ms_mascotresfile_dat ctor terminates
        // the process on a missing file rather than reporting via isValid() +
        // getLastError() (it goes through abort()-style internal recovery, so
        // /EHa + catch(...) can't intercept it). Doing the existence check
        // ourselves yields a clean SdkException + thread-local message instead
        // of a hard host crash.
        std::error_code ec;
        if (!std::filesystem::exists(utf8_path, ec) || ec) {
            std::string msg = "File not found: ";
            msg += utf8_path;
            if (ec) { msg += " ("; msg += ec.message(); msg += ")"; }
            set_error(msg.c_str());
            return MASCOT_ERR_SDK_EXCEPTION;
        }

        auto wrapper = std::make_unique<mascot_dat_impl>();
        // cpp uses cacheFlag=getCacheFlag(...) which today resolves to
        // RESFILE_NOFLAG for files small enough to keep in memory. Phase 2
        // skips the cache decision — all our test fixtures are < 2 MB.
        wrapper->file.reset(new matrix_science::ms_mascotresfile_dat(
            utf8_path,
            /*keepAliveInterval=*/0,
            /*keepAliveText=*/"",
            /*flags=*/matrix_science::ms_mascotresfilebase::RESFILE_NOFLAG,
            /*cacheDirectory=*/"."));
        if (!wrapper->file->isValid()) {
            capture_msparser_error(*wrapper->file,
                "ms_mascotresfile_dat reported invalid state");
            return MASCOT_ERR_SDK_EXCEPTION;
        }
        wrapper->score_cutoff = score_cutoff;
        *out = reinterpret_cast<mascot_dat_t*>(wrapper.release());
        return MASCOT_OK;
    }
    catch (const std::exception& e) {
        set_error(e.what());
        return MASCOT_ERR_SDK_EXCEPTION;
    }
    catch (...) {
        set_error("unknown C++ exception in mascot_dat_open");
        return MASCOT_ERR_SDK_EXCEPTION;
    }
}

MASCOT_API void mascot_dat_close(mascot_dat_t* h) {
    if (h == nullptr) return;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try { delete impl; }
    catch (...) { /* swallow — Close has no return path */ }
}

/* ---- Metadata ----------------------------------------------------------- */
MASCOT_API int mascot_dat_is_msms(mascot_dat_t* h, int* out_is_msms) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_is_msms == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        *out_is_msms = impl->file->isMSMS() ? 1 : 0;
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_is_msms"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_num_queries(mascot_dat_t* h, int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        *out_count = impl->file->getNumQueries();
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_num_queries"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* ---- Modifications ----------------------------------------------------- */
/* msparser indexes both lists 1-based and uses delta == 0 as the terminator
 * past the last valid entry. cpp MascotResultsReader.cpp:134 walks until
 * `getFixedModsDelta(i)` returns 0; same idea here, just exposed through a
 * count + indexed-get pair so the C# side doesn't deal with the sentinel. */
MASCOT_API int mascot_dat_num_fixed_mods(mascot_dat_t* h, int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto& params = impl->ensure_params();
        int n = 0;
        while (params.getFixedModsDelta(n + 1) != 0.0) n++;
        *out_count = n;
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_num_fixed_mods"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_fixed_mod(mascot_dat_t* h, int index_1based,
                                        mascot_mod_t* out) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out == nullptr) return MASCOT_ERR_NULL_BUFFER;
    if (index_1based < 1) return MASCOT_ERR_NO_DATA;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto& params = impl->ensure_params();
        const double delta = params.getFixedModsDelta(index_1based);
        if (delta == 0.0) return MASCOT_ERR_NO_DATA;
        std::memset(out, 0, sizeof(*out));
        out->delta = delta;
        copy_fixed(out->name, params.getFixedModsName(index_1based));
        copy_fixed(out->residues, params.getFixedModsResidues(index_1based));
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_fixed_mod"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* Variable mods are referenced from per-PSM varModsStr by 1-based index, so
 * `residues` is left empty for them — the residue identity comes from the
 * peptide position in varModsStr, not from the mod table itself. cpp uses
 * the same shape via getVarModsDelta + getVarModsName. */
MASCOT_API int mascot_dat_num_var_mods(mascot_dat_t* h, int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto& params = impl->ensure_params();
        int n = 0;
        while (params.getVarModsDelta(n + 1) != 0.0) n++;
        *out_count = n;
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_num_var_mods"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_var_mod(mascot_dat_t* h, int index_1based,
                                      mascot_mod_t* out) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out == nullptr) return MASCOT_ERR_NULL_BUFFER;
    if (index_1based < 1) return MASCOT_ERR_NO_DATA;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto& params = impl->ensure_params();
        const double delta = params.getVarModsDelta(index_1based);
        if (delta == 0.0) return MASCOT_ERR_NO_DATA;
        std::memset(out, 0, sizeof(*out));
        out->delta = delta;
        copy_fixed(out->name, params.getVarModsName(index_1based));
        // residues stays empty for var mods (see note above).
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_var_mod"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* ---- Quantitation / isotope labels (Phase 4c) -------------------------- */
MASCOT_API int mascot_dat_set_quant_config_dir(mascot_dat_t* h,
                                               const char* utf8_dir) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (utf8_dir == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        impl->quant_config_dir = utf8_dir;
        // Append a trailing slash if missing so callers can concat filenames.
        if (!impl->quant_config_dir.empty()) {
            char last = impl->quant_config_dir.back();
            if (last != '\\' && last != '/') {
#ifdef _WIN32
                impl->quant_config_dir.push_back('\\');
#else
                impl->quant_config_dir.push_back('/');
#endif
            }
        }
        impl->quant_initialised = false;
        impl->quant_name.clear();
        impl->quant_components.clear();
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_set_quant_config_dir"); return MASCOT_ERR_SDK_EXCEPTION; }
}

namespace {

/* cpp parity: MascotResultsReader.cpp:500-577. Resolve the quant method
 * named in search-params, load the XSDs, walk every component and build
 * the (residue, isotope-delta) table for it. Cached on first call. Returns
 * MASCOT_OK with empty quant_name when the .dat declares no labeling
 * (cpp also returns silently in that case). */
int ensure_quant_loaded(mascot_dat_impl& impl) {
    if (impl.quant_initialised) return MASCOT_OK;
    impl.quant_initialised = true;  // even on early-return, don't retry
    impl.quant_name.clear();
    impl.quant_components.clear();

    auto& params = impl.ensure_params();
    std::string quantName = params.getQUANTITATION();
    if (quantName.empty() || quantName == "None") return MASCOT_OK;

    if (impl.quant_config_dir.empty()) {
        set_error("Quantitation requested but mascot_dat_set_quant_config_dir "
                  "was not called");
        return MASCOT_ERR_SDK_EXCEPTION;
    }

    // Quantitation schema for the .dat-embedded method description.
    matrix_science::ms_quant_configfile quantConfig;
    std::string quantSchemas =
        "http://www.matrixscience.com/xmlns/schema/quantitation_1 " +
        impl.quant_config_dir + "quantitation_1.xsd"
        " http://www.matrixscience.com/xmlns/schema/quantitation_2 " +
        impl.quant_config_dir + "quantitation_2.xsd";
    quantConfig.setSchemaFileName(quantSchemas.c_str());
    if (!impl.file->getQuantitation(&quantConfig)) {
        set_error("ms_mascotresfile_dat::getQuantitation failed");
        return MASCOT_ERR_SDK_EXCEPTION;
    }

    const matrix_science::ms_quant_method* method =
        quantConfig.getMethodByName(quantName.c_str());
    if (method == nullptr) {
        std::string msg = "Quantitation method '" + quantName + "' not found in the .dat";
        set_error(msg.c_str());
        return MASCOT_ERR_SDK_EXCEPTION;
    }

    // Unimod schema for the residue mass table.
    matrix_science::ms_umod_configfile massConfig;
    std::string unimodPath = impl.quant_config_dir + "unimod_2.xsd";
    massConfig.setSchemaFileName(unimodPath.c_str());
    if (!impl.file->getUnimod(&massConfig) || !massConfig.isValid()) {
        set_error("ms_mascotresfile_dat::getUnimod failed");
        return MASCOT_ERR_SDK_EXCEPTION;
    }

    matrix_science::ms_masses defaultMasses;
    impl.quant_name = quantName;

    // For each component, compute the per-residue isotope diff. cpp uses
    // `aa < 'Z'` (NOT <=), which silently omits 'Z' — preserve that quirk.
    const int n = method->getNumberOfComponents();
    for (int idx = 0; idx < n; idx++) {
        const matrix_science::ms_quant_component* comp = method->getComponentByNumber(idx);
        if (comp == nullptr) continue;

        quant_component_diffs cc;
        cc.name = comp->getName();

        matrix_science::ms_masses heavyMasses;
        heavyMasses.applyIsotopes(&massConfig, comp);
        for (char aa = 'A'; aa < 'Z'; ++aa) {
            double heavy = heavyMasses.getResidueMass(
                matrix_science::MASS_TYPE_MONO, aa);
            double light = defaultMasses.getResidueMass(
                matrix_science::MASS_TYPE_MONO, aa);
            double delta = heavy - light;
            if (delta > 0.000005 || delta < -0.000005) {
                cc.diffs.push_back({aa, delta});
            }
        }
        impl.quant_components.push_back(std::move(cc));
    }
    return MASCOT_OK;
}

}  // namespace

MASCOT_API int mascot_dat_get_quant_name(mascot_dat_t* h,
                                         char* out_buf, int out_buf_size,
                                         int* out_required) {
    if (out_required) *out_required = 0;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        int rc = ensure_quant_loaded(*impl);
        if (rc != MASCOT_OK) return rc;
        return copy_string_out(impl->quant_name, out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_quant_name"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_num_quant_components(mascot_dat_t* h, int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        int rc = ensure_quant_loaded(*impl);
        if (rc != MASCOT_OK) { *out_count = 0; return rc; }
        *out_count = static_cast<int>(impl->quant_components.size());
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_num_quant_components"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_quant_component_name(mascot_dat_t* h,
                                                   int component_index,
                                                   char* out_buf, int out_buf_size,
                                                   int* out_required) {
    if (out_required) *out_required = 0;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        int rc = ensure_quant_loaded(*impl);
        if (rc != MASCOT_OK) return rc;
        if (component_index < 0 ||
            component_index >= static_cast<int>(impl->quant_components.size())) {
            return MASCOT_ERR_NO_DATA;
        }
        return copy_string_out(impl->quant_components[component_index].name,
                               out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_quant_component_name"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_quant_component_diffs(mascot_dat_t* h,
                                                    int component_index,
                                                    mascot_isotope_diff_t* out_buf,
                                                    int out_buf_size,
                                                    int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        int rc = ensure_quant_loaded(*impl);
        if (rc != MASCOT_OK) { *out_count = 0; return rc; }
        if (component_index < 0 ||
            component_index >= static_cast<int>(impl->quant_components.size())) {
            *out_count = 0;
            return MASCOT_ERR_NO_DATA;
        }
        const auto& diffs = impl->quant_components[component_index].diffs;
        const int n = static_cast<int>(diffs.size());
        *out_count = n;
        if (out_buf == nullptr || out_buf_size <= 0) {
            return n == 0 ? MASCOT_OK : MASCOT_ERR_NOT_ENOUGH_SPACE;
        }
        if (out_buf_size < n) return MASCOT_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < n; ++i) out_buf[i] = diffs[i];
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_quant_component_diffs"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_global_param(mascot_dat_t* h, int which,
                                           char* out_buf, int out_buf_size,
                                           int* out_required) {
    if (out_required) *out_required = 0;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto& params = impl->ensure_params();
        std::string value;
        switch (which) {
            case MASCOT_GLOBAL_FILENAME: value = params.getFILENAME(); break;
            case MASCOT_GLOBAL_DATAURL:  value = params.getDATAURL();  break;
            case MASCOT_GLOBAL_COM:      value = params.getCOM();      break;
            default: return MASCOT_ERR_NO_DATA;
        }
        return copy_string_out(value, out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_global_param"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* ---- Distiller ---------------------------------------------------------- */
/* cpp parity: MascotResultsReader.cpp:617 getDistillerRawFiles. The
 * Distiller-produced .dat embeds a series of `_DISTILLER_RAWFILE_NAMES_..=
 * {N}filename` USER params; each filename is emitted via the caller's
 * callback. Sample numbers other than {1} get rejected because cpp does
 * the same — they need the special multi-sample handling cpp doesn't
 * support either. */
MASCOT_API int mascot_dat_enumerate_distiller_raw_files(
    mascot_dat_t* h, mascot_string_callback cb, void* userdata) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (cb == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        const std::string& params = impl->ensure_params().getAllUSERParams();
        const std::string header = "_DISTILLER_RAWFILE";

        size_t pos = 0;
        while (pos < params.size()) {
            size_t end = params.find_first_of("\r\n", pos);
            if (end == std::string::npos) end = params.size();
            std::string line = params.substr(pos, end - pos);
            // Skip line terminators.
            pos = end;
            while (pos < params.size() && (params[pos] == '\r' || params[pos] == '\n'))
                pos++;

            if (line.compare(0, header.size(), header) != 0) continue;

            size_t eq = line.find('=');
            if (eq == std::string::npos) continue;
            size_t braceClose = line.find('}', eq + 1);
            if (braceClose == std::string::npos) continue;
            std::string sampleStr = line.substr(eq + 1, 3);
            std::string rawFile = line.substr(braceClose + 1);
            if (rawFile.empty()) continue;

            if (sampleStr != "{1}") {
                std::string msg = "Distiller raw file '" + rawFile +
                    "' had a sample number other than 1.";
                set_error(msg.c_str());
                return MASCOT_ERR_SDK_EXCEPTION;
            }

            cb(rawFile.c_str(), userdata);
        }
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_enumerate_distiller_raw_files"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* ---- PSM iteration ------------------------------------------------------ */
MASCOT_API int mascot_dat_open_psm_iter(mascot_dat_t* h, mascot_psm_iter_t** out) {
    if (out) *out = nullptr;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* dat = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        auto iter = std::make_unique<mascot_psm_iter_impl>();
        iter->dat = dat;
        iter->summary = build_summary(*dat->file);
        iter->num_queries = dat->file->getNumQueries();
        iter->cur_query = 0;          // mascot_dat_next_psm advances to 1 first
        iter->cur_rank = 0;
        iter->query_active = false;
        *out = reinterpret_cast<mascot_psm_iter_t*>(iter.release());
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_open_psm_iter"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API void mascot_dat_close_psm_iter(mascot_psm_iter_t* h) {
    if (h == nullptr) return;
    auto* impl = reinterpret_cast<mascot_psm_iter_impl*>(h);
    try { delete impl; }
    catch (...) { /* swallow */ }
}

/* Mirrors MascotResultsReader.cpp:197-264.
 *   Outer loop: walk queryId 1..num_queries.
 *     Fetch rank=1 peptide; if !getAnyMatch(): skip query.
 *     Compute expectation from the rank-1 ion score; if it exceeds the
 *     score_cutoff, skip this query entirely.
 *     Build an ms_inputquery for the observed m/z.
 *   Inner loop: emit every rank that has the same ion score as rank 1.
 *
 * Returns 1 with `*out` populated for each emitted PSM, 0 when the iterator
 * has run off the end, or a negative MASCOT_ERR_* code on failure. */
MASCOT_API int mascot_dat_next_psm(mascot_psm_iter_t* h, mascot_psm_record_t* out) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* iter = reinterpret_cast<mascot_psm_iter_impl*>(h);
    try {
        clear_error();
        for (;;) {
            // Move to the next query if we haven't started one yet OR the
            // current one is exhausted (query_active=false). Reset rank=1 on
            // each new query.
            if (!iter->query_active) {
                if (iter->cur_query >= iter->num_queries) return 0;
                iter->cur_query++;
                iter->cur_rank = 1;
            }

            // Fetch the peptide at (cur_query, cur_rank) by value. The
            // pointer-out overload (bool getPeptide(q, p, ms_peptide*&)) is
            // ABI-fragile across the msparser.dll boundary — first dereference
            // of the returned pointer crashes the process. The by-value
            // overload returns a fresh ms_peptide whose lifetime we own.
            matrix_science::ms_peptide pep =
                iter->summary->getPeptide(iter->cur_query, iter->cur_rank);

            if (!iter->query_active) {
                // First peek at this query: rank 1 establishes the top-score
                // bar and per-query metadata. Skip the query if it has no
                // matches at all.
                if (!pep.getAnyMatch()) continue;

                iter->cur_top_score = pep.getIonsScore();
                iter->cur_expectation = iter->summary->getPeptideExpectationValue(
                    iter->cur_top_score, iter->cur_query);
                // cpp filters by score threshold (MascotResultsReader.cpp:226).
                // score_cutoff == 0 means "no filtering" — expectation values
                // are non-negative so 0 wouldn't exclude anything anyway.
                if (iter->dat->score_cutoff > 0 &&
                    iter->cur_expectation > iter->dat->score_cutoff) {
                    continue;
                }
                // cpp MascotSpecReader.h:128 reads observed m/z off rank-1.
                iter->cur_observed_mz = pep.getObserved();
                iter->query_active = true;
            }
            else {
                // Subsequent ranks of an active query: stop emitting when the
                // ion score drops or there's no match.
                if (!pep.getAnyMatch() || pep.getIonsScore() != iter->cur_top_score) {
                    iter->query_active = false;
                    continue;
                }
            }

            // Charge sanity. cpp warns + skips implausible charges; we just
            // advance past them silently (C# side can summarise if needed).
            int charge = pep.getCharge();
            if (charge <= 0 || charge > 50) {
                iter->cur_rank++;
                continue;
            }

            // Populate the record from the same `pep` we just inspected — no
            // second fetch, no second by-value copy.
            std::memset(out, 0, sizeof(*out));
            out->query_id = iter->cur_query;
            out->rank = iter->cur_rank;
            out->charge = charge;
            out->ions_score = iter->cur_top_score;
            out->expectation_value = iter->cur_expectation;
            out->observed_mz = iter->cur_observed_mz;
            copy_fixed(out->peptide, pep.getPeptideStr());
            copy_fixed(out->var_mods_str, pep.getVarModsStr());
            copy_fixed(out->readable_mods,
                iter->summary->getReadableVarMods(iter->cur_query, iter->cur_rank));
            // cpp parity: MascotResultsReader.cpp:245 — getComponentStr is
            // the quantitation-label tag (e.g. "heavy"/"light") for this
            // PSM. Empty for unlabeled runs. Phase 4c uses this to apply
            // per-component isotope deltas.
            copy_fixed(out->component_str, pep.getComponentStr());
            // Mascot doesn't emit prev/next AA; leave those empty (Phase 4 may
            // wire them via getComponentStr or getReadableVarMods parsing).

            iter->cur_rank++;
            return 1;
        }
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_next_psm"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* ---- Spectrum lookup ---------------------------------------------------- */
/* Each query stores its title (free-text usually containing the originating
 * spec-file name) and a peak list. cpp MascotSpecReader.h:158/161/167 uses
 * these via ms_inputquery; we mirror that. The `ions` argument to
 * getNumberOfPeaks / getPeakList is the ion-series number — cpp passes 1
 * (the first / only series for typical MS2 searches), so do the same. */
MASCOT_API int mascot_dat_get_query_title(mascot_dat_t* h, int query_id,
                                          char* out_buf, int out_buf_size,
                                          int* out_required) {
    if (out_required) *out_required = 0;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        matrix_science::ms_inputquery query(*impl->file, query_id);
        // unescaped=true matches MascotSpecReader.h:158 — gives the raw title
        // string as written in the .dat (no XML entity escaping).
        std::string title = query.getStringTitle(true);
        return copy_string_out(title, out_buf, out_buf_size, out_required);
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_query_title"); return MASCOT_ERR_SDK_EXCEPTION; }
}

/* msparser's ms_inputquery::getRetentionTimes returns the RT recorded by
 * the search engine (seconds, string-encoded). cpp MascotSpecReader.h:135
 * lexical_casts to double; we do the same on the shim side and surface
 * MASCOT_ERR_NO_DATA when the string is empty or unparseable so the C#
 * spec reader can fall back to title parsing. raw_file_index < 0 asks
 * msparser for the "no raw file" overload (the typical case); the
 * cpp loop at MascotSpecReader.h:140-148 walks numRawFiles_ to find one
 * that has a value — Phase 4b only needs the default. */
MASCOT_API int mascot_dat_get_query_rt(mascot_dat_t* h, int query_id,
                                       int raw_file_index, double* out_rt) {
    if (out_rt) *out_rt = 0.0;
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_rt == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        matrix_science::ms_inputquery query(*impl->file, query_id);
        std::string rt_str = raw_file_index < 0
            ? query.getRetentionTimes()
            : query.getRetentionTimes(raw_file_index);
        if (rt_str.empty()) return MASCOT_ERR_NO_DATA;
        try {
            *out_rt = std::stod(rt_str);
            return MASCOT_OK;
        } catch (...) {
            return MASCOT_ERR_NO_DATA;
        }
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_query_rt"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_query_peak_count(mascot_dat_t* h, int query_id,
                                               int* out_count) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (out_count == nullptr) return MASCOT_ERR_NULL_BUFFER;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        matrix_science::ms_inputquery query(*impl->file, query_id);
        *out_count = query.getNumberOfPeaks(1);  // ion series 1, cpp parity
        return MASCOT_OK;
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_query_peak_count"); return MASCOT_ERR_SDK_EXCEPTION; }
}

MASCOT_API int mascot_dat_get_query_peaks(mascot_dat_t* h, int query_id,
                                          double* mz_buf, double* intensity_buf,
                                          int buf_size) {
    if (h == nullptr) return MASCOT_ERR_INVALID_HANDLE;
    if (mz_buf == nullptr || intensity_buf == nullptr) return MASCOT_ERR_NULL_BUFFER;
    if (buf_size <= 0) return MASCOT_ERR_NOT_ENOUGH_SPACE;
    auto* impl = reinterpret_cast<mascot_dat_impl*>(h);
    try {
        clear_error();
        matrix_science::ms_inputquery query(*impl->file, query_id);
        auto peaks = query.getPeakList(1);  // ion series 1, cpp parity
        const int n = static_cast<int>(peaks.size());
        if (buf_size < n) return MASCOT_ERR_NOT_ENOUGH_SPACE;
        for (int i = 0; i < n; ++i) {
            mz_buf[i] = peaks[i].first;
            intensity_buf[i] = peaks[i].second;
        }
        return n;  // positive = number of peaks written
    }
    catch (const std::exception& e) { set_error(e.what()); return MASCOT_ERR_SDK_EXCEPTION; }
    catch (...) { set_error("unknown C++ exception in mascot_dat_get_query_peaks"); return MASCOT_ERR_SDK_EXCEPTION; }
}

}  // extern "C"

// Silence the unused-helper warning until Phase 2 calls it.
namespace { [[maybe_unused]] auto& _silence = copy_string_out; }
