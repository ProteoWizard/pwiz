//
// PeptideTest.cpp
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


#include "Peptide.hpp"
#include "IsotopeCalculator.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <iomanip>
#include <algorithm>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void test()
{
    Peptide angiotensin("DRVYIHPF");
    if (os_) *os_ << "angiotensin: " << angiotensin.sequence() << " " << angiotensin.formula() << endl;

    Peptide alpha16("WHWLQL");
    if (os_) *os_ << "alpha16: " << alpha16.sequence() << " " << alpha16.formula() << endl;
}


const char* sequences[] = 
{
    "CLPMILDNK",
    "AVSNPFQQR",
    "CELLFFFK",
    "AGASIVGVNCR",
    "QPTPPFFGR",
    "AVFHMQSVK"
};


void isotopeTest()
{
    cout << "\n\npeptide isotope test\n";
    using namespace Chemistry;
    IsotopeCalculator calc(.0001, .2);

    cout.precision(10);

    for (int i=0; i<6; i++)
    {
        Peptide peptide(sequences[i]);
        MassDistribution md = calc.distribution(peptide.formula());
        cout << peptide.formula() << " (" << peptide.formula().monoisotopicMass() << "):\n" << md;

        for (int j=0; j<4; j++)
            cout << md[j].abundance/md[0].abundance << " ";
        cout << "\n\n";
    }
}


const char* mrfaSequences[] =
{
    "MRFA",
    "MRF",
    "MR",
    "M",
    "RFA",
    "FA",
    "A"
};


const int mrfaSequencesSize = sizeof(mrfaSequences)/sizeof(const char*);


void printSequenceInfo(const char* sequence)
{
    using Chemistry::Formula;

    Peptide peptide(sequence);
    Formula water("H2O1");
    Formula residueSum = peptide.formula() - water; 
    Formula bIon = residueSum + Formula("H1");
    Formula yIon = residueSum + Formula("H3O1");

    if (os_) *os_ 
         << fixed << setprecision(2) 
         << setw(6) << peptide.sequence() << " " 
         << setw(8) << peptide.formula().monoisotopicMass() << " " 
         << setw(8) << residueSum.monoisotopicMass() << " "
         << setw(8) << bIon.monoisotopicMass() << " "
         << setw(8) << yIon.monoisotopicMass() << " "
         << endl; 
}


void fragmentTest()
{
    for_each(mrfaSequences, mrfaSequences + mrfaSequencesSize, printSequenceInfo);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeptideTest\n";
        //test();
        //isotopeTest();
        fragmentTest();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

