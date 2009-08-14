//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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


#include "SpectrumList_Sorter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <vector>
#include <iostream>
#include <iterator>
#include <cstring>
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/logic/tribool.hpp"


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using boost::logic::tribool;

ostream* os_ = 0;


struct DefaultArrayLengthSorter : public SpectrumList_Sorter::Predicate
{
    virtual tribool less(const SpectrumIdentity& lhs,
                         const SpectrumIdentity& rhs) const 
    {
        return boost::logic::indeterminate;
    }

    virtual bool less(const Spectrum& lhs,
                      const Spectrum& rhs) const
    {
        return lhs.defaultArrayLength < rhs.defaultArrayLength;
    }
};

struct MSLevelSorter : public SpectrumList_Sorter::Predicate
{
    virtual tribool less(const SpectrumIdentity& lhs,
                         const SpectrumIdentity& rhs) const 
    {
        return boost::logic::indeterminate;
    }

    virtual bool less(const Spectrum& lhs,
                      const Spectrum& rhs) const
    {
        CVParam lhsMSLevel = lhs.cvParam(MS_ms_level);
        CVParam rhsMSLevel = rhs.cvParam(MS_ms_level);
        if (lhsMSLevel.empty() || rhsMSLevel.empty())
            throw runtime_error("[MSLevelSorter::less()] Spectrum operands must be mass spectra");
        return lhsMSLevel.valueAs<int>() < rhsMSLevel.valueAs<int>();
    }
};


