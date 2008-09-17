//
// DigestionTest.cpp
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
#include "utility/misc/unit.hpp"
#include "utility/misc/String.hpp"
#include <iostream>
#include <iterator>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void test()
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

    Digestion trypticDigestion(bsa, ProteolyticEnzyme_Trypsin, Digestion::Config(3, 5, 40));
    vector<Peptide> trypticPeptides(trypticDigestion.begin(), trypticDigestion.end());

    // test order of enumeration and trypticPeptides at the N terminus
    unit_assert(trypticPeptides[0].sequence() == "MKWVTFISLLLLFSSAYSR");
    unit_assert(trypticPeptides[1].sequence() == "MKWVTFISLLLLFSSAYSRGVFR");
    unit_assert(trypticPeptides[2].sequence() == "MKWVTFISLLLLFSSAYSRGVFRR");

    // test for non-tryptic trypticPeptides
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "MKWVTFISLLLL"));
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "STQTALA"));

    // test some middle trypticPeptides
    unit_assert(count(trypticPeptides.begin(), trypticPeptides.end(), "RDTHKSEIAHRFK"));
    unit_assert(count(trypticPeptides.begin(), trypticPeptides.end(), "DTHKSEIAHRFK"));

    // test trypticPeptides at the C terminus
    unit_assert(count(trypticPeptides.begin(), trypticPeptides.end(), "EACFAVEGPKLVVSTQTALA"));
    unit_assert(trypticPeptides.back().sequence() == "LVVSTQTALA");

    // test maximum missed cleavages
    unit_assert(trypticPeptides[3].sequence() != "MKWVTFISLLLLFSSAYSRGVFRRDTHK");
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "LKPDPNTLCDEFKADEKKFWGKYLYEIARR"));

    // test minimum peptide length
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "LR"));
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "QRLR"));
    unit_assert(count(trypticPeptides.begin(), trypticPeptides.end(), "VLASSARQRLR"));

    // test maximum peptide length
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), "MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFK"));
    unit_assert(!count(trypticPeptides.begin(), trypticPeptides.end(), bsa.sequence()));

    //for (Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr)
    //    cout << itr->sequence() << endl;


    // test funky digestion
    Digestion funkyDigestion(bsa, "A[DE]|[FG]", Digestion::Config(0));
    vector<Peptide> funkyPeptides(funkyDigestion.begin(), funkyDigestion.end());

    unit_assert(funkyPeptides[0].sequence() == "MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPFDEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEPERNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYYANKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVARLSQKFPKAE");
    unit_assert(funkyPeptides[1].sequence() == "FVEVTKLVTDLTKVHKECCHGDLLECADDRADLAKYICDNQDTISSKLKECCDKPLLEKSHCIAEVEKDAIPENLPPLTAD");
    unit_assert(funkyPeptides[2].sequence() == "FAEDKDVCKNYQEAKDAFLGSFLYEYSRRHPEYAVSVLLRLAKEYEATLEECCAKDDPHACYSTVFDKLKHLVDEPQNLIKQNCDQFEKLGEYGFQNALIVRYTRKVPQVSTPTLVEVSRSLGKVGTRCCTKPESERMPCTEDYLSLILNRLCVLHEKTPVSEKVTKCCTESLVNRRPCFSALTPDETYVPKAFDEKLFTFHADICTLPDTEKQIKKQTALVELLKHKPKATEEQLKTVMENFVAFVDKCCAADDKEACFAVEGPKLVVSTQTALA");
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "DigestionTest\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
