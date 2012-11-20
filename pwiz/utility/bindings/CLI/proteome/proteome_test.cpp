//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "../common/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::chemistry;
using namespace pwiz::CLI::proteome;
using namespace System::Collections::Generic;
using System::Exception;
using System::ArgumentException;
using System::String;
using System::Console;


ostream* os_ = 0;


struct TestAminoAcid
{
    double monoMass;
    double avgMass;
    char symbol;
};

// masses copied from http://www.unimod.org/xml/unimod.xml
TestAminoAcid testAminoAcidsArray[] =
{
    { 71.0371140, 71.07790, 'A' },    // Alanine
    { 156.101111, 156.1857, 'R' },    // Arginine
    { 114.042927, 114.1026, 'N' },    // Asparagine
    { 115.026943, 115.0874, 'D' },    // Aspartic acid
    { 103.009185, 103.1429, 'C' },    // Cysteine
    { 129.042593, 129.1140, 'E' },    // Glutamic acid
    { 128.058578, 128.1292, 'Q' },    // Glutamine
    { 57.0214640, 57.05130, 'G' },    // Glycine
    { 137.058912, 137.1393, 'H' },    // Histidine
    { 113.084064, 113.1576, 'I' },    // Isoleucine
    { 113.084064, 113.1576, 'L' },    // Leucine
    { 128.094963, 128.1723, 'K' },    // Lysine
    { 131.040485, 131.1961, 'M' },    // Methionine
    { 147.068414, 147.1739, 'F' },    // Phenylalanine
    { 97.0527640, 97.11520, 'P' },    // Proline
    { 87.0320280, 87.07730, 'S' },    // Serine
    { 150.953636, 150.0379, 'U' },    // Selenocysteine
    { 101.047679, 101.1039, 'T' },    // Threonine
    { 186.079313, 186.2099, 'W' },    // Tryptophan
    { 163.063329, 163.1733, 'Y' },    // Tyrosine
    { 99.0684140, 99.13110, 'V' },    // Valine
    { 114.042927, 114.1026, 'B' },    // AspX
    { 128.058578, 128.1292, 'Z' },    // GlutX
    { 114.091900, 114.1674, 'X' }     // Unknown (Averagine)
};


void testAminoAcids()
{
    // get a copy of all the records

    List<AminoAcidInfo::Record^> records;

    for (char symbol='A'; symbol<='Z'; symbol++)
    {
        try 
        {
            records.Add(AminoAcidInfo::record(symbol));
        }
        catch (Exception^)
        {}
    }

    //for (vector<AminoAcidInfo::Record>::iterator it=records.begin(); it!=records.end(); ++it)
    //    printRecord(os_, *it);

    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->residueFormula->Item[Element::C] == 3);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->residueFormula->Item[Element::H] == 5);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->residueFormula->Item[Element::N] == 1);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->residueFormula->Item[Element::O] == 1);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->residueFormula->Item[Element::S] == 0);

    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->formula->Item[Element::C] == 3);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->formula->Item[Element::H] == 7);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->formula->Item[Element::N] == 1);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->formula->Item[Element::O] == 2);
    unit_assert(AminoAcidInfo::record(AminoAcid::Alanine)->formula->Item[Element::S] == 0);

    unit_assert(AminoAcidInfo::record(AminoAcid::Selenocysteine)->formula->Item[Element::Se] == 1);

    // test single amino acids
    for (int i=0; i < 22; ++i) // skip X for now
    {
        TestAminoAcid& aa = testAminoAcidsArray[i];
        Formula^ residueFormula = AminoAcidInfo::record(aa.symbol)->residueFormula;
        unit_assert_equal(residueFormula->monoisotopicMass(), aa.monoMass, 0.00001);
        unit_assert_equal(residueFormula->molecularWeight(), aa.avgMass, 0.0001);
        //set<char> mmNames = mm2n.getNames(aa.monoMass, EPSILON);
        //set<char> amNames = am2n.getNames(aa.avgMass, EPSILON);
        //unit_assert(mmNames.count(aa.symbol) > 0);
        //unit_assert(amNames.count(aa.symbol) > 0);
    }


    // compute some averages

    /*double averageMonoisotopicMass = 0;
    double averageC = 0;
    double averageH = 0;
    double averageN = 0;
    double averageO = 0;
    double averageS = 0;

    for each (AminoAcidInfo::Record record in records)
    {
        Formula% residueFormula = %record->residueFormula;
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
    for (vector<AminoAcidInfo::Record>::iterator it=records.begin(); it!=records.end(); ++it)
        printRecord(os_, *it);*/
}


