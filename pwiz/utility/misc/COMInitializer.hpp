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


#ifndef COMINITIALIZER_HPP_
#define COMINITIALIZER_HPP_


#include "Export.hpp"


namespace pwiz {
namespace util {


/// Singleton used to initialize and uninitialize COM once per thread
class PWIZ_API_DECL COMInitializer
{
    public:

    /// If COM is not yet initialized on the calling thread, initializes COM and returns true
    /// If COM is already initialized on the calling thread, increments reference count and returns false
    static bool initialize();

    /// If COM is not initialized on the calling thread, does nothing and returns false
    /// If COM is initialized on the calling thread, decreases reference count:
    /// If reference count is 0, uninitializes COM and return true, otherwise returns false
    static bool uninitialize();

    private:
    class Impl;
};


} // namespace util
} // namespace pwiz


#endif // COMINITIALIZER_HPP_
