//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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


namespace pwiz {
namespace CLI {
namespace util {


namespace b = pwiz::util;
using namespace System::Runtime::InteropServices;
using namespace System::Collections::Generic;


IterationListener::IterationListener()
{
}


IterationListener::UpdateMessage::UpdateMessage(int iterationIndex,
                                                int iterationCount,
                                                System::String^ message)
{
    this->iterationIndex = iterationIndex;
    this->iterationCount = iterationCount;
    this->message = message;
}


struct IterationListenerForwarder : public b::IterationListener
{
    typedef pwiz::CLI::util::IterationListener::Status (__stdcall *IterationListenerCallback)(pwiz::CLI::util::IterationListener::UpdateMessage^);
    IterationListenerCallback managedFunctionPtr;

    IterationListenerForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<IterationListenerCallback>(managedFunctionPtr))
    {}

    virtual Status update(const UpdateMessage& updateMessage)
    {
        if (managedFunctionPtr != NULL)
        {
            pwiz::CLI::util::IterationListener::UpdateMessage^ managedUpdateMessage =
                gcnew pwiz::CLI::util::IterationListener::UpdateMessage(updateMessage.iterationIndex,
                                                                        updateMessage.iterationCount,
                                                                        ToSystemString(updateMessage.message));
            return (Status) managedFunctionPtr(managedUpdateMessage);
        }

        return Status_Ok;
    }
};


IterationListenerRegistry::IterationListenerRegistry()
{
    base_ = new b::IterationListenerRegistry();
    _listeners = gcnew Dictionary<IterationListener^, KeyValuePair<IterationListenerUpdate^, System::IntPtr> >();
}


void IterationListenerRegistry::addListener(IterationListener^ listener, System::UInt32 iterationPeriod)
{
    IterationListenerUpdate^ handler = gcnew IterationListenerUpdate(listener, &IterationListener::update);
    IterationListenerPtr forwarder(new IterationListenerForwarder(Marshal::GetFunctionPointerForDelegate(handler).ToPointer()));
    _listeners->Add(listener, KeyValuePair<IterationListenerUpdate^, System::IntPtr>(handler, System::IntPtr(&forwarder)));
    base().addListener(forwarder, (size_t) iterationPeriod);
}


void IterationListenerRegistry::addListenerWithTimer(IterationListener^ listener, double timePeriod)
{
    IterationListenerUpdate^ handler = gcnew IterationListenerUpdate(listener, &IterationListener::update);
    IterationListenerPtr forwarder(new IterationListenerForwarder(Marshal::GetFunctionPointerForDelegate(handler).ToPointer()));
    _listeners->Add(listener, KeyValuePair<IterationListenerUpdate^, System::IntPtr>(handler, System::IntPtr(&forwarder)));
    base().addListenerWithTimer(forwarder, timePeriod);
}


void IterationListenerRegistry::removeListener(IterationListener^ listener)
{
    base().removeListener(*static_cast<b::IterationListenerPtr*>(_listeners[listener].Value.ToPointer()));
    _listeners->Remove(listener);
}


IterationListener::Status IterationListenerRegistry::broadcastUpdateMessage(IterationListener::UpdateMessage^ updateMessage)
{
    std::string message = ToStdString(updateMessage->message);
    b::IterationListener::UpdateMessage nativeUpdateMessage(updateMessage->iterationIndex,
                                                            updateMessage->iterationCount,
                                                            message);
    return (IterationListener::Status) base().broadcastUpdateMessage(nativeUpdateMessage);
}


} // namespace util
} // namespace CLI
} // namespace pwiz
