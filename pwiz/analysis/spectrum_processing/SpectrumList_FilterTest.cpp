//
// SpectrumList_FilterTest.cpp
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


#include "SpectrumList_Filter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>


using namespace pwiz::msdata;
using namespace pwiz::analysis;
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
        spectrum->id = "scan=" + lexical_cast<string>(100+i);
        vector<MZIntensityPair> pairs(i);
        spectrum->setMZIntensityPairs(pairs);
        spectrum->set(MS_ms_level, i%3==0?1:2);
        spectrum->scanList.scans.push_back(Scan());
        spectrum->scanList.scans[0].set(MS_preset_scan_configuration, i%4);
        sl->spectra.push_back(spectrum);
    }

    if (os_)
    {
        *os_ << "original spectrum list:\n";
        
        for (size_t i=0, end=sl->size(); i<end; i++)
        {
            SpectrumPtr spectrum = sl->spectrum(i, false);
            *os_ << spectrum->index << " " 
                 << spectrum->id << " "
                 << "ms" << spectrum->cvParam(MS_ms_level).value << " "
                 << "scanEvent:" << spectrum->scanList.scans[0].cvParam(MS_preset_scan_configuration).value << " "
                 << endl;
        }

        *os_ << endl;
    }

    return sl;
}


struct EvenPredicate : public SpectrumList_Filter::Predicate
{
    virtual tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return spectrumIdentity.index%2 == 0;
    }
};


void testEven(SpectrumListPtr sl)
{
    if (os_) *os_ << "testEven:\n";

    SpectrumList_Filter filter(sl, EvenPredicate());
    if (os_) *os_ << "size: " << filter.size() << endl;
    unit_assert(filter.size() == 5);

    for (size_t i=0, end=filter.size(); i<end; i++)
    {
        const SpectrumIdentity& id = filter.spectrumIdentity(i); 
        unit_assert(id.index == i);
        unit_assert(id.id == "scan=" + lexical_cast<string>(100+i*2));

        SpectrumPtr spectrum = filter.spectrum(i);
        if (os_) *os_ << spectrum->index << " " << spectrum->id << endl;
        unit_assert(spectrum->index == i);
        unit_assert(spectrum->id == "scan=" + lexical_cast<string>(100+i*2));
    }

    if (os_) *os_ << endl;
}


struct EvenMS2Predicate : public SpectrumList_Filter::Predicate
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

    SpectrumList_Filter filter(sl, EvenMS2Predicate());
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->id << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 3);
    unit_assert(filter.spectrumIdentity(0).id == "scan=102");
    unit_assert(filter.spectrumIdentity(1).id == "scan=104");
    unit_assert(filter.spectrumIdentity(2).id == "scan=108");
}


struct SelectedIndexPredicate : public SpectrumList_Filter::Predicate
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

    SpectrumList_Filter filter(sl, SelectedIndexPredicate());
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->id << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 3);
    unit_assert(filter.spectrumIdentity(0).id == "scan=101");
    unit_assert(filter.spectrumIdentity(1).id == "scan=103");
    unit_assert(filter.spectrumIdentity(2).id == "scan=105");
}


void testIndexSet(SpectrumListPtr sl)
{
    if (os_) *os_ << "testIndexSet:\n";

    IntegerSet indexSet;
    indexSet.insert(3,5);
    indexSet.insert(7);
    indexSet.insert(9);

    SpectrumList_Filter filter(sl, SpectrumList_FilterPredicate_IndexSet(indexSet));
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->id << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 5);
    unit_assert(filter.spectrumIdentity(0).id == "scan=103");
    unit_assert(filter.spectrumIdentity(1).id == "scan=104");
    unit_assert(filter.spectrumIdentity(2).id == "scan=105");
    unit_assert(filter.spectrumIdentity(3).id == "scan=107");
    unit_assert(filter.spectrumIdentity(4).id == "scan=109");
}


void testScanNumberSet(SpectrumListPtr sl)
{
    if (os_) *os_ << "testScanNumberSet:\n";

    IntegerSet scanNumberSet;
    scanNumberSet.insert(102,104);
    scanNumberSet.insert(107);

    SpectrumList_Filter filter(sl, SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet));
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->id << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 4);
    unit_assert(filter.spectrumIdentity(0).id == "scan=102");
    unit_assert(filter.spectrumIdentity(1).id == "scan=103");
    unit_assert(filter.spectrumIdentity(2).id == "scan=104");
    unit_assert(filter.spectrumIdentity(3).id == "scan=107");
}


void testScanEventSet(SpectrumListPtr sl)
{
    if (os_) *os_ << "testScanEventSet:\n";

    IntegerSet scanEventSet;
    scanEventSet.insert(0,0);
    scanEventSet.insert(2,3);

    SpectrumList_Filter filter(sl, SpectrumList_FilterPredicate_ScanEventSet(scanEventSet));
    
    if (os_)
    {
        *os_ << "size: " << filter.size() << endl;

        for (size_t i=0, end=filter.size(); i<end; i++)
        {
            SpectrumPtr spectrum = filter.spectrum(i);
            *os_ << spectrum->index << " " << spectrum->id << endl;
        }

        *os_ << endl;
    }

    unit_assert(filter.size() == 7);
    unit_assert(filter.spectrumIdentity(0).id == "scan=100");
    unit_assert(filter.spectrumIdentity(1).id == "scan=102");
    unit_assert(filter.spectrumIdentity(2).id == "scan=103");
    unit_assert(filter.spectrumIdentity(3).id == "scan=104");
    unit_assert(filter.spectrumIdentity(4).id == "scan=106");
    unit_assert(filter.spectrumIdentity(5).id == "scan=107");
    unit_assert(filter.spectrumIdentity(6).id == "scan=108");
}


void test()
{
    SpectrumListPtr sl = createSpectrumList();
    testEven(sl);
    testEvenMS2(sl);
    testSelectedIndices(sl);
    testIndexSet(sl);
    testScanNumberSet(sl);
    testScanEventSet(sl);
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


