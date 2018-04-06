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


#include "Chemistry.hpp"
#include <stdexcept>


namespace pwiz {
namespace chemistry {


namespace Ion
{
    /*!
     * Converts the m/z of an ion to a neutral mass.
     * @param[in] mz The m/z to convert.
     * @param[in] protonDelta The number of extra protons attached to the ion.
     * @param[in] electronDelta The number of extra electrons attached to the ion.
     * @param[in] neutronDelta The number of extra neutrons attached to the ion.
     * \pre protonDelta != electronDelta
     */
    inline double neutralMass(double mz, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        int charge = protonDelta - electronDelta;
        return charge == 0 ? throw std::invalid_argument("[Ion::neutralMass()] m/z with protonDelta=electronDelta is impossible")
                           : mz * charge - ((Proton * protonDelta) +
                                            (Electron * electronDelta) +
                                            (Neutron * neutronDelta));
    }

    /*!
     * Converts a neutral mass to an ionized mass.
     * @param[in] neutralMass The neutral mass to ionize.
     * @param[in] protonDelta The number of extra protons to attach to the ion.
     * @param[in] electronDelta The number of extra electrons to attach to the ion.
     * @param[in] neutronDelta The number of extra neutrons to attach to the ion.
     * \pre protonDelta != electronDelta
     */
    inline double ionMass(double neutralMass, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        return neutralMass + (Proton * protonDelta) +
                             (Electron * electronDelta) +
                             (Neutron * neutronDelta);
    }

    /*!
     * Converts a neutral mass to an m/z.
     * @param[in] neutralMass The neutral mass to ionize.
     * @param[in] protonDelta The number of extra protons to attach to the ion.
     * @param[in] electronDelta The number of extra electrons to attach to the ion.
     * @param[in] neutronDelta The number of extra neutrons to attach to the ion.
     * \pre protonDelta != electronDelta
     */
    inline double mz(double neutralMass, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        int z = protonDelta - electronDelta;
        double m = ionMass(neutralMass, protonDelta, electronDelta, neutronDelta);
        return z == 0 ? throw std::invalid_argument("[Ion::mz()] m/z with protonDelta=electronDelta is impossible")
                      : m / z;
    }
}


} // namespace chemistry 
} // namespace pwiz


#endif // _ION_HPP_

