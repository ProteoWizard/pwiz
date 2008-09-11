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

    for (size_t i=0; i<6; i++)
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
    double monoMassNeutral, monoMassPlus1, monoMassPlus2;
    double avgMassNeutral, avgMassPlus1, avgMassPlus2;
};

TestPeptide testPeptides[] =
{
    { "ELK", 388.2322, 389.24005, 195.12396, 388.4643, 389.4723, 195.2401 },
    { "DEERLICKER", 1289.6398, 1290.6477, 645.8278, 1290.4571, 1291.465, 646.2365 },
    { "ELVISLIVES", 1100.6329, 1101.6408, 551.3243, 1101.3056, 1102.3135, 551.6607 },
    { "THEQICKRWNFMPSVERTHELAYDG", 3046.4178, 3047.4257, 1524.2168, 3048.4004, 3049.4083, 1525.2082 },
    //{ "(THEQICKBRWNFXMPSVERTHELAZYDG)", 3402.5874, 3404.7792 }
};

const size_t testPeptidesSize = sizeof(testPeptides)/sizeof(TestPeptide);

void peptideTest()
{
    for (size_t i=0; i < testPeptidesSize; ++i)
    {
        const TestPeptide& p = testPeptides[i];
        double BIG_EPSILON = 0.05;

        Peptide peptide(p.sequence);
        if (os_) *os_ << peptide.sequence() << ": " << peptide.formula() <<
                                               " " << peptide.monoisotopicMass() <<
                                               " " << peptide.molecularWeight() << endl;
        unit_assert_equal(peptide.formula().monoisotopicMass(), p.monoMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.formula().molecularWeight(), p.avgMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(), p.monoMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(), p.avgMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(1, true), p.monoMassPlus1, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(1, true), p.avgMassPlus1, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(2, true), p.monoMassPlus2, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(2, true), p.avgMassPlus2, BIG_EPSILON);

        peptide = p.sequence; // test assignment
        if (os_) *os_ << peptide.sequence() << ": " << peptide.formula() <<
                                               " " << peptide.monoisotopicMass() <<
                                               " " << peptide.molecularWeight() << endl;
        unit_assert_equal(peptide.formula().monoisotopicMass(), p.monoMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.formula().molecularWeight(), p.avgMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(), p.monoMassNeutral, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(), p.avgMassNeutral, BIG_EPSILON);
    }
}


struct TestModification
{
    const char* motif;
    const char* formula;
    double deltaMonoMass;
    double deltaAvgMass;
    bool isDynamic;
};

TestModification testModifications[] =
{
    { "M", "O1", 15.9949, 15.9994, true }, // Oxidation of M
    { "C", "C2H3N1O1", 57.02146, 57.052, false }, // Carboxyamidomethylation of C
    { "(Q", "N-1H-3", -17.02655, -17.0306, false }, // Pyroglutamic acid from Q at the N terminus
    { "(E", "N-1H-3", -17.02655, -17.0306, true }, // Pyroglutamic acid from E at the N terminus
    { "N!G", "N-1H-3", -17.02655, -17.0306, true }, // Succinimide from N when N terminal to G
    //{ "[NQ]", "O1N-1H-2", 0.98402, 0.9847, true }, // Deamidation of N or Q
    { "[STY]!{STY}", "H1P1O3", 79.96633, 79.9799, true } // Phosphorylation of S, T, or Y when not N terminal to S, T, or Y
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

const size_t testModificationsSize = sizeof(testModifications)/sizeof(TestModification);
const size_t testModifiedPeptidesSize = sizeof(testModifiedPeptides)/sizeof(TestModifiedPeptide);

void modificationTest()
{
    for (size_t i=0; i < testModifiedPeptidesSize; ++i)
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
                modMap[lexical_cast<int>(tokens[i+1])].push_back(Modification(mod.formula));
                modMap2[lexical_cast<int>(tokens[i+1])].push_back(Modification(mod.deltaMonoMass, mod.deltaAvgMass));
                monoDeltaMass += mod.deltaMonoMass;
                avgDeltaMass += mod.deltaAvgMass;
            }
        }

        if (os_) *os_ << peptide.sequence() << ": " << peptide.monoisotopicMass() << " " << peptide.molecularWeight() << endl;
        if (os_) *os_ << peptide2.sequence() << ": " << peptide2.monoisotopicMass() << " " << peptide2.molecularWeight() << endl;

        double BIG_EPSILON = 0.05;

        unit_assert_equal(peptide.formula(true).monoisotopicMass(), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(true).molecularWeight(), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(false).monoisotopicMass(), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.formula(false).molecularWeight(), p.avgMass-avgDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(0, true), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(0, true), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide.monoisotopicMass(0, false), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide.molecularWeight(0, false), p.avgMass-avgDeltaMass, BIG_EPSILON);

        unit_assert_equal(peptide2.monoisotopicMass(0, true), p.monoMass, BIG_EPSILON);
        unit_assert_equal(peptide2.molecularWeight(0, true), p.avgMass, BIG_EPSILON);
        unit_assert_equal(peptide2.monoisotopicMass(0, false), p.monoMass-monoDeltaMass, BIG_EPSILON);
        unit_assert_equal(peptide2.molecularWeight(0, false), p.avgMass-avgDeltaMass, BIG_EPSILON);
    }
}


