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


#include "pwiz/utility/misc/unit.hpp"
#include "Peptide.hpp"
#include "Digestion.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"
#include "boost/exception/all.hpp"
#include "boost/foreach_field.hpp"


using namespace pwiz::cv;
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

    unit_assert(cleavageAgents.size() >= 14);
    unit_assert_operator_equal(MS_Trypsin, *cleavageAgents.begin());
    unit_assert(!cleavageAgents.count(MS_NoEnzyme_OBSOLETE));
    unit_assert(cleavageAgents.count(MS_no_cleavage));
    unit_assert(cleavageAgents.count(MS_unspecific_cleavage));

    unit_assert_operator_equal(MS_Trypsin, Digestion::getCleavageAgentByName("TRYPSIN"));
    unit_assert_operator_equal(MS_Trypsin, Digestion::getCleavageAgentByName("trypsin"));
    unit_assert_operator_equal(MS_Trypsin_P, Digestion::getCleavageAgentByName("TRYPSIN/P"));
    unit_assert_operator_equal(MS_Trypsin_P, Digestion::getCleavageAgentByName("trypsin/p"));
    unit_assert_operator_equal(MS_glutamyl_endopeptidase, Digestion::getCleavageAgentByName("glutamyl endopeptidase"));
    unit_assert_operator_equal(MS_Glu_C, Digestion::getCleavageAgentByName("Glu-C")); // test exact synonyms
    unit_assert_operator_equal(CVID_Unknown, Digestion::getCleavageAgentByName("ion trap"));
    unit_assert_operator_equal(CVID_Unknown, Digestion::getCleavageAgentByName("!@#$%^&*"));

    unit_assert_operator_equal(MS_Trypsin, Digestion::getCleavageAgentByRegex("(?<=[KR])(?!P)"));
    unit_assert_operator_equal(MS_Trypsin_P, Digestion::getCleavageAgentByRegex("(?<=[KR])"));
    unit_assert_operator_equal(MS_Lys_C_P, Digestion::getCleavageAgentByRegex("(?<=K)"));
    unit_assert_operator_equal(CVID_Unknown, Digestion::getCleavageAgentByRegex("!@#$%^&*"));

    unit_assert_operator_equal(MS_Trypsin, Digestion::getCleavageAgentByName(cleavageAgentNames[0]));
    unit_assert_operator_equal(MS_Arg_C, Digestion::getCleavageAgentByName(cleavageAgentNames[1]));
    unit_assert_operator_equal(MS_Asp_N, Digestion::getCleavageAgentByName(cleavageAgentNames[2]));
    unit_assert_operator_equal(MS_Asp_N_ambic, Digestion::getCleavageAgentByName(cleavageAgentNames[3]));
    unit_assert_operator_equal(MS_Chymotrypsin, Digestion::getCleavageAgentByName(cleavageAgentNames[4]));
    unit_assert_operator_equal(MS_CNBr, Digestion::getCleavageAgentByName(cleavageAgentNames[5]));
    unit_assert_operator_equal(MS_Formic_acid, Digestion::getCleavageAgentByName(cleavageAgentNames[6]));
    unit_assert_operator_equal(MS_Lys_C, Digestion::getCleavageAgentByName(cleavageAgentNames[7]));
    unit_assert_operator_equal(MS_Lys_C_P, Digestion::getCleavageAgentByName(cleavageAgentNames[8]));
    unit_assert_operator_equal(MS_PepsinA, Digestion::getCleavageAgentByName(cleavageAgentNames[9]));
    unit_assert_operator_equal(MS_TrypChymo, Digestion::getCleavageAgentByName(cleavageAgentNames[10]));
    unit_assert_operator_equal(MS_Trypsin_P, Digestion::getCleavageAgentByName(cleavageAgentNames[11]));
    unit_assert_operator_equal(MS_V8_DE, Digestion::getCleavageAgentByName(cleavageAgentNames[12]));
    unit_assert_operator_equal(MS_V8_E, Digestion::getCleavageAgentByName(cleavageAgentNames[13]));

    unit_assert_operator_equal("(?<=[KR])(?!P)", Digestion::getCleavageAgentRegex(MS_Trypsin));
    unit_assert_operator_equal("(?<=[EZ])(?!P)", Digestion::getCleavageAgentRegex(MS_V8_E));
    unit_assert_throws(Digestion::getCleavageAgentRegex(MS_ion_trap), std::invalid_argument);
    
    unit_assert_operator_equal("(?=[BD])", Digestion::getCleavageAgentRegex(MS_Asp_N));
    unit_assert_operator_equal("(?=[BNDD])", Digestion::disambiguateCleavageAgentRegex(Digestion::getCleavageAgentRegex(MS_Asp_N)));
    unit_assert_operator_equal("(?=[A-Z])", Digestion::disambiguateCleavageAgentRegex("(?=X)"));
    unit_assert_operator_equal("(?=[A-Z])", Digestion::disambiguateCleavageAgentRegex("(?=[X])"));
    unit_assert_operator_equal("(?![BND])", Digestion::disambiguateCleavageAgentRegex("(?!B)"));
    unit_assert_operator_equal("(?<![JIL])(?=[BNDK])", Digestion::disambiguateCleavageAgentRegex("(?<![J])(?=[BK])"));
}


