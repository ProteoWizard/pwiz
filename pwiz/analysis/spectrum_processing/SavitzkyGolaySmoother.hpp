//
// SavitzkyGolaySmoother.hpp
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _SAVITZKYGOLAYSMOOTHER_HPP_ 
#define _SAVITZKYGOLAYSMOOTHER_HPP_


#include "utility/misc/Export.hpp"
#include <vector>


template <typename T>
class SavitzkyGolaySmoother
{
    public:
    static std::vector<T> smooth_copy(const std::vector<T>& data)
    {
        if (data.size() < 9)
            return data;
        typename std::vector<T>::const_iterator start, stop;
        typename std::vector<T> smoothedData;
        for (start = data.begin(), stop = data.begin()+9;
            stop != data.end()-9;
            ++start, ++stop)
        {
            T sum = 59 * *(start+4) + 54 * (*(start+3) + *(start+5)) +
                    39 * (*(start+2) + *(start+6)) + 14 * (*(start+1) + *(start+7)) -
                    121 * (*start + *(start+8));
            smoothedData.push_back(sum / 231);
        }
        return smoothedData;
    }
};

#endif // _SAVITZKYGOLAYSMOOTHER_HPP_
