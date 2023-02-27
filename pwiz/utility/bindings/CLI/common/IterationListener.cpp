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
#include "pwiz/utility/misc/Stream.hpp"


namespace pwiz {
namespace CLI {
namespace util {

class functionbuf : public std::streambuf
{
private:
    typedef std::streambuf::traits_type traits_type;
    LogCallback logCallback;
    char buf[1024];
    std::wstring wbuf;

    int overflow(int c) {
        if (!traits_type::eq_int_type(c, traits_type::eof())) {
            *this->pptr() = traits_type::to_char_type(c);
            this->pbump(1);
        }
        return this->sync() ? traits_type::not_eof(c) : traits_type::eof();
    }
    int sync() {
        if (this->pbase() != this->pptr())
        {
            const size_t cSize = (this->pptr() - this->pbase()) + 1;
            size_t t;
            wbuf.resize(cSize, L'#');
            mbstowcs_s(&t, &wbuf[0], cSize, this->pbase(), cSize - 1);

            logCallback(wbuf.c_str());

            this->setp(this->pbase(), this->epptr());
        }
        return 0;
    }
public:
    functionbuf(LogCallback function)
        : logCallback(function), wbuf(2048, L'#') {
        this->setp(this->buf, this->buf + sizeof(this->buf) - 1);
    }
};

static std::unique_ptr<functionbuf> callback_on_cout_stream;
static std::unique_ptr<functionbuf> callback_on_cerr_stream;

void SetCoutCallback(LogCallback cb) {
    callback_on_cout_stream = std::make_unique<functionbuf>(cb);
    cout.rdbuf(callback_on_cout_stream.get()); // unqualified cout should be boost::nowide::cout
    cout.setf(ios::unitbuf);
}

void SetCerrCallback(LogCallback cb) {
    callback_on_cerr_stream = std::make_unique<functionbuf>(cb);
    cerr.rdbuf(callback_on_cerr_stream.get()); // unqualified cerr should be boost::nowide::cerr
    cerr.setf(ios::unitbuf);
}

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

IterationListenerRegistry::~IterationListenerRegistry()
{
    LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), true);
    SAFEDELETE(base_);

    for each (KeyValuePair<IterationListener^, KeyValuePair<IterationListenerUpdate^, System::IntPtr> > kvp in _listeners)
    {
        IterationListenerPtr* forwarder = static_cast<b::IterationListenerPtr*>(kvp.Value.Value.ToPointer());
        delete forwarder;
    }
    _listeners->Clear();
}


void IterationListenerRegistry::addListener(IterationListener^ listener, System::UInt32 iterationPeriod)
{
    IterationListenerUpdate^ handler = gcnew IterationListenerUpdate(listener, &IterationListener::update);
    IterationListenerPtr* forwarder = new IterationListenerPtr(new IterationListenerForwarder(Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer()));
    _listeners->Add(listener, KeyValuePair<IterationListenerUpdate^, System::IntPtr>(handler, System::IntPtr(forwarder)));
    base().addListener(*forwarder, (size_t) iterationPeriod);
}


void IterationListenerRegistry::addListenerWithTimer(IterationListener^ listener, double timePeriod)
{
    IterationListenerUpdate^ handler = gcnew IterationListenerUpdate(listener, &IterationListener::update);
    IterationListenerPtr* forwarder = new IterationListenerPtr(new IterationListenerForwarder(Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer()));
    _listeners->Add(listener, KeyValuePair<IterationListenerUpdate^, System::IntPtr>(handler, System::IntPtr(forwarder)));
    base().addListenerWithTimer(*forwarder, timePeriod);
}


void IterationListenerRegistry::removeListener(IterationListener^ listener)
{
    IterationListenerPtr* forwarder = static_cast<b::IterationListenerPtr*>(_listeners[listener].Value.ToPointer());
    base().removeListener(*forwarder);
    delete forwarder;
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
