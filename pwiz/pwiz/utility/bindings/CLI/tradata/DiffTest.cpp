//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
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

//#include "Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"


using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::tradata;
using System::Console;
typedef System::String string;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};

void testTraData()
{
	if (Log::writer != nullptr) Log::writer->Write("testTraData()\n");

    TraData a, b;

    Diff diff(a, b);
    unit_assert(!(bool)diff);

    a.cvs->Add(gcnew CV());
    b.softwareList->Add(gcnew Software("software"));

    Publication^ pub = gcnew Publication();
    pub->id = "PUBMED1";
    pub->set(CVID::UO_dalton, 123);
    a.publications->Add(pub);
    b.publications->Add(pub);

    diff.apply(a, b);
    if (Log::writer != nullptr) Log::writer->WriteLine((string^)diff);
    unit_assert((bool)diff);

    unit_assert(diff.a_b->cvs->Count == 1);
    unit_assert(diff.b_a->cvs->Count == 0);

    unit_assert(diff.a_b->softwareList->Count == 0);
    unit_assert(!diff.b_a->softwareList->Count == 0);

    unit_assert(diff.a_b->publications->Count == 0);
    unit_assert(diff.b_a->publications->Count == 0);
}


int main(int argc, char* argv[])
{
    try
    {
		if (argc>1 && !strcmp(argv[1],"-v")) Log::writer = Console::Out;
        testTraData();
        return 0;
    }
    catch (std::exception& e)
    {
        Console::Error->WriteLine("std::exception: " + gcnew string(e.what()));
    }
    catch (System::Exception^ e)
    {
        Console::Error->WriteLine("System.Exception: " + e->Message);
    }
    catch (...)
    {
        Console::Error->WriteLine("Caught unknown exception.\n");
    }
    
    return 1;
}

