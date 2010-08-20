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


#ifndef _BINARYDATAENCODER_HPP_
#define _BINARYDATAENCODER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"
#include <string>
#include <vector>
#include <map>
#include "pwiz/data/common/cv.hpp"


namespace pwiz {
namespace msdata {


/// binary-to-text encoding
class PWIZ_API_DECL BinaryDataEncoder
{
    public:

    enum Precision {Precision_32, Precision_64};
    enum ByteOrder {ByteOrder_LittleEndian, ByteOrder_BigEndian};
    enum Compression {Compression_None, Compression_Zlib};

    /// encoding/decoding configuration 
    struct PWIZ_API_DECL Config
    {
        Precision precision;
        ByteOrder byteOrder;
        Compression compression;

        std::map<cv::CVID, Precision> precisionOverrides;

        Config()
        :   precision(Precision_64),
            byteOrder(ByteOrder_LittleEndian),
            compression(Compression_None)
        {}
    };

    BinaryDataEncoder(const Config& config = Config());

    /// encode binary data as a text string
    void encode(const std::vector<double>& data, std::string& result, size_t* binaryByteCount = NULL) const;

    /// encode binary data as a text string
    void encode(const double* data, size_t dataSize, std::string& result, size_t* binaryByteCount = NULL) const;

    /// decode text-encoded data as binary 
    void decode(const std::string& encodedData, std::vector<double>& result) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    BinaryDataEncoder(const BinaryDataEncoder&);
    BinaryDataEncoder& operator=(const BinaryDataEncoder&);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const BinaryDataEncoder::Config& config);


} // namespace msdata
} // namespace pwiz


#endif // _BINARYDATAENCODER_HPP_

