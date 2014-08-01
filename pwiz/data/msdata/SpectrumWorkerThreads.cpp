//
// $Id: SpectrumList_MGF.cpp 4922 2013-09-05 22:33:08Z pcbrefugee $
//
//
// Original author: William French <william.r.french .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#define PWIZ_SOURCE

#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/data/vendor_readers/Bruker/SpectrumList_Bruker.hpp"
#include <Windows.h>

namespace pwiz {
namespace msdata {

SpectrumWorkerThreads::SpectrumWorkerThreads(SpectrumListPtr listOfSpectra)
    : sl(listOfSpectra), useMultiThreading(true), keepWorking(true), spectraReady(false), 
      workersFinished(false), finalSpectrumProcessed(false), consumerWaitCnt(0)
{
    detail::SpectrumList_Bruker* bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*sl);
    if (!bruker)
    {
        SpectrumListWrapper* wrapper = dynamic_cast<SpectrumListWrapper*>(&*sl);
        if (wrapper)
            bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*wrapper->innermost());
    }
    if (bruker) useMultiThreading = false; // Bruker library is not thread-friendly
    nThreads = getNumProcessors();
    nSpectra = sl->size();
    alreadyProcessed.resize(nSpectra,false);
    if ( nSpectra > 0 && useMultiThreading )
    {
        for (int i = 0; i < nThreads; ++i)
            workerThreads.push_back(workerThreadGroup.create_thread( boost::bind(&SpectrumWorkerThreads::consume, this) ));
    }
;}

bool sortByIdentity(SpectrumPtr i, SpectrumPtr j) { return (i->index < j->index); }

SpectrumPtr SpectrumWorkerThreads::processBatch(int start_id)
{

    if ( useMultiThreading )
    {

        if ( !alreadyProcessed[start_id] )
        {

            processedSpectra.clear();
            processedSpectraCnt = 0;

            int iend = start_id + nThreads > nSpectra ? nSpectra : start_id + nThreads;
            for (int i=start_id; i < iend; ++i)
                indexQueue.push(i);

            workersFinished = false;
            spectraReady = true;
            boost::unique_lock<boost::mutex> lock2(m_mutex2);
            m_cond2.notify_all();
            lock2.unlock();
        
            boost::unique_lock<boost::mutex> lock3(m_mutex3);
            while ( !workersFinished )
            {
                m_cond3.wait(lock3);
            }
            lock3.unlock();

            sort(processedSpectra.begin(),processedSpectra.end(),sortByIdentity);
        }

        return processedSpectra[processedSpectraCnt++];

    }

    else
        return sl->spectrum(start_id,true);

}

int SpectrumWorkerThreads::getNumProcessors()
{
    return boost::thread::hardware_concurrency();
}

int SpectrumWorkerThreads::getSpectrumIndex()
{
    boost::lock_guard<boost::mutex> lock(m_queueMutex);
    if ( !indexQueue.empty() )
    {
        int spectrumID = indexQueue.front();
        indexQueue.pop();
        return spectrumID;
    }
    return -1;
}

void SpectrumWorkerThreads::pushBackSpectrum(SpectrumPtr s,int id)
{
    boost::lock_guard<boost::mutex> lock(m_queueMutex);
    processedSpectra.push_back(s);
    alreadyProcessed[id] = true;
}

void SpectrumWorkerThreads::barrier(int id)
{
    boost::unique_lock<boost::mutex> lock1(m_mutex1);
    passBarrier = false;
    if ( id == nSpectra-1 ) finalSpectrumProcessed = true; // need this b/c final thread that enters may not have this id!

    if ( ++consumerWaitCnt == nThreads )
    {
        if ( finalSpectrumProcessed ) workComplete();
        passBarrier = true;
        if ( consumerWaitCnt > 1 ) m_cond1.notify_all(); // if consumerWaitCnt == 1, there are no threads to notify
        spectraReady = false;
        boost::unique_lock<boost::mutex> lock3(m_mutex3);
        workersFinished = true;
        m_cond3.notify_one(); // wake up the producer
        lock3.unlock();
    }
    else
    {
        while ( !passBarrier )
        {
            m_cond1.wait(lock1);
        }
    }
    consumerWaitCnt = 0;
}

void SpectrumWorkerThreads::consume()
{
    while (keepWorking) 
    {

        boost::unique_lock<boost::mutex> lock2(m_mutex2);
        while ( !spectraReady )
        {
            m_cond2.wait(lock2);
        }
        lock2.unlock();

        int spectrumID = getSpectrumIndex(); // blocking
        if ( spectrumID != -1 )
        {
            SpectrumPtr s = sl->spectrum(spectrumID,true);
            pushBackSpectrum(s,spectrumID); // blocking
        }
        barrier(spectrumID); // threads rendevous here. final worker thread wakes other threads and producer, and resets boolean variables.

    }
}


} // namespace msdata
} // namespace pwiz