void test()
{
    MSData msd;
    examples::initializeTiny(msd);

    SpectrumListPtr originalList = msd.run.spectrumListPtr;

    SpectrumListPtr defaultArrayLengthSortedList(
        new SpectrumList_Sorter(originalList, DefaultArrayLengthSorter()));

    SpectrumListPtr msLevelUnstableSortedList(
        new SpectrumList_Sorter(originalList, MSLevelSorter()));

    SpectrumListPtr msLevelStableSortedList(
        new SpectrumList_Sorter(originalList, MSLevelSorter(), true));

    SpectrumListPtr sillySortedList(
        new SpectrumList_Sorter(
            SpectrumListPtr(new SpectrumList_Sorter(originalList, MSLevelSorter())),
            DefaultArrayLengthSorter()));

    if (os_)
    {
        *os_ << "Original spectrum list (" << originalList->size() << "):\n";
        TextWriter write(*os_);
        write(*originalList);
        *os_ << endl;
    }

    if (os_)
    {
        *os_ << "Default array length sorted spectrum list (" << defaultArrayLengthSortedList->size() << "):\n";
        TextWriter write(*os_);
        write(*defaultArrayLengthSortedList);
        *os_ << endl;
    }

    if (os_)
    {
        *os_ << "MS level unstable sorted spectrum list (" << msLevelUnstableSortedList->size() << "):\n";
        TextWriter write(*os_);
        write(*msLevelUnstableSortedList);
        *os_ << endl;
    }

    if (os_)
    {
        *os_ << "MS level stable sorted spectrum list (" << msLevelStableSortedList->size() << "):\n";
        TextWriter write(*os_);
        write(*msLevelStableSortedList);
        *os_ << endl;
    }

    if (os_)
    {
        *os_ << "Silly (nested) sorted spectrum list (" << sillySortedList->size() << "):\n";
        TextWriter write(*os_);
        write(*sillySortedList);
        *os_ << endl;
    }

    unit_assert(originalList->size() == defaultArrayLengthSortedList->size() &&
                originalList->size() == msLevelUnstableSortedList->size() &&
                originalList->size() == msLevelStableSortedList->size() &&
                originalList->size() == sillySortedList->size());

    SpectrumPtr s;

    // assert that the original list is unmodified
    unit_assert(originalList->spectrumIdentity(0).id == "scan=19");
    unit_assert(originalList->spectrumIdentity(0).index == 0);
    unit_assert(originalList->spectrumIdentity(1).id == "scan=20");
    unit_assert(originalList->spectrumIdentity(1).index == 1);
    unit_assert(originalList->spectrumIdentity(2).id == "scan=21");
    unit_assert(originalList->spectrumIdentity(2).index == 2);
    unit_assert(originalList->spectrumIdentity(3).id == "sample=1 period=1 cycle=22 experiment=1");
    unit_assert(originalList->spectrumIdentity(3).index == 3);
    s = originalList->spectrum(0);
    unit_assert(s->id == "scan=19");
    unit_assert(s->index == 0);

    // validate the default array length sorted list (ascending order, scan=19 and scan=22 are interchangeable)
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(0).id == "scan=21");
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(0).index == 0);
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(1).id == "scan=20");
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(1).index == 1);
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(2).index == 2);
    unit_assert(defaultArrayLengthSortedList->spectrumIdentity(3).index == 3);
    s = defaultArrayLengthSortedList->spectrum(0);
    unit_assert(s->id == "scan=21");
    unit_assert(s->index == 0);
    s = defaultArrayLengthSortedList->spectrum(1);
    unit_assert(s->id == "scan=20");
    unit_assert(s->index == 1);
    s = defaultArrayLengthSortedList->spectrum(2);
    unit_assert(s->index == 2);
    s = defaultArrayLengthSortedList->spectrum(3);
    unit_assert(s->index == 3);
    for (size_t i=1, end=defaultArrayLengthSortedList->size(); i < end; ++i)
        unit_assert(defaultArrayLengthSortedList->spectrum(i)->defaultArrayLength >=
                    defaultArrayLengthSortedList->spectrum(i-1)->defaultArrayLength);

    // validate the MS level unstable sorted list (scan=19, scan=21, and scan=22 are interchangeable)
    unit_assert(msLevelUnstableSortedList->spectrumIdentity(0).index == 0);
    unit_assert(msLevelUnstableSortedList->spectrumIdentity(1).index == 1);
    unit_assert(msLevelUnstableSortedList->spectrumIdentity(2).index == 2);
    unit_assert(msLevelUnstableSortedList->spectrumIdentity(3).id == "scan=20");
    unit_assert(msLevelUnstableSortedList->spectrumIdentity(3).index == 3);
    s = msLevelUnstableSortedList->spectrum(0);
    unit_assert(s->index == 0);
    s = msLevelUnstableSortedList->spectrum(1);
    unit_assert(s->index == 1);
    s = msLevelUnstableSortedList->spectrum(2);
    unit_assert(s->index == 2);
    s = msLevelUnstableSortedList->spectrum(3);
    unit_assert(s->id == "scan=20");
    unit_assert(s->index == 3);

    // validate the MS level stable sorted list (scan=19, scan=21, and scan=22 should stay in order)
    unit_assert(msLevelStableSortedList->spectrumIdentity(0).id == "scan=19");
    unit_assert(msLevelStableSortedList->spectrumIdentity(0).index == 0);
    unit_assert(msLevelStableSortedList->spectrumIdentity(1).id == "scan=21");
    unit_assert(msLevelStableSortedList->spectrumIdentity(1).index == 1);
    unit_assert(msLevelStableSortedList->spectrumIdentity(2).id == "sample=1 period=1 cycle=22 experiment=1");
    unit_assert(msLevelStableSortedList->spectrumIdentity(2).index == 2);
    unit_assert(msLevelStableSortedList->spectrumIdentity(3).id == "scan=20");
    unit_assert(msLevelStableSortedList->spectrumIdentity(3).index == 3);
    s = msLevelStableSortedList->spectrum(0);
    unit_assert(s->id == "scan=19");
    unit_assert(s->index == 0);
    s = msLevelStableSortedList->spectrum(1);
    unit_assert(s->id == "scan=21");
    unit_assert(s->index == 1);
    s = msLevelStableSortedList->spectrum(2);
    unit_assert(s->id == "sample=1 period=1 cycle=22 experiment=1");
    unit_assert(s->index == 2);
    s = msLevelStableSortedList->spectrum(3);
    unit_assert(s->id == "scan=20");
    unit_assert(s->index == 3);

    // validate the silly (nested) sorted list
    unit_assert(sillySortedList->spectrumIdentity(0).id == "scan=21");
    unit_assert(sillySortedList->spectrumIdentity(0).index == 0);
    unit_assert(sillySortedList->spectrumIdentity(1).id == "scan=20");
    unit_assert(sillySortedList->spectrumIdentity(1).index == 1);
    unit_assert(sillySortedList->spectrumIdentity(2).index == 2);
    unit_assert(sillySortedList->spectrumIdentity(3).index == 3);
    s = sillySortedList->spectrum(0);
    unit_assert(s->id == "scan=21");
    unit_assert(s->index == 0);
    s = sillySortedList->spectrum(1);
    unit_assert(s->id == "scan=20");
    unit_assert(s->index == 1);
    s = sillySortedList->spectrum(2);
    unit_assert(s->index == 2);
    s = sillySortedList->spectrum(3);
    unit_assert(s->index == 3);
    for (size_t i=1, end=sillySortedList->size(); i < end; ++i)
        unit_assert(sillySortedList->spectrum(i)->defaultArrayLength >=
                    sillySortedList->spectrum(i-1)->defaultArrayLength);
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
    }
    
    return 1;
}
