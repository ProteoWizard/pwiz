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
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::calibration;


void test_integer()
{
    auto_ptr<MassDatabase> mdb = MassDatabase::createIntegerTestDatabase();
    vector<MassDatabase::Entry> entries = mdb->range(0, 3000);
    unit_assert((int)entries.size() == mdb->size()); 
    unit_assert(entries[0].mass == 100);
    unit_assert(entries[entries.size()-1].mass == 2200);
}


void test_pdb()
{
    const string& filename = "MassDatabaseTest.test_pdb.pdb";
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
   
    const int recordCount = 10;
    for (int i=0; i<recordCount; i++)
    {
        PeptideDatabaseRecord record;
        record.abundance = i;
        record.mass = i;
        pdb->append(record);
    }
    pdb->write(filename);

    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(filename);
    unit_assert(mdb->size() == recordCount);

    for (int i=0; i<recordCount; i++)
    {
        MassDatabase::Entry entry = mdb->entry(i); 
        unit_assert(entry.mass == i);
        unit_assert(entry.weight == i);
    }

    vector<MassDatabase::Entry> range = mdb->range(2,6);
    unit_assert(range.size() == 5);
    for (unsigned int i=0; i<range.size(); i++)
        unit_assert(range[i].mass == i+2);

    system(("rm " + filename).c_str());
}


namespace pwiz {
namespace calibration {
bool operator==(const MassDatabase::Entry& a, const MassDatabase::Entry& b)
{
    return (a.mass==b.mass && a.weight==b.weight);
}
}} // namespaces


void test_pdb_vs_integer()
{
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
    for (int i=100; i<=2200; i++)
    {
        PeptideDatabaseRecord record;
        record.mass = i;
        pdb->append(record);
    }
    const string& filename = "temp.pdb";
    pdb->write(filename);

    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(filename);
    auto_ptr<MassDatabase> mdb2 = MassDatabase::createIntegerTestDatabase();
    unit_assert(mdb->size() == mdb2->size());

    vector<MassDatabase::Entry> entries1 = mdb->range(150, 250);
    vector<MassDatabase::Entry> entries2 = mdb2->range(150, 250);
    unit_assert(entries1 == entries2);

    system(("rm " + filename).c_str());
}


int main()
{
    try
    {
        test_integer();
        test_pdb();
        test_pdb_vs_integer();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

