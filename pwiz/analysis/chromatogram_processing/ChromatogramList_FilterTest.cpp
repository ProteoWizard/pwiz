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


#include "ChromatogramList_Filter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include <cstring>


using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using boost::logic::tribool;


ostream* os_ = 0;


void printChromatogramList(const ChromatogramList& sl, ostream& os)
{
    os << "size: " << sl.size() << endl;

    for (size_t i=0, end=sl.size(); i<end; i++)
    {
        ChromatogramPtr chromatogram = sl.chromatogram(i, false);
        os << chromatogram->index << " " 
           << chromatogram->id << " "
           << endl;
    }
}


ChromatogramListPtr createChromatogramList()
{
    ChromatogramListSimplePtr sl(new ChromatogramListSimple);

    {
        ChromatogramPtr chromatogram(new Chromatogram);
        chromatogram->index = sl->size();

        chromatogram->id = "TIC";
        chromatogram->setTimeIntensityPairs(vector<TimeIntensityPair>(42), UO_second, MS_number_of_detector_counts);
        chromatogram->set(MS_TIC_chromatogram);

        sl->chromatograms.push_back(chromatogram);
    }

    {
        ChromatogramPtr chromatogram(new Chromatogram);
        chromatogram->index = sl->size();

        chromatogram->id = "SRM SIC Q1=123.45 Q3=234.56";
        chromatogram->setTimeIntensityPairs(vector<TimeIntensityPair>(42), UO_second, MS_number_of_detector_counts);
        chromatogram->set(MS_selected_reaction_monitoring_chromatogram);
        chromatogram->precursor.isolationWindow.set(MS_isolation_window_target_m_z, 123.45, MS_m_z);
        chromatogram->precursor.activation.set(MS_CID);
        chromatogram->product.isolationWindow.set(MS_isolation_window_target_m_z, 234.56, MS_m_z);

        sl->chromatograms.push_back(chromatogram);
    }

    {
        ChromatogramPtr chromatogram(new Chromatogram);
        chromatogram->index = sl->size();

        chromatogram->id = "SIM SIC Q1=123.45";
        chromatogram->setTimeIntensityPairs(vector<TimeIntensityPair>(42), UO_second, MS_number_of_detector_counts);
        chromatogram->set(MS_selected_ion_monitoring_chromatogram);
        chromatogram->precursor.isolationWindow.set(MS_isolation_window_target_m_z, 123.45, MS_m_z);

        sl->chromatograms.push_back(chromatogram);
    }

    if (os_)
    {
        *os_ << "original chromatogram list:\n";
        printChromatogramList(*sl, *os_); 
        *os_ << endl;
    }

    return sl;
}


struct EvenPredicate : public ChromatogramList_Filter::Predicate
{
    virtual tribool accept(const ChromatogramIdentity& chromatogramIdentity) const
    {
        return chromatogramIdentity.index%2 == 0;
    }
};


void testEven(ChromatogramListPtr sl)
{
    if (os_) *os_ << "testEven:\n";

    ChromatogramList_Filter filter(sl, EvenPredicate());

    if (os_) 
    {
        printChromatogramList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(2, filter.size());
    unit_assert_operator_equal(0, filter.chromatogramIdentity(0).index);
    unit_assert_operator_equal("TIC", filter.chromatogramIdentity(0).id);
    unit_assert_operator_equal(1, filter.chromatogramIdentity(1).index);
    unit_assert_operator_equal("SIM SIC Q1=123.45", filter.chromatogramIdentity(1).id);

}


struct SelectedIndexPredicate : public ChromatogramList_Filter::Predicate
{
    mutable bool pastMaxIndex;

    SelectedIndexPredicate() : pastMaxIndex(false) {}

    virtual tribool accept(const ChromatogramIdentity& chromatogramIdentity) const
    {
        if (chromatogramIdentity.index>2) pastMaxIndex = true;

        return (chromatogramIdentity.index==1 ||
                chromatogramIdentity.index==2);
    }

    virtual bool done() const
    {
        return pastMaxIndex;
    }
};


void testSelectedIndices(ChromatogramListPtr sl)
{
    if (os_) *os_ << "testSelectedIndices:\n";

    ChromatogramList_Filter filter(sl, SelectedIndexPredicate());
    
    if (os_) 
    {
        printChromatogramList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(2, filter.size());
    unit_assert_operator_equal("SRM SIC Q1=123.45 Q3=234.56", filter.chromatogramIdentity(0).id);
    unit_assert_operator_equal("SIM SIC Q1=123.45", filter.chromatogramIdentity(1).id);
}


void testIndexSet(ChromatogramListPtr sl)
{
    if (os_) *os_ << "testIndexSet:\n";

    IntegerSet indexSet;
    indexSet.insert(1);
    indexSet.insert(2);

    ChromatogramList_Filter filter(sl, ChromatogramList_FilterPredicate_IndexSet(indexSet));
    
    if (os_) 
    {
        printChromatogramList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(2, filter.size());
    unit_assert_operator_equal("SRM SIC Q1=123.45 Q3=234.56", filter.chromatogramIdentity(0).id);
    unit_assert_operator_equal("SIM SIC Q1=123.45", filter.chromatogramIdentity(1).id);
}


void test()
{
    ChromatogramListPtr sl = createChromatogramList();
    testEven(sl);
    testSelectedIndices(sl);
    testIndexSet(sl);
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


