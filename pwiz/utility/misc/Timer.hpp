//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#ifndef _TIMER_H_
#define _TIMER_H_


#include "pwiz/utility/misc/Export.hpp"
#include <ctime>
#include <iostream>


namespace pwiz {
namespace util {


class PWIZ_API_DECL Timer
{
    public:
    Timer() {time(&start_);}
    double elapsed() const {time_t now; time(&now); return difftime(now, start_);}

    private:
    time_t start_;
    time_t finish_;
};


} // namespace util
} // namespace pwiz


#endif //_TIMER_H_