struct DigestedPeptideLessThan
{
    bool operator() (const DigestedPeptide& lhs, const DigestedPeptide& rhs) const
    {
        return lhs.sequence() < rhs.sequence();
    }
};

bool testDigestionMetadata(const DigestedPeptide& peptide,
                           const string& expectedSequence,
                           size_t expectedOffset,
                           size_t expectedMissedCleavages,
                           size_t expectedSpecificTermini,
                           const string& expectedPrefix,
                           const string& expectedSuffix)
{
    try
    {
        unit_assert_operator_equal(expectedSequence, peptide.sequence());
        unit_assert_operator_equal(expectedOffset, peptide.offset());
        unit_assert_operator_equal(expectedMissedCleavages, peptide.missedCleavages());
        unit_assert_operator_equal(expectedSpecificTermini, peptide.specificTermini());
        unit_assert_operator_equal(expectedPrefix, peptide.NTerminusPrefix());
        unit_assert_operator_equal(expectedSuffix, peptide.CTerminusSuffix());
        return true;
    }
    catch(exception& e)
    {
        cerr << "Testing peptide " << peptide.sequence() << ": " << e.what() << endl;
        return false;
    }
}

void testTrypticBSA(const Digestion& trypticDigestion)
{
    if (os_) *os_ << "Fully-specific BSA digest (offset, missed cleavages, specific termini, length, sequence)" << endl;

    vector<DigestedPeptide> trypticPeptides(trypticDigestion.begin(), trypticDigestion.end());
    set<DigestedPeptide, DigestedPeptideLessThan> trypticPeptideSet(trypticPeptides.begin(), trypticPeptides.end());

    if (os_)
    {
        BOOST_FOREACH(const DigestedPeptide& peptide, trypticPeptides)
        {
            *os_ << peptide.offset() << "\t" << peptide.missedCleavages() << "\t" <<
                    peptide.specificTermini() << "\t" << peptide.sequence().length() <<
                    "\t" << peptide.sequence() << "\n";
        }
    }

    // test count
    unit_assert(trypticPeptides.size() > 4);

    // test order of enumeration and metadata: sequence,         Off, NMC, NTT, Pre, Suf
    unit_assert(testDigestionMetadata(trypticPeptides[0], "MKWVTFISLLLLFSSAYSR", 0, 1, 2, "", "G"));
    unit_assert(testDigestionMetadata(trypticPeptides[1], "MKWVTFISLLLLFSSAYSRGVFR", 0, 2, 2, "", "R"));
    unit_assert(testDigestionMetadata(trypticPeptides[2], "MKWVTFISLLLLFSSAYSRGVFRR", 0, 3, 2, "", "D"));
    unit_assert(testDigestionMetadata(trypticPeptides[3], "KWVTFISLLLLFSSAYSR", 1, 1, 2, "M", "G"));

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

    // test methionine clipping at the N-terminus
    unit_assert(trypticPeptideSet.count("KWVTFISLLLLFSSAYSR"));
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
    unit_assert_operator_equal("MKWVT", semitrypticPeptides[0].sequence());
    unit_assert_operator_equal("MKWVTF", semitrypticPeptides[1].sequence());
    unit_assert_operator_equal("MKWVTFI", semitrypticPeptides[2].sequence());

    // test order of enumeration and peptides at the C terminus
    unit_assert_operator_equal("QTALA", semitrypticPeptides.rbegin()->sequence());
    unit_assert_operator_equal("TQTALA", (semitrypticPeptides.rbegin()+1)->sequence());
    unit_assert_operator_equal("STQTALA", (semitrypticPeptides.rbegin()+2)->sequence());
    unit_assert_operator_equal("LVVSTQTALA", (semitrypticPeptides.rbegin()+5)->sequence());
    unit_assert_operator_equal("LVVSTQTAL", (semitrypticPeptides.rbegin()+6)->sequence());
    unit_assert_operator_equal("LVVST", (semitrypticPeptides.rbegin()+10)->sequence());

    // test digestion metadata
    unit_assert_operator_equal(0, semitrypticPeptides[0].offset());
    unit_assert_operator_equal(1, semitrypticPeptides[0].missedCleavages());
    unit_assert_operator_equal(1, semitrypticPeptides[0].specificTermini());
    unit_assert(semitrypticPeptides[0].NTerminusIsSpecific() &&
                !semitrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("MKWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(0, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(1, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(0, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(1, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("VTFISLLLLFSSAYSRG"); // non-tryptic
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    // test for non-specific peptides
    unit_assert(semitrypticPeptideSet.count("WVTFISLLLLFSSAYSR")); // tryptic
    unit_assert(semitrypticPeptideSet.count("VTFISLLLLFSSAYSR")); // semi-tryptic
    unit_assert(!semitrypticPeptideSet.count("VTFISLLLLFSSAYS")); // non-tryptic

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
    unit_assert_operator_equal("MKWVT", nontrypticPeptides[0].sequence());
    unit_assert_operator_equal("MKWVTF", nontrypticPeptides[1].sequence());
    unit_assert_operator_equal("MKWVTFI", nontrypticPeptides[2].sequence());

    // test digestion metadata
    unit_assert_operator_equal(0, nontrypticPeptides[0].offset());
    unit_assert_operator_equal(1, nontrypticPeptides[0].missedCleavages());
    unit_assert_operator_equal(1, nontrypticPeptides[0].specificTermini());
    unit_assert(nontrypticPeptides[0].NTerminusIsSpecific() &&
                !nontrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("MKWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert_operator_equal(0, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("KWVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert_operator_equal(1, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == nontrypticPeptideSet.end());

    peptideItr = nontrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(0, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(1, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    peptideItr = nontrypticPeptideSet.find("VTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != nontrypticPeptideSet.end());
    unit_assert_operator_equal(3, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(0, peptideItr->specificTermini());
    unit_assert(!peptideItr->NTerminusIsSpecific() &&
                !peptideItr->CTerminusIsSpecific());

    // test for peptides of all specificities
    unit_assert(nontrypticPeptideSet.count("WVTFISLLLLFSSAYSR")); // tryptic
    unit_assert(nontrypticPeptideSet.count("VTFISLLLLFSSAYSR")); // semi-tryptic
    unit_assert(nontrypticPeptideSet.count("VTFISLLLLFSSAYS")); // non-tryptic

    // test non-specific peptides at the C terminus
    unit_assert(nontrypticPeptideSet.count("FAVEGPKLVVSTQTALA")); // semi-tryptic
    unit_assert(nontrypticPeptideSet.count("FAVEGPKLVVSTQTAL")); // non-tryptic
    unit_assert_operator_equal("QTALA", nontrypticPeptides.back().sequence()); // semi-tryptic

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
    // even with methionine clipping, MKWVT contains just one missed cleavage
    unit_assert_operator_equal("MKWVT", semitrypticPeptides[0].sequence());
    unit_assert_operator_equal("MKWVTF", semitrypticPeptides[1].sequence());
    unit_assert_operator_equal("MKWVTFI", semitrypticPeptides[2].sequence());

    // test order of enumeration and peptides at the C terminus
    unit_assert_operator_equal("QTALA", semitrypticPeptides.rbegin()->sequence());
    unit_assert_operator_equal("TQTALA", (semitrypticPeptides.rbegin()+1)->sequence());
    unit_assert_operator_equal("STQTALA", (semitrypticPeptides.rbegin()+2)->sequence());
    unit_assert_operator_equal("LVVSTQTALA", (semitrypticPeptides.rbegin()+5)->sequence());
    unit_assert_operator_equal("LVVSTQTAL", (semitrypticPeptides.rbegin()+6)->sequence());
    unit_assert_operator_equal("LVVST", (semitrypticPeptides.rbegin()+10)->sequence());

    // test digestion metadata ([0]: MKWVT)
    unit_assert_operator_equal(0, semitrypticPeptides[0].offset());
    unit_assert_operator_equal(1, semitrypticPeptides[0].missedCleavages());
    unit_assert_operator_equal(1, semitrypticPeptides[0].specificTermini());
    unit_assert(semitrypticPeptides[0].NTerminusIsSpecific() &&
                !semitrypticPeptides[0].CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYS"); // clipped methionine
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(1, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(1, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSR"); // clipped methionine
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(1, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("KWVTFISLLLLFSSAYSRG"); // 2 missed cleavages
    unit_assert(peptideItr == semitrypticPeptideSet.end());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSR");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(0, peptideItr->missedCleavages());
    unit_assert_operator_equal(2, peptideItr->specificTermini());
    unit_assert(peptideItr->NTerminusIsSpecific() &&
                peptideItr->CTerminusIsSpecific());

    peptideItr = semitrypticPeptideSet.find("WVTFISLLLLFSSAYSRG");
    unit_assert(peptideItr != semitrypticPeptideSet.end());
    unit_assert_operator_equal(2, peptideItr->offset());
    unit_assert_operator_equal(1, peptideItr->missedCleavages());
    unit_assert_operator_equal(1, peptideItr->specificTermini());
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
    testTrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(3, 5, 40)));
    testTrypticBSA(Digestion(bsa, "(?<=[KR])", Digestion::Config(3, 5, 40)));

    // test semi-specific trypsin digest
    testSemitrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticBSA(Digestion(bsa, "(?<=[KR])", Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));

    // test non-specific trypsin digest
    testNontrypticBSA(Digestion(bsa, MS_Trypsin_P, Digestion::Config(1, 5, 20, Digestion::NonSpecific)));
    testNontrypticBSA(Digestion(bsa, "(?<=[KR])", Digestion::Config(1, 5, 20, Digestion::NonSpecific)));

    // test semi-specific trypsin digest with n-terminal methionine clipping (motif and regex only)
    testSemitrypticMethionineClippingBSA(Digestion(bsa, "(?<=^M)|(?<=[KR])", Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));
    testSemitrypticMethionineClippingBSA(Digestion(bsa, "(?<=(^M)|([KR]))", Digestion::Config(1, 5, 20, Digestion::SemiSpecific)));

    // test funky digestion
    Digestion funkyDigestion(bsa, "(?<=A[DE])(?=[FG])", Digestion::Config(0, 5, 100000, Digestion::FullySpecific, false));
    vector<Peptide> funkyPeptides(funkyDigestion.begin(), funkyDigestion.end());

    unit_assert_operator_equal("MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPFDEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEPERNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYYANKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVARLSQKFPKAE", funkyPeptides[0].sequence());
    unit_assert_operator_equal("FVEVTKLVTDLTKVHKECCHGDLLECADDRADLAKYICDNQDTISSKLKECCDKPLLEKSHCIAEVEKDAIPENLPPLTAD", funkyPeptides[1].sequence());
    unit_assert_operator_equal("FAEDKDVCKNYQEAKDAFLGSFLYEYSRRHPEYAVSVLLRLAKEYEATLEECCAKDDPHACYSTVFDKLKHLVDEPQNLIKQNCDQFEKLGEYGFQNALIVRYTRKVPQVSTPTLVEVSRSLGKVGTRCCTKPESERMPCTEDYLSLILNRLCVLHEKTPVSEKVTKCCTESLVNRRPCFSALTPDETYVPKAFDEKLFTFHADICTLPDTEKQIKKQTALVELLKHKPKATEEQLKTVMENFVAFVDKCCAADDKEACFAVEGPKLVVSTQTALA", funkyPeptides[2].sequence());

    // test fully specific Asp-N digest (thus testing ambiguous residue disambiguation)
    Digestion aspnDigestion(bsa, MS_Asp_N, Digestion::Config(0, 5, 100000, Digestion::FullySpecific, false));
    vector<Peptide> aspnPeptides(aspnDigestion.begin(), aspnDigestion.end());
    unit_assert_operator_equal("MKWVTFISLLLLFSSAYSRGVFRR", aspnPeptides[0].sequence());
    unit_assert_operator_equal("DTHKSEIAHRFK", aspnPeptides[1].sequence());
    unit_assert_operator_equal("DLGEEHFKGLVLIAFSQYLQQCPF", aspnPeptides[2].sequence());
    unit_assert_operator_equal("DEHVKLV", aspnPeptides[3].sequence());
    unit_assert_operator_equal("NELTEFAKTCVA", aspnPeptides[4].sequence());

    // test no cleavage "digestion"
    Digestion noCleavageDigestion("ELVISLIVESK", MS_no_cleavage);
    vector<Peptide> noCleavagePeptides(noCleavageDigestion.begin(), noCleavageDigestion.end());

    unit_assert_operator_equal(1, noCleavagePeptides.size());
    unit_assert_operator_equal("ELVISLIVESK", noCleavagePeptides[0].sequence());

    // test unspecific cleavage digestion
    Digestion unspecificCleavageDigestion("ELVISLK", MS_unspecific_cleavage, Digestion::Config(0, 5, 5, Digestion::FullySpecific, false));
    vector<Peptide> unspecificCleavagePeptides(unspecificCleavageDigestion.begin(), unspecificCleavageDigestion.end());

    unit_assert_operator_equal(3, unspecificCleavagePeptides.size());
    unit_assert_operator_equal("ELVIS", unspecificCleavagePeptides[0].sequence());
    unit_assert_operator_equal("LVISL", unspecificCleavagePeptides[1].sequence());
    unit_assert_operator_equal("VISLK", unspecificCleavagePeptides[2].sequence());
}

void testDigestionCriteria()
{
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

    for (const DigestedPeptide& peptide : Digestion(bsa, MS_Trypsin_P, Digestion::Config(3, 5, 40)))
    {
        unit_assert(peptide.missedCleavages() <= 3);
        unit_assert(peptide.sequence().length() >= 5);
        unit_assert(peptide.sequence().length() <= 40);
    }

    for (const DigestedPeptide& peptide : Digestion(bsa, MS_Trypsin_P, Digestion::Config(0, 3, 10)))
    {
        unit_assert(peptide.missedCleavages() <= 0);
        unit_assert(peptide.sequence().length() >= 3);
        unit_assert(peptide.sequence().length() <= 10);
    }

    for (const DigestedPeptide& peptide : Digestion(bsa, MS_Trypsin_P, Digestion::Config(10, 25, 100)))
    {
        unit_assert(peptide.missedCleavages() <= 10);
        unit_assert(peptide.sequence().length() >= 25);
        unit_assert(peptide.sequence().length() <= 100);
    }

    unit_assert_operator_equal(0, boost::distance(Digestion(bsa, MS_Trypsin_P, Digestion::Config(10, 1000, 2000))));
}


void testFind()
{
    Digestion fully("PEPKTIDEKPEPTIDERPEPKTIDEKKKPEPTIDER", MS_Lys_C_P, Digestion::Config(2, 5, 10));
    Digestion semi("PEPKTIDEKPEPTIDERPEPKTIDEKKKPEPTIDER", MS_Lys_C_P, Digestion::Config(2, 5, 10, Digestion::SemiSpecific));
    Digestion non("PEPKTIDEKPEPTIDERPEPKTIDEKKKPEPTIDER", MS_Lys_C_P, Digestion::Config(2, 5, 10, Digestion::NonSpecific));
    Digestion clipped("MPEPKTIDEKPEPTIDERPEPKTIDEKKKPEPTIDER", MS_Lys_C_P, Digestion::Config(2, 5, 10));

    // test find_all
    unit_assert(fully.find_all("ABC").empty()); // not in peptide
    unit_assert(fully.find_all("PEPK").empty()); // too short
    unit_assert(fully.find_all("PEPKTIDEKK").empty()); // no N-terminal cleavage
    unit_assert(fully.find_all("PEPTIDERPEPK").empty()); // too long
    unit_assert(fully.find_all("PEPTIDERPEPTIDEK").empty()); // too long
    unit_assert(semi.find_all("PEPKTIDEKKK").empty()); // too many missed cleavages
    unit_assert(semi.find_all("EPKTIDE").empty()); // no specific termini
    unit_assert(non.find_all("EPKTIDEKKK").empty()); // too many missed cleavages

    unit_assert(fully.find_all("PEPKTIDEK").size() == 1);
    unit_assert(testDigestionMetadata(fully.find_all("PEPKTIDEK")[0], "PEPKTIDEK", 0, 1, 2, "", "P"));

    unit_assert(fully.find_all("TIDEK").size() == 2);
    unit_assert(testDigestionMetadata(fully.find_all("TIDEK")[0], "TIDEK", 4, 0, 2, "K", "P"));
    unit_assert(testDigestionMetadata(fully.find_all("TIDEK")[1], "TIDEK", 21, 0, 2, "K", "K"));

    unit_assert(fully.find_all("TIDEKK").size() == 1);
    unit_assert(testDigestionMetadata(fully.find_all("TIDEKK")[0], "TIDEKK", 21, 1, 2, "K", "K"));

    unit_assert(fully.find_all("TIDEKKK").size() == 1);
    unit_assert(testDigestionMetadata(fully.find_all("TIDEKKK")[0], "TIDEKKK", 21, 2, 2, "K", "P"));

    unit_assert(fully.find_all("PEPTIDER").size() == 1);
    unit_assert(testDigestionMetadata(fully.find_all("PEPTIDER")[0], "PEPTIDER", 28, 0, 2, "K", ""));

    unit_assert(semi.find_all("PEPKTIDEKK").size() == 1);
    unit_assert(testDigestionMetadata(semi.find_all("PEPKTIDEKK")[0], "PEPKTIDEKK", 17, 2, 1, "R", "K"));

    unit_assert(semi.find_all("EPKTIDEKK").size() == 1);
    unit_assert(testDigestionMetadata(semi.find_all("EPKTIDEKK")[0], "EPKTIDEKK", 18, 2, 1, "P", "K"));

    unit_assert(non.find_all("PEPKTIDE").size() == 2);
    unit_assert(testDigestionMetadata(non.find_all("PEPKTIDE")[0], "PEPKTIDE", 0, 1, 1, "", "K"));
    unit_assert(testDigestionMetadata(non.find_all("PEPKTIDE")[1], "PEPKTIDE", 17, 1, 0, "R", "K"));

    unit_assert(fully.find_all("EPKTIDEK").empty()); // N-terminal 'P' is not clipped
    unit_assert(clipped.find_all("PEPKTIDEK").size() == 1); // N-terminal 'M' is clipped
    unit_assert(testDigestionMetadata(clipped.find_all("PEPKTIDEK")[0], "PEPKTIDEK", 1, 1, 2, "M", "P"));

    // test find_first
    unit_assert_throws(fully.find_first("ABC"), runtime_error); // not in peptide
    unit_assert_throws(fully.find_first("PEPK"), runtime_error); // too short
    unit_assert_throws(fully.find_first("PEPKTIDEKK"), runtime_error); // no N-terminal cleavage
    unit_assert_throws(fully.find_first("PEPTIDERPEPK"), runtime_error); // too long
    unit_assert_throws(fully.find_first("PEPTIDERPEPTIDEK"), runtime_error); // too long
    unit_assert_throws(semi.find_first("EPKTIDE"), runtime_error); // no specific termini
    unit_assert_throws(semi.find_first("PEPKTIDEKKK"), runtime_error); // too many missed cleavages
    unit_assert_throws(non.find_first("PEPKTIDEKKK"), runtime_error); // too many missed cleavages

    unit_assert(testDigestionMetadata(fully.find_first("PEPKTIDEK"), "PEPKTIDEK", 0, 1, 2, "", "P"));
    unit_assert(testDigestionMetadata(fully.find_first("PEPKTIDEK", 4242), "PEPKTIDEK", 0, 1, 2, "", "P"));

    unit_assert(testDigestionMetadata(fully.find_first("TIDEK"), "TIDEK", 4, 0, 2, "K", "P"));
    unit_assert(testDigestionMetadata(fully.find_first("TIDEK", 4242), "TIDEK", 4, 0, 2, "K", "P"));
    unit_assert(testDigestionMetadata(fully.find_first("TIDEK", 15), "TIDEK", 21, 0, 2, "K", "K"));
    unit_assert(testDigestionMetadata(fully.find_first("TIDEK", 21), "TIDEK", 21, 0, 2, "K", "K"));

    unit_assert(testDigestionMetadata(fully.find_first("TIDEKK"), "TIDEKK", 21, 1, 2, "K", "K"));
    unit_assert(testDigestionMetadata(fully.find_first("TIDEKKK"), "TIDEKKK", 21, 2, 2, "K", "P"));
    unit_assert(testDigestionMetadata(fully.find_first("PEPTIDER"), "PEPTIDER", 28, 0, 2, "K", ""));

    unit_assert(testDigestionMetadata(semi.find_first("IDEKK"), "IDEKK", 22, 1, 1, "T", "K"));
    unit_assert(testDigestionMetadata(semi.find_first("IDEKKK"), "IDEKKK", 22, 2, 1, "T", "P"));
    unit_assert(testDigestionMetadata(semi.find_first("PEPTIDER"), "PEPTIDER", 9, 0, 1, "K", "P"));
    unit_assert(testDigestionMetadata(semi.find_first("PEPTIDER", 28), "PEPTIDER", 28, 0, 2, "K", ""));

    unit_assert(testDigestionMetadata(non.find_first("EPTIDE"), "EPTIDE", 10, 0, 0, "P", "R"));
    unit_assert(testDigestionMetadata(non.find_first("EPTIDE", 29), "EPTIDE", 29, 0, 0, "P", "R"));
}


struct ThreadStatus
{
    boost::exception_ptr exception;

    ThreadStatus() {}
    ThreadStatus(const boost::exception_ptr& e) : exception(e) {}
};


void testThreadSafetyWorker(boost::barrier* testBarrier, ThreadStatus& status)
{
    testBarrier->wait(); // wait until all threads have started

    try
    {
        testCleavageAgents();
        testBSADigestion();
        testDigestionCriteria();
        testFind();
    }
    catch (exception& e)
    {
        status.exception = boost::copy_exception(runtime_error(e.what()));
    }
    catch (...)
    {
        status.exception = boost::copy_exception(runtime_error("Unhandled exception in worker thread."));
    }
}

void testThreadSafety(const int& testThreadCount)
{
    using boost::thread;

    boost::barrier testBarrier(testThreadCount);
    list<pair<boost::shared_ptr<thread>, ThreadStatus> > threads;
    for (int i=0; i < testThreadCount; ++i)
    {
        threads.push_back(make_pair(boost::shared_ptr<thread>(), ThreadStatus()));
        threads.back().first.reset(new thread(testThreadSafetyWorker, &testBarrier, boost::ref(threads.back().second)));
    }
    
    set<boost::shared_ptr<thread> > finishedThreads;
    while (finishedThreads.size() < threads.size())
        BOOST_FOREACH_FIELD((boost::shared_ptr<thread>& t)(ThreadStatus& status), threads)
        {
            if (t->timed_join(boost::posix_time::seconds(1)))
                finishedThreads.insert(t);

            if (status.exception != NULL) // non-null exception?
                boost::rethrow_exception(status.exception);
        }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "DigestionTest\n";

    try
    {
        //testThreadSafety(1); // does not test thread-safety of singleton initialization
        testThreadSafety(2);
        testThreadSafety(4);
        //testThreadSafety(8);
        //testThreadSafety(16); // high thread count fails non-deterministically on MSVC; I haven't been able to find the cause.
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
