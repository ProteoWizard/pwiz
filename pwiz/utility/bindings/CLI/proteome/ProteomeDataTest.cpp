//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


//#include "MSData.hpp"
#include "../common/unit.hpp"
#include <stdexcept>


using namespace System;
using namespace pwiz::CLI;
using namespace pwiz::CLI::proteome;
using namespace pwiz::CLI::util;


void testProteinListSimple()
{
    // fill in ProteinListSimple

    ProteinListSimple^ proteinListSimple = gcnew ProteinListSimple();

    Protein^ protein0 = gcnew Protein("IPI-1701", 0, "The final frontier!", "");
    Protein^ protein1 = gcnew Protein("SPROT|42", 1, "Life, the universe, and everything.", "");

    unit_assert(!protein0->empty());

    proteinListSimple->proteins->Add(protein0);
    proteinListSimple->proteins->Add(protein1);

    unit_assert(!proteinListSimple->empty());

    // let a ProteomeData object hold onto it as a ProteinList

    ProteomeData^ data = gcnew ProteomeData();
    data->proteinList = proteinListSimple;

    // test ProteinList interface

    // verify index()
    ProteinList^ proteinList = data->proteinList;
    unit_assert(proteinList->size() == 2);
    unit_assert(proteinList->find("IPI-1701") == 0);
    unit_assert(proteinList->find("SPROT|42") == 1);

    // verify findKeyword
    IndexList^ result = proteinList->findKeyword("final");
    unit_assert(result->Count == 1 && result[0] == 0);

    result = proteinList->findKeyword("the", false);
    unit_assert(result->Count == 2 && result[0] == 0 && result[1] == 1);

    result = proteinList->findKeyword("the");
    unit_assert(result->Count == 1 && result[0] == 1);

    result = proteinList->findKeyword("42");
    unit_assert(result->Count == 0);

    // verify protein 0
    Protein^ protein = proteinList->protein(0);
    unit_assert(protein->index == protein0->index);
    unit_assert(protein->id == protein0->id);

    // verify protein 1
    protein = proteinList->protein(1);
    unit_assert(protein->index == protein1->index);
    unit_assert(protein->id == protein1->id);
}


/*void testExample()
{
    MSData tiny;
    examples::initializeTiny(%tiny);

    ProteinList^ sl = tiny.run->proteinList;
    Protein^ s = sl->protein(1, false);
    unit_assert(s->precursors->Count == 1);
    Precursor^ p = s->precursors[0];
    IsolationWindow^ iw = p->isolationWindow;
    unit_assert_equal(s->precursors[0]->isolationWindow->cvParam(CVID::MS_isolation_window_lower_offset)->value, 0.5, 1e-6);
}*/


void testCatchAndForward()
{
    // cause some exceptions in native code and test that we can catch them as .NET exceptions
    ProteinListSimple^ sl = gcnew ProteinListSimple();
    unit_assert_throws_what(sl->protein(123), System::Exception, "[ProteinListSimple::protein()] Invalid index.");

    // TODO: where should we test catch-and-forward for other unbound classes
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        testProteinListSimple();
        //testExample();
        testCatchAndForward();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
