//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "shared_types.h"
#include "shared_defs.h"
#include "shared_funcs.h"
#include "pwiz/utility/misc/unit.hpp"
#include "float.h"
#include "SubsetEnumerator.h"
#include "PTMVariantList.h"
#include "proteinStore.h"
#include "AhoCorasickTrie.hpp"
#include <iostream>
#include <fstream>
#include <string>
#include <math.h>

using namespace pwiz::util;

ostream* os_ = 0;

struct DigestedPeptideLessThan
{
    bool operator() (const DigestedPeptide& lhs, const DigestedPeptide& rhs) const
    {
        return lhs.sequence() < rhs.sequence();
    }
};

namespace std
{
    ostream& operator<< (ostream& os, const Modification& rhs)
    {
        return os << "[" << rhs.monoisotopicDeltaMass() << "]";
    }

    ostream& operator<< (ostream& os, const Peptide& rhs)
    {
        string sequence = rhs.sequence();
        const ModificationMap& mods = rhs.modifications();
        for (ModificationMap::const_reverse_iterator itr = mods.rbegin(); itr != mods.rend(); ++itr)
            for (size_t i=0; i < itr->second.size(); ++i)
                sequence.insert(itr->first+1, lexical_cast<string>(itr->second[i]));
        return os << sequence;
    }

    ostream& operator<< (ostream& os, const ModificationMap& rhs)
    {
        for (ModificationMap::const_reverse_iterator itr = rhs.rbegin(); itr != rhs.rend(); ++itr)
            for (size_t i = 0; i < itr->second.size(); ++i)
                os << lexical_cast<string>(itr->second[i]) << "@" << itr->first;
        return os;
    }
}

