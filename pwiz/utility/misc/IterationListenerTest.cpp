//
// IterationListenerTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "IterationListener.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <string>


using namespace std;
using namespace pwiz::util;


ostream* os_ = 0;


class TestListener : public IterationListener
{
    public:
    
    TestListener(const string& name, size_t iterationPeriod)
    :   name_(name), iterationPeriod_(iterationPeriod), count_(0) 
    {}

    virtual size_t iterationPeriod() const {return iterationPeriod_;}
    
    virtual Status update(const UpdateMessage& updateMessage) 
    {
        if (os_) *os_ << "[" << name_ << "] " << updateMessage.iterationIndex << "/"
                      << updateMessage.iterationCount << endl;
        count_++;
        return Status_Ok;
    }

    size_t count() const {return count_;}
    
    private:
    string name_;
    size_t iterationPeriod_;
    size_t count_;
};


class CancelListener : public IterationListener
{
    public:
    
    CancelListener(size_t cancelIndex)
    :   cancelIndex_(cancelIndex)
    {}

    virtual Status update(const UpdateMessage& updateMessage) 
    {
        if (os_) *os_ << "[cancel] " << updateMessage.iterationIndex << "/"
                      << updateMessage.iterationCount << endl;

        return updateMessage.iterationIndex==cancelIndex_ ? Status_Cancel : Status_Ok;
    }
    
    private:
    size_t cancelIndex_;
};


void test()
{
    if (os_) *os_ << "test()\n";

    IterationListenerRegistry registry;

    TestListener test3("test3", 3);
    TestListener test4("test4", 4);
    TestListener test6("test6", 6);

    registry.addListener(test3);
    registry.addListener(test4);
    registry.addListener(test6);

    size_t iterationCount = 24;
    for (size_t i=0; i<iterationCount; i++)
        registry.broadcastUpdateMessage(IterationListener::UpdateMessage(i, iterationCount));
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(iterationCount, iterationCount));

    // validate

    unit_assert(test3.count() == 9);
    unit_assert(test4.count() == 7);
    unit_assert(test6.count() == 5);

    if (os_) *os_ << endl;
}


void testCancel()
{
    if (os_) *os_ << "testCancel()\n";

    IterationListenerRegistry registry;

    CancelListener cancelListener(12);
    TestListener test3("test3", 3);
    TestListener test4("test4", 4);
    TestListener test6("test6", 6);

    registry.addListener(cancelListener);
    registry.addListener(test3);
    registry.addListener(test4);
    registry.addListener(test6);

    // typical use of IterationListenerRegistry, with proper Status_Cancel handling

    bool canceled = false;
    
    size_t iterationCount = 24;
    for (size_t i=0; i<iterationCount; i++)
    {
        IterationListener::Status status = 
            registry.broadcastUpdateMessage(IterationListener::UpdateMessage(i, iterationCount));

        // handle Status_Cancel
        if (status == IterationListener::Status_Cancel) 
        {
            canceled = true;
            break;
        }
    }

    // implementations should send a final update on completion of the iteration

    if (!canceled)
        registry.broadcastUpdateMessage(IterationListener::UpdateMessage(iterationCount, iterationCount));

    // validate

    unit_assert(test3.count() == 5);
    unit_assert(test4.count() == 4);
    unit_assert(test6.count() == 3);

    if (os_) *os_ << endl;
}


class BadListener : public IterationListener
{
    public:
    
    virtual Status update(const UpdateMessage& updateMessage) 
    {
        throw runtime_error("bad");
    }
};


void testRemove()
{
    if (os_) *os_ << "testRemove()\n";

    IterationListenerRegistry registry;

    BadListener bad;
    TestListener test3("test3", 3);
    TestListener test4("test4", 4);

    registry.addListener(test3);
    registry.addListener(bad);
    registry.addListener(test4);

    // sanity check -- verify that broadcast throws if BadListener is in the registry    
    
    bool caught = false;

    try
    {
        registry.broadcastUpdateMessage(IterationListener::UpdateMessage(0,0));
    }
    catch (exception& e)
    {
        if (e.what() == string("bad")) caught = true;
    }

    unit_assert(caught);

    // remove BadListener -- broadcast will throw if not removed properly

    registry.removeListener(bad);
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(0,0));
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testCancel();
        testRemove();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


