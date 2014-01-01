//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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


#include "SpectrumList_MZWindow.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace pwiz::analysis;


ostream* os_ = 0;


void printSpectrumList(const SpectrumList& sl, ostream& os)
{
    os << "size: " << sl.size() << endl;

    for (size_t i=0, end=sl.size(); i<end; i++)
    {
        SpectrumPtr spectrum = sl.spectrum(i, false);
        vector<MZIntensityPair> data;
        spectrum->getMZIntensityPairs(data);

        os << spectrum->index << " " 
           << spectrum->id << ": ";

        copy(data.begin(), data.end(), ostream_iterator<MZIntensityPair>(os, " "));

        os << endl;
    }
}


SpectrumListPtr createSpectrumList()
{
    SpectrumListSimplePtr sl(new SpectrumListSimple);

    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->index = i;
        spectrum->id = "scan=" + lexical_cast<string>(100+i);

        // data: (i,1000) (i+1,1001) (i+2,1002) (i+3,1003) (i+4,1004)
        vector<MZIntensityPair> data(5);
        for (size_t j=0; j<5; j++) data[j] = MZIntensityPair(i+j, 1000+j); 
        spectrum->setMZIntensityPairs(data, MS_number_of_detector_counts);

        sl->spectra.push_back(spectrum);
    }

    if (os_)
    {
        *os_ << "original spectrum list:\n";
        printSpectrumList(*sl, *os_); 
        *os_ << endl;
    }

    return sl;
}


void verifySpectrumSize(const SpectrumList& sl, size_t index, size_t size)
{
    SpectrumPtr spectrum = sl.spectrum(index, true);
    vector<MZIntensityPair> data;
    spectrum->getMZIntensityPairs(data);
    unit_assert(data.size() == size);
}

 
void test()
{
    SpectrumListPtr sl = createSpectrumList();
    for (size_t i=0; i<sl->size(); i++)
        verifySpectrumSize(*sl, i, 5);

    SpectrumList_MZWindow window(sl, 4.20, 6.66);

    if (os_) 
    {
        *os_ << "filtered list:\n";
        printSpectrumList(window, *os_);
        *os_ << endl;
    }

    unit_assert(window.size() == sl->size());
    verifySpectrumSize(window, 0, 0);
    verifySpectrumSize(window, 1, 1);
    verifySpectrumSize(window, 2, 2);
    verifySpectrumSize(window, 3, 2);
    verifySpectrumSize(window, 4, 2);
    verifySpectrumSize(window, 5, 2);
    verifySpectrumSize(window, 6, 1);
    verifySpectrumSize(window, 7, 0);
    verifySpectrumSize(window, 8, 0);
    verifySpectrumSize(window, 9, 0);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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


