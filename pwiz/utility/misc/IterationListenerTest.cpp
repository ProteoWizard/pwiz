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


#include "Std.hpp"
#include "IterationListener.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <ctime>


using namespace pwiz::util;


ostream* os_ = 0;


class TestListener : public IterationListener
{
    public:
    
    TestListener(const string& name)
    :   name_(name), count_(0) 
    {}

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


// null deallactor to create shared_ptrs that do not delete when reset
void nullDeallocate(IterationListener* s)
{
    // do nothing
}

void test()
{
    if (os_) *os_ << "test()\n";

    IterationListenerRegistry registry;

    TestListener test3("test3");
    TestListener test4("test4");
    TestListener test5("test5");
    TestListener test6("test6");

    registry.addListener(IterationListenerPtr(&test3, nullDeallocate), 3);
    registry.addListener(IterationListenerPtr(&test4, nullDeallocate), 4);
    registry.addListener(IterationListenerPtr(&test5, nullDeallocate), 5);
    registry.addListener(IterationListenerPtr(&test6, nullDeallocate), 6);

    size_t iterationCount = 24;
    for (size_t i=0; i<iterationCount; i++)
        registry.broadcastUpdateMessage(IterationListener::UpdateMessage(i, iterationCount));

    // repeating last iteration should not create new messages
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(iterationCount - 1, iterationCount));
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(iterationCount - 1, iterationCount));
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(iterationCount - 1, iterationCount));

    // validate

    unit_assert_operator_equal(9, test3.count()); // 0 2 5 8 11 14 17 20 23
    unit_assert_operator_equal(7, test4.count());
    unit_assert_operator_equal(6, test5.count());
    unit_assert_operator_equal(5, test6.count());

    if (os_) *os_ << endl;
}


void testCancel()
{
    if (os_) *os_ << "testCancel()\n";

    IterationListenerRegistry registry;

    CancelListener cancelListener(12);
    TestListener test3("test3");
    TestListener test4("test4");
    TestListener test6("test6");

    registry.addListener(IterationListenerPtr(&cancelListener, nullDeallocate), 1);
    registry.addListener(IterationListenerPtr(&test3, nullDeallocate), 3);
    registry.addListener(IterationListenerPtr(&test4, nullDeallocate), 4);
    registry.addListener(IterationListenerPtr(&test6, nullDeallocate), 5);

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
    TestListener test3("test3");
    TestListener test4("test4");

    IterationListenerPtr badPtr(&bad, nullDeallocate);

    registry.addListener(IterationListenerPtr(&test3, nullDeallocate), 3);
    registry.addListener(badPtr, 1);
    registry.addListener(IterationListenerPtr(&test4, nullDeallocate), 4);

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

    registry.removeListener(badPtr);
    registry.broadcastUpdateMessage(IterationListener::UpdateMessage(0,0));

    if (os_) *os_ << endl;
}


void testTime()
{
    if (os_) *os_ << "testTime()\n";

    IterationListenerRegistry registry;

    TestListener test_iteration("test_iteration");
    TestListener test_time("test_time");

    registry.addListener(IterationListenerPtr(&test_iteration, nullDeallocate), 1000000);
    registry.addListenerWithTimer(IterationListenerPtr(&test_time, nullDeallocate), 1.0); 

    time_t start;
    time(&start);

    const double iterationDuration = 5.0;
    for (int i=0; ; i++) 
    {
        time_t now;
        time(&now);
        if (difftime(now, start) > iterationDuration) break;

        registry.broadcastUpdateMessage(IterationListener::UpdateMessage(i,0));
    }

    if (os_) *os_ << endl;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testCancel();
        testRemove();
        testTime();
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


