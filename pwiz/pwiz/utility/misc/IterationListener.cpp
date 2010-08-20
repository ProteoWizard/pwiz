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


#define PWIZ_SOURCE

#include "IterationListener.hpp" 
#include "pwiz/utility/misc/Std.hpp"
#include <ctime>


namespace pwiz {
namespace util {


//
// IterationListenerRegistry::Impl
//


class IterationListenerRegistry::Impl
{
    public:

    void addListener(IterationListener& listener, size_t iterationPeriod)
    {
        listeners_.push_back(&listener);
        callbackInfo_[&listener] = iterationPeriod;
    }

    void addListenerWithTimer(IterationListener& listener, double timePeriod)
    {
        listeners_.push_back(&listener);
        callbackInfo_[&listener] = CallbackInfo(timePeriod, true);
    }

    void removeListener(IterationListener& listener)
    {
        listeners_.erase(remove(listeners_.begin(), listeners_.end(), &listener)); 
        callbackInfo_.erase(&listener);
    }

    IterationListener::Status broadcastUpdateMessage(
        const IterationListener::UpdateMessage& updateMessage) const
    {
        IterationListener::Status result = IterationListener::Status_Ok;

        for (Listeners::const_iterator it=listeners_.begin(), end=listeners_.end(); it!=end; ++it)
        {
            time_t now;
            time(&now);

            bool shouldUpdate = 
                updateMessage.iterationIndex == 0 ||
                updateMessage.iterationIndex+1 >= updateMessage.iterationCount ||
                callbackInfo_[*it].periodType == CallbackInfo::PeriodType_Iteration &&
                    (updateMessage.iterationIndex+1) % callbackInfo_[*it].iterationPeriod == 0 ||
                callbackInfo_[*it].periodType == CallbackInfo::PeriodType_Time &&
                    difftime(now, callbackInfo_[*it].timestamp) >= callbackInfo_[*it].timePeriod;

            if (shouldUpdate)
            {
                IterationListener::Status status = (*it)->update(updateMessage);
                if (status == IterationListener::Status_Cancel) result = status;

                if (callbackInfo_[*it].periodType == CallbackInfo::PeriodType_Time)
                    callbackInfo_[*it].timestamp = now;
            }
        }

        return result;
    }

    private:

    typedef vector<IterationListener*> Listeners;
    Listeners listeners_;

    struct CallbackInfo
    {
        enum PeriodType {PeriodType_Iteration, PeriodType_Time};
        PeriodType periodType;

        size_t iterationPeriod;
        double timePeriod; // seconds

        time_t timestamp;

        CallbackInfo(size_t _iterationPeriod = 1)
        :   periodType(PeriodType_Iteration),
            iterationPeriod(_iterationPeriod),
            timePeriod(0)
        {}

        CallbackInfo(double _timePeriod, bool mustBeTrue)
        :   periodType(PeriodType_Time),
            iterationPeriod(0),
            timePeriod(_timePeriod)
        {
            if (mustBeTrue != true)
                throw runtime_error("[IterationListenerRegistry::CallbackInfo] Wrong constructor."); 
        }

    };

    mutable map<IterationListener*, CallbackInfo> callbackInfo_;
};


//
// IterationListenerRegistry
//


PWIZ_API_DECL IterationListenerRegistry::IterationListenerRegistry()
:   impl_(new Impl)
{}


PWIZ_API_DECL 
void IterationListenerRegistry::addListener(IterationListener& listener, 
                                            size_t iterationPeriod)
{
    impl_->addListener(listener, iterationPeriod);
}


PWIZ_API_DECL 
void IterationListenerRegistry::addListenerWithTimer(IterationListener& listener, 
                                                     double timePeriod)
{
    impl_->addListenerWithTimer(listener, timePeriod);
}


PWIZ_API_DECL void IterationListenerRegistry::removeListener(IterationListener& listener)
{
    impl_->removeListener(listener);
}


PWIZ_API_DECL IterationListener::Status 
IterationListenerRegistry::broadcastUpdateMessage(
    const IterationListener::UpdateMessage& updateMessage) const
{
    return impl_->broadcastUpdateMessage(updateMessage);
}


} // namespace util
} // namespace pwiz

