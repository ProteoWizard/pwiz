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
#include "boost/iostreams/filter/zlib.hpp"
#include "boost/iostreams/device/array.hpp"
#include "pwiz/utility/misc/Std.hpp"

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
    void decode(const string& encodedData, vector<double>& result);

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


template <typename filter_type>
string filterArray(const void* byteBuffer, size_t byteCount)
{
    ostringstream result(ios::binary);
    filtering_ostream fos;
    fos.push(filter_type());
    fos.push(result);
    fos.write((const char*)byteBuffer, byteCount);
    fos.pop(); 
    fos.pop(); // forces buffer to flush
    return result.str();

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
    //
    // We use buffer abstractions, since we may need to change buffers during
    // downconversion and compression -- these are eventually passed to
    // the Base64 encoder.  Note that:
    //  - byteBuffer and byteCount must remain valid at the end of any block
    //    in this function
    //  - by default, no processing is done, resulting in no buffer changes
    //    and fall through to Base64 encoding
    //

    const void* byteBuffer = reinterpret_cast<const void*>(data); 
    size_t byteCount = dataSize * sizeof(double);

    // 64-bit -> 32-bit downconversion

    vector<float> data32;

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

    vector<double> data64endianized;

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

    // compression

    string compressed;
    if (config_.compression == Compression_Zlib)
    {
        compressed = filterArray<zlib_compressor>(byteBuffer, byteCount);
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
    size_t textSize = Base64::binaryToText(byteBuffer, byteCount, &result[0]);
    result.resize(textSize);

    if (binaryByteCount != NULL)
        *binaryByteCount = byteCount; // size before base64 encoding
}


template <typename float_type>
void copyBuffer(const void* byteBuffer, size_t byteCount, vector<double>& result)
{
    const float_type* floatBuffer = reinterpret_cast<const float_type*>(byteBuffer);

    if (byteCount % sizeof(float_type) != 0) 
        throw runtime_error("[BinaryDataEncoder::copyBuffer()] Bad byteCount.");

    size_t floatCount = byteCount / sizeof(float_type);

    result.resize(floatCount);

    copy(floatBuffer, floatBuffer+floatCount, result.begin());
}


void BinaryDataEncoder::Impl::decode(const string& encodedData, vector<double>& result)
{
    if (encodedData.empty()) return;

    // Base64 decoding

    vector<unsigned char> binary(Base64::textToBinarySize(encodedData.size()));
    size_t binarySize = Base64::textToBinary(&encodedData[0], encodedData.size(), &binary[0]);
    binary.resize(binarySize);

    // buffer abstractions

    void* byteBuffer = &binary[0];
    size_t byteCount = binarySize;

    // decompression

    string decompressed;
    if (config_.compression == Compression_Zlib)
    {
        decompressed = filterArray<zlib_decompressor>(byteBuffer, byteCount);
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

    // endianization

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


PWIZ_API_DECL void BinaryDataEncoder::decode(const std::string& encodedData, std::vector<double>& result) const
{
    impl_->decode(encodedData, result);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const BinaryDataEncoder::Config& config)
{
    os << "(" 
       << (config.precision==BinaryDataEncoder::Precision_64 ? "Precision_64" : "Precision_32");
    os << " [";
    for( map<CVID, BinaryDataEncoder::Precision>::const_iterator itr = config.precisionOverrides.begin();
        itr != config.precisionOverrides.end();
        ++itr )
        os << " " << itr->first << ":" << (itr->second==BinaryDataEncoder::Precision_64 ? "Precision_64" : "Precision_32");
    os << " ], "
       << (config.byteOrder==BinaryDataEncoder::ByteOrder_LittleEndian ? "ByteOrder_LittleEndian" : "ByteOrder_BigEndian") << ", "
       << (config.compression==BinaryDataEncoder::Compression_None ? "Compression_None" : "Compression_Zlib") 
       << ")";
    return os;   
}


} // namespace msdata
} // namespace pwiz

