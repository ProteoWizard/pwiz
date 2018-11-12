//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "BinaryDataEncoder.hpp"
#include "pwiz/utility/misc/Base64.hpp"
#include "pwiz/utility/misc/endian.hpp"
#include "boost/static_assert.hpp"
#include "boost/iostreams/filtering_streambuf.hpp"
#include "boost/iostreams/filtering_stream.hpp"
#include "boost/iostreams/copy.hpp"
#include <boost/math/special_functions/fpclassify.hpp>
#include "iosfwd"
#include "boost/iostreams/categories.hpp"
#include "boost/iostreams/filter/zlib.hpp"
#include "boost/iostreams/device/array.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/MSNumpress.hpp"

namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace boost::iostreams;

//
// BinaryDataEncoder::Impl
//


class BinaryDataEncoder::Impl
{
    public:

    Impl(const Config& config)
    :   config_(config)
    {}

    void encode(const vector<double>& data, string& result, size_t* binaryByteCount);
    void encode(const double* data, size_t dataSize, std::string& result, size_t* binaryByteCount);
    void decode(const char *encodedData, size_t len, pwiz::util::BinaryData<double>& result);
    void decode(const string& encodedData, pwiz::util::BinaryData<double>& result)
    {
        decode(encodedData.c_str(),encodedData.length(),result);
    }
    const Config & getConfig() const
    {
        return config_;
    }
    private:
    Config config_;
};


BOOST_STATIC_ASSERT(sizeof(float) == 4);
BOOST_STATIC_ASSERT(sizeof(double) == 8);


struct DoubleToFloat
{
    float operator()(double d) {return float(d);}
};


void BinaryDataEncoder::Impl::encode(const vector<double>& data, string& result, size_t* binaryByteCount)
{
    if (data.empty()) return;
    encode(&data[0], data.size(), result, binaryByteCount);
}

// formerly returned a string, but those are not guaranteed to have
// contiguous storage space in older C++ standards, so use vector instead
// thx to Johan Teleman
template <typename filter_type>
void filterArray(const void* byteBuffer, size_t byteCount, vector<unsigned char> &result)
{
    result.reserve(byteCount); // preallocate (more than we need), for speed
    filtering_ostream fos;
    fos.push(filter_type());
    fos.push(back_inserter(result));
    fos.write((const char*)byteBuffer, byteCount);
    fos.pop(); 
    fos.pop(); // forces buffer to flush

/* 
    // original implementation, using technique in boost iostreams docs
    // this doesn't flush properly in all cases -- see unit test
    ostringstream result;
    array_source source(reinterpret_cast<const char*>(byteBuffer), byteCount);
    filtering_streambuf<input> in;
    in.push(filter_type());
    in.push(source);
    boost::iostreams::copy(in, result);
    return result.str();
*/
}


