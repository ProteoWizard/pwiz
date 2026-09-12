// MascotShim.h — flat C surface around Matrix Science's msparser library.
//
// Built into MascotShim.dll (Windows) / libMascotShim.so (Linux) and consumed
// via P/Invoke from pwiz-sharp/Tools/BiblioSpec/src/BiblioSpec/MascotShimInterop.cs.
//
// Naming: every exported symbol starts with `mascot_` for clarity in stack
// traces / map files. All entry points that can fail return an int status
// (0 = success, negative = error code; see MASCOT_OK / MASCOT_ERR_*) and
// write the real value through an out-pointer.
//
// Phase 1 scaffolding: msparser is NOT linked yet; every entry point apart
// from version + last-error returns MASCOT_ERR_NOT_IMPLEMENTED. Subsequent
// phases fill in the real implementations.
//
// Strings: every string crosses the ABI as UTF-8 (`const char*`). `wchar_t`
// is 16-bit on Windows / 32-bit on Linux, so wide-char ABIs aren't portable.
// The C# side uses `[MarshalAs(UnmanagedType.LPUTF8Str)]` for inputs and
// `Marshal.PtrToStringUTF8` for outputs.
//
// Ownership:
//   - mascot_dat_t* is an opaque handle that owns wrapper state (msparser
//     objects + cached metadata). Callers must call mascot_dat_close()
//     exactly once per successful mascot_dat_open().
//   - String getters use the two-call pattern: pass `out_buf=NULL, buf_size=0`
//     to query the required size (returned via `*out_required`), then call
//     again with the allocated buffer.

#ifndef MASCOT_SHIM_H_INCLUDED
#define MASCOT_SHIM_H_INCLUDED

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#  ifdef MASCOT_SHIM_EXPORTS
#    define MASCOT_API __declspec(dllexport)
#  else
#    define MASCOT_API __declspec(dllimport)
#  endif
#else
#  define MASCOT_API __attribute__((visibility("default")))
#endif

#include <stddef.h>
#include <stdint.h>

/* ---- Opaque handles ----------------------------------------------------- */
typedef struct mascot_dat_      mascot_dat_t;
typedef struct mascot_psm_iter_ mascot_psm_iter_t;

/* ---- Result codes ------------------------------------------------------- */
#define MASCOT_OK                       0
#define MASCOT_ERR_INVALID_HANDLE      -1
#define MASCOT_ERR_NULL_BUFFER         -2
#define MASCOT_ERR_NOT_ENOUGH_SPACE    -3   /* caller buffer too small */
#define MASCOT_ERR_SDK_EXCEPTION       -4   /* msparser call threw — message via mascot_last_error */
#define MASCOT_ERR_NO_DATA             -5   /* metadata key missing / array empty */
#define MASCOT_ERR_NOT_IMPLEMENTED     -6   /* Phase 1 placeholder */

/* ---- Diagnostics -------------------------------------------------------- */
/* Returns the shim's ABI version. Used as a smoke-test that P/Invoke can
 * reach the DLL — Phase 1 ships only this and mascot_last_error. */
MASCOT_API int mascot_get_version(int* major, int* minor, int* patch);

/* Returns the last std::exception::what() message captured by the shim,
 * or empty string if the last call succeeded. Buffer is per-thread; the
 * returned pointer is owned by the shim and stays valid until the next
 * shim call on the same thread. */
MASCOT_API const char* mascot_last_error(void);

/* ---- Lifecycle (Phase 2) ------------------------------------------------ */
/* Open a .dat file. Returns MASCOT_OK and writes the new handle through
 * `*out` on success, or a MASCOT_ERR_* code on failure (handle untouched).
 * Phase 1 returns MASCOT_ERR_NOT_IMPLEMENTED. */
MASCOT_API int  mascot_dat_open(const char* utf8_path, double score_cutoff,
                                mascot_dat_t** out);
MASCOT_API void mascot_dat_close(mascot_dat_t* h);

/* ---- Metadata (Phase 2) ------------------------------------------------- */
MASCOT_API int mascot_dat_is_msms(mascot_dat_t* h, int* out_is_msms);
MASCOT_API int mascot_dat_num_queries(mascot_dat_t* h, int* out_count);

/* ---- Modifications (Phase 4) ------------------------------------------- */
/* msparser indexes both modification lists 1-based and terminates them by
 * returning delta=0 on the first index past the end. The shim collapses both
 * to a count plus indexed-get pattern so the C# side doesn't have to know
 * about the sentinel. Names + residue specs live in fixed UTF-8 buffers in
 * mascot_mod_t — 128 bytes for the modification name (Unimod-style, e.g.
 * "Carbamidomethyl (C)" or "Label:13C(6)15N(2) (K)"), 64 for the residue
 * spec (e.g. "STY", "C", "N_term"). */
typedef struct {
    double delta;
    char   name[128];
    char   residues[64];   /* fixed mods only; var mods leave this empty */
} mascot_mod_t;

MASCOT_API int mascot_dat_num_fixed_mods(mascot_dat_t* h, int* out_count);
MASCOT_API int mascot_dat_get_fixed_mod(mascot_dat_t* h, int index_1based,
                                        mascot_mod_t* out);
MASCOT_API int mascot_dat_num_var_mods(mascot_dat_t* h, int* out_count);
MASCOT_API int mascot_dat_get_var_mod(mascot_dat_t* h, int index_1based,
                                      mascot_mod_t* out);

