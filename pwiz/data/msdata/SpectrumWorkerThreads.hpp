//
// $Id: SpectrumList_MGF.hpp 1188 2009-08-14 17:19:55Z chambm $
//
//
// Original author: William French <william.r.frenchwr .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMWORKERTHREADS_HPP_
#define _SPECTRUMWORKERTHREADS_HPP_

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/msdata.hpp"
#include <boost/thread.hpp>
#include <queue>

namespace pwiz {
namespace msdata {


class SpectrumWorkerThreads
{

    public:

        SpectrumWorkerThreads(SpectrumListPtr); 
        SpectrumPtr processBatch(int);

    private:

        int getNumProcessors();
        void consume();
        void produce();
        int getSpectrumIndex();
        void pushBackSpectrum(SpectrumPtr,int);
        void barrier(int);
        void workComplete() { keepWorking = false; };

        SpectrumListPtr sl;
        int nSpectra;
        int nThreads;
        int processedSpectraCnt;
        std::vector<SpectrumPtr> processedSpectra;
        std::vector<bool> alreadyProcessed;

        bool useMultiThreading;
        bool keepWorking;
        bool spectraReady;
        bool workersFinished;
        bool passBarrier;
        bool finalSpectrumProcessed;
        int consumerWaitCnt;
        std::queue<int> indexQueue;

        boost::mutex m_mutex1;
        boost::mutex m_mutex2;
        boost::mutex m_mutex3;
        boost::mutex m_queueMutex;
        boost::mutex m_listMutex;
        boost::condition_variable m_cond1;
        boost::condition_variable m_cond2;
        boost::condition_variable m_cond3;

        boost::thread_group workerThreadGroup;
        std::vector<boost::thread*> workerThreads;

};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMWORKERTHREADS_HPP_