void testMakePtmVariants()
{
    if (os_) cout << CustomBaseNumber(1, 2).str() << endl;
    if (os_) cout << CustomBaseNumber(2, 2).str() << endl;
    if (os_) cout << CustomBaseNumber(3, 2).str() << endl;
    if (os_) cout << CustomBaseNumber(4, 2).str() << endl;

    unit_assert(CustomBaseNumber(1, 2).str() == "1");
    unit_assert(CustomBaseNumber(2, 2).str() == "10");
    unit_assert(CustomBaseNumber(3, 2).str() == "11");
    unit_assert(CustomBaseNumber(4, 2).str() == "100");

    if (os_) cout << CustomBaseNumber(2, 3).str() << endl;
    if (os_) cout << CustomBaseNumber(3, 3).str() << endl;
    if (os_) cout << CustomBaseNumber(8, 3).str() << endl;
    if (os_) cout << CustomBaseNumber(9, 3).str() << endl;

    unit_assert(CustomBaseNumber(2, 3).str() == "2");
    unit_assert(CustomBaseNumber(3, 3).str() == "10");
    unit_assert(CustomBaseNumber(8, 3).str() == "22");
    unit_assert(CustomBaseNumber(9, 3).str() == "100");

    if (os_) cout << CustomBaseNumber(1, 10).str() << endl;
    if (os_) cout << CustomBaseNumber(22, 10).str() << endl;
    if (os_) cout << CustomBaseNumber(333, 10).str() << endl;
    if (os_) cout << CustomBaseNumber(4444, 10).str() << endl;

    unit_assert(CustomBaseNumber(1, 10).str() == "1");
    unit_assert(CustomBaseNumber(22, 10).str() == "22");
    unit_assert(CustomBaseNumber(333, 10).str() == "333");
    unit_assert(CustomBaseNumber(4444, 10).str() == "4444");

    unit_assert((CustomBaseNumber(4444, 10) << 1).str() == "4440");
    unit_assert((CustomBaseNumber(4444, 10) << 2).str() == "4400");
    unit_assert((CustomBaseNumber(4444, 10) << 4).str() == "0000");
    unit_assert((CustomBaseNumber(4444, 10) << 5).str() == "0000");

    unit_assert((CustomBaseNumber(4444, 10) >> 1).str() == "0444");
    unit_assert((CustomBaseNumber(4444, 10) >> 2).str() == "0044");
    unit_assert((CustomBaseNumber(4444, 10) >> 4).str() == "0000");
    unit_assert((CustomBaseNumber(4444, 10) >> 5).str() == "0000");

    unit_assert((++CustomBaseNumber(1, 2)).str() == "10");
    unit_assert((++CustomBaseNumber(2, 2)).str() == "11");
    unit_assert((++CustomBaseNumber(3, 2)).str() == "100");
    unit_assert((++CustomBaseNumber(4, 2)).str() == "101");

    unit_assert((++CustomBaseNumber(2, 3)).str() == "10");
    unit_assert((++CustomBaseNumber(3, 3)).str() == "11");
    unit_assert((++CustomBaseNumber(4, 3)).str() == "12");
    unit_assert((++CustomBaseNumber(5, 3)).str() == "20");

    unit_assert((++CustomBaseNumber(1, 10)).str() == "2");
    unit_assert((++CustomBaseNumber(2, 10)).str() == "3");
    unit_assert((++CustomBaseNumber(5, 10)).str() == "6");
    unit_assert((++CustomBaseNumber(9, 10)).str() == "10");

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

    typedef multiset<DigestedPeptide, DigestedPeptideLessThan> DigestedPeptideSet;
    DigestedPeptideSet::const_iterator peptideItr;

    DynamicModSet dynamicMods("[KR] * 87");
    StaticModSet staticMods;

    INIT_PROFILERS(2);

    Digestion digestion(bsa, pwiz::cv::MS_Trypsin_P, Digestion::Config(1, 5, 40));

    vector<DigestedPeptide> peptides;
    for (Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr) {
        START_PROFILER(0);
        MakePtmVariants(*itr, peptides, 2, dynamicMods, staticMods, 5000);
        STOP_PROFILER(0);
        //START_PROFILER(1);
        //MakePtmVariants(*itr, peptides, 2, dynamicMods, staticMods);
        //STOP_PROFILER(1);
    }
    PRINT_PROFILERS(cout,"blah");

    DigestedPeptide longPeptide("LVCAYDPEAASAPGSGNPCHEASAAQKENAGEDPGLARQAPKPRKQRSSLLEKGLDGAKKAVGGLGKLGKDAVEDLESVGKGAVHDVKDVLDSVL");
    DynamicModSet mod("[LVCAYDPESGNHEQKIR] . 174.896");
    vector<DigestedPeptide> longPeps;
    MakePeptideVariants(longPeptide,longPeps,1,1,mod,35,96);

    //exit(1);
    DigestedPeptideSet peptideSet(peptides.begin(), peptides.end());

    unit_assert(peptideSet.count("MKWVTFISLLLLFSSAYSR")); // N terminal
    unit_assert(peptideSet.count("KWVTFISLLLLFSSAYSR")); // clipped M at N terminus
    unit_assert(peptideSet.count("LVVSTQTALA")); // C terminal

    DigestedPeptide testPeptide("MKWVTFISLLLLFSSAYSR");
    boost::iterator_range<DigestedPeptideSet::const_iterator> range(boost::make_iterator_range(peptideSet.equal_range(testPeptide)));
    unit_assert(std::distance(range.begin(), range.end()) == 4); // four PTM permutations

    DigestedPeptideSet::const_iterator modifiedPeptide = range.begin();

    unit_assert(modifiedPeptide->modifications().empty());

    ++modifiedPeptide;
    unit_assert(modifiedPeptide->modifications().size() == 1);
    unit_assert(modifiedPeptide->modifications().count(18));
    unit_assert(modifiedPeptide->modifications().find(18)->second[0].monoisotopicDeltaMass() == 87);
    unit_assert(modifiedPeptide->monoisotopicMass(0, false) + 87 == modifiedPeptide->monoisotopicMass(0, true));

    ++modifiedPeptide;
    unit_assert(modifiedPeptide->modifications().size() == 2);
    unit_assert(modifiedPeptide->modifications().count(1));
    unit_assert(modifiedPeptide->modifications().find(1)->second[0].monoisotopicDeltaMass() == 87);
    unit_assert(modifiedPeptide->modifications().count(18));
    unit_assert(modifiedPeptide->modifications().find(18)->second[0].monoisotopicDeltaMass() == 87);
    unit_assert(modifiedPeptide->monoisotopicMass(0, false) + (87 * 2) == modifiedPeptide->monoisotopicMass(0, true));

    ++modifiedPeptide;
    unit_assert(modifiedPeptide->modifications().size() == 1);
    unit_assert(modifiedPeptide->modifications().count(1));
    unit_assert(modifiedPeptide->modifications().find(1)->second[0].monoisotopicDeltaMass() == 87);
    unit_assert(modifiedPeptide->monoisotopicMass(0, false) + 87 == modifiedPeptide->monoisotopicMass(0, true));

    /*{
    vector<DigestedPeptide> crazyPeptides;
    MakePtmVariants("KRKRKR", crazyPeptides, 6, crazyMods, staticMods);
    if (os_) *os_ << crazyPeptides << endl;
    unit_assert(crazyPeptides.size() == (size_t) pow(4.0, 6.0));
    }*/


}

