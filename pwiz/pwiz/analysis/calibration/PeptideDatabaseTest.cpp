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


#include "PeptideDatabase.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void compareRecords(const PeptideDatabase* pdb1, const PeptideDatabaseRecord* record1,
                    const PeptideDatabase* pdb2, const PeptideDatabaseRecord* record2)
{
    unit_assert(record1->id_ipi == record2->id_ipi);
    unit_assert(record1->abundance == record2->abundance);
    unit_assert(record1->mass == record2->mass);
    unit_assert(record1->formula == record2->formula);
    unit_assert(pdb1->sequence(*record1) == pdb2->sequence(*record2));
}


void test_basic()
{
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();

    PeptideDatabaseRecord record;

    record.abundance = 10;
    record.mass = 27;
    record.formula = PeptideDatabaseFormula(1, 2, 3, 4, 5);
    pdb->append(record, "darren");

    record.abundance = 20;
    record.mass = 666;
    record.formula = PeptideDatabaseFormula(6, 7, 8, 9, 10);
    pdb->append(record, "goo");

    unit_assert(pdb->size()==2);
   
    const PeptideDatabaseRecord* p = pdb->records();
    unit_assert(p->abundance == 10);
    unit_assert(p->mass == 27);
    unit_assert(p->formula.C == 1); 
    unit_assert(pdb->sequence(*p) == "darren");
    
    p++;
    unit_assert(p->abundance == 20);
    unit_assert(p->mass == 666);
    unit_assert(p->formula.C == 6);
    unit_assert(p->formula.S == 10);
    unit_assert(pdb->sequence(*p) == "goo");
     
    // iterator interface 
    if (os_)
        for (PeptideDatabase::iterator it=pdb->begin(); it!=pdb->end(); ++it) 
            *os_ << *it << " " << pdb->sequence(*it) << endl;

    // test i/o
    string filename = "PeptideDatabaseTest.output.pdb";
    {
        pdb->write(filename);
        auto_ptr<const PeptideDatabase> pdb2 = PeptideDatabase::create(filename);
        unit_assert(pdb2->size() == pdb->size());
      
        for (int i=0; i<pdb->size(); ++i)
        {
            const PeptideDatabaseRecord* record1 = pdb->records()+i;
            const PeptideDatabaseRecord* record2 = pdb2->records()+i;
            compareRecords(pdb.get(), record1, pdb2.get(), record2);
        }
    }

    boost::filesystem::remove(filename);
}
        
        
void test_range()
{
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();

    for (int i=0; i<10; i++)
    {
        PeptideDatabaseRecord record;
        record.mass = i;
        pdb->append(record);
    }

    PeptideDatabase::iterator begin = pdb->begin();
    PeptideDatabase::iterator six = pdb->mass_lower_bound(5.5);
    PeptideDatabase::iterator eight = pdb->mass_upper_bound(7.5);
    unit_assert(six == begin+6);
    unit_assert(eight == begin+8);
}


int main(int argc, char* argv[])
{

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeptideDatabaseTest\n";
        test_basic();
        test_range();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


