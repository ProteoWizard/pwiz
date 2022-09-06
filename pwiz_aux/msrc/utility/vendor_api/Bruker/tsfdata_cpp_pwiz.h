#ifndef DE_BDAL_CPP_IO_TSFDATA_CPP_H
#define DE_BDAL_CPP_IO_TSFDATA_CPP_H

/** \file
 *
 * Sample for a light-weight header-only C++ layer wrapping the C API for Bruker's TSF
 * reader DLL. You can modify this file as desired.
 *
 * See 'tsfdata.h' for more details about the underlying C API.
 *
 */

#include <stdexcept>
#include <string>
#include <cstdint>
#include <numeric>
#include <vector>
#include <limits>

#include "boost/throw_exception.hpp"
#include "boost/noncopyable.hpp"
#include "boost/shared_array.hpp"
#include "boost/range/iterator_range.hpp"
#include "boost/optional.hpp"
#include "boost/thread.hpp"
#include "include/c/tsfdata.h" // fundamental C API

namespace tsfdata
{
    /// \throws std::runtime_error containing the last timsdata.dll error string.
    inline void throwLastError ()
    {
        uint32_t len = tsf_get_last_error_string(0, 0);

        boost::shared_array<char> buf(new char[len]);
        tsf_get_last_error_string(buf.get(), len);

        BOOST_THROW_EXCEPTION(std::runtime_error(buf.get()));
    }

    /// Reader for TSF binary data (.tsf_bin). (The SQLite file (.tsf) containing all the
    /// metadata may be opened separately using any desired SQLite API.)
    class TsfData : public boost::noncopyable
    {
    public:
        /// Open specified TIMS analysis.
        ///
        /// \param[in] analysis_directory_name in UTF-8 encoding.
        /// \param[in] use_recalibration if DA re-calibration shall be used if available
        ///
        /// \throws std::exception in case of an error
        explicit TsfData(const std::string &analysis_directory_name, bool use_recalibration=false)
            : handle(0)
            , initial_frame_buffer_size(128)
        {
            handle = tsf_open(analysis_directory_name.c_str(), use_recalibration);
            ctor_complete();
        }

        /// Close TIMS analysis.
        ~TsfData()
        {
            tsf_close(handle);
        }

        /// Get the C-API handle corresponding to this instance. (Caller does not get
        /// ownership of the handle.) (This call is here for the case that the user wants
        /// to call C-library functions directly.)
        uint64_t getHandle () const
        {
            return handle;
        }

        typedef std::pair<std::reference_wrapper<const std::vector<double>>, std::reference_wrapper<const std::vector<float>>> MzIntensityArrayPair;

        /// Read a line spectrum from binary data. Not thread-safe.
        /// \returns const references to the m/z and intensity arrays
        MzIntensityArrayPair readLineSpectrum(
            int64_t frame_id,     //< frame index
            bool perform_mz_conversion) // <do/don't convert to mz from index
        {
            // grow buffers to their full size
            mzIndexBuffer.resize(initial_frame_buffer_size);
            intensityBuffer.resize(initial_frame_buffer_size);

            // buffer-growing loop
            for(;;) {
                uint32_t required_len = tsf_read_line_spectrum_v2(handle, frame_id, &mzIndexBuffer[0], &intensityBuffer[0], initial_frame_buffer_size);
                if(required_len == 0)
                    throwLastError();

                if (initial_frame_buffer_size >= required_len)
                {
                    if(required_len < 1)
                        BOOST_THROW_EXCEPTION(std::runtime_error("Data array too small."));

                    // shrink buffers down to required size (this just changes the vector end pointer, it does not deallocate)
                    mzIndexBuffer.resize(required_len);
                    intensityBuffer.resize(required_len);

                    if (perform_mz_conversion)
                    {
                        mzBuffer.resize(required_len);
                        indexToMz(frame_id, mzIndexBuffer, mzBuffer);
                        return std::make_pair(std::cref(mzBuffer), std::cref(intensityBuffer));
                    }
                    return std::make_pair(std::cref(mzIndexBuffer), std::cref(intensityBuffer));
                }

                if (required_len > 16777216) // arbitrary limit for now...
                    BOOST_THROW_EXCEPTION(std::runtime_error("Maximum expected frame size exceeded."));
                
                initial_frame_buffer_size = required_len; // grow buffer
                mzIndexBuffer.resize(initial_frame_buffer_size);
                mzBuffer.resize(initial_frame_buffer_size);
                intensityBuffer.resize(initial_frame_buffer_size);
            }
        }

