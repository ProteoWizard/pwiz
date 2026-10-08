// pwiz_msdata.hpp — header-only C++ RAII wrapper around the AOT C API in
// pwiz_msdata.h. Consumers that want an object-oriented + exception-based interface
// should #include this; the underlying C functions remain available via the
// pwiz::msdata::c namespace for callers that need them.
//
// Pure inline header. No additional .cpp / .lib needed.
#ifndef PWIZ_MSDATA_HPP
#define PWIZ_MSDATA_HPP

#include <iterator>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include "pwiz_msdata.h"

namespace pwiz::msdata {

/// Thrown by every API call that can fail. The C API's last-error string is captured
/// into <see cref="what"/>; the int error code from <c>pwiz_msdata.h</c> is exposed
/// via <see cref="code"/> so callers can switch on the failure reason.
class Error : public std::runtime_error
{
public:
    Error(const std::string& msg, int code) : std::runtime_error(msg), code_(code) {}
    int code() const noexcept { return code_; }
private:
    int code_;
};

namespace detail {

/// Drains a "writes up to N bytes, returns full length" C export into a std::string.
/// Grows the buffer once if the first try was too small so we don't pay a length-probe
/// round-trip in the common case.
template <typename Fn>
inline std::string read_grow_on_truncate(Fn fn)
{
    std::vector<char> buf(256);
    while (true)
    {
        int n = fn(buf.data(), static_cast<int>(buf.size()));
        if (n < 0) return std::string();           // error path; caller handles via the int return
        if (n < static_cast<int>(buf.size())) return std::string(buf.data(), n);
        buf.resize(static_cast<size_t>(n) + 1);
    }
}

/// Pulls the thread-local last-error string out of the C API for exception messages.
inline std::string last_error()
{
    return read_grow_on_truncate([](char* b, int n) { return pwiz_msdata_last_error(b, n); });
}

/// Translates a negative-rc API call into an <see cref="Error"/> throw.
inline void check(int rc, const char* op)
{
    if (rc >= 0) return;
    std::string msg = std::string(op) + " failed (rc=" + std::to_string(rc) + ")";
    auto detail = last_error();
    if (!detail.empty()) msg += ": " + detail;
    throw Error(msg, rc);
}

} // namespace detail

class File;

/// Lightweight proxy for a single spectrum inside a <see cref="File"/>. Construction
/// is cheap (just an index + parent pointer); each property accessor calls the C API.
/// The proxy is valid only as long as the parent File is alive.
class Spectrum
{
public:
    /// Ordinal position in the file's spectrum list (0-based).
    int index() const noexcept { return index_; }

    /// The spectrum's mzML id (e.g. <c>"scan=42"</c>).
    std::string id() const;

    /// Number of (m/z, intensity) peaks. Reads the binary data lazily — only call
    /// when needed.
    int peakCount() const;

private:
    friend class File;
    Spectrum(const File* parent, int index) : parent_(parent), index_(index) {}
    const File* parent_;
    int index_;
};

/// RAII wrapper around an MS data file opened through the AOT shim. Construction opens
/// the file (throws <see cref="Error"/> on failure); destruction closes the handle.
class File
{
public:
    /// Opens <paramref name="path"/>. Throws <see cref="Error"/> if the file isn't
    /// readable or the format isn't recognized.
    explicit File(const std::string& path)
    {
        int rc = pwiz_msdata_open(path.c_str(), &handle_);
        if (rc != PWIZ_MSDATA_OK) detail::check(rc, "pwiz_msdata_open");
    }

    File(const File&) = delete;
    File& operator=(const File&) = delete;

    File(File&& other) noexcept : handle_(other.handle_) { other.handle_ = nullptr; }
    File& operator=(File&& other) noexcept
    {
        if (this != &other)
        {
            close();
            handle_ = other.handle_;
            other.handle_ = nullptr;
        }
        return *this;
    }

    ~File() { close(); }

    /// Opaque handle, for callers that need to drop down to the raw C API.
    pwiz_msdata_handle handle() const noexcept { return handle_; }

    /// The file's source id (typically the basename without extension).
    std::string sourceId() const
    {
        return detail::read_grow_on_truncate(
            [&](char* b, int n) { return pwiz_msdata_source_id(handle_, b, n); });
    }

    /// Number of spectra. Throws <see cref="Error"/> on a stale handle.
    int spectrumCount() const
    {
        int rc = pwiz_msdata_spectrum_count(handle_);
        if (rc < 0) detail::check(rc, "pwiz_msdata_spectrum_count");
        return rc;
    }

    /// Returns a proxy for the spectrum at <paramref name="index"/>. The proxy is
    /// cheap; it does no work until you ask for a property.
    Spectrum spectrum(int index) const { return Spectrum(this, index); }

    Spectrum operator[](int index) const { return spectrum(index); }

    // ------- range-for support -------
    class Iterator
    {
    public:
        using iterator_category = std::input_iterator_tag;
        using value_type = Spectrum;
        using difference_type = std::ptrdiff_t;
        using pointer = void;
        using reference = Spectrum;

        Iterator(const File* parent, int index) : parent_(parent), index_(index) {}
        Spectrum operator*() const { return parent_->spectrum(index_); }
        Iterator& operator++() { ++index_; return *this; }
        Iterator operator++(int) { auto t = *this; ++(*this); return t; }
        bool operator==(const Iterator& other) const noexcept { return index_ == other.index_; }
        bool operator!=(const Iterator& other) const noexcept { return !(*this == other); }

    private:
        const File* parent_;
        int index_;
    };

    Iterator begin() const { return Iterator(this, 0); }
    Iterator end() const { return Iterator(this, spectrumCount()); }

private:
    void close() noexcept
    {
        if (handle_)
        {
            pwiz_msdata_close(handle_);
            handle_ = nullptr;
        }
    }

    pwiz_msdata_handle handle_ = nullptr;
};

// Inline Spectrum impls (deferred because File wasn't complete above).
inline std::string Spectrum::id() const
{
    return detail::read_grow_on_truncate([&](char* b, int n) {
        return pwiz_msdata_spectrum_id(parent_->handle(), index_, b, n);
    });
}

inline int Spectrum::peakCount() const
{
    int rc = pwiz_msdata_spectrum_peak_count(parent_->handle(), index_);
    if (rc < 0) detail::check(rc, "pwiz_msdata_spectrum_peak_count");
    return rc;
}

} // namespace pwiz::msdata

#endif // PWIZ_MSDATA_HPP