void testCleavageAgents()
{
    List<CVID>^ cleavageAgents = Digestion::getCleavageAgents();
    List<String^>^ cleavageAgentNames = Digestion::getCleavageAgentNames();

    /*if (os_)
    {
        *os_ << "Cleavage agents:" << endl;
        BOOST_FOREACH(CVID agentCvid, cleavageAgents)
        {
            *os_ << cvTermInfo(agentCvid).name << " ("
                 << Digestion::getCleavageAgentRegex(agentCvid)
                 << ")" << endl;
        }

        *os_ << "\nCleavage agent names" << endl;
        BOOST_FOREACH(string agentName, cleavageAgentNames)
        {
            *os_ << agentName << endl;
        }
    }*/

    unit_assert(cleavageAgents->Count > 14);
    unit_assert(cleavageAgents[0] == CVID::MS_Trypsin);
    unit_assert(!cleavageAgents->Contains(CVID::MS_NoEnzyme_OBSOLETE));

    unit_assert(Digestion::getCleavageAgentByName("TRYPSIN") == CVID::MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName("trypsin") == CVID::MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName("TRYPSIN/P") == CVID::MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName("trypsin/p") == CVID::MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName("ion trap") == CVID::CVID_Unknown);
    unit_assert(Digestion::getCleavageAgentByName("!@#$%^&*") == CVID::CVID_Unknown);

    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[0]) == CVID::MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[1]) == CVID::MS_Arg_C);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[2]) == CVID::MS_Asp_N);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[3]) == CVID::MS_Asp_N_ambic);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[4]) == CVID::MS_Chymotrypsin);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[5]) == CVID::MS_CNBr);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[6]) == CVID::MS_Formic_acid);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[7]) == CVID::MS_Lys_C);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[8]) == CVID::MS_Lys_C_P);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[9]) == CVID::MS_PepsinA);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[10]) == CVID::MS_TrypChymo);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[11]) == CVID::MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[12]) == CVID::MS_V8_DE);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[13]) == CVID::MS_V8_E);

    unit_assert(Digestion::getCleavageAgentRegex(CVID::MS_Trypsin) == "(?<=[KR])(?!P)");
    unit_assert(Digestion::getCleavageAgentRegex(CVID::MS_V8_E) == "(?<=[EZ])(?!P)");
    unit_assert_throws(Digestion::getCleavageAgentRegex(CVID::MS_ion_trap), ArgumentException);
}

void testDigestionMetadata(DigestedPeptide^ peptide,
                           String^ expectedSequence,
                           int expectedOffset,
                           int expectedMissedCleavages,
                           int expectedSpecificTermini,
                           String^ expectedPrefix,
                           String^ expectedSuffix)
{
    try
    {
        unit_assert(peptide->sequence == expectedSequence);
        unit_assert(peptide->offset() == expectedOffset);
        unit_assert(peptide->missedCleavages() == expectedMissedCleavages);
        unit_assert(peptide->specificTermini() == expectedSpecificTermini);
        unit_assert(peptide->NTerminusPrefix() == expectedPrefix);
        unit_assert(peptide->CTerminusSuffix() == expectedSuffix);
    }
    catch(Exception^ e)
    {
        Console::Error->WriteLine("Testing peptide " + peptide->sequence + ": " + e->Message);
    }
}

