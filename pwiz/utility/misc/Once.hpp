//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#ifndef _ONCE_HPP_
#define _ONCE_HPP_

#include <boost/thread/once.hpp>


namespace pwiz {
namespace util {


// since the call_once implementation may be defined as an aggregate,
// this proxy class can be used for initializing per-instance once_flags
struct once_flag_proxy
{
    boost::once_flag flag;
};

/// this proxy class can be used for initializing per-instance once_flag_proxy(s)
static once_flag_proxy init_once_flag_proxy = {BOOST_ONCE_INIT};


} // namespace util
} // namespace pwiz

#endif // _ONCE_HPP_
