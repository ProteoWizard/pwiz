/*
 * Loads pwiz-sharp's MsData C API into a native process via nethost/hostfxr.
 *
 * The AOT example (../cpp-aot-reader) links the same C API at build time and gets a
 * self-contained native DLL with no runtime — but no vendor readers, because the vendor
 * SDK assemblies have no AOT-compatible form. This header takes the other trade: boot a
 * real .NET runtime in-process, and get every reader msconvert-sharp has.
 *
 * The managed side is IDENTICAL. Pwiz.MsData.Hosted compiles the very same Exports.cs as
 * the AOT shim; because those exports are [UnmanagedCallersOnly], hostfxr can hand back a
 * raw function pointer to each one via the UNMANAGEDCALLERSONLY_METHOD sentinel. So the
 * only thing that differs between the two backends is how the function pointers are
 * obtained: the linker's import table there, HostedRuntime::load() here.
 */
#ifndef PWIZ_MSDATA_HOST_HPP
#define PWIZ_MSDATA_HOST_HPP

#include <cstring>
#include <stdexcept>
#include <string>
#include <vector>

#include <coreclr_delegates.h>
#include <hostfxr.h>
#include <nethost.h>

#ifdef _WIN32
#  include <windows.h>
#else
#  include <dlfcn.h>
#endif

namespace pwiz {
namespace msdata {
namespace hosted {

/* Mirrors the codes in ../cpp-aot-reader/pwiz_msdata.h — same managed source, same ABI. */
enum : int {
    Ok = 0,
    ErrInvalidHandle = -1,
    ErrInvalidArg = -2,
    ErrIndex = -3,
    ErrIo = -4,
};

using handle = void*;

/// Thrown for both hosting failures (runtime wouldn't start, export missing) and data
/// failures (file wouldn't open). `code()` is the C API's return value where there was
/// one, 0 for hosting failures that never reached managed code.
class Error : public std::runtime_error {
public:
    Error(int code, const std::string& what) : std::runtime_error(what), code_(code) {}
    int code() const noexcept { return code_; }
private:
    int code_;
};

/// The C API as a dispatch table. Populated by HostedRuntime; the AOT backend gets the
/// equivalent from its import library.
struct Api {
    int  (*init)();
    int  (*open)(const char* path, handle* out);
    int  (*spectrum_count)(handle);
    int  (*spectrum_id)(handle, int index, char* buf, int buf_len);
    int  (*spectrum_peak_count)(handle, int index);
    int  (*source_id)(handle, char* buf, int buf_len);
    void (*close)(handle);
    int  (*last_error)(char* buf, int buf_len);
};

namespace detail {

#ifdef _WIN32
using native_char = wchar_t;
using module_t = HMODULE;
inline module_t load_module(const native_char* p) { return ::LoadLibraryW(p); }
inline void* find_symbol(module_t m, const char* n) { return (void*)::GetProcAddress(m, n); }
inline std::wstring widen(const std::string& s) {
    if (s.empty()) return std::wstring();
    int n = ::MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
    std::wstring w((size_t)n, L'\0');
    ::MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), &w[0], n);
    return w;
}
#else
using native_char = char;
using module_t = void*;
inline module_t load_module(const native_char* p) { return ::dlopen(p, RTLD_LAZY | RTLD_LOCAL); }
inline void* find_symbol(module_t m, const char* n) { return ::dlsym(m, n); }
inline std::string widen(const std::string& s) { return s; }
#endif

} // namespace detail

/// Boots a .NET runtime and binds the pwiz_msdata exports out of Pwiz.MsData.Hosted.
///
/// One instance per process is the intended usage: hostfxr will happily hand back the
/// already-initialized runtime for a second call, but there is no reason to pay for it.
class HostedRuntime {
public:
    /// `assembly_dir` is the directory holding Pwiz.MsData.Hosted.dll plus its
    /// .runtimeconfig.json / .deps.json and the vendor assemblies — i.e. the project's
    /// build output directory.
    explicit HostedRuntime(const std::string& assembly_dir) {
        const std::string stem = assembly_dir + "/Pwiz.MsData.Hosted";
        load(detail::widen(stem + ".runtimeconfig.json"), detail::widen(stem + ".dll"));
    }

    const Api& api() const noexcept { return api_; }

    /// Calls pwiz_msdata_init, which builds the reader list and — in this vendor-enabled
    /// build — installs the vendor SDK assembly resolver. Must happen before open().
    void init() const {
        int rc = api_.init();
        if (rc != Ok) throw Error(rc, "pwiz_msdata_init failed: " + last_error());
    }