struct TestFragmentation
{
    const char* sequence;
    double a1, aN, a1Plus2, aNPlus2;
    double b1, bN, b1Plus2, bNPlus2;
    double c1, cN, c1Plus2, cNPlus2;
    double x1, xN, x1Plus2, xNPlus2;
    double y1, yN, y1Plus2, yNPlus2;
    double z1, zN, z1Plus2, zNPlus2;
};

// test masses calculated at:
// http://db.systemsbiology.net:8080/proteomicsToolkit/FragIonServlet.html
const TestFragmentation testFragmentations[] =
{
    { "MEERKAT",
      104.05344, 818.41949, 52.53066, 409.71368,
      132.04836, 846.41441, 66.52811, 423.71114,
      149.07490, 762.39328, 75.04139, 381.70057,
      146.04538, 759.36375, 73.52662, 380.18581,
      120.06611, 864.42497, 60.53699, 432.71642,
      103.03956, 847.39842, 52.02372, 424.20315
    },

    { "THEQICKRWNFMPSVERTHELAYDG",
       74.06063, 3001.42018, 37.53425, 1501.21402,
      102.05555, 3029.41509, 51.53171, 1515.21148,
      119.08210, 2989.42018, 60.04498, 1495.21402,
      102.01916, 2972.35724, 51.51352, 1486.68256,
       76.03990, 3047.42566, 38.52388, 1524.21676,
       59.01335, 3030.39911, 30.01061, 1515.70349
    },
};

const size_t testFragmentationsSize = sizeof(testFragmentations)/sizeof(TestFragmentation);

void writeFragmentation(const Peptide& p, const Fragmentation& f, ostream& os)
{
    size_t length = p.sequence().length();

    os << "a:";
    for (size_t i=1; i <= length; ++i)
        os << " " << f.a(i);
    os << endl;

    os << "b:";
    for (size_t i=1; i <= length; ++i)
        os << " " << f.b(i);
    os << endl;

    os << "c:";
    for (size_t i=1; i < length; ++i)
        os << " " << f.c(i);
    os << endl;

    os << "x:";
    for (size_t i=1; i < length; ++i)
        os << " " << f.x(i);
    os << endl;

    os << "y:";
    for (size_t i=1; i <= length; ++i)
        os << " " << f.y(i);
    os << endl;

    os << "z:";
    for (size_t i=1; i <= length; ++i)
        os << " " << f.z(i);
    os << endl;
}

