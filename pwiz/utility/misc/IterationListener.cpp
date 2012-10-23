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

#include "Std.hpp"
#include "DateTime.hpp"
#include "IterationListener.hpp"


namespace pwiz {
namespace util {


//
// IterationListenerRegistry::Impl
//


class IterationListenerRegistry::Impl
{
    public:

    void addListener(const IterationListenerPtr& listener, size_t iterationPeriod)
    {
        listeners_[listener] = iterationPeriod;
    }

    void addListenerWithTimer(const IterationListenerPtr& listener, double timePeriod)
    {
        listeners_[listener] = CallbackInfo(timePeriod, true);
    }

    void removeListener(const IterationListenerPtr& listener)
    {
        listeners_.erase(listener);
    }

    IterationListener::Status broadcastUpdateMessage(
        const IterationListener::UpdateMessage& updateMessage) const
    {
        IterationListener::Status result = IterationListener::Status_Ok;

        bpt::ptime now = bpt::microsec_clock::local_time();

        for (Listeners::const_iterator itr = listeners_.begin(); itr != listeners_.end(); ++itr)
        {
            const IterationListenerPtr& listener = itr->first;
            const CallbackInfo& callbackInfo = itr->second;
            CallbackInfo::PeriodType periodType = callbackInfo.periodType;
            bpt::time_duration timeElapsed = now - callbackInfo.timestamp;

            bool shouldUpdate =
                updateMessage.iterationIndex == 0 ||
                (updateMessage.iterationCount > 0 && updateMessage.iterationIndex+1 >= updateMessage.iterationCount) ||
                (periodType == CallbackInfo::PeriodType_Iteration && (updateMessage.iterationIndex+1) % callbackInfo.iterationPeriod == 0) ||
                (periodType == CallbackInfo::PeriodType_Time && timeElapsed.total_milliseconds()/1000.0 >= callbackInfo.timePeriod);

            if (shouldUpdate)
            {
                IterationListener::Status status = listener->update(updateMessage);
                if (status == IterationListener::Status_Cancel) result = status;

                if (periodType == CallbackInfo::PeriodType_Time)
                    callbackInfo.timestamp = now;
            }
        }

        return result;
    }

    private:

    struct CallbackInfo
    {
        enum PeriodType {PeriodType_Iteration, PeriodType_Time};
        PeriodType periodType;

        size_t iterationPeriod;
        double timePeriod; // seconds

        mutable bpt::ptime timestamp;

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
    
    typedef map<IterationListenerPtr, CallbackInfo> Listeners;
    mutable Listeners listeners_;
};


//
// IterationListenerRegistry
//


PWIZ_API_DECL IterationListenerRegistry::IterationListenerRegistry()
:   impl_(new Impl)
{}


PWIZ_API_DECL 
void IterationListenerRegistry::addListener(const IterationListenerPtr& listener, 
                                            size_t iterationPeriod)
{
    impl_->addListener(listener, iterationPeriod);
}


PWIZ_API_DECL 
void IterationListenerRegistry::addListenerWithTimer(const IterationListenerPtr& listener, 
                                                     double timePeriod)
{
    impl_->addListenerWithTimer(listener, timePeriod);
}


PWIZ_API_DECL void IterationListenerRegistry::removeListener(const IterationListenerPtr& listener)
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

