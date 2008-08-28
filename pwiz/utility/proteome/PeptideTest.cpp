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
#include "utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <algorithm>
#include "utility/misc/String.hpp"


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


struct TestPeptide
{
    const char* sequence;
    double monoMass;
    double avgMass;
};

TestPeptide testPeptides[] =
{
    { "ELK", 388.2322, 388.4643 },
    { "DEERLICKER", 1289.6398, 1290.4571 },
    { "ELVISLIVES", 1100.6329, 1101.3056 },
    { "THEQICKRWNFMPSVERTHELAYDG", 3046.4178, 3048.4004 },
    //{ "(THEQICKBRWNFXMPSVERTHELAZYDG)", 3402.5874, 3404.7792 }
};

const int testPeptidesSize = sizeof(testPeptides)/sizeof(TestPeptide);

void peptideTest()
{
    for (int i=0; i < testPeptidesSize; ++i)
    {
        string sequence(testPeptides[i].sequence);
        double monoMass = testPeptides[i].monoMass;
        double avgMass = testPeptides[i].avgMass;
        double BIG_EPSILON = 0.05;

        Peptide peptide(sequence.begin(), sequence.end());
        unit_assert_equal(peptide.formula().monoisotopicMass(), monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula().molecularWeight(), avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(), monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(), avgMass, BIG_EPSILON);

        peptide = sequence; // test assignment
        unit_assert_equal(peptide.formula().monoisotopicMass(), monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula().molecularWeight(), avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(), monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(), avgMass, BIG_EPSILON);
    }
}


struct TestModification
{
    const char* motif;
    const char* adductFormula;
    const char* deductFormula;
    double deltaMonoMass;
    double deltaAvgMass;
    bool isDynamic;
};

TestModification testModifications[] =
{
    { "M", "O1", "", 15.9949, 15.9994, true }, // Oxidation of M
    { "C", "C2H3N1O1", "", 57.02146, 57.052, false }, // Carboxyamidomethylation of C
    { "(Q", "", "N1H3", -17.02655, -17.0306, false }, // Pyroglutamic acid from Q at the N terminus
    { "(E", "", "N1H3", -17.02655, -17.0306, true }, // Pyroglutamic acid from E at the N terminus
    { "N!G", "", "N1H3", -17.02655, -17.0306, true }, // Succinimide from N when N terminal to G
    //{ "[NQ]", "O1H1", "N1H3", 0.98402, 0.9847, true }, // Deamidation of N or Q
    { "[STY]!{STY}", "H1P1O3", "", 79.96633, 79.9799, true } // Phosphorylation of S, T, or Y when not N terminal to S, T, or Y
};

struct TestModifiedPeptide
{
    const char* sequence;
    const char* mods; // index pairs
    double monoMass;
    double avgMass;
};

TestModifiedPeptide testModifiedPeptides[] =
{
    // M+16
    { "MEERKAT",
      "0 0",
      879.41205, 879.98369
    },

    // C+57
    { "THEQICKRWNFMPSVERTHELAYDG",
      "1 5",
      3103.43929, 3105.45241
    },

    // C+57, M+16
    { "THEQICKRWNFMPSVERTHELAYDG",
      "0 11 1 5",
      3119.43419, 3121.45181
    },

    // C+57, Q-17
    { "QICKRWNFMPSVERTHELAYDG",
      "2 0 1 2",
      2719.26356, 2721.06016
    },

    // no mods
    { "ELVISLIVES", 0, 1100.63293, 1101.30557 },

    // E-17
    { "ELVISLIVES",
      "3 0",
      1083.60638, 1084.27497
    },

    // no mods
    { "PINGPNG", 0, 667.32898, 667.71964 },

    // N-17
    { "PINGPNG",
      "4 2",
      650.30243, 650.68904
    },

    //{ "(QINT)",,}, // Q-17

    // no mods
    { "MISSISSIPPI", 0, 1143.62099, 1144.3918 },

    // S+80, S+80
    { "MISSISSIPPI",
      "5 3 5 5",
      1303.55365, 1304.3516
    }
};

const int testModificationsSize = sizeof(testModifications)/sizeof(TestModification);
const int testModifiedPeptidesSize = sizeof(testModifiedPeptides)/sizeof(TestModifiedPeptide);

void modificationTest()
{
    for (int i=0; i < testModifiedPeptidesSize; ++i)
    {
        TestModifiedPeptide& p = testModifiedPeptides[i];
        Peptide peptide(p.sequence); // mods by formula
        Peptide peptide2(p.sequence); // mods by mass

        double monoDeltaMass = 0;
        double avgDeltaMass = 0;
        if (p.mods != NULL)
        {
            ModificationMap& modMap = peptide.modifications();
            ModificationMap& modMap2 = peptide2.modifications();
            vector<string> tokens;
            boost::split(tokens, p.mods, boost::is_space());
            for (size_t i=0; i < tokens.size(); i+=2)
            {
                TestModification& mod = testModifications[lexical_cast<size_t>(tokens[i])];
                modMap[lexical_cast<int>(tokens[i+1])].push_back(Modification(mod.adductFormula, mod.deductFormula));
                modMap2[lexical_cast<int>(tokens[i+1])].push_back(Modification(mod.deltaMonoMass, mod.deltaAvgMass));
                monoDeltaMass += mod.deltaMonoMass;
                avgDeltaMass += mod.deltaAvgMass;
            }
        }

        double BIG_EPSILON = 0.05;

        unit_assert_equal(peptide.formula(true).monoisotopicMass(), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(true).molecularWeight(), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(false).monoisotopicMass(), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(false).molecularWeight(), p.avgMass-avgDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(true), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(true), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(false), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(false), p.avgMass-avgDeltaMass, BIG_EPSILON);

        unit_assert_equal(peptide2.monoisotopicMass(true), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide2.molecularWeight(true), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide2.monoisotopicMass(false), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide2.molecularWeight(false), p.avgMass-avgDeltaMass, BIG_EPSILON);
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
        peptideTest();
        modificationTest();
        fragmentTest();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

