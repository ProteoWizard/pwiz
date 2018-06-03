//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::data;
using namespace pwiz::msdata;



void writeTiny()
{
    // create the MSData object in memory
    MSData msd;
    examples::initializeTiny(msd); 

    // write out mzML 
    string filename = "tiny.pwiz.mzML";
    cout << "Writing file " << filename << endl;
    MSDataFile::write(msd, filename);

    // write out mzXML
    filename = "tiny.pwiz.mzXML";
    cout << "Writing file " << filename << endl;
    MSDataFile::write(msd, filename, MSDataFile::Format_mzXML);
}


void writeSmall()
{
#ifdef PWIZ_READER_THERMO
    const string& inputFile = "small.RAW";

    try
    {
        FullReaderList readers;
        MSDataFile msd(inputFile, &readers, true);

        // msconvert defaults

        MSDataFile::WriteConfig config;
        config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
        config.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
        config.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;

        // basic mzML conversion

        string outputFile = "small.pwiz.mzML";
        cout << "Writing file " << outputFile << endl;
        msd.write(outputFile, config);
    
        // with zlib compression, 32-bit encoding

        config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
        config.binaryDataEncoderConfig.precision
            = config.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
            = config.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] 
            = BinaryDataEncoder::Precision_32;
        outputFile = "small_zlib.pwiz.mzML";
        cout << "Writing file " << outputFile << endl;
        msd.write(outputFile, config);

        // with MIAPE metadata added 

        examples::addMIAPEExampleMetadata(msd);
        outputFile = "small_miape.pwiz.mzML";
        cout << "Writing file " << outputFile << endl;
        msd.write(outputFile, config);
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        cerr << "Error opening file " << inputFile << endl;
    }
#endif
}


int main()
{
    try
    {
        writeTiny();
        writeSmall();

        cout << "\nhttps://github.com/ProteoWizard\n"
             << "support@proteowizard.org\n";

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }

    return 1;
}