/* msparser's quantitation parser needs the XSD schema files
 * (quantitation_1.xsd, quantitation_2.xsd, unimod_2.xsd). Set the directory
 * containing them before calling any of the quant entry points below — cpp
 * BiblioSpec uses getExeDirectory() to resolve them; the shim can't make
 * that call portably (no Win-specific GetModuleFileName), so it asks the
 * C# side to hand the path in. Idempotent. */
MASCOT_API int mascot_dat_set_quant_config_dir(mascot_dat_t* h,
                                               const char* utf8_dir);

/* ---- Quantitation / isotope labels (Phase 4c) --------------------------- */
/* Per-residue isotope mass difference within a quantitation component (e.g.
 * the 15N delta on lysine). cpp parity: MascotResultsReader.cpp:557-574. */
typedef struct {
    char   residue;     /* 'A'-'Z' */
    double delta;       /* heavy - light, monoisotopic */
} mascot_isotope_diff_t;

/* Name of the quantitation method (e.g. "15N") or empty string when the
 * .dat doesn't declare one. cpp: searchparams->getQUANTITATION(). Empty
 * means "no labeling"; the caller can skip the rest of this section. */
MASCOT_API int mascot_dat_get_quant_name(mascot_dat_t* h,
                                         char* out_buf, int out_buf_size,
                                         int* out_required);

/* Number of components (labels) in the active quantitation method.
 * Returns 0 with MASCOT_OK when no method is active. cpp uses
 * ms_quant_method::getNumberOfComponents(). */
MASCOT_API int mascot_dat_num_quant_components(mascot_dat_t* h, int* out_count);

/* Name of the component at 0-based index. cpp: getComponentByNumber(idx)
 * ->getName(). */
MASCOT_API int mascot_dat_get_quant_component_name(mascot_dat_t* h,
                                                   int component_index,
                                                   char* out_buf, int out_buf_size,
                                                   int* out_required);

/* Two-call: pass out_buf=NULL/out_buf_size=0 to query the diff count via
 * `*out_count`, then call again with the allocated buffer. Each entry is
 * one (residue, isotope-delta) pair. cpp computes this via ms_masses with
 * applyIsotopes(component) and compares against the default mass table. */
MASCOT_API int mascot_dat_get_quant_component_diffs(mascot_dat_t* h,
                                                    int component_index,
                                                    mascot_isotope_diff_t* out_buf,
                                                    int out_buf_size,
                                                    int* out_count);

/* Global search-params filename fallbacks. cpp parity:
 * MascotResultsReader.cpp:780-789 — getFILENAME / getDATAURL / getCOM are
 * the trio BiblioSpec walks for a plausible source-file name when the
 * query title doesn't carry one. Returns MASCOT_ERR_NO_DATA when empty. */
typedef enum {
    MASCOT_GLOBAL_FILENAME = 1,
    MASCOT_GLOBAL_DATAURL  = 2,
    MASCOT_GLOBAL_COM      = 3,
} mascot_global_param_t;

MASCOT_API int mascot_dat_get_global_param(mascot_dat_t* h,
                                           int which,
                                           char* out_buf, int out_buf_size,
                                           int* out_required);

/* ---- Distiller raw-file enumeration (Phase 5) --------------------------- */
typedef void (*mascot_string_callback)(const char* utf8_string, void* userdata);
MASCOT_API int mascot_dat_enumerate_distiller_raw_files(
    mascot_dat_t* h, mascot_string_callback cb, void* userdata);

/* ---- PSM iteration (Phase 3) -------------------------------------------- */
/* Iterator state lives in mascot_psm_iter_t. NextPsm returns 0 when
 * exhausted, 1 with `*out` populated, or a negative MASCOT_ERR_* on error. */
typedef struct {
    int    query_id;          /* 1-based, matches msparser */
    int    rank;
    int    charge;
    double ions_score;
    double expectation_value;
    double observed_mz;
    /* Fixed-size UTF-8 buffers: large enough for any realistic PSM. The
     * full ABI freeze adds the variable-length fields (peptide, mods,
     * readable mod string, prev/next AA) — Phase 1 only declares the
     * struct shape so MascotShimInterop.cs can pin the size at 256. */
    char   peptide[256];
    char   readable_mods[256];
    char   var_mods_str[64];
    char   prev_aa[8];
    char   next_aa[8];
    /* Phase 4c — quantitation component this PSM was assigned to (cpp
     * pep->getComponentStr()). Empty for unlabeled runs. */
    char   component_str[64];
} mascot_psm_record_t;

MASCOT_API int  mascot_dat_open_psm_iter(mascot_dat_t* h, mascot_psm_iter_t** out);
MASCOT_API void mascot_dat_close_psm_iter(mascot_psm_iter_t* h);
MASCOT_API int  mascot_dat_next_psm(mascot_psm_iter_t* h, mascot_psm_record_t* out);

/* ---- Spectrum lookup (Phase 6) ------------------------------------------ */
MASCOT_API int mascot_dat_get_query_title(mascot_dat_t* h, int query_id,
                                          char* out_buf, int out_buf_size,
                                          int* out_required);
MASCOT_API int mascot_dat_get_query_rt(mascot_dat_t* h, int query_id,
                                       int raw_file_index, double* out_rt);
MASCOT_API int mascot_dat_get_query_peak_count(mascot_dat_t* h, int query_id,
                                               int* out_count);
MASCOT_API int mascot_dat_get_query_peaks(mascot_dat_t* h, int query_id,
                                          double* mz_buf, double* intensity_buf,
                                          int buf_size);

#ifdef __cplusplus
}
#endif

#endif /* MASCOT_SHIM_H_INCLUDED */
