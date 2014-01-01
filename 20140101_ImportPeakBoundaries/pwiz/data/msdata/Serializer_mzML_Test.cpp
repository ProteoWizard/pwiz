//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "Serializer_mzML.hpp"
#include "Diff.hpp"
#include "References.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testWriteRead(const MSData& msd, const Serializer_mzML::Config& config, const DiffConfig &diffcfg)
{
    if (os_) *os_ << "testWriteRead() " << config << endl;

    Serializer_mzML mzmlSerializer(config);

    ostringstream oss;
    mzmlSerializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    MSData msd2;
    mzmlSerializer.read(iss, msd2);

    References::resolve(msd2);

    Diff<MSData, DiffConfig> diff(msd, msd2, diffcfg);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}


void testWriteRead()
{
    MSData msd;
    examples::initializeTiny(msd);
    for (int zloop=2;zloop--;) // run through once without zlib, then with
    {
        DiffConfig diffcfg;
        Serializer_mzML::Config config;

        if (!zloop) // retest with compression 
            config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;

        unit_assert(config.binaryDataEncoderConfig.precision == BinaryDataEncoder::Precision_64);
        testWriteRead(msd, config, diffcfg);

        config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
        testWriteRead(msd, config, diffcfg);

        config.indexed = false;
        testWriteRead(msd, config, diffcfg);

        // lossy compression, increase allowable mismatch
        diffcfg.precision = 0.01;
        config.binaryDataEncoderConfig.numpress = BinaryDataEncoder::Numpress_Linear;
        testWriteRead(msd, config, diffcfg);

        config.binaryDataEncoderConfig.numpress = BinaryDataEncoder::Numpress_Pic;
        testWriteRead(msd, config, diffcfg);

        config.binaryDataEncoderConfig.numpress = BinaryDataEncoder::Numpress_None;
        config.binaryDataEncoderConfig.numpressOverrides[MS_intensity_array] = BinaryDataEncoder::Numpress_Slof;
        testWriteRead(msd, config, diffcfg);
        
        if (!zloop) // provoke numpress temp. disable
        {
            config.binaryDataEncoderConfig.numpressLinearErrorTolerance = .000000001; 
            config.binaryDataEncoderConfig.numpressSlofErrorTolerance = .000000001; 
            testWriteRead(msd, config, diffcfg);
        }
    }

}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testWriteRead();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

