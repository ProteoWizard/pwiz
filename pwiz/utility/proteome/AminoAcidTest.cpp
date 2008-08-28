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

	Chemistry::Formula residueFormula = record.formula;
    
    *os << record.symbol << ": " 
        << setw(14) << record.name << " " 
        << setw(11) << record.formula << " " 
        << setprecision(3)
        << setw(5) << record.abundance << " "
        << fixed 
        << setw(7) << record.formula.monoisotopicMass() << " "
        << setw(7) << residueFormula.monoisotopicMass() << endl;
}


struct TestAminoAcid
{
    double monoMass;
    double avgMass;
    char symbol;
};

TestAminoAcid testAminoAcids[] =
{
    { 71.03711, 71.07880, 'A' },    // Alanine
    { 103.0092, 103.1388, 'C' },    // Cysteine
    { 115.0269, 115.0886, 'D' },    // Aspartic acid
    { 129.0426, 129.1155, 'E' },    // Glutamic acid
    { 147.0684, 147.1766, 'F' },    // Phenylalanine
    { 57.02146, 57.05190, 'G' },    // Glycine
    { 137.0589, 137.1411, 'H' },    // Histidine
    { 113.0841, 113.1594, 'I' },    // Isoleucine
    { 128.0949, 128.1741, 'K' },    // Lysine
    { 113.0841, 113.1594, 'L' },    // Leucine
    { 131.0405, 131.1926, 'M' },    // Methionine
    { 114.0429, 114.1038, 'N' },    // Asparagine
    { 97.05276, 97.11670, 'P' },    // Proline
    { 128.0586, 128.1307, 'Q' },    // Glutamine
    { 156.1011, 156.1875, 'R' },    // Arginine
    { 87.03203, 87.07820, 'S' },    // Serine
    { 101.0477, 101.1051, 'T' },    // Threonine
    { 186.0793, 186.2132, 'W' },    // Tryptophan
    { 163.0633, 163.1760, 'Y' },    // Tyrosine
    { 99.06841, 99.13260, 'V' },    // Valine
    { 114.0429, 114.1038, 'B' },    // AspX
    { 128.0586, 128.1307, 'Z' },    // GlutX
    { 114.0919, 114.1674, 'X' }     // Unknown (Averagine)
};


void test()
{
    AminoAcid::Info info;

    unit_assert(info[Alanine].residueFormula[C] == 3);
    unit_assert(info[Alanine].residueFormula[H] == 5);
    unit_assert(info[Alanine].residueFormula[N] == 1);
    unit_assert(info[Alanine].residueFormula[O] == 1);
    unit_assert(info[Alanine].residueFormula[S] == 0);

    unit_assert(info[Alanine].formula[C] == 3);
    unit_assert(info[Alanine].formula[H] == 7);
    unit_assert(info[Alanine].formula[N] == 1);
    unit_assert(info[Alanine].formula[O] == 2);
    unit_assert(info[Alanine].formula[S] == 0);


    // test single amino acids
    for (int i=0; i < 22; ++i) // skip X for now
    {
        TestAminoAcid& aa = testAminoAcids[i];
        Chemistry::Formula residueFormula = info[aa.symbol].residueFormula;
        unit_assert_equal(residueFormula.monoisotopicMass(), aa.monoMass, 0.01);
        unit_assert_equal(residueFormula.molecularWeight(), aa.avgMass, 0.01);
        //set<char> mmNames = mm2n.getNames(aa.monoMass, EPSILON);
        //set<char> amNames = am2n.getNames(aa.avgMass, EPSILON);
        //unit_assert(mmNames.count(aa.symbol) > 0);
        //unit_assert(amNames.count(aa.symbol) > 0);
    }


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

        Chemistry::Formula residueFormula = record.residueFormula;
        averageMonoisotopicMass += residueFormula.monoisotopicMass() * record.abundance; 
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
    double averageResidueMass = averageMonoisotopicMass;
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
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


