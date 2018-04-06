//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _ISOTOPETABLE_HPP_
#define _ISOTOPETABLE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "Chemistry.hpp"


namespace pwiz {
namespace chemistry {


/// Class representing a table of isotope distributions for collections of multiple
/// atoms of a single element; the table is computed on instantiation, based on the 
/// element's mass distribution, a maximum atom count, and abundance cutoff value.
class PWIZ_API_DECL IsotopeTable
{
    public:

    IsotopeTable(const MassDistribution& md, int maxAtomCount, double cutoff); 
    ~IsotopeTable();

    MassDistribution distribution(int atomCount) const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    IsotopeTable(const IsotopeTable&);
    IsotopeTable& operator=(const IsotopeTable&);

    /// debugging
    friend PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const IsotopeTable& isotopeTable);
};


} // namespace chemistry
} // namespace pwiz


#endif // _ISOTOPETABLE_HPP_

