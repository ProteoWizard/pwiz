//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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

#include "unit.hpp"
#include <stdexcept>


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using System::String;
using System::Collections::Generic::List;


public ref struct IterationListenerTest : IterationListener
{
    List<int>^ iterationIndexes;
    int iterationCount;
    String^ message;

    IterationListenerTest()
    {
        iterationIndexes = gcnew List<int>();
    }

    virtual Status update(UpdateMessage^ updateMessage) override
    {
        iterationIndexes->Add(updateMessage->iterationIndex);
        iterationCount = updateMessage->iterationCount;
        message = updateMessage->message;
        return Status::Ok;
    }
};


void test()
{
    IterationListenerTest^ iterationListenerTest = gcnew IterationListenerTest();

    IterationListenerRegistry^ ilr = gcnew IterationListenerRegistry();
    ilr->addListener(iterationListenerTest, 10);
    for (size_t i=0; i < 100; ++i)
        ilr->broadcastUpdateMessage(gcnew IterationListener::UpdateMessage(i, 100, "iteration"));

    // 0 9 19 29 39 49 59 69 79 89 99
    unit_assert(iterationListenerTest->iterationIndexes->Count == 11);
    unit_assert(iterationListenerTest->iterationIndexes[0] == 0);
    unit_assert(iterationListenerTest->iterationIndexes[10] == 99);
    unit_assert(iterationListenerTest->iterationCount == 100);
    unit_assert(iterationListenerTest->message == "iteration");
}


int main()
{
    try
    {
        test();
        return 0;
    }
    catch (std::exception& e)
    {
        System::Console::Error->WriteLine("Caught std::exception not converted to System::Exception: " + gcnew System::String(e.what()));
        return 1;
    }
    catch (System::Exception^ e)
    {
        System::Console::Error->WriteLine(e->Message);
        return 1;
    }
    catch (...)
    {
        System::Console::Error->WriteLine("Caught unknown exception.");
        return 1;
    }
}
