//
// $Id$
//
//
// Original author: William French <william.r.frenchwr .@. vanderbilt.edu>
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


#ifndef _SPECTRUMWORKERTHREADS_HPP_
#define _SPECTRUMWORKERTHREADS_HPP_

#include "pwiz/data/msdata/MSData.hpp"
#include <boost/smart_ptr.hpp>


namespace pwiz {
namespace msdata {


class SpectrumWorkerThreads
{
    public:

    SpectrumWorkerThreads(const SpectrumList& sl);
    ~SpectrumWorkerThreads();
    SpectrumPtr processBatch(size_t index, bool getBinaryData = true);

    private:
    class Impl;
    boost::scoped_ptr<Impl> impl_;
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMWORKERTHREADS_HPP_