        /// Read a profile spectrum from binary data. Not thread-safe.
        /// \returns const references to the m/z and intensity arrays
        MzIntensityArrayPair readProfileSpectrum(
            int64_t frame_id,     //< frame index
            bool perform_mz_conversion) // <do/don't convert to mz from index
        {
            // grow buffers to their full size
            profileIntensityBuffer.resize(initial_frame_buffer_size);

            // buffer-growing loop
            for(;;) {
                uint32_t required_len = tsf_read_profile_spectrum_v2(handle, frame_id, &profileIntensityBuffer[0], initial_frame_buffer_size);
                if(required_len == 0)
                    throwLastError();

                if (initial_frame_buffer_size >= required_len)
                {
                    if(required_len < 1)
                        BOOST_THROW_EXCEPTION(std::runtime_error("Data array too small."));

                    // shrink profile buffer down to required size (this just changes the vector end pointer, it does not deallocate)
                    profileIntensityBuffer.resize(required_len);

                    // the main buffers do not necessarily need to be as big since many 0 intensity points may be dropped
                    intensityBuffer.reserve(required_len);
                    mzIndexBuffer.reserve(required_len);
                    intensityBuffer.resize(0);
                    mzIndexBuffer.resize(0);

                    mzIndexBuffer.push_back(0);
                    intensityBuffer.push_back(profileIntensityBuffer[0]);
                    for (size_t i = 1; i < required_len-1; ++i)
                        if (profileIntensityBuffer[i] > 0 ||
                            profileIntensityBuffer[i - 1] > 0 ||
                            profileIntensityBuffer[i + 1] > 0)
                        {
                            mzIndexBuffer.push_back(i);
                            intensityBuffer.push_back(profileIntensityBuffer[i]);
                        }
                    mzIndexBuffer.push_back(required_len-1);
                    intensityBuffer.push_back(profileIntensityBuffer[required_len-1]);

                    if (perform_mz_conversion)
                    {
                        mzBuffer.resize(mzIndexBuffer.size());
                        indexToMz(frame_id, mzIndexBuffer, mzBuffer);
                        return std::make_pair(std::cref(mzBuffer), std::cref(intensityBuffer));
                    }
                    return std::make_pair(std::cref(mzIndexBuffer), std::cref(intensityBuffer));
                }

                if (required_len > 16777216) // arbitrary limit for now...
                    BOOST_THROW_EXCEPTION(std::runtime_error("Maximum expected frame size exceeded."));
                
                initial_frame_buffer_size = required_len; // grow buffer
                profileIntensityBuffer.resize(initial_frame_buffer_size);
            }
        }

        #define BDAL_tsf_DEFINE_CONVERSION_FUNCTION(CPPNAME, CNAME) \
        template<typename Vector> \
        void CPPNAME ( \
            int64_t frame_id,               /**< from .tdf SQLite: Frames.Id */ \
            boost::iterator_range<const uint32_t *> & in, /**< vector of input values (can be empty) */ \
            Vector<double> & out )     /**< vector of corresponding output values (will be resized automatically) */ \
        { \
            doTransformation(frame_id, in, out, CNAME); \
        }

        //BDAL_tsf_DEFINE_CONVERSION_FUNCTION(indexToMz, tsf_index_to_mz)
        //BDAL_tsf_DEFINE_CONVERSION_FUNCTION(mzToIndex, tsf_mz_to_index)

        template<typename VectorIn, typename VectorOut>
        void indexToMz(int64_t frame_id, const VectorIn& in, VectorOut& out) const
        {
            if (in.empty()) { out.clear(); return; }
            if (in.size() > std::numeric_limits<uint32_t>::max()) BOOST_THROW_EXCEPTION(std::runtime_error("Input range too large."));
            out.resize(in.size());
            tsf_index_to_mz(handle, frame_id, &in[0], &out[0], uint32_t(in.size()));
        }

    private:
        void ctor_complete()
        {
            if (handle == 0)
                throwLastError();
            mzIndexBuffer.resize(initial_frame_buffer_size);
            mzBuffer.resize(initial_frame_buffer_size);
            intensityBuffer.resize(initial_frame_buffer_size);
            profileIntensityBuffer.resize(initial_frame_buffer_size);
        }

        uint64_t handle;
        size_t initial_frame_buffer_size; // number of uint32_t elements
        std::vector<double> mzIndexBuffer, mzBuffer;
        std::vector<float> intensityBuffer;
        std::vector<uint32_t> profileIntensityBuffer;
    };

} // namespace tsfdata

#endif // DE_BDAL_CPP_IO_TSFDATA_CPP_H
