//
// $Id$
//
//
// Original author: William French <william.r.french .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Demux.hpp"
#include "pwiz/analysis/demux/DemuxDataProcessingStrings.hpp"
#include "pwiz/utility/misc/mru_list.hpp"
#include <boost/thread.hpp>
#include <deque>


using std::deque;
using namespace pwiz::util;
using namespace pwiz::analysis;

namespace pwiz {
namespace msdata {

class SpectrumWorkerThreads::Impl
{
    public:

    Impl(const SpectrumList& sl, bool useWorkerThreads)
        : sl_(sl)
        , numThreads_(min(16u, boost::thread::hardware_concurrency()))
        , maxProcessedTaskCount_(numThreads_ * 4)
        , taskMRU_(maxProcessedTaskCount_)
    {
        InstrumentConfigurationPtr icPtr;
        if (sl.size() > 0)
        {
            SpectrumPtr s0 = sl.spectrum(0, false);
            if (s0->scanList.scans.size() > 0)
                icPtr = s0->scanList.scans[0].instrumentConfigurationPtr;
        }

        bool isBruker = icPtr.get() && icPtr->hasCVParamChild(MS_Bruker_Daltonics_instrument_model);
        bool isShimadzu = icPtr.get() && icPtr->hasCVParamChild(MS_Shimadzu_instrument_model);
        bool isThermoOnWine = icPtr.get() && icPtr->hasCVParamChild(MS_Thermo_Fisher_Scientific_instrument_model) && running_on_wine();

        bool isDemultiplexed = false;
        const boost::shared_ptr<const DataProcessing> dp = sl.dataProcessingPtr();
        if (dp)
        {
            BOOST_FOREACH(const ProcessingMethod& pm, dp->processingMethods)
            {
                if (!pm.hasCVParam(MS_data_processing)) continue;
                BOOST_FOREACH(const UserParam& up, pm.userParams)
                {
                    if (up.name.find(DemuxDataProcessingStrings::kDEMUX_NAME) != std::string::npos)
                    {
                        isDemultiplexed = true;
                    }
                }
                if (isDemultiplexed) break;
            }
        }

        useThreads_ = useWorkerThreads && !(isBruker || isShimadzu || isThermoOnWine || isDemultiplexed); // some libraries/platforms are not thread-friendly

        if (sl.size() > 0 && useThreads_)
        {
            // create one task per spectrum
            tasks_.resize(sl.size(), Task());

            // create and start worker threads
            for (size_t i = 0; i < numThreads_; ++i)
            {
                workers_.push_back(TaskWorker());
                workers_.back().start(this);
            }
        }
    }

    ~Impl()
    {
        BOOST_FOREACH(TaskWorker& worker, workers_)
            if (worker.thread)
            {
                worker.thread->interrupt();
                worker.thread->join();
            }
    }

    SpectrumPtr spectrum(size_t index, DetailLevel detailLevel)
    {
        if (!useThreads_)
            return sl_.spectrum(index, detailLevel);

        boost::unique_lock<boost::mutex> taskLock(taskMutex_);

        // if the task is already finished and has binary data if getBinaryData is true, return it as-is
        Task& task = tasks_[index];
        if (task.result && (detailLevel < DetailLevel_FullData || task.detailLevel == DetailLevel_FullData))
            return task.result;

        // otherwise, add this task and the numThreads following tasks to the queue (skipping the tasks that are already processed or being worked on)
        for (size_t i = index; taskQueue_.size() < numThreads_ && i < tasks_.size(); ++i)
        {
            Task& task = tasks_[i];

            // if the task result is already ready
            if (task.result)
            {
                // if it has binary data and getBinaryData is true, the task need not be queued
                if (task.detailLevel == DetailLevel_FullData || detailLevel < DetailLevel_FullData)
                    continue;

                // otherwise the current result is cleared
                task.result.reset();
            }
            // if the task is already being worked on and the existing task will get binary data or binary data isn't being requested, the task need not be requeued
            else if (task.worker != NULL && (task.detailLevel == DetailLevel_FullData || detailLevel < DetailLevel_FullData))
                continue;

            // if the task is already queued, set its getBinaryData variable to the logical OR of the current task and the current spectrum request
            if (!task.isQueued)
            {
                taskQueue_.push_back(i);
                task.isQueued = true;
            }
            if (detailLevel > task.detailLevel)
                task.detailLevel = detailLevel;
        }

        // wait for the result to be set
        while (!task.result)
        {
            // notify workers that tasks are available
            taskQueuedCondition_.notify_all();
            taskFinishedCondition_.wait_for(taskLock, boost::chrono::milliseconds(100));
        }

        return task.result;
    }

