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


#include "MassDatabase.hpp"
#include "PeptideDatabase.hpp"
#include <fstream>
#include <algorithm>
#include <stdexcept>
#include <iostream>
#include <cmath>


namespace pwiz {
namespace calibration {


using namespace std;
using namespace pwiz::proteome;


class MassDatabaseIntegers : public MassDatabase
{
    public:

    virtual int size() const {return high_-low_+1;}
    virtual Entry entry(int index) const;
    virtual vector<Entry> range(double massLow, double massHigh) const;

    private:

    static const int low_ = 100;
    static const int high_ = 2200;
};


const int MassDatabaseIntegers::low_;
const int MassDatabaseIntegers::high_;


MassDatabase::Entry MassDatabaseIntegers::entry(int index) const
{
    if (index<0 || index>=size())
        throw runtime_error("[MassDatabaseIntegers::entry()] Index out of range.\n");

    return Entry(low_ + index);
}


vector<MassDatabase::Entry> MassDatabaseIntegers::range(double massLow, double massHigh) const
{
    vector<Entry> result;

    int low = max(low_, int(ceil(massLow)));
    int high = min(high_, int(floor(massHigh)));

    for (int i=low; i<=high; i++)
        result.push_back(Entry(i, 1));

    return result;
}


auto_ptr<MassDatabase> MassDatabase::createIntegerTestDatabase()
{
    return auto_ptr<MassDatabase>(new MassDatabaseIntegers);
}


class MassDatabaseFile : public MassDatabase
{
    public:

    MassDatabaseFile(const string& filename);
    virtual int size() const {return masses_.size();} 
    virtual Entry entry(int index) const {return Entry(masses_[index]);}
    virtual vector<Entry> range(double massLow, double massHigh) const;

    private:

    // simple implementation -- everything stored in memory
    vector<double> masses_;
};


auto_ptr<MassDatabase> MassDatabase::createFromTextFile(const string& filename)
{
    return auto_ptr<MassDatabase>(new MassDatabaseFile(filename));
}


MassDatabaseFile::MassDatabaseFile(const string& filename)
{
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error("[MassDatabase] File not found: " + filename);

    cout << "[MassDatabase] Reading database " << filename << "..." << flush;

    masses_.reserve(1000000);

    while (is)
    {
        string buffer;
        getline(is, buffer);
        double mass = atof(buffer.c_str());
        if (mass)
            masses_.push_back(mass);
    }

    cout << "done.\n";
    // sort if necessary
}


vector<MassDatabase::Entry> MassDatabaseFile::range(double massLow, double massHigh) const
{
    vector<Entry> result;
    vector<double>::const_iterator low = lower_bound(masses_.begin(), masses_.end(), massLow);
    vector<double>::const_iterator high = upper_bound(low, masses_.end(), massHigh);
    copy(low, high, back_inserter(result)); // relies on automatic conversion double->Entry
    return result;
}


class MassDatabasePDB : public MassDatabase
{
    public:

    MassDatabasePDB(const string& filename);
    virtual int size() const {return pdb_->size();}
    virtual Entry entry(int index) const;
    virtual vector<Entry> range(double massLow, double massHigh) const;

    private:
    auto_ptr<const PeptideDatabase> pdb_;
};


auto_ptr<MassDatabase> MassDatabase::createFromPeptideDatabase(const string& filename)
{
    return auto_ptr<MassDatabase>(new MassDatabasePDB(filename));
}


MassDatabasePDB::MassDatabasePDB(const string& filename)
:   pdb_(PeptideDatabase::create(filename))
{}


MassDatabase::Entry MassDatabasePDB::entry(int index) const
{
    if (index<0 || index>=pdb_->size())
        throw out_of_range("[MassDatabasePDB::entry()] Invalid index."); 

    Entry result;
    const PeptideDatabaseRecord& record = pdb_->records()[index];
    result.weight = record.abundance;
    result.mass = record.mass;
    return result; 
}


vector<MassDatabase::Entry> MassDatabasePDB::range(double massLow, double massHigh) const
{
    vector<Entry> result;
    PeptideDatabase::iterator low = pdb_->mass_lower_bound(massLow);
    PeptideDatabase::iterator high = pdb_->mass_upper_bound(massHigh);
    for (PeptideDatabase::iterator it=low; it!=high; ++it)
        result.push_back(Entry(it->mass, it->abundance));
    return result;
}


} // namespace calibration
} // namespace pwiz