void testPeptideComparators()
{
    Peptide testPeptide1("GLVPR");
    Peptide testPeptide2("GLVPR");
    unit_assert_operator_equal(testPeptide1, testPeptide1);
    unit_assert_operator_equal(testPeptide1, testPeptide2);
    unit_assert(!(testPeptide1<testPeptide1));
    unit_assert(!(testPeptide2<testPeptide1));
    unit_assert(!(testPeptide1<testPeptide2));

    Peptide testPeptide3("LVGPR");
    unit_assert(testPeptide2<testPeptide3);
    unit_assert(!(testPeptide3<testPeptide2));
}

void testEnumeration()
{
    vector<size_t> pos;
    pos.push_back(10);
    pos.push_back(8);

    SubsetEnumerator enumerator(2,1,2,pos);
    do {
        enumerator.print_set();
    } while(enumerator.next());
}



/*void testPreferredDeltaMasses()
{
    PreferredDeltaMassesList pdm("[STY] 79.996 M 15.996 P -77.5", 2);
    unit_assert(pdm.size()==15);
    DigestedPeptide peptide("QADTDNYKMNVSMK");
    size_t minCombin, maxCombin;
    DynamicModSet mods = pdm.getMatchingMassShifts(160.1f,1.25f);
    vector<DigestedPeptide> variants;
    MakePeptideVariants(peptide,variants,1,1,mods,1,peptide.sequence().length());
    unit_assert(std::distance(variants.begin(), variants.end()) == 3); // three PTM permutations
    unit_assert((pdm.containsMassShift(80.1f,1.25f)==true)); // Membership tests
    unit_assert((pdm.containsMassShift(160.1f,1.25f)==true));
    unit_assert((pdm.containsMassShift(168.1f,1.25f)==false));
}*/

void testTerminalModMassChecks()
{
    Peptide bsa("MKWVTFISSAYSRGVFRRDTHKAVTKYTSSKSSL");

    Digestion::Specificity specificity = (Digestion::Specificity) 1;
    Digestion::Config digestionConfig = Digestion::Config( 2, 8, 100, specificity );
    Digestion digestion(bsa, pwiz::cv::MS_Trypsin_P, digestionConfig);
    vector<DigestedPeptide> peptides;
    for (Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr)
    {
        if((*itr).sequence()=="WVTFISSAYSR")
        {
            float modMass = 259.1f;
            bool legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, NTERM);
            unit_assert((legitMass == false));
            modMass = 250.1f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, NTERM);
            unit_assert((legitMass == true));
            modMass = -285.1f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, NTERM);
            unit_assert((legitMass == false));
            modMass = -280.1f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, NTERM);
            unit_assert((legitMass == true));
        }
        if((*itr).sequence()=="MKWVTFISSAYSR")
        {
            float modMass = 156.f;
            bool legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, CTERM);
            unit_assert((legitMass == false));
            modMass = 154.f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, CTERM);
            unit_assert((legitMass == true));
            modMass = -156.1f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, CTERM);
            unit_assert((legitMass == false));
            modMass = -154.1f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, CTERM);
            unit_assert((legitMass == true));

            modMass = 154.f;
            legitMass = checkForTerminalErrors(bsa, (*itr), modMass, 1.25f, NTERM);
            unit_assert((legitMass == true));
        }
        if((*itr).sequence()=="VTKYTSSK")
        {
            float modMass = 71.04f;
            bool legitMass = checkForTerminalErrors(bsa, (*itr), modMass, max(NEUTRON,0.5), NTERM);
            cout << (*itr).sequence() << "," << modMass << "," << legitMass << endl;
        }
    }   
    
}

