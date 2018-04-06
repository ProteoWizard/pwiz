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


#ifndef _ITERATIONLISTENER_HPP_
#define _ITERATIONLISTENER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"
#include <string>


namespace pwiz {
namespace util {


/// interface to be implemented by clients who want progress callbacks
class PWIZ_API_DECL IterationListener
{
    public:

    enum Status {Status_Ok, Status_Cancel};
    static std::string no_message;

    struct UpdateMessage
    {
        size_t iterationIndex;
        size_t iterationCount; // 0 == unknown
        const std::string& message;

        UpdateMessage(size_t index, size_t count, const std::string& message = no_message)
        :   iterationIndex(index), iterationCount(count), message(message)
        {}
    };

    virtual Status update(const UpdateMessage& updateMessage) {return Status_Ok;}

    virtual ~IterationListener(){}
};

typedef boost::shared_ptr<IterationListener> IterationListenerPtr;


/// handles registration of IterationListeners and broadcast of update messages
class PWIZ_API_DECL IterationListenerRegistry
{
    public:

    IterationListenerRegistry();
    void addListener(const IterationListenerPtr& listener, size_t iterationPeriod);
    void addListenerWithTimer(const IterationListenerPtr& listener, double timePeriod); // seconds
    void removeListener(const IterationListenerPtr& listener);

    IterationListener::Status broadcastUpdateMessage(
        const IterationListener::UpdateMessage& updateMessage) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    IterationListenerRegistry(IterationListenerRegistry&);
    IterationListenerRegistry& operator=(IterationListenerRegistry&);
};


} // namespace util
} // namespace pwiz


#endif // _ITERATIONLISTENER_HPP_