void BinaryDataEncoder::Impl::encode(const double* data, size_t dataSize, std::string& result, size_t* binaryByteCount)
{
    // using MSNumpress, from johan.teleman@immun.lth.se
    size_t byteCount;
    const void* byteBuffer;
    vector<unsigned char> compressed;
    vector<unsigned char> numpressed;
    vector<float> data32;
    vector<double> data64endianized;

    if (Numpress_None != config_.numpress) { // lossy numerical representation
        try {
            switch(config_.numpress)
            {
            case Numpress_Linear:
                numpressed.resize(dataSize * sizeof(double) + 8);
                break;

            case Numpress_Pic:
                numpressed.resize(dataSize * sizeof(double));
                break;

            case Numpress_Slof:
                numpressed.resize(dataSize * 2 + 8);
                break;

            default: 
                throw runtime_error("[BinaryDataEncoder::encode()] unknown numpress mode");
                break;
            }
            vector<double> unpressed; // for checking excessive accuracy loss
            int n=-1;
            double numpressErrorTolerance = 0.0;
            switch (config_.numpress) {
                case Numpress_Linear:
                    if (config_.numpressLinearAbsMassAcc > 0.0)
                    {
                      double fp = MSNumpress::optimalLinearFixedPointMass(data, dataSize, config_.numpressLinearAbsMassAcc);
                      if (fp < 0.0) 
                      { 
                        // failure: cannot achieve that accuracy, thus don't numpress (see below)
                        n = 0;
                        byteCount = MSNumpress::encodeLinear(data, dataSize, &numpressed[0], config_.numpressFixedPoint);
                      }
                      else
                      {
                        byteCount = MSNumpress::encodeLinear(data, dataSize, &numpressed[0], fp);
                      }
                    }
                    else {
                      byteCount = MSNumpress::encodeLinear(data, dataSize, &numpressed[0], config_.numpressFixedPoint);
                    }
                    numpressed.resize(byteCount);
                    if ((numpressErrorTolerance=config_.numpressLinearErrorTolerance) > 0) // decompress to check accuracy loss
                        MSNumpress::decodeLinear(numpressed,unpressed); 
                    break;

                case Numpress_Pic:
                    byteCount = MSNumpress::encodePic(data, dataSize, &numpressed[0]);
                    numpressed.resize(byteCount);
                    numpressErrorTolerance = 0.5; // it's an integer rounding, so always +- 0.5
                    MSNumpress::decodePic(numpressed,unpressed); // but susceptible to overflow, so always check
                    break; 

                case Numpress_Slof:
                    byteCount = MSNumpress::encodeSlof(data, dataSize, &numpressed[0], config_.numpressFixedPoint);
                    numpressed.resize(byteCount);
                    if ((numpressErrorTolerance=config_.numpressSlofErrorTolerance) > 0) // decompress to check accuracy loss
                        MSNumpress::decodeSlof(numpressed,unpressed); 
                    break;

                default:
                    break;
            }
            // now check to see if encoding introduces excessive error
            if (numpressErrorTolerance) 
            {
                if (Numpress_Pic == config_.numpress)  // integer rounding, abs accuracy is +- 0.5
                {
                    for (n=(int)dataSize;n--;) // check for overflow, strange rounding
                    {
                        if ((!boost::math::isfinite(unpressed[n])) || (fabs(data[n]-unpressed[n])>=1.0)) 
                            break;
                    }
                }
                else // check for tolerance as well as overflow
                {
                    for (n=(int)dataSize;n--;)
                    {
                        double d,u;
                        if (!boost::math::isfinite(u = unpressed[n])||!boost::math::isfinite(d = data[n]))
                            break;
                        if (!d)
                        {
                           if (fabs(u) > numpressErrorTolerance)
                               break;
                        }
                        else if (!u)
                        {
                           if (fabs(d) > numpressErrorTolerance)
                               break;
                        }
                        else if (fabs(1.0-(d/u)) > numpressErrorTolerance)
                            break;
                    }
                }
            }
            if (n>=0)
                config_.numpress = Numpress_None; // excessive error, don't numpress
            else
                byteBuffer = reinterpret_cast<const void*>(&numpressed[0]);
        } catch (int e) {
            cerr << "MZNumpress encoder threw exception: " << e << endl;
        } catch (...) {
            cerr << "Unknown exception while encoding " << dataSize << " doubles" << endl;
        }

    }
    if (Numpress_None == config_.numpress) { //  may need 64->32bit conversion and byte ordering

        //
        // We use buffer abstractions, since we may need to change buffers during
        // downconversion and compression -- these are eventually passed to
        // the Base64 encoder.  Note that:
        //  - byteBuffer and byteCount must remain valid at the end of any block
        //    in this function
        //  - by default, no processing is done, resulting in no buffer changes
        //    and fall through to Base64 encoding
        //

        byteBuffer = reinterpret_cast<const void*>(data); 
        byteCount = dataSize * sizeof(double);

        // 64-bit -> 32-bit downconversion


        if (config_.precision == Precision_32)
        {
            data32.resize(dataSize);
            transform(data, data+dataSize, data32.begin(), DoubleToFloat());
            byteBuffer = reinterpret_cast<void*>(&data32[0]);
            byteCount = data32.size() * sizeof(float);
        }

        // byte ordering

        #ifdef PWIZ_LITTLE_ENDIAN
        bool mustEndianize = (config_.byteOrder == ByteOrder_BigEndian);
        #elif defined(PWIZ_BIG_ENDIAN)
        bool mustEndianize = (config_.byteOrder == ByteOrder_LittleEndian);
        #endif

        if (mustEndianize)
        {
            if (config_.precision == Precision_32)
            {
                unsigned int* p = reinterpret_cast<unsigned int *>(&data32[0]);
                transform(p, p+data32.size(), p, endianize32);
            }
            else // Precision_64 
            {
                data64endianized.resize(dataSize);
                const unsigned long long* from = reinterpret_cast<const unsigned long long*>(data);
                unsigned long long* to = reinterpret_cast<unsigned long long*>(&data64endianized[0]);
                transform(from, from+dataSize, to, endianize64);
                byteBuffer = reinterpret_cast<void*>(&data64endianized[0]);
                byteCount = dataSize * sizeof(double);
            }
        }
    }

    // zlib compression (is done after 32/64bit conversion and after numpress)
    if (config_.compression == Compression_Zlib)
    {
        filterArray<zlib_compressor>(byteBuffer, byteCount,compressed);
        if (!compressed.empty())
        {
            byteBuffer = reinterpret_cast<void*>(&compressed[0]);
            byteCount = compressed.size();
        }
        else
        {
            throw runtime_error("[BinaryDataEncoder::encode()] Compression error?");
        }
    }

    // Base64 encoding

    result.resize(Base64::binaryToTextSize(byteCount));    

    // std::string storage is not guaranteed contiguous in older C++ standards,
    // and on long strings this has caused problems in the wild.  So test for
    // actual contiguousness, and fall back to std::vector if needed
    // thx Johan Teleman
    size_t textSize;
    char *first = &result[0];
    char *last = &result[result.size()-1];
    if ((int)result.size() == 1+(last-first)) // pointer math agrees with [] operator
        textSize = Base64::binaryToText(byteBuffer, byteCount, &result[0]);
    else 
    {
        std::vector<char> contig;  // work in this contiguous memory then copy to string
        contig.resize(result.size());
        textSize = Base64::binaryToText(byteBuffer, byteCount, &contig[0]);
        copy(contig.begin(), contig.end(), result.begin());
    }
    result.resize(textSize);

    if (binaryByteCount != NULL)
        *binaryByteCount = byteCount; // size before base64 encoding
}


