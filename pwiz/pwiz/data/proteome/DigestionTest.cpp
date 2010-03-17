//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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
#include "Digestion.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <iostream>
#include <iterator>
#include "boost/foreach.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"
#include <set>

using namespace std;
using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void testCleavageAgents()
{
    const set<CVID>& cleavageAgents = Digestion::getCleavageAgents();
    const vector<string>& cleavageAgentNames = Digestion::getCleavageAgentNames();

    if (os_)
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
    }

    unit_assert(cleavageAgents.size() == 14);
    unit_assert(*cleavageAgents.begin() == MS_Trypsin);
    unit_assert(*cleavageAgents.rbegin() == MS_V8_E);
    unit_assert(!cleavageAgents.count(MS_NoEnzyme));

    unit_assert(Digestion::getCleavageAgentByName("TRYPSIN") == MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName("trypsin") == MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName("TRYPSIN/P") == MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName("trypsin/p") == MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName("ion trap") == CVID_Unknown);
    unit_assert(Digestion::getCleavageAgentByName("!@#$%^&*") == CVID_Unknown);

    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[0]) == MS_Trypsin);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[1]) == MS_Arg_C);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[2]) == MS_Asp_N);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[3]) == MS_Asp_N_ambic);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[4]) == MS_Chymotrypsin);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[5]) == MS_CNBr);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[6]) == MS_Formic_acid);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[7]) == MS_Lys_C);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[8]) == MS_Lys_C_P);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[9]) == MS_PepsinA);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[10]) == MS_TrypChymo);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[11]) == MS_Trypsin_P);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[12]) == MS_V8_DE);
    unit_assert(Digestion::getCleavageAgentByName(cleavageAgentNames[13]) == MS_V8_E);

    unit_assert(Digestion::getCleavageAgentRegex(MS_Trypsin) == "(?<=[KR])(?!P)");
    unit_assert(Digestion::getCleavageAgentRegex(MS_V8_E) == "(?<=[EZ])(?!P)");
    unit_assert_throws(Digestion::getCleavageAgentRegex(MS_ion_trap), std::invalid_argument);
}


struct DigestedPeptideLessThan
{
    bool operator() (const DigestedPeptide& lhs, const DigestedPeptide& rhs) const
    {
        return lhs.sequence() < rhs.sequence();
    }
};

