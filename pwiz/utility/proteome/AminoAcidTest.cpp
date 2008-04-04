//
// AminoAcidTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "AminoAcid.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>
#include <functional>
#include <algorithm>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::proteome::Chemistry::Element;
using namespace pwiz::proteome::AminoAcid;


ostream* os_ = 0;


bool hasLowerMass(const AminoAcid::Info::Record& a, const AminoAcid::Info::Record& b)
{
    return a.formula.monoisotopicMass() < b.formula.monoisotopicMass();
}


void printRecord(ostream* os, const AminoAcid::Info::Record& record)
{
    if (!os) return;

	Chemistry::Formula water_("H2O1");
	Chemistry::Formula residueFormula = record.formula - water_;
    
    *os << record.symbol << ": " 
        << setw(14) << record.name << " " 
        << setw(11) << record.formula << " " 
        << setprecision(3)
        << setw(5) << record.abundance << " "
        << fixed 
        << setw(7) << record.formula.monoisotopicMass() << " "
        << setw(7) << residueFormula.monoisotopicMass() << endl;
}


void test()
{
    AminoAcid::Info info;

    unit_assert(info[Alanine].formula[C] == 3);
    unit_assert(info[Alanine].formula[H] == 7);
    unit_assert(info[Alanine].formula[N] == 1);
    unit_assert(info[Alanine].formula[O] == 2);
    unit_assert(info[Alanine].formula[S] == 0);
}


void printAminoAcidInfo()
{
    AminoAcid::Info info;
	Chemistry::Formula water_("H2O1");

    // get a copy of all the records

    vector<AminoAcid::Info::Record> records;

    for (char symbol='A'; symbol<='Z'; symbol++)
    {
        try 
        {
            const AminoAcid::Info::Record& record = info[symbol];
            records.push_back(record);
        }
        catch (exception&)
        {}
    }

    // compute some averages

    double averageMonoisotopicMass = 0;
    double averageC = 0;
    double averageH = 0;
    double averageN = 0;
    double averageO = 0;
    double averageS = 0;

    for (vector<AminoAcid::Info::Record>::iterator it=records.begin(); it!=records.end(); ++it)
    {
        const AminoAcid::Info::Record& record = *it;
        printRecord(os_, record);

        Chemistry::Formula residueFormula = record.formula - water_;
        averageMonoisotopicMass += record.formula.monoisotopicMass() * record.abundance; 
        averageC += residueFormula[C] * record.abundance;
        averageH += residueFormula[H] * record.abundance;
        averageN += residueFormula[N] * record.abundance;
        averageO += residueFormula[O] * record.abundance;
        averageS += residueFormula[S] * record.abundance;
    }

    if (os_) *os_ << setprecision(8) << endl;
    if (os_) *os_ << "average residue C: " << averageC << endl;    
    if (os_) *os_ << "average residue H: " << averageH << endl;    
    if (os_) *os_ << "average residue N: " << averageN << endl;    
    if (os_) *os_ << "average residue O: " << averageO << endl;    
    if (os_) *os_ << "average residue S: " << averageS << endl;    
    if (os_) *os_ << endl;

    if (os_) *os_ << "average monoisotopic mass: " << averageMonoisotopicMass << endl;
    double averageResidueMass = averageMonoisotopicMass - water_.monoisotopicMass();
    if (os_) *os_ << "average residue mass: " << averageResidueMass << endl << endl;

    // sort by monoisotopic mass and print again
    sort(records.begin(), records.end(), hasLowerMass);
    for (vector<AminoAcid::Info::Record>::iterator it=records.begin(); it!=records.end(); ++it)
        printRecord(os_, *it); 
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "AminoAcidTest\n";
        test();
        printAminoAcidInfo();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