template <typename float_type>
void copyBuffer(const void* byteBuffer, size_t byteCount, pwiz::util::BinaryData<double>& result)
{
    const float_type* floatBuffer = reinterpret_cast<const float_type*>(byteBuffer);

    if (byteCount % sizeof(float_type) != 0) 
        throw runtime_error("[BinaryDataEncoder::copyBuffer()] Bad byteCount.");

    size_t floatCount = byteCount / sizeof(float_type);

    result.resize(floatCount);

    copy(floatBuffer, floatBuffer+floatCount, result.begin());
}


void BinaryDataEncoder::Impl::decode(const char *encodedData, size_t length, pwiz::util::BinaryData<double>& result)
{
    if (!encodedData || !length) return;

    // Base64 decoding

    vector<unsigned char> binary(Base64::textToBinarySize(length));
    size_t binarySize = Base64::textToBinary(encodedData, length, &binary[0]);
    binary.resize(binarySize);

    // buffer abstractions

    void* byteBuffer = &binary[0];
    size_t byteCount = binarySize;
    size_t initialSize;

    // decompression

    vector<unsigned char> decompressed;
    switch (config_.compression) {
        case Compression_Zlib:
            {
                filterArray<zlib_decompressor>(byteBuffer, byteCount,decompressed);
                if (!decompressed.empty())
                {
                    byteBuffer = reinterpret_cast<void*>(&decompressed[0]);
                    byteCount = decompressed.size();
                }
                else
                {
                    throw runtime_error("[BinaryDataEncoder::decode()] Compression error?");
                }
            } 
            break;
        case Compression_None:
            break;
        default:
            throw runtime_error("[BinaryDataEncoder::decode()] unknown compression type");
            break;
    }
    // numpress expansion or endian correction
    switch (config_.numpress) 
    {
        case Numpress_Linear:
            initialSize = byteCount * 2;
            if (result.size() < initialSize)
                result.resize(initialSize);
            try {
                size_t count = MSNumpress::decodeLinear((const unsigned char *)byteBuffer, byteCount, &result[0]);
                result.resize(count);
            } catch (...) {
                throw runtime_error("BinaryDataEncoder::Impl::decode  error in numpress linear decompression");
            }
            break;
        case Numpress_Pic:
            initialSize = byteCount * 2;
            if (result.size() < initialSize)
                result.resize(initialSize);
            try {
                size_t count = MSNumpress::decodePic((const unsigned char *)byteBuffer, byteCount, &result[0]);
                result.resize(count);
            } catch (...) {
                throw runtime_error("BinaryDataEncoder::Impl::decode  error in numpress pic decompression");
            }
            break;
        case Numpress_Slof:
            initialSize = byteCount / 2;
            if (result.size() < initialSize)
                result.resize(initialSize);
            try {
                size_t count = MSNumpress::decodeSlof((const unsigned char *)byteBuffer, byteCount, &result[0]);
                result.resize(count);
            } catch (...) {
                throw runtime_error("BinaryDataEncoder::Impl::decode  error in numpress slof decompression");
            }
            break;
        case Numpress_None:
            {
            // endianization for non-numpress cases

            #ifdef PWIZ_LITTLE_ENDIAN
            bool mustEndianize = (config_.byteOrder == ByteOrder_BigEndian);
            #elif defined(PWIZ_BIG_ENDIAN)
            bool mustEndianize = (config_.byteOrder == ByteOrder_LittleEndian);
            #endif

            if (mustEndianize)
            {
                if (config_.precision == Precision_32)
                {
                    unsigned int* p = reinterpret_cast<unsigned int*>(byteBuffer);
                    size_t floatCount = byteCount / sizeof(float);
                    transform(p, p+floatCount, p, endianize32);
                }
                else // Precision_64
                {
                    unsigned long long* p = reinterpret_cast<unsigned long long*>(byteBuffer);
                    size_t doubleCount = byteCount / sizeof(double);
                    transform(p, p+doubleCount, p, endianize64);
                }
            }

            // (upconversion and) copy to result buffer

            if (config_.precision == Precision_32)
                copyBuffer<float>(byteBuffer, byteCount, result);
            else // Precision_64
                copyBuffer<double>(byteBuffer, byteCount, result);
            }
            break;
        default: 
            throw runtime_error("BinaryDataEncoder::Impl::decode  unknown numpress method");
            break;
   }
}