void testTrypticBSA(const Digestion& trypticDigestion)
{
    if (os_) *os_ << "Fully-specific BSA digest (offset, missed cleavages, specific termini, length, sequence)" << endl;

    set<DigestedPeptide, DigestedPeptideLessThan>::const_iterator peptideItr;

    vector<DigestedPeptide> trypticPeptides(trypticDigestion.begin(), trypticDigestion.end());
    set<DigestedPeptide, DigestedPeptideLessThan> trypticPeptideSet(trypticPeptides.begin(), trypticPeptides.end());

    if (os_)
    {
        BOOST_FOREACH(DigestedPeptide peptide, trypticPeptides)
        {
            *os_ << peptide.offset() << "\t" << peptide.missedCleavages() << "\t" <<
                    peptide.specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }

    // test count
    unit_assert(trypticPeptides.size() > 3);

    // test order of enumeration and trypticPeptides at the N terminus
    unit_assert(trypticPeptides[0].sequence() == "MKWVTFISLLLLFSSAYSR");
    unit_assert(trypticPeptides[1].sequence() == "MKWVTFISLLLLFSSAYSRGVFR");
    unit_assert(trypticPeptides[2].sequence() == "MKWVTFISLLLLFSSAYSRGVFRR");

    // test digestion metadata
    unit_assert(trypticPeptides[0].offset() == 0);
    unit_assert(trypticPeptides[0].missedCleavages() == 1);
    unit_assert(trypticPeptides[0].specificTermini() == 2);
    unit_assert(trypticPeptides[0].NTerminusIsSpecific() &&
                trypticPeptides[0].CTerminusIsSpecific());
    unit_assert(trypticPeptides[1].offset() == 0);
    unit_assert(trypticPeptides[1].missedCleavages() == 2);
    unit_assert(trypticPeptides[1].specificTermini() == 2);
    unit_assert(trypticPeptides[1].NTerminusIsSpecific() &&
                trypticPeptides[1].CTerminusIsSpecific());
    unit_assert(trypticPeptides[2].offset() == 0);
    unit_assert(trypticPeptides[2].missedCleavages() == 3);
    unit_assert(trypticPeptides[2].specificTermini() == 2);
    unit_assert(trypticPeptides[2].NTerminusIsSpecific() &&
                trypticPeptides[2].CTerminusIsSpecific());

    // test for non-tryptic peptides
    unit_assert(!trypticPeptideSet.count("MKWVTFISLLLL"));
    unit_assert(!trypticPeptideSet.count("STQTALA"));

    // test some middle peptides
    unit_assert(trypticPeptideSet.count("RDTHKSEIAHRFK"));
    unit_assert(trypticPeptideSet.count("DTHKSEIAHRFK"));

    // test trypticPeptides at the C terminus
    unit_assert(trypticPeptideSet.count("EACFAVEGPKLVVSTQTALA"));
    unit_assert(trypticPeptides.back().sequence() == "LVVSTQTALA");

    // test maximum missed cleavages
    unit_assert(!trypticPeptideSet.count("MKWVTFISLLLLFSSAYSRGVFRRDTHK"));
    unit_assert(!trypticPeptideSet.count("LKPDPNTLCDEFKADEKKFWGKYLYEIARR"));

    // test minimum peptide length
    unit_assert(!trypticPeptideSet.count("LR"));
    unit_assert(!trypticPeptideSet.count("QRLR"));
    unit_assert(trypticPeptideSet.count("VLASSARQRLR"));

    // test maximum peptide length
    unit_assert(!trypticPeptideSet.count("MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFK"));
}

void testSemitrypticBSA(const Digestion& semitrypticDigestion)
{
    if (os_) *os_ << "Semi-specific BSA digest (offset, missed cleavages, specific termini, length, sequence)" << endl;

    set<DigestedPeptide, DigestedPeptideLessThan>::const_iterator peptideItr;

    vector<DigestedPeptide> semitrypticPeptides(semitrypticDigestion.begin(), semitrypticDigestion.end());
    set<DigestedPeptide, DigestedPeptideLessThan> semitrypticPeptideSet(semitrypticPeptides.begin(), semitrypticPeptides.end());
    
    if (os_)
    {
        BOOST_FOREACH(DigestedPeptide peptide, semitrypticPeptides)
        {
            *os_ << peptide.offset() << "\t" << peptide.missedCleavages() << "\t" <<
                    peptide.specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }

    // test count
    unit_assert(semitrypticPeptides.size() > 3);

    // test order of enumeration and peptides at the N terminus
    unit_assert(semitrypticPeptides[0].sequence() == "MKWVT");
    unit_assert(semitrypticPeptides[1].sequence() == "MKWVTF");
    unit_assert(semitrypticPeptides[2].sequence() == "MKWVTFI");

    // test order of enumeration and peptides at the C terminus
    unit_assert(semitrypticPeptides.rbegin()->sequence() == "QTALA");
    unit_assert((semitrypticPeptides.rbegin()+1)->sequence() == "TQTALA");
    unit_assert((semitrypticPeptides.rbegin()+2)->sequence() == "STQTALA");
    unit_assert((semitrypticPeptides.rbegin()+5)->sequence() == "LVVSTQTALA");
    unit_assert((semitrypticPeptides.rbegin()+6)->sequence() == "LVVSTQTAL");
    unit_assert((semitrypticPeptides.rbegin()+10)->sequence() == "LVVST");

    // test digestion metadata
    unit_assert(semitrypticPeptides[0].offset() == 0);
    unit_assert(semitrypticPeptides[0].missedCleavages() == 1);
    unit_assert(semitrypticPeptides[0].specificTermini() == 1);
    unit_assert(semitrypticPeptides[0].NTerminusIsSpecific() &&
                !semitrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("MKWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 0);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 1);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(!peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 0);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("VTFISLLLLFSSAYSRG"); // non-tryptic
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    // test for non-specific peptides
    unit_assert(semitrypticPeptideSet.count("WVTFISLLLLFSSAYSR")); // tryptic
    unit_assert(semitrypticPeptideSet.count("KWVTFISLLLLFSSAYSR")); // semi-tryptic
    unit_assert(!semitrypticPeptideSet.count("KWVTFISLLLLFSSAYS")); // non-tryptic

    // test semi-specific peptides at the C terminus
    unit_assert(semitrypticPeptideSet.count("FAVEGPKLVVSTQTALA")); // semi-tryptic
    unit_assert(!semitrypticPeptideSet.count("FAVEGPKLVVSTQTAL")); // non-tryptic
}

void testNontrypticBSA(const Digestion& nontrypticDigestion)
{
    if (os_) *os_ << "Non-specific BSA digest (offset, missed cleavages, specific termini, length, sequence)" << endl;

    set<DigestedPeptide, DigestedPeptideLessThan>::const_iterator peptideItr;

    vector<DigestedPeptide> nontrypticPeptides(nontrypticDigestion.begin(), nontrypticDigestion.end());
    set<DigestedPeptide, DigestedPeptideLessThan> nontrypticPeptideSet(nontrypticPeptides.begin(), nontrypticPeptides.end());
    
    if (os_)
    {
        BOOST_FOREACH(DigestedPeptide peptide, nontrypticPeptides)
        {
            *os_ << peptide.offset() << "\t" << peptide.missedCleavages() << "\t" <<
                    peptide.specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }

    // test count
    unit_assert(nontrypticPeptides.size() > 3);

    // test order of enumeration and peptides at the N terminus
    unit_assert(nontrypticPeptides[0].sequence() == "MKWVT");
    unit_assert(nontrypticPeptides[1].sequence() == "MKWVTF");
    unit_assert(nontrypticPeptides[2].sequence() == "MKWVTFI");

    // test digestion metadata
    unit_assert(nontrypticPeptides[0].offset() == 0);
    unit_assert(nontrypticPeptides[0].missedCleavages() == 1);
    unit_assert(nontrypticPeptides[0].specificTermini() == 1);
    unit_assert(nontrypticPeptides[0].NTerminusIsSpecific() &&
                !nontrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("MKWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 0);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("KWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 1);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(!peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == nontrypticPeptideSet.end());

    peptideItr = nontrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 0);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("VTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 3);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 0);
    unit_assert(!peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    // test for peptides of all specificities
    unit_assert(nontrypticPeptideSet.count("WVTFISLLLLFSSAYSR")); // tryptic
    unit_assert(nontrypticPeptideSet.count("KWVTFISLLLLFSSAYSR")); // semi-tryptic
    unit_assert(nontrypticPeptideSet.count("KWVTFISLLLLFSSAYS")); // non-tryptic

    // test non-specific peptides at the C terminus
    unit_assert(nontrypticPeptideSet.count("FAVEGPKLVVSTQTALA")); // semi-tryptic
    unit_assert(nontrypticPeptideSet.count("FAVEGPKLVVSTQTAL")); // non-tryptic
    unit_assert(nontrypticPeptides.back().sequence() == "QTALA"); // semi-tryptic

    // test maximum missed cleavages
    unit_assert(nontrypticPeptideSet.count("KWVTFISLLLLFSSAYSR"));
    unit_assert(!nontrypticPeptideSet.count("KWVTFISLLLLFSSAYSRG"));

    // test minimum peptide length
    unit_assert(!nontrypticPeptideSet.count("LR"));
    unit_assert(!nontrypticPeptideSet.count("QRLR"));
    unit_assert(nontrypticPeptideSet.count("VLASSAR"));

    // test maximum peptide length
    unit_assert(!nontrypticPeptideSet.count("EYEATLEECCAKDDPHACYSTVFDK"));
}

void testSemitrypticMethionineClippingBSA(const Digestion& semitrypticDigestion)
{
    if (os_) *os_ << "Semi-specific BSA digest w/ methionine clipping (offset, missed cleavages, specific termini, length, sequence)" << endl;

    set<DigestedPeptide, DigestedPeptideLessThan>::const_iterator peptideItr;

    vector<DigestedPeptide> semitrypticPeptides(semitrypticDigestion.begin(), semitrypticDigestion.end());
    set<DigestedPeptide, DigestedPeptideLessThan> semitrypticPeptideSet(semitrypticPeptides.begin(), semitrypticPeptides.end());
    
    if (os_)
    {
        BOOST_FOREACH(DigestedPeptide peptide, semitrypticPeptides)
        {
            *os_ << peptide.offset() << "\t" << peptide.missedCleavages() << "\t" <<
                    peptide.specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }

    // test count
    unit_assert(semitrypticPeptides.size() > 3);

    // test order of enumeration and peptides at the N terminus;
    // with methionine clipping, MKWVT contains two missed cleavages
    unit_assert(semitrypticPeptides[0].sequence() == "KWVTF");
    unit_assert(semitrypticPeptides[1].sequence() == "KWVTFI");
    unit_assert(semitrypticPeptides[2].sequence() == "KWVTFIS");

    // test order of enumeration and peptides at the C terminus
    unit_assert(semitrypticPeptides.rbegin()->sequence() == "QTALA");
    unit_assert((semitrypticPeptides.rbegin()+1)->sequence() == "TQTALA");
    unit_assert((semitrypticPeptides.rbegin()+2)->sequence() == "STQTALA");
    unit_assert((semitrypticPeptides.rbegin()+5)->sequence() == "LVVSTQTALA");
    unit_assert((semitrypticPeptides.rbegin()+6)->sequence() == "LVVSTQTAL");
    unit_assert((semitrypticPeptides.rbegin()+10)->sequence() == "LVVST");

    // test digestion metadata ([0]: KWVTF)
    unit_assert(semitrypticPeptides[0].offset() == 1);
    unit_assert(semitrypticPeptides[0].missedCleavages() == 1);
    unit_assert(semitrypticPeptides[0].specificTermini() == 1);
    unit_assert(semitrypticPeptides[0].NTerminusIsSpecific() &&
                !semitrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("MKWVTFISLLLLFSSAYSR"); // 2 missed cleavages
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYS"); // clipped methionine
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 1);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(peptideItr->NTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSR"); // clipped methionine
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 1);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 0);
    unit_assert(peptideItr->specificTermini() == 2);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert(peptideItr->offset() == 2);
    unit_assert(peptideItr->missedCleavages() == 1);
    unit_assert(peptideItr->specificTermini() == 1);
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("VTFISLLLLFSSAYSRG"); // non-tryptic
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    // test for non-specific peptides
    unit_assert(semitrypticPeptideSet.count("WVTFISLLLLFSSAYSR")); // tryptic
    unit_assert(semitrypticPeptideSet.count("KWVTFISLLLLFSSAYSR")); // semi-tryptic
    unit_assert(semitrypticPeptideSet.count("KWVTFISLLLLFSSAYS")); // clipped methionine & semi-specific
    unit_assert(!semitrypticPeptideSet.count("VTFISLLLLFSSAYS")); // non-specific

    // test semi-specific peptides at the C terminus
    unit_assert(semitrypticPeptideSet.count("FAVEGPKLVVSTQTALA")); // semi-tryptic
    unit_assert(!semitrypticPeptideSet.count("FAVEGPKLVVSTQTAL")); // non-tryptic
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
    testTrypticBSA(Digestion(bsa, ProteolyticEnzyme_Trypsin, Digestion::Config(3, 5, 40)));
    testTrypticBSA(Digestion(bsa, "[KR]|", Digestion::Config(3, 5, 40)));
    testTrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(3, 5, 40)));
    testTrypticBSA(Digestion(bsa, boost::regex("(?<=[KR])"), Digestion::Config(3, 5, 40)));

    // test semi-specific trypsin digest
    testSemitrypticBSA(Digestion(bsa, ProteolyticEnzyme_Trypsin, Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticBSA(Digestion(bsa, "[KR]|", Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticBSA(Digestion(bsa, boost::regex("(?<=[KR])"), Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));

    // test non-specific trypsin digest
    testNontrypticBSA(Digestion(bsa, ProteolyticEnzyme_Trypsin, Digestion::Config(1, 5, 20, Digestion::NonSpecific)));
    testNontrypticBSA(Digestion(bsa, "[KR]|", Digestion::Config(1, 5, 20, Digestion::NonSpecific)));
    testNontrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(1, 5, 20, Digestion::NonSpecific)));
    testNontrypticBSA(Digestion(bsa, boost::regex("(?<=[KR])"), Digestion::Config(1, 5, 20, Digestion::NonSpecific)));

    // test semi-specific trypsin digest with n-terminal methionine clipping (motif and regex only)
    vector<Digestion::Motif> motifs; motifs.push_back("{M|"); motifs.push_back("[KR]|");
    testSemitrypticMethionineClippingBSA(Digestion(bsa, motifs, Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticMethionineClippingBSA(Digestion(bsa, boost::regex("(?<=^M)|(?<=[KR])"), Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticMethionineClippingBSA(Digestion(bsa, boost::regex("(?<=(^M)|([KR]))"), Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));

    // test funky digestion
    Digestion funkyDigestion(bsa, "A[DE]|[FG]", Digestion::Config(0));
    vector<Peptide> funkyPeptides(funkyDigestion.begin(), funkyDigestion.end());

    unit_assert(funkyPeptides[0].sequence() == "MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPFDEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEPERNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYYANKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVARLSQKFPKAE");
    unit_assert(funkyPeptides[1].sequence() == "FVEVTKLVTDLTKVHKECCHGDLLECADDRADLAKYICDNQDTISSKLKECCDKPLLEKSHCIAEVEKDAIPENLPPLTAD");
    unit_assert(funkyPeptides[2].sequence() == "FAEDKDVCKNYQEAKDAFLGSFLYEYSRRHPEYAVSVLLRLAKEYEATLEECCAKDDPHACYSTVFDKLKHLVDEPQNLIKQNCDQFEKLGEYGFQNALIVRYTRKVPQVSTPTLVEVSRSLGKVGTRCCTKPESERMPCTEDYLSLILNRLCVLHEKTPVSEKVTKCCTESLVNRRPCFSALTPDETYVPKAFDEKLFTFHADICTLPDTEKQIKKQTALVELLKHKPKATEEQLKTVMENFVAFVDKCCAADDKEACFAVEGPKLVVSTQTALA");
}


void testThreadSafetyWorker(boost::barrier* testBarrier)
{
    testBarrier->wait(); // wait until all threads have started

    try
    {
        testCleavageAgents();
        testBSADigestion();
    }
    catch (exception& e)
    {
        cerr << "Exception in worker thread: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unhandled exception in worker thread." << endl;
    }
}

void testThreadSafety()
{
    const int testThreadCount = 10;
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, &testBarrier));
    testThreadGroup.join_all();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "DigestionTest\n";
        testCleavageAgents();
        testBSADigestion();
        testThreadSafety();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
