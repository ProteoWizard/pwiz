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
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/logic/tribool.hpp"


using namespace pwiz::util;
using namespace pwiz::cv;
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

    virtual tribool less(const Spectrum& lhs,
                         const Spectrum& rhs) const
    {
        if (lhs.id.empty())
            return boost::logic::indeterminate;
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

    virtual tribool less(const Spectrum& lhs,
                         const Spectrum& rhs) const
    {
        CVParam lhsMSLevel = lhs.cvParam(MS_ms_level);
        CVParam rhsMSLevel = rhs.cvParam(MS_ms_level);
        if (lhsMSLevel.empty() || rhsMSLevel.empty())
            return boost::logic::indeterminate;
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
        new SpectrumList_Sorter(msLevelStableSortedList, DefaultArrayLengthSorter()));

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

    unit_assert_operator_equal(originalList->size(), defaultArrayLengthSortedList->size());
    unit_assert_operator_equal(originalList->size(), msLevelUnstableSortedList->size());
    unit_assert_operator_equal(originalList->size(), msLevelStableSortedList->size());
    unit_assert_operator_equal(originalList->size(), sillySortedList->size());

    SpectrumPtr s;

    // assert that the original list is unmodified
    unit_assert_operator_equal("scan=19", originalList->spectrumIdentity(0).id);
    unit_assert_operator_equal(0, originalList->spectrumIdentity(0).index);
    unit_assert_operator_equal("scan=20", originalList->spectrumIdentity(1).id);
    unit_assert_operator_equal(1, originalList->spectrumIdentity(1).index);
    unit_assert_operator_equal("scan=21", originalList->spectrumIdentity(2).id);
    unit_assert_operator_equal(2, originalList->spectrumIdentity(2).index);
    unit_assert_operator_equal("scan=22", originalList->spectrumIdentity(3).id);
    unit_assert_operator_equal(3, originalList->spectrumIdentity(3).index);
    s = originalList->spectrum(0);
    unit_assert_operator_equal("scan=19", s->id);
    unit_assert_operator_equal(0, s->index);

    // validate the default array length sorted list (ascending order, scan=19 and scan=22 are interchangeable)
    unit_assert_operator_equal("scan=21", defaultArrayLengthSortedList->spectrumIdentity(0).id);
    unit_assert_operator_equal(0, defaultArrayLengthSortedList->spectrumIdentity(0).index);
    unit_assert_operator_equal("scan=20", defaultArrayLengthSortedList->spectrumIdentity(1).id);
    unit_assert_operator_equal(1, defaultArrayLengthSortedList->spectrumIdentity(1).index);
    unit_assert_operator_equal(2, defaultArrayLengthSortedList->spectrumIdentity(2).index);
    unit_assert_operator_equal(3, defaultArrayLengthSortedList->spectrumIdentity(3).index);
    s = defaultArrayLengthSortedList->spectrum(0);
    unit_assert_operator_equal("scan=21", s->id);
    unit_assert_operator_equal(0, s->index);
    s = defaultArrayLengthSortedList->spectrum(1);
    unit_assert_operator_equal("scan=20", s->id);
    unit_assert_operator_equal(1, s->index);
    s = defaultArrayLengthSortedList->spectrum(2);
    unit_assert_operator_equal(2, s->index);
    s = defaultArrayLengthSortedList->spectrum(3);
    unit_assert_operator_equal(3, s->index);
    for (size_t i=1, end=defaultArrayLengthSortedList->size(); i < end; ++i)
        unit_assert(defaultArrayLengthSortedList->spectrum(i)->defaultArrayLength >=
                    defaultArrayLengthSortedList->spectrum(i-1)->defaultArrayLength);

    // validate the MS level unstable sorted list (scan=19, scan=21, and scan=22 are interchangeable)
    unit_assert_operator_equal(0, msLevelUnstableSortedList->spectrumIdentity(0).index);
    unit_assert_operator_equal(1, msLevelUnstableSortedList->spectrumIdentity(1).index);
    unit_assert_operator_equal(2, msLevelUnstableSortedList->spectrumIdentity(2).index);
    unit_assert_operator_equal("scan=20", msLevelUnstableSortedList->spectrumIdentity(3).id);
    unit_assert_operator_equal(3, msLevelUnstableSortedList->spectrumIdentity(3).index);
    s = msLevelUnstableSortedList->spectrum(0);
    unit_assert_operator_equal(0, s->index);
    s = msLevelUnstableSortedList->spectrum(1);
    unit_assert_operator_equal(1, s->index);
    s = msLevelUnstableSortedList->spectrum(2);
    unit_assert_operator_equal(2, s->index);
    s = msLevelUnstableSortedList->spectrum(3);
    unit_assert_operator_equal("scan=20", s->id);
    unit_assert_operator_equal(3, s->index);

    // validate the MS level stable sorted list (scan=19, scan=21, and scan=22 should stay in order)
    unit_assert_operator_equal("scan=19", msLevelStableSortedList->spectrumIdentity(0).id);
    unit_assert_operator_equal(0, msLevelStableSortedList->spectrumIdentity(0).index);
    unit_assert_operator_equal("scan=21", msLevelStableSortedList->spectrumIdentity(1).id);
    unit_assert_operator_equal(1, msLevelStableSortedList->spectrumIdentity(1).index);
    unit_assert_operator_equal("sample=1 period=1 cycle=23 experiment=1", msLevelStableSortedList->spectrumIdentity(2).id);
    unit_assert_operator_equal(2, msLevelStableSortedList->spectrumIdentity(2).index);
    unit_assert_operator_equal("scan=20", msLevelStableSortedList->spectrumIdentity(3).id);
    unit_assert_operator_equal(3, msLevelStableSortedList->spectrumIdentity(3).index);
    s = msLevelStableSortedList->spectrum(0);
    unit_assert_operator_equal("scan=19", s->id);
    unit_assert_operator_equal(0, s->index);
    s = msLevelStableSortedList->spectrum(1);
    unit_assert_operator_equal("scan=21", s->id);
    unit_assert_operator_equal(1, s->index);
    s = msLevelStableSortedList->spectrum(2);
    unit_assert_operator_equal("sample=1 period=1 cycle=23 experiment=1", s->id);
    unit_assert_operator_equal(2, s->index);
    s = msLevelStableSortedList->spectrum(3);
    unit_assert_operator_equal("scan=20", s->id);
    unit_assert_operator_equal(3, s->index);

    // validate the silly (nested) sorted list
    unit_assert_operator_equal("scan=21", sillySortedList->spectrumIdentity(0).id);
    unit_assert_operator_equal(0, sillySortedList->spectrumIdentity(0).index);
    unit_assert_operator_equal("scan=20", sillySortedList->spectrumIdentity(1).id);
    unit_assert_operator_equal(1, sillySortedList->spectrumIdentity(1).index);
    unit_assert_operator_equal(2, sillySortedList->spectrumIdentity(2).index);
    unit_assert_operator_equal(3, sillySortedList->spectrumIdentity(3).index);
    s = sillySortedList->spectrum(0);
    unit_assert_operator_equal("scan=21", s->id);
    unit_assert_operator_equal(0, s->index);
    s = sillySortedList->spectrum(1);
    unit_assert_operator_equal("scan=20", s->id);
    unit_assert_operator_equal(1, s->index);
    s = sillySortedList->spectrum(2);
    unit_assert_operator_equal(2, s->index);
    s = sillySortedList->spectrum(3);
    unit_assert_operator_equal(3, s->index);
    for (size_t i=1, end=sillySortedList->size(); i < end; ++i)
        unit_assert(sillySortedList->spectrum(i)->defaultArrayLength >=
                    sillySortedList->spectrum(i-1)->defaultArrayLength);
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
