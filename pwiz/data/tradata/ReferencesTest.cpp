//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "References.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::tradata;


ostream* os_ = 0;



void testTransition()
{
    if (os_) *os_ << "testTransition()\n"; 

    Transition transition;
    transition.peptidePtr = PeptidePtr(new Peptide("pep1"));
    transition.peptidePtr->proteinPtrs.push_back(ProteinPtr(new Protein("prot1")));
    transition.compoundPtr = CompoundPtr(new Compound("cmp1"));

    TraData td;
    td.peptidePtrs.push_back(PeptidePtr(new Peptide("pep1")));
    td.peptidePtrs.back()->set(MS_theoretical_mass, 123);
    td.peptidePtrs.back()->proteinPtrs.push_back(ProteinPtr(new Protein("prot1")));
    td.peptidePtrs.back()->proteinPtrs.back()->sequence = "ABCD";

    td.compoundPtrs.push_back(CompoundPtr(new Compound("cmp1")));
    td.compoundPtrs.back()->retentionTimes.push_back(RetentionTime());
    td.compoundPtrs.back()->retentionTimes.back().set(MS_peak_intensity, 123);

    References::resolve(transition, td);

    unit_assert(transition.peptidePtr->cvParam(MS_theoretical_mass).value == "123");
    unit_assert(transition.peptidePtr->proteinPtrs.back().get());
    unit_assert(transition.peptidePtr->proteinPtrs.back()->sequence == "ABCD");
    unit_assert(!transition.compoundPtr->retentionTimes.empty());
    unit_assert(transition.compoundPtr->retentionTimes.back().cvParam(MS_peak_intensity).value == "123");
}


void testTraData()
{
    TraData td;
    td.proteinPtrs.push_back(ProteinPtr(new Protein("prot1")));
    td.proteinPtrs.back()->sequence = "ABCD";

    td.peptidePtrs.push_back(PeptidePtr(new Peptide("pep1")));
    td.peptidePtrs.back()->set(MS_theoretical_mass, 123);
    td.peptidePtrs.back()->proteinPtrs.push_back(ProteinPtr(new Protein("prot1")));

    td.compoundPtrs.push_back(CompoundPtr(new Compound("cmp1")));
    td.compoundPtrs.back()->retentionTimes.push_back(RetentionTime());
    td.compoundPtrs.back()->retentionTimes.back().set(MS_peak_intensity, 123);

    td.transitions.push_back(Transition());
    Transition& transition = td.transitions.back();
    transition.peptidePtr = PeptidePtr(new Peptide("pep1"));
    transition.peptidePtr->proteinPtrs.push_back(ProteinPtr(new Protein("prot1")));
    transition.compoundPtr = CompoundPtr(new Compound("cmp1"));

    References::resolve(td);

    unit_assert(transition.peptidePtr->cvParam(MS_theoretical_mass).value == "123");
    unit_assert(transition.peptidePtr->proteinPtrs.back().get());
    unit_assert(transition.peptidePtr->proteinPtrs.back()->sequence == "ABCD");
    unit_assert(!transition.compoundPtr->retentionTimes.empty());
    unit_assert(transition.compoundPtr->retentionTimes.back().cvParam(MS_peak_intensity).value == "123");
}


void test()
{
    testTransition();
    testTraData();
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
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

