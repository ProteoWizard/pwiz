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


#ifndef _MASSDATABASE_H_
#define _MASSDATABASE_H_


#include <memory>
#include <string>
#include <vector>


namespace pwiz {
namespace calibration {


class MassDatabase
{
    public:

    static std::auto_ptr<MassDatabase> createFromPeptideDatabase(const std::string& filename);
    static std::auto_ptr<MassDatabase> createFromTextFile(const std::string& filename);
    static std::auto_ptr<MassDatabase> createIntegerTestDatabase();

    struct Entry
    {
        double mass;
        double weight;
        Entry(double m=0, double w=1) : mass(m), weight(w) {}
    };

    virtual int size() const = 0;
    virtual Entry entry(int index) const = 0;
    virtual std::vector<Entry> range(double massLow, double massHigh) const = 0;

    virtual ~MassDatabase(){}
};


} // namespace calibration
} // namespace pwiz


#endif // _MASSDATABASE_H_

