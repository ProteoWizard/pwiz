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


#include "Serializer_mzXML.hpp"
#include "Serializer_mzML.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testWriteRead(const MSData& msd, const Serializer_mzXML::Config& config)
{
    if (os_) *os_ << "testWriteRead() " << config << endl;

    Serializer_mzXML mzxmlSerializer(config);

    ostringstream oss;
    mzxmlSerializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    MSData msd2;
    mzxmlSerializer.read(iss, msd2);

    DiffConfig diffConfig;
    diffConfig.ignoreMetadata = true;
    diffConfig.ignoreChromatograms = true;

    Diff<MSData, DiffConfig> diff(msd, msd2, diffConfig);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);

    if (os_)
    {
        *os_ << "msd2:\n";
        Serializer_mzML mzmlSerializer;
        mzmlSerializer.write(*os_, msd2);
        *os_ << endl;

        *os_ << "msd2::";
        TextWriter write(*os_);
        write(msd2);
        
        *os_ << endl;
    }
}


void testWriteRead()
{
    MSData msd;
    examples::initializeTiny(msd);

    // remove s22 since it is not written to mzXML
    static_cast<SpectrumListSimple&>(*msd.run.spectrumListPtr).spectra.pop_back();

    Serializer_mzXML::Config config;
    unit_assert(config.binaryDataEncoderConfig.precision == BinaryDataEncoder::Precision_64);
    testWriteRead(msd, config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    testWriteRead(msd, config);

    config.indexed = false;
    testWriteRead(msd, config);
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