    std::string last_error() const {
        if (!api_.last_error) return "<no runtime>";
        std::vector<char> buf(512);
        int n = api_.last_error(buf.data(), (int)buf.size());
        if (n >= (int)buf.size()) {           // grow-and-retry, per the API's length convention
            buf.assign((size_t)n + 1, '\0');
            n = api_.last_error(buf.data(), (int)buf.size());
        }
        return n <= 0 ? std::string() : std::string(buf.data());
    }

private:
    void load(const std::basic_string<detail::native_char>& runtime_config,
              const std::basic_string<detail::native_char>& assembly) {
        // 1. Ask nethost where hostfxr lives. This is the only piece that must be linked
        //    statically (libnethost.lib); everything past here is resolved by name.
        detail::native_char fxr_path[1024];
        size_t fxr_len = sizeof(fxr_path) / sizeof(detail::native_char);
        if (get_hostfxr_path(fxr_path, &fxr_len, nullptr) != 0)
            throw Error(0, "get_hostfxr_path failed — is a .NET runtime installed?");

        auto fxr = detail::load_module(fxr_path);
        if (!fxr) throw Error(0, "could not load hostfxr");

        auto init_fn = (hostfxr_initialize_for_runtime_config_fn)
            detail::find_symbol(fxr, "hostfxr_initialize_for_runtime_config");
        auto get_delegate_fn = (hostfxr_get_runtime_delegate_fn)
            detail::find_symbol(fxr, "hostfxr_get_runtime_delegate");
        auto close_fn = (hostfxr_close_fn)detail::find_symbol(fxr, "hostfxr_close");
        if (!init_fn || !get_delegate_fn || !close_fn)
            throw Error(0, "hostfxr is missing expected exports");

        // 2. Start (or attach to) a runtime configured by the shim's runtimeconfig.json.
        //    A class library only emits that file when the csproj sets EnableDynamicLoading.
        hostfxr_handle ctx = nullptr;
        int rc = init_fn(runtime_config.c_str(), nullptr, &ctx);
        if (rc < 0 || ctx == nullptr)
            throw Error(rc, "hostfxr_initialize_for_runtime_config failed (hresult " +
                            std::to_string(rc) + ")");

        load_assembly_and_get_function_pointer_fn load_fn = nullptr;
        rc = get_delegate_fn(ctx, hdt_load_assembly_and_get_function_pointer, (void**)&load_fn);
        close_fn(ctx);                      // the delegate keeps the runtime alive
        if (rc != 0 || load_fn == nullptr)
            throw Error(rc, "hostfxr_get_runtime_delegate failed (hresult " +
                            std::to_string(rc) + ")");

        // 3. One lookup per export. Note the name passed is the MANAGED METHOD name
        //    ("Open"), not the EntryPoint string from [UnmanagedCallersOnly]
        //    ("pwiz_msdata_open") — that one only governs the AOT-exported symbol name.
        //    Passing the entry-point string here fails with a bare "method not found".
        const auto type = detail::widen("Pwiz.MsData.NativeAot.Exports, Pwiz.MsData.Hosted");
        auto bind = [&](const char* method, void** slot) {
            int brc = load_fn(assembly.c_str(), type.c_str(), detail::widen(method).c_str(),
                              UNMANAGEDCALLERSONLY_METHOD, nullptr, slot);
            if (brc != 0 || *slot == nullptr)
                throw Error(brc, std::string("could not bind export '") + method +
                                 "' (hresult " + std::to_string(brc) + ")");
        };

        bind("Init",              (void**)&api_.init);
        bind("Open",              (void**)&api_.open);
        bind("SpectrumCount",     (void**)&api_.spectrum_count);
        bind("SpectrumId",        (void**)&api_.spectrum_id);
        bind("SpectrumPeakCount", (void**)&api_.spectrum_peak_count);
        bind("SourceId",          (void**)&api_.source_id);
        bind("Close",             (void**)&api_.close);
        bind("GetLastError",      (void**)&api_.last_error);
    }

    Api api_{};
};

/// RAII handle over one open MS data file. Mirrors pwiz::msdata::File in the AOT example's
/// pwiz_msdata.hpp; it binds to a runtime's dispatch table instead of linked symbols.
/// Move-only — copying would double-close.
class File {
public:
    File(const HostedRuntime& rt, const std::string& path) : rt_(&rt) {
        int rc = rt.api().open(path.c_str(), &handle_);
        if (rc != Ok) throw Error(rc, "could not open '" + path + "': " + rt.last_error());
    }

    ~File() { if (handle_ && rt_) rt_->api().close(handle_); }

    File(const File&) = delete;
    File& operator=(const File&) = delete;
    File(File&& o) noexcept : rt_(o.rt_), handle_(o.handle_) { o.handle_ = nullptr; }

    int spectrum_count() const {
        int rc = rt_->api().spectrum_count(handle_);
        if (rc < 0) throw Error(rc, "spectrum_count: " + rt_->last_error());
        return rc;
    }

    std::string source_id() const {
        return read_string([&](char* b, int n) { return rt_->api().source_id(handle_, b, n); });
    }

    std::string spectrum_id(int index) const {
        return read_string([&](char* b, int n) { return rt_->api().spectrum_id(handle_, index, b, n); });
    }

    int peak_count(int index) const {
        int rc = rt_->api().spectrum_peak_count(handle_, index);
        if (rc < 0) throw Error(rc, "spectrum_peak_count: " + rt_->last_error());
        return rc;
    }

    handle raw() const noexcept { return handle_; }

private:
    /// The C API returns the FULL byte length and truncates into whatever buffer it was
    /// given, so one retry with the reported size always suffices.
    template <typename Fn>
    static std::string read_string(Fn&& fn) {
        std::vector<char> buf(256);
        int n = fn(buf.data(), (int)buf.size());
        if (n < 0) throw Error(n, "string accessor failed");
        if (n >= (int)buf.size()) {
            buf.assign((size_t)n + 1, '\0');
            n = fn(buf.data(), (int)buf.size());
            if (n < 0) throw Error(n, "string accessor failed on retry");
        }
        return std::string(buf.data());
    }

    const HostedRuntime* rt_ = nullptr;
    handle handle_ = nullptr;
};

} // namespace hosted
} // namespace msdata
} // namespace pwiz

#endif /* PWIZ_MSDATA_HOST_HPP */
