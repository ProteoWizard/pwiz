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

#ifndef _ITERATIONLISTENER_HPP_CLI_
#define _ITERATIONLISTENER_HPP_CLI_

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "SharedCLI.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace util {


public ref struct IterationEventArgs : System::EventArgs
{
    property System::String^ Message;
    property int IterationIndex;
    property int IterationCount;
    property bool Cancel;
};

public delegate void IterationEventHandler(System::Object^ sender, IterationEventArgs^ e);


struct IterationListenerForwarder : public pwiz::util::IterationListener
{
    typedef void (__stdcall *IterationListenerCallback)(System::Object^ sender, IterationEventArgs^ e);
    IterationListenerCallback managedFunctionPtr;

    IterationListenerForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<IterationListenerCallback>(managedFunctionPtr))
    {}

    virtual Status update(const UpdateMessage& updateMessage)
    {
        if (managedFunctionPtr != NULL)
        {
            IterationEventArgs^ args = gcnew IterationEventArgs();
            args->Message = ToSystemString(updateMessage.message);
            args->IterationIndex = updateMessage.iterationIndex;
            args->IterationCount = updateMessage.iterationCount;
            managedFunctionPtr(nullptr, args);
            return args->Cancel ? Status_Cancel : Status_Ok;
        }

        return Status_Ok;
    }
};


} // namespace util
} // namespace CLI
} // namespace pwiz


#endif // _ITERATIONLISTENER_HPP_CLI_