void testNewPTM() {

    StaticModSet staticMods;
    DigestedPeptide testPeptide("QAKDRDNYKMNVLMK");
    DynamicModSet crazyMods("M * 15.996 (Q @ -17.03 ( ^ 42.01 C & 57.02");
    {
        PTMVariantList iter(testPeptide, 2, crazyMods, staticMods, 100000);
        size_t numCandidates = 0;
        do {
            ++numCandidates;
            //cout << getInterpretation(iter.ptmVariant) << endl;
        } while (iter.next());
        unit_assert_operator_equal(11, numCandidates);
    }

    DigestedPeptide testPeptide2("AKDRDNCYKMNVLMK");
    DynamicModSet crazyMods2("M * 15.996 ( ^ 42.01 C & 57.02");
    {
        PTMVariantList iter(testPeptide2, 2, crazyMods2, staticMods, 100000);
        size_t numCandidates = 0;
        do {
            ++numCandidates;
            //cout << getInterpretation(iter.ptmVariant) << endl;
        } while (iter.next());
        unit_assert_operator_equal(11, numCandidates);
    }

    {
        PTMVariantList iter(testPeptide2, 2, crazyMods2, staticMods, 100000);
        size_t numCandidates = 0;
        while(iter.nextWithoutStaticPeptide()) {
            ++numCandidates;
            //cout << getInterpretation(iter.ptmVariant) << endl;
        }
        unit_assert_operator_equal(10, numCandidates);
    }

    {
        PTMVariantList iter(testPeptide, 0, crazyMods, staticMods, 100000);
        size_t numCandidates = 0;
        do {
            ++numCandidates;
            //cout << getInterpretation(iter.ptmVariant) << endl;
        } while (iter.next());
        unit_assert_operator_equal(1, numCandidates);
    }
    
    {
        PTMVariantList iter(testPeptide2, 2, crazyMods2, staticMods, 100000);
        vector<DigestedPeptide> variants;
        iter.getVariantsAsList(variants,false);
        //for(int i = 0; i < variants.size(); ++i)
        //    cout << getInterpretation(variants[i]) << endl;
        unit_assert_operator_equal(10, variants.size());
    }

    {
        PTMVariantList iter(testPeptide2, 2, crazyMods2, staticMods, 100000);
        vector<DigestedPeptide> variants;
        iter.getVariantsAsList(variants);
        //for(int i = 0; i < variants.size(); ++i)
        //    cout << getInterpretation(variants[i]) << endl;
        unit_assert_operator_equal(11, variants.size());
    }
}


void testCleavageRuleSetAsRegex()
{
    CleavageRuleSet rules;
    
    rules.initialize("[|K|R . . ]");
    cout << "\"[|K|R . . ]\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "(?<=[KR])");

    rules.initialize("K|R A|C|D|E|F|G|H|I|K|L|M|N|Q|R|S|T|V|W|Y");
    cout << "\"K|R A|C|D|E|F|G|H|I|K|L|M|N|Q|R|S|T|V|W|Y\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "(?<=[KR])(?=[ACDEFGHIKLMNQRSTVWY])");

    rules.initialize("K .");
    cout << "\"K .\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "(?<=K)");

    rules.initialize(". B|D");
    cout << "\". B|D\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "(?=[BD])");

    rules.initialize("D . . D");
    cout << "\"D . . D\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "((?<=D))|((?=D))");

    rules.initialize("[M . . M]");
    cout << "\"[M . . M]\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "((?<=^M))|((?=M$))");

    rules.initialize("[M|K|R . . ]");
    cout << "\"[M|K|R . . ]\": " << rules.asCleavageAgentRegex() << endl;
    unit_assert(rules.asCleavageAgentRegex() == "(?<=(^M)|([KR]))");//"((?<=^M))|((?<=[KR]))");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "SharedTests\n";
        testMakePtmVariants();
        testPeptideComparators();
        testCleavageRuleSetAsRegex();
        testNewPTM();
        testEnumeration();
        //testPreferredDeltaMasses();
        testTerminalModMassChecks();        
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
