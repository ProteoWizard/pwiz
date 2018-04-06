//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
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


#define PWIZ_SOURCE


#include "COMInitializer.hpp"
#include "ObjBase.h" // basic COM declarations
#include "boost/utility/thread_specific_singleton.hpp"
#include "pwiz/utility/misc/Singleton.hpp"


namespace pwiz {
namespace util {


class COMInitializer::Impl : public boost::thread_specific_singleton<COMInitializer::Impl>
{
    public:
    Impl(boost::restricted)
    : refCount_(0)
    {
    }

    ~Impl()
    {
        refCount_ = 1;
        uninitialize();
    }

    inline bool initialize()
    {   
        if (!refCount_)
            CoInitialize(NULL);
        ++refCount_;
        return refCount_ == 1;
    }

    inline bool uninitialize()
    {
        if (refCount_)
            --refCount_;
        if (!refCount_)
            CoUninitialize();
        return refCount_ == 0;
    }

    private:
    int refCount_;
};


PWIZ_API_DECL bool COMInitializer::initialize()
{
    return Impl::instance->initialize();
}

PWIZ_API_DECL bool COMInitializer::uninitialize()
{
    return Impl::instance->uninitialize();
}


} // namespace util
} // namespace pwiz