void fragmentTest()
{
    using Chemistry::Proton;
    const double EPSILON = 0.005;

    for (size_t i=0; i < testFragmentationsSize; ++i)
    {
        const TestFragmentation& tf = testFragmentations[i];
        size_t length = string(tf.sequence).length();
        Peptide peptide(tf.sequence);
        if (os_) *os_ << peptide.sequence() << ": " << peptide.monoisotopicMass() << endl;

        Fragmentation f = peptide.fragmentation();
        if (os_) writeFragmentation(peptide, f, *os_);

        unit_assert_equal(tf.a1 - Proton, f.a(1), EPSILON);
        unit_assert_equal(tf.b1 - Proton, f.b(1), EPSILON);
        unit_assert_equal(tf.c1 - Proton, f.c(1), EPSILON);
        unit_assert_equal(tf.x1 - Proton, f.x(1), EPSILON);
        unit_assert_equal(tf.y1 - Proton, f.y(1), EPSILON);
        unit_assert_equal(tf.z1 - Proton, f.z(1), EPSILON);

        unit_assert_equal(tf.aN - Proton, f.a(length), EPSILON);
        unit_assert_equal(tf.bN - Proton, f.b(length), EPSILON);
        unit_assert_equal(tf.cN - Proton, f.c(length-1), EPSILON);
        unit_assert_equal(tf.xN - Proton, f.x(length-1), EPSILON);
        unit_assert_equal(tf.yN - Proton, f.y(length), EPSILON);
        unit_assert_equal(tf.zN - Proton, f.z(length), EPSILON);

        unit_assert_equal(tf.a1, f.a(1, 1), EPSILON);
        unit_assert_equal(tf.b1, f.b(1, 1), EPSILON);
        unit_assert_equal(tf.c1, f.c(1, 1), EPSILON);
        unit_assert_equal(tf.x1, f.x(1, 1), EPSILON);
        unit_assert_equal(tf.y1, f.y(1, 1), EPSILON);
        unit_assert_equal(tf.z1, f.z(1, 1), EPSILON);

        unit_assert_equal(tf.aN, f.a(length, 1), EPSILON);
        unit_assert_equal(tf.bN, f.b(length, 1), EPSILON);
        unit_assert_equal(tf.cN, f.c(length-1, 1), EPSILON);
        unit_assert_equal(tf.xN, f.x(length-1, 1), EPSILON);
        unit_assert_equal(tf.yN, f.y(length, 1), EPSILON);
        unit_assert_equal(tf.zN, f.z(length, 1), EPSILON);

        unit_assert_equal(tf.a1Plus2, f.a(1, 2), EPSILON);
        unit_assert_equal(tf.b1Plus2, f.b(1, 2), EPSILON);
        unit_assert_equal(tf.c1Plus2, f.c(1, 2), EPSILON);
        unit_assert_equal(tf.x1Plus2, f.x(1, 2), EPSILON);
        unit_assert_equal(tf.y1Plus2, f.y(1, 2), EPSILON);
        unit_assert_equal(tf.z1Plus2, f.z(1, 2), EPSILON);

        unit_assert_equal(tf.aNPlus2, f.a(length, 2), EPSILON);
        unit_assert_equal(tf.bNPlus2, f.b(length, 2), EPSILON);
        unit_assert_equal(tf.cNPlus2, f.c(length-1, 2), EPSILON);
        unit_assert_equal(tf.xNPlus2, f.x(length-1, 2), EPSILON);
        unit_assert_equal(tf.yNPlus2, f.y(length, 2), EPSILON);
        unit_assert_equal(tf.zNPlus2, f.z(length, 2), EPSILON);
    }

    // test fragmentation with mods
    {
        Peptide p("THEQICKRWNFMPSVERTHELAYDG");
        Modification C57("C2H3N1O1"), M16("O1");
        (p.modifications())[5].push_back(C57);
        (p.modifications())[11].push_back(M16);
        Fragmentation f = p.fragmentation(true, false);
        Fragmentation fWithMods = p.fragmentation(true, true);
        if (os_) writeFragmentation(p, f, *os_);
        if (os_) writeFragmentation(p, fWithMods, *os_);
        double EPSILON = 0.00000001;
        for (size_t i=1; i <= 5; ++i)
        {
            unit_assert_equal(f.a(i), fWithMods.a(i), EPSILON);
            unit_assert_equal(f.b(i), fWithMods.b(i), EPSILON);
            unit_assert_equal(f.c(i), fWithMods.c(i), EPSILON);
        }

        for (size_t i=6; i <= 11; ++i)
        {
            double deltaMass = C57.monoisotopicDeltaMass();
            unit_assert_equal(f.a(i)+deltaMass, fWithMods.a(i), EPSILON);
            unit_assert_equal(f.b(i)+deltaMass, fWithMods.b(i), EPSILON);
            unit_assert_equal(f.c(i)+deltaMass, fWithMods.c(i), EPSILON);
        }

        for (size_t i=12; i <= p.sequence().length(); ++i)
        {
            double deltaMass = C57.monoisotopicDeltaMass() + M16.monoisotopicDeltaMass();
            unit_assert_equal(f.a(i)+deltaMass, fWithMods.a(i), EPSILON);
            unit_assert_equal(f.b(i)+deltaMass, fWithMods.b(i), EPSILON);
            if (i < p.sequence().length())
                unit_assert_equal(f.c(i)+deltaMass, fWithMods.c(i), EPSILON);
        }

        for (size_t i=1; i <= 13; ++i)
        {
            unit_assert_equal(f.x(i), fWithMods.x(i), EPSILON);
            unit_assert_equal(f.y(i), fWithMods.y(i), EPSILON);
            unit_assert_equal(f.z(i), fWithMods.z(i), EPSILON);
        }

        for (size_t i=14; i <= 19; ++i)
        {
            double deltaMass = M16.monoisotopicDeltaMass();
            unit_assert_equal(f.x(i)+deltaMass, fWithMods.x(i), EPSILON);
            unit_assert_equal(f.y(i)+deltaMass, fWithMods.y(i), EPSILON);
            unit_assert_equal(f.z(i)+deltaMass, fWithMods.z(i), EPSILON);
        }

        for (size_t i=20; i <= p.sequence().length(); ++i)
        {
            double deltaMass = C57.monoisotopicDeltaMass() + M16.monoisotopicDeltaMass();
            if (i < p.sequence().length())
                unit_assert_equal(f.x(i)+deltaMass, fWithMods.x(i), EPSILON);
            unit_assert_equal(f.y(i)+deltaMass, fWithMods.y(i), EPSILON);
            unit_assert_equal(f.z(i)+deltaMass, fWithMods.z(i), EPSILON);
        }
    }

    {
        Peptide p("QICKRWNFMPSVERTHELAYDG");
        Modification Q17("N-1H-3"), S80("H1P1O3");
        (p.modifications())[ModificationMap::NTerminus()].push_back(Q17); // close enough
        (p.modifications())[10].push_back(S80);
        Fragmentation f = p.fragmentation(true, false);
        Fragmentation fWithMods = p.fragmentation(true, true);
        if (os_) writeFragmentation(p, f, *os_);
        if (os_) writeFragmentation(p, fWithMods, *os_);
        double EPSILON = 0.00000001;

        for (size_t i=0; i <= 10; ++i)
        {
            double deltaMass = Q17.monoisotopicDeltaMass();
            unit_assert_equal(f.a(i)+deltaMass, fWithMods.a(i), EPSILON);
            unit_assert_equal(f.b(i)+deltaMass, fWithMods.b(i), EPSILON);
            unit_assert_equal(f.c(i)+deltaMass, fWithMods.c(i), EPSILON);
        }

        for (size_t i=11; i <= p.sequence().length(); ++i)
        {
            double deltaMass = Q17.monoisotopicDeltaMass() + S80.monoisotopicDeltaMass();
            unit_assert_equal(f.a(i)+deltaMass, fWithMods.a(i), EPSILON);
            unit_assert_equal(f.b(i)+deltaMass, fWithMods.b(i), EPSILON);
            if (i < p.sequence().length())
                unit_assert_equal(f.c(i)+deltaMass, fWithMods.c(i), EPSILON);
        }

        for (size_t i=1; i <= 11; ++i)
        {
            unit_assert_equal(f.x(i), fWithMods.x(i), EPSILON);
            unit_assert_equal(f.y(i), fWithMods.y(i), EPSILON);
            unit_assert_equal(f.z(i), fWithMods.z(i), EPSILON);
        }

        for (size_t i=12; i <= p.sequence().length(); ++i)
        {
            double deltaMass = S80.monoisotopicDeltaMass();
            if (i < p.sequence().length())
                unit_assert_equal(f.x(i)+deltaMass, fWithMods.x(i), EPSILON);
            unit_assert_equal(f.y(i)+deltaMass, fWithMods.y(i), EPSILON);
            unit_assert_equal(f.z(i)+deltaMass, fWithMods.z(i), EPSILON);
        }
    }
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

