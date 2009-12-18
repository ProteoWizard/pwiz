//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
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


#ifndef _ION_HPP_
#define _ION_HPP_


namespace pwiz {
namespace chemistry {


namespace Ion
{
    const double protonMass_ = 1.00727647;

    inline double mz(double neutralMass, int charge)
    {
        return neutralMass/charge + protonMass_;
    } 

    inline double neutralMass(double mz, int charge)
    {
        return (mz - protonMass_)*charge; 
    }

    inline double ionMass(double neutralMass, int charge)
    {
        return neutralMass + protonMass_*charge;
    }
}


} // namespace chemistry 
} // namespace pwiz


#endif // _ION_HPP_

