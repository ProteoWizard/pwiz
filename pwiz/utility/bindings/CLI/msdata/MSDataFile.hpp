//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _MSDATAFILE_HPP_CLI_
#define _MSDATAFILE_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "MSData.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "../common/IterationListener.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {


/// <summary>
/// MSData object plus file I/O
/// </summary>
public ref class MSDataFile : public MSData
{
    INTERNAL:
    MSDataFile(boost::shared_ptr<pwiz::msdata::MSDataFile>* base);
    virtual ~MSDataFile();
    !MSDataFile();
    boost::shared_ptr<pwiz::msdata::MSDataFile>* base_;
    pwiz::msdata::MSDataFile& base() new;

    public:

    /// <summary>
    /// constructs MSData object backed by file
    /// </summary>
    MSDataFile(System::String^ path);

    /// <summary>
    /// constructs MSData object backed by file with callbacks to an IterationListener
    /// </summary>
    MSDataFile(System::String^ path, util::IterationListenerRegistry^ iterationListenerRegistry);

    /// <summary>
    /// supported data formats for write()
    /// </summary>
    enum class Format {Format_Text, Format_mzML, Format_mzXML, Format_MGF, Format_MS1, Format_CMS1, Format_MS2, Format_CMS2, Format_MZ5};

    enum class Precision {Precision_32, Precision_64};
    enum class ByteOrder {ByteOrder_LittleEndian, ByteOrder_BigEndian};
    enum class Compression {Compression_None, Compression_Zlib};
    enum class Numpress {Numpress_None, Numpress_Linear, Numpress_Pic, Numpress_Slof}; // lossy numerical representations

    /// <summary>
    /// configuration options for write()
    /// </summary>
    ref class WriteConfig
    {
        public:
        property Format format;
        property Precision precision;
        property ByteOrder byteOrder;
        property Compression compression;
        property bool numpressPic;  // lossy numerical representations for intensities
        property bool numpressSlof; // lossy numerical representations for intensities
        property bool numpressLinear; // lossy numerical representations for rt and mz
        property double numpressLinearErrorTolerance;  // guarantee abs(1.0-(encoded/decoded)) <= this, 0=do not guarantee anything
        property double numpressSlofErrorTolerance;  // guarantee abs(1.0-(encoded/decoded)) <= this, 0=do not guarantee anything
		property double numpressLinearAbsMassAcc; // absolute mass error for lossy linear compression in Th (e.g. use 1e-4 for 1ppm @ 100 Th)
        property bool indexed;
        property bool gzipped;
        property bool useWorkerThreads;

        WriteConfig()
        {
            format = Format::Format_mzML;
            numpressPic = false;
            numpressSlof = false;
            numpressLinear = false;
            numpressLinearErrorTolerance = pwiz::msdata::BinaryDataEncoder_default_numpressLinearErrorTolerance;
			numpressSlofErrorTolerance = pwiz::msdata::BinaryDataEncoder_default_numpressSlofErrorTolerance;
			numpressLinearAbsMassAcc = -1.0;
			indexed = true;
            useWorkerThreads = true;
        }
    };

    /// <summary>
    /// static write function for any MSData object with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    static void write(MSData^ msd, System::String^ filename);

    /// <summary>
    /// static write function for any MSData object with the specified configuration
    /// </summary>
    static void write(MSData^ msd, System::String^ filename, WriteConfig^ config);

    /// <summary>
    /// static write function for any MSData object with the specified configuration
    /// </summary>
    static void write(MSData^ msd,
                      System::String^ filename,
                      WriteConfig^ config,
                      util::IterationListenerRegistry^ iterationListenerRegistry);

    /// <summary>
    /// member write function with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    void write(System::String^ filename);

    /// <summary>
    /// member write function with the specified configuration
    /// </summary>
    void write(System::String^ filename, WriteConfig^ config);

    /// <summary>
    /// member write function with the specified configuration
    /// </summary>
    void write(System::String^ filename,
               WriteConfig^ config,
               util::IterationListenerRegistry^ iterationListenerRegistry);

	/// <summary>
    /// calculate SHA1 checksums for all source files
    /// </summary>
    static void calculateSHA1Checksums(MSData^ msd);
};

} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _MSDATAFILE_HPP_CLI_