void testTrypticBSA(Digestion^ trypticDigestion)
{
    if (os_) *os_ << "Fully-specific BSA digest (offset, missed cleavages, specific termini, length, sequence)" << endl;

    List<DigestedPeptide^>^ trypticPeptides = gcnew List<DigestedPeptide^>(trypticDigestion);
    Dictionary<String^, bool>^ trypticPeptideSet = gcnew Dictionary<String^, bool>();
    for each (DigestedPeptide^ peptide in trypticPeptides)
        trypticPeptideSet->Add(peptide->sequence, true);

    /*if (os_)
    {
        BOOST_FOREACH(DigestedPeptide peptide, trypticPeptides)
        {
            *os_ << peptide->offset() << "\t" << peptide->missedCleavages() << "\t" <<
                    peptide->specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }*/

    // test count
    unit_assert(trypticPeptides->Count > 3);

    // test order of enumeration and trypticPeptides at the N terminus
    unit_assert(trypticPeptides[0]->sequence == "MKWVTFISLLLLFSSAYSR");
    unit_assert(trypticPeptides[1]->sequence == "MKWVTFISLLLLFSSAYSRGVFR");
    unit_assert(trypticPeptides[2]->sequence == "MKWVTFISLLLLFSSAYSRGVFRR");

    // test digestion metadata
    unit_assert(trypticPeptides[0]->offset() == 0);
    unit_assert(trypticPeptides[0]->missedCleavages() == 1);
    unit_assert(trypticPeptides[0]->specificTermini() == 2);
    unit_assert(trypticPeptides[0]->NTerminusIsSpecific() &&
                trypticPeptides[0]->CTerminusIsSpecific());
    unit_assert(trypticPeptides[1]->offset() == 0);
    unit_assert(trypticPeptides[1]->missedCleavages() == 2);
    unit_assert(trypticPeptides[1]->specificTermini() == 2);
    unit_assert(trypticPeptides[1]->NTerminusIsSpecific() &&
                trypticPeptides[1]->CTerminusIsSpecific());
    unit_assert(trypticPeptides[2]->offset() == 0);
    unit_assert(trypticPeptides[2]->missedCleavages() == 3);
    unit_assert(trypticPeptides[2]->specificTermini() == 2);
    unit_assert(trypticPeptides[2]->NTerminusIsSpecific() &&
                trypticPeptides[2]->CTerminusIsSpecific());

    // test for non-tryptic peptides
    unit_assert(!trypticPeptideSet->ContainsKey("MKWVTFISLLLL"));
    unit_assert(!trypticPeptideSet->ContainsKey("STQTALA"));

    // test some middle peptides
    unit_assert(trypticPeptideSet->ContainsKey("RDTHKSEIAHRFK"));
    unit_assert(trypticPeptideSet->ContainsKey("DTHKSEIAHRFK"));

    // test trypticPeptides at the C terminus
    unit_assert(trypticPeptideSet->ContainsKey("EACFAVEGPKLVVSTQTALA"));
    unit_assert(trypticPeptides[trypticPeptides->Count-1]->sequence == "LVVSTQTALA");

    // test maximum missed cleavages
    unit_assert(!trypticPeptideSet->ContainsKey("MKWVTFISLLLLFSSAYSRGVFRRDTHK"));
    unit_assert(!trypticPeptideSet->ContainsKey("LKPDPNTLCDEFKADEKKFWGKYLYEIARR"));

    // test minimum peptide length
    unit_assert(!trypticPeptideSet->ContainsKey("LR"));
    unit_assert(!trypticPeptideSet->ContainsKey("QRLR"));
    unit_assert(trypticPeptideSet->ContainsKey("VLASSARQRLR"));

    // test maximum peptide length
    unit_assert(!trypticPeptideSet->ContainsKey("MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFK"));
}


void testBSADigestion()
{
    if (os_) *os_ << "BSA digestion test" << endl;

    // >P02769|ALBU_BOVIN Serum albumin - Bos taurus (Bovine).
    Peptide bsa("MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPF"
                "DEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEP"
                "ERNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYY"
                "ANKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVA"
                "RLSQKFPKAEFVEVTKLVTDLTKVHKECCHGDLLECADDRADLAKYICDNQDTISSKLKE"
                "CCDKPLLEKSHCIAEVEKDAIPENLPPLTADFAEDKDVCKNYQEAKDAFLGSFLYEYSRR"
                "HPEYAVSVLLRLAKEYEATLEECCAKDDPHACYSTVFDKLKHLVDEPQNLIKQNCDQFEK"
                "LGEYGFQNALIVRYTRKVPQVSTPTLVEVSRSLGKVGTRCCTKPESERMPCTEDYLSLIL"
                "NRLCVLHEKTPVSEKVTKCCTESLVNRRPCFSALTPDETYVPKAFDEKLFTFHADICTLP"
                "DTEKQIKKQTALVELLKHKPKATEEQLKTVMENFVAFVDKCCAADDKEACFAVEGPKLVV"
                "STQTALA");

    // test fully-specific trypsin digest
    testTrypticBSA(gcnew Digestion(%bsa, CVID::MS_Trypsin_P, gcnew Digestion::Config(3, 5, 40, Digestion::Specificity::FullySpecific)));
    testTrypticBSA(gcnew Digestion(%bsa, "(?<=[KR])", gcnew Digestion::Config(3, 5, 40, Digestion::Specificity::FullySpecific)));
}


void testFind()
{
    Digestion^ non = gcnew Digestion(gcnew Peptide("PEPKTIDEKPEPTIDERPEPKTIDEKKKPEPTIDER"), CVID::MS_Lys_C_P, gcnew Digestion::Config(2, 5, 10, Digestion::Specificity::NonSpecific));

    // test find_all
    unit_assert(non->find_all("EPKTIDEKKK")->Count == 0); // too many missed cleavages
    
    unit_assert(non->find_all("PEPKTIDE")->Count == 2);
    testDigestionMetadata(non->find_all("PEPKTIDE")[0], "PEPKTIDE", 0, 1, 1, "", "K");
    testDigestionMetadata(non->find_all("PEPKTIDE")[1], "PEPKTIDE", 17, 1, 0, "R", "K");
    
    // test find_first
    unit_assert_throws(non->find_first("ABC"), Exception); // not in peptide

    testDigestionMetadata(non->find_first("EPTIDE"), "EPTIDE", 10, 0, 0, "P", "R");
    testDigestionMetadata(non->find_first("EPTIDE", 29), "EPTIDE", 29, 0, 0, "P", "R");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "proteome_test_cli\n";
        testAminoAcids();
        testCleavageAgents();
        testBSADigestion();
        testFind();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