    private:

    struct TaskWorker
    {
        void start(Impl* instance)
        {
            if (!thread)
                thread.reset(new boost::thread(boost::bind(&SpectrumWorkerThreads::Impl::work, instance, this)));
        }

        shared_ptr<boost::thread> thread;
    };

    // each spectrum in the list is a task
    struct Task
    {
        Task() : worker(NULL), detailLevel(DetailLevel_InstantMetadata), isQueued(false) {}

        TaskWorker* worker; // the thread currently working on this task
        SpectrumPtr result; // the spectrum produced by this task
        DetailLevel detailLevel;
        bool isQueued; // true if the task is currently in the taskQueue
    };

    friend struct TaskWorker;

    // function executed by worker threads
    static void work(Impl* instance, TaskWorker* worker)
    {
        vector<Task>& tasks = instance->tasks_;
        TaskQueue& taskQueue = instance->taskQueue_;
        mru_list<size_t>& taskMRU = instance->taskMRU_;

        // loop until the main thread kills the worker threads; the condition_variable::wait() call is an interruption point
        try
        {
            boost::unique_lock<boost::mutex> taskLock(instance->taskMutex_, boost::defer_lock);

            while (true)
            {
                taskLock.lock();

                // wait for a new spectrum to be queued (while the task queue is either empty or the task at the end of the queue already has a worker assigned)
                while (instance->taskQueue_.empty())
                    instance->taskQueuedCondition_.wait(taskLock);

                // get the next queued Task
                TaskQueue::value_type queuedTask = taskQueue.front();
                taskQueue.pop_front();

                // set worker pointer on the Task
                size_t taskIndex = queuedTask;
                Task& task = tasks[taskIndex];
                DetailLevel detailLevel = task.detailLevel;
                task.worker = worker;
                task.isQueued = false;
                //cout << taskIndex << " " << task.worker << " " << getBinaryData << endl;
                // unlock taskLock
                taskLock.unlock();

                // get the spectrum
                SpectrumPtr result = instance->sl_.spectrum(taskIndex, detailLevel);

                // lock the taskLock
                taskLock.lock();

                // set the result on the Task, and set its worker to empty
                // if not getting binary data, check if another thread already finished this task which did get binary data
                if (detailLevel == DetailLevel_FullData || task.detailLevel < DetailLevel_FullData)
                {
                    task.result = result;
                    task.detailLevel = detailLevel;
                }
                task.worker = NULL;

                // notify the main thread that a task has finished
                instance->taskFinishedCondition_.notify_one();

                // add the task to the MRU; if doing so will push an old task off the MRU, then reset the oldest (LRU) task;
                // to know whether an old task was pushed off, keep a copy of the LRU item before adding the task
                boost::optional<size_t> lruToReset;
                if (taskMRU.size() == taskMRU.max_size())
                {
                    lruToReset = taskMRU.lru();
                }

                taskMRU.insert(taskIndex);

                // if the MRU list's LRU is different now, it means the LRU was popped and needs to be reset; if the popped LRU is the current task, don't reset it
                if (lruToReset.is_initialized() && lruToReset.get() != taskMRU.lru() && lruToReset.get() != taskIndex)
                {
                    tasks[lruToReset.get()].result.reset();
                }

                taskLock.unlock();
            }
        }
        catch (boost::thread_interrupted&)
        {
            // return
        }
        catch (exception& e)
        {
            // TODO: log this
            cerr << "[SpectrumWorkerThreads::work] error in thread: " << e.what() << endl;
        }
        catch (...)
        {
            cerr << "[SpectrumWorkerThreads::work] unknown exception in worker thread" << endl;
        }
    }

    const SpectrumList& sl_;
    bool useThreads_;
    size_t numThreads_;

    const size_t maxProcessedTaskCount_;
    vector<Task> tasks_;
    typedef deque<size_t> TaskQueue;
    TaskQueue taskQueue_;
    mru_list<size_t> taskMRU_;
    boost::mutex taskMutex_;
    boost::condition_variable taskQueuedCondition_, taskFinishedCondition_;

    vector<TaskWorker> workers_;
};


SpectrumWorkerThreads::SpectrumWorkerThreads(const SpectrumList& sl, bool useWorkerThreads) : impl_(new Impl(sl, useWorkerThreads)) {}

SpectrumWorkerThreads::~SpectrumWorkerThreads() {}

SpectrumPtr SpectrumWorkerThreads::processBatch(size_t index, DetailLevel detailLevel)
{
    return impl_->spectrum(index, detailLevel);
}


} // namespace msdata
} // namespace pwiz