//
// BinaryDataEncoder
//


PWIZ_API_DECL BinaryDataEncoder::BinaryDataEncoder(const Config& config)
:   impl_(new Impl(config))
{}

PWIZ_API_DECL void BinaryDataEncoder::encode(const std::vector<double>& data, std::string& result, size_t* binaryByteCount /*= NULL*/) const
{
    impl_->encode(data, result, binaryByteCount);
}


PWIZ_API_DECL void BinaryDataEncoder::encode(const double* data, size_t dataSize, std::string& result, size_t* binaryByteCount /*= NULL*/) const
{
    impl_->encode(data, dataSize, result, binaryByteCount);
}


PWIZ_API_DECL void BinaryDataEncoder::decode(const char * encodedData, size_t len, pwiz::util::BinaryData<double> &result) const
{
    impl_->decode(encodedData, len, result);
}

PWIZ_API_DECL const BinaryDataEncoder::Config& BinaryDataEncoder::getConfig() const // get the config actually used - may differ from input for numpress use
{
    return impl_->getConfig();
}

void writeConfig(ostream& os, const BinaryDataEncoder::Config& config, CVID cvid) 
{

    BinaryDataEncoder::Precision p;
    BinaryDataEncoder::Numpress c;
    map<CVID, BinaryDataEncoder::Precision>::const_iterator pOverrideItr;
    map<CVID, BinaryDataEncoder::Numpress>::const_iterator cOverrideItr;
    cOverrideItr = config.numpressOverrides.find(cvid);

    if (cOverrideItr != config.numpressOverrides.end())
        c = cOverrideItr->second;
    else 
        c = config.numpress;
    const char *commaspace =  (BinaryDataEncoder::Compression_Zlib == config.compression)?", ":" ";
    switch (c) {
        case BinaryDataEncoder::Numpress_Linear:
            os << "Compression-Numpress-Linear" << commaspace;
            break;
        case BinaryDataEncoder::Numpress_Pic:
            os << "Compression-Numpress-Pic" << commaspace;
            break;
        case BinaryDataEncoder::Numpress_Slof:
            os << "Compression-Numpress-Slof" << commaspace;
            break;
        case BinaryDataEncoder::Numpress_None:
            break;
        default:
            throw runtime_error("[BinaryDataEncoder::writeConfig] Unknown binary numpress mode");
            break;
    }
    switch (config.compression) {
        case BinaryDataEncoder::Compression_Zlib :
            os << "Compression-Zlib";
            break;
        case BinaryDataEncoder::Compression_None :
            if (BinaryDataEncoder::Numpress_None == c)
                os << "Compression-None";
            break;
        default:
            throw runtime_error("[BinaryDataEncoder::writeConfig] Unknown binary numeric compression");
    }

    pOverrideItr = config.precisionOverrides.find(cvid);
    if (pOverrideItr != config.precisionOverrides.end())
        p = pOverrideItr->second;
    else 
        p = config.precision;

    switch (p) {
        case BinaryDataEncoder::Precision_64:
            os << ", 64-bit";
            break;
        case BinaryDataEncoder::Precision_32:
            os << ", 32-bit";
            break;
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const BinaryDataEncoder::Config& config)
{
    BinaryDataEncoder::Config usedConfig = config;

    os << endl << "    m/z: ";
    writeConfig(os, config, MS_m_z_array);
    
    os << endl << "    intensity: ";
    writeConfig(os, config, MS_intensity_array);

    os << endl << "    rt: ";
    writeConfig(os, config, MS_time_array);

    os << endl << (config.byteOrder==BinaryDataEncoder::ByteOrder_LittleEndian ? "ByteOrder_LittleEndian" : "ByteOrder_BigEndian") << endl;
    return os;   
}


} // namespace msdata
} // namespace pwiz

