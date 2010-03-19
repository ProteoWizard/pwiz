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


#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iterator>
#include "boost/foreach.hpp"
#include <set>
#include <vector>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::proteome;
using namespace System::Collections::Generic;
using System::String;
using System::Console;


ostream* os_ = 0;


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

    unit_assert(cleavageAgents->Count == 14);
    unit_assert(cleavageAgents[0] == CVID::MS_Trypsin);
    unit_assert(cleavageAgents[cleavageAgents->Count-1] == CVID::MS_V8_E);
    unit_assert(!cleavageAgents->Contains(CVID::MS_NoEnzyme));

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
    unit_assert_throws(Digestion::getCleavageAgentRegex(CVID::MS_ion_trap), std::invalid_argument);
}


/*struct DigestedPeptideLessThan
{
    bool operator() (const DigestedPeptide& lhs, const DigestedPeptide& rhs) const
    {
        return lhs.sequence() < rhs.sequence();
    }
};*/

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


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "DigestionTest\n";
        testCleavageAgents();
        testBSADigestion();
        return 0;
    }
    catch (std::exception& e)
    {
        Console::Error->WriteLine("std::exception: " + gcnew String(e.what()));
    }
    catch (System::Exception^ e)
    {
        Console::Error->WriteLine("System.Exception: " + e->Message);
    }
    catch (...)
    {
        Console::Error->WriteLine("Caught unknown exception.\n");
    }
}
