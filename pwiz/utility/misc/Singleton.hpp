//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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

#ifndef _SINGLETON_HPP_
#define _SINGLETON_HPP_

// Include this file in any file that creates a singleton.
#include <boost/utility/singleton.hpp>

namespace
{
    struct Destroyer {~Destroyer() {boost::destroy_singletons();}};
    Destroyer destroyer;
}

#endif // _SINGLETON_HPP_
