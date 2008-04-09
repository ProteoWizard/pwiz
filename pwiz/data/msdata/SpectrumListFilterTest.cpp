//
// SpectrumListFilterTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "SpectrumListFilter.hpp"
#include "utility/misc/unit.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>


using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace std;
using boost::lexical_cast;


ostream* os_ = 0;


class EvenPredicate : public SpectrumListFilter::Predicate
{
    virtual bool accept(const Spectrum& spectrum) const
    {
        return spectrum.index%2 == 0; 
    }
};


void test()
{
    SpectrumListSimplePtr sl(new SpectrumListSimple);

    if (os_) *os_ << "original:\n";
    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->index = i;
        spectrum->nativeID = lexical_cast<string>(i);
        vector<MZIntensityPair> pairs(i);
        spectrum->setMZIntensityPairs(pairs);
        if (os_) *os_ << spectrum->index << " " << spectrum->nativeID << endl;
        sl->spectra.push_back(spectrum);
    }

    if (os_) *os_ << "filtered:\n";
    SpectrumListFilter filter(sl, EvenPredicate());
    unit_assert(filter.size() == 5);
    for (size_t i=0; i<5; i++)
    {
        SpectrumPtr spectrum = filter.spectrum(i);
        if (os_) *os_ << spectrum->index << " " << spectrum->nativeID << endl;
        unit_assert(spectrum->index == i);
        unit_assert(spectrum->nativeID == lexical_cast<string>(i*2));

        const SpectrumIdentity& id = filter.spectrumIdentity(i); 
        //unit_assert(id.index == i); // TODO: fix
        unit_assert(id.nativeID == lexical_cast<string>(i*2));
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


