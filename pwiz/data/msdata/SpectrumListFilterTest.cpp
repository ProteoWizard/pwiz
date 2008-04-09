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
using boost::logic::tribool;


ostream* os_ = 0;


SpectrumListPtr createSpectrumList()
{
    SpectrumListSimplePtr sl(new SpectrumListSimple);

    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->index = i;
        spectrum->nativeID = lexical_cast<string>(100+i);
        vector<MZIntensityPair> pairs(i);
        spectrum->setMZIntensityPairs(pairs);
        spectrum->set(MS_ms_level, i%3==0?1:2);
        sl->spectra.push_back(spectrum);
    }

    if (os_)
    {
        *os_ << "original spectrum list:\n";
        
        for (size_t i=0, end=sl->size(); i<end; i++)
        {
            SpectrumPtr spectrum = sl->spectrum(i, false);
            *os_ << spectrum->index << " " 
                 << spectrum->nativeID << " "
                 << "ms" << spectrum->cvParam(MS_ms_level).value << " "
                 << endl;
        }

        *os_ << endl;
    }

    return sl;
}


struct EvenPredicate : public SpectrumListFilter::Predicate
{
    virtual tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return spectrumIdentity.index%2 == 0;
    }
};


void testEven(SpectrumListPtr sl)
{
    if (os_) *os_ << "testEven:\n";

    SpectrumListFilter filter(sl, EvenPredicate());
    if (os_) *os_ << "size: " << filter.size() << endl;
    unit_assert(filter.size() == 5);

    for (size_t i=0, end=filter.size(); i<end; i++)
    {
        const SpectrumIdentity& id = filter.spectrumIdentity(i); 
        unit_assert(id.index == i);
        unit_assert(id.nativeID == lexical_cast<string>(100+i*2));

        SpectrumPtr spectrum = filter.spectrum(i);
        if (os_) *os_ << spectrum->index << " " << spectrum->nativeID << endl;
        unit_assert(spectrum->index == i);
        unit_assert(spectrum->nativeID == lexical_cast<string>(100+i*2));
    }

    if (os_) *os_ << endl;
}


struct EvenMS2Predicate : public SpectrumListFilter::Predicate
{
    virtual tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        if (spectrumIdentity.index%2 != 0) return false;
        return boost::logic::indeterminate;
    }

    virtual bool accept(const Spectrum& spectrum) const
    {
        return (spectrum.cvParam(MS_ms_level).valueAs<int>() == 2);
    }
};


void testEvenMS2(SpectrumListPtr sl)
{
    if (os_) *os_ << "testEvenMS2:\n";

    SpectrumListFilter filter(sl, EvenMS2Predicate());
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->nativeID << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 3);
    unit_assert(filter.spectrumIdentity(0).nativeID == "102");
    unit_assert(filter.spectrumIdentity(1).nativeID == "104");
    unit_assert(filter.spectrumIdentity(2).nativeID == "108");
}


struct SelectedIndexPredicate : public SpectrumListFilter::Predicate
{
    mutable bool pastMaxIndex;

    SelectedIndexPredicate() : pastMaxIndex(false) {}

    virtual tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        if (spectrumIdentity.index>5) pastMaxIndex = true;

        return (spectrumIdentity.index==1 ||
                spectrumIdentity.index==3 ||
                spectrumIdentity.index==5);
    }

    virtual bool done() const
    {
        return pastMaxIndex;
    }
};


void testSelectedIndices(SpectrumListPtr sl)
{
    if (os_) *os_ << "testSelectedIndices:\n";

    SpectrumListFilter filter(sl, SelectedIndexPredicate());
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->nativeID << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 3);
    unit_assert(filter.spectrumIdentity(0).nativeID == "101");
    unit_assert(filter.spectrumIdentity(1).nativeID == "103");
    unit_assert(filter.spectrumIdentity(2).nativeID == "105");
}


void test()
{
    SpectrumListPtr sl = createSpectrumList();
    testEven(sl);
    testEvenMS2(sl);
    testSelectedIndices(sl);
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


