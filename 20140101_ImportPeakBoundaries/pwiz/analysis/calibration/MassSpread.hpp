//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#ifndef _MASSSPREAD_H_
#define _MASSSPREAD_H_


#include <memory>
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace calibration {


class MassDatabase;


class MassSpread
{
    public:

    struct Pair
    {
        double mass;
        double probability;
        Pair(double m=0, double p=0) : mass(m), probability(p) {}
    };

    // const interface
    static std::auto_ptr<const MassSpread> create(double measurement,
                                                  double initialError,
                                                  const MassDatabase* massDatabase);
    virtual double measurement() const = 0;
    virtual double error() const = 0;
    virtual const std::vector<Pair>& distribution() const = 0;
    virtual double sumProbabilityOverMass() const = 0;
    virtual double sumProbabilityOverMass2() const = 0;
    virtual void output(std::ostream& os) const = 0;

    // non-const interface
    // note: must recalculate() sums after changing distribution()
    static std::auto_ptr<MassSpread> create();
    virtual std::vector<Pair>& distribution() = 0;
    virtual void recalculate() = 0;

    virtual ~MassSpread(){}
};


std::ostream& operator<<(std::ostream& os, const MassSpread& massSpread);


} // namespace calibration
} // namespace pwiz


#endif // _MASSSPREAD_H_

