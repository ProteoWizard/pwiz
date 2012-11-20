//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2012 Vanderbilt University - Nashville, TN 37232
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


#include "ProteinList_Filter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/assign.hpp>


using namespace pwiz;
using namespace pwiz::proteome;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace boost::assign;
using boost::logic::tribool;


ostream* os_ = 0;


void printProteinList(const ProteinList& pl, ostream& os)
{
    os << "size: " << pl.size() << endl;

    for (size_t i=0, end=pl.size(); i<end; i++)
    {
        ProteinPtr protein = pl.protein(i, false);
        os << protein->index << " " 
           << protein->id << " "
           << endl;
    }
}


ProteinListPtr createProteinList()
{
    shared_ptr<ProteinListSimple> pl(new ProteinListSimple);

    for (size_t i=0; i<10; ++i)
    {
        ProteinPtr protein(new Protein("Pro" + lexical_cast<string>(i+1), i, "", string(16, 'A'+i)));
        pl->proteins.push_back(protein);
    }

    if (os_)
    {
        *os_ << "original protein list:\n";
        printProteinList(*pl, *os_); 
        *os_ << endl;
    }

    return pl;
}


struct SelectedIndexPredicate : public ProteinList_Filter::Predicate
{
    mutable bool pastMaxIndex;

    SelectedIndexPredicate() : pastMaxIndex(false) {}

    virtual tribool accept(const Protein& protein) const
    {
        if (protein.index>5) pastMaxIndex = true;

        return (protein.index==1 ||
                protein.index==3 ||
                protein.index==5);
    }

    virtual bool done() const
    {
        return pastMaxIndex;
    }
};


void testSelectedIndices(ProteinListPtr pl)
{
    if (os_) *os_ << "testSelectedIndices:\n";

    ProteinList_Filter filter(pl, SelectedIndexPredicate());
    
    if (os_) 
    {
        printProteinList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(3, filter.size());
    unit_assert_operator_equal("Pro2", filter.protein(0)->id);
    unit_assert_operator_equal("Pro4", filter.protein(1)->id);
    unit_assert_operator_equal("Pro6", filter.protein(2)->id);
}


void testIndexSet(ProteinListPtr pl)
{
    if (os_) *os_ << "testIndexSet:\n";

    IntegerSet indexSet;
    indexSet.insert(3,5);
    indexSet.insert(7);
    indexSet.insert(9);

    ProteinList_Filter filter(pl, ProteinList_FilterPredicate_IndexSet(indexSet));

    if (os_) 
    {
        printProteinList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(5, filter.size());
    unit_assert_operator_equal("Pro4", filter.protein(0)->id);
    unit_assert_operator_equal("Pro5", filter.protein(1)->id);
    unit_assert_operator_equal("Pro6", filter.protein(2)->id);
    unit_assert_operator_equal("Pro8", filter.protein(3)->id);
    unit_assert_operator_equal("Pro10", filter.protein(4)->id);
}


void testIdSet(ProteinListPtr pl)
{
    if (os_) *os_ << "testIdSet:\n";

    set<string> idSet;
    idSet += "Pro2", "Pro3", "Pro4", "Pro7";

    ProteinList_Filter filter(pl, ProteinList_FilterPredicate_IdSet(idSet));
    
    if (os_) 
    {
        printProteinList(filter, *os_);
        *os_ << endl;
    }

    unit_assert_operator_equal(4, filter.size());
    unit_assert_operator_equal("Pro2", filter.protein(0)->id);
    unit_assert_operator_equal("Pro3", filter.protein(1)->id);
    unit_assert_operator_equal("Pro4", filter.protein(2)->id);
    unit_assert_operator_equal("Pro7", filter.protein(3)->id);
}


void test()
{
    ProteinListPtr pl = createProteinList();
    testSelectedIndices(pl);
    testIndexSet(pl);
    testIdSet(pl);
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


