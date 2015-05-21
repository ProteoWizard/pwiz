//
// $Id$ 
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
// Copyright 2011 Vanderbilt University
//
// Licensed under the Code Project Open License, Version 1.02 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.codeproject.com/info/cpol10.aspx
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "AhoCorasickTrie.hpp"

using namespace pwiz::util;
using namespace freicore;

const char* text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec quis purus nec diam "
                   "sollicitudin posuere. Sed molestie tincidunt velit elementum tincidunt. Curabitur a "
                   "mauris vitae nisl vulputate blandit in in dolor. Nam vel tincidunt massa. Quisque nunc "
                   "nisl, commodo ac consectetur non, venenatis ac dolor. Vestibulum ante ipsum primis in "
                   "faucibus orci luctus et ultrices posuere cubilia Curae; Nulla id sagittis magna. "
                   "Pellentesque velit nibh, pellentesque at faucibus sollicitudin, iaculis vel massa. "
                   "Ut pharetra, massa at aliquet ultrices, nulla massa lobortis elit, non scelerisque est "
                   "nunc sed tellus. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec magna "
                   "purus, facilisis nec pulvinar id, sagittis vel dolor. Pellentesque in neque eu ipsum "
                   "mollis sodales.";

const char* testKeywords[] =
{
    // 4        5        4        2         1        5    (occurrences)
    "massa", "dolor", "ipsum", "purus", "sodales", "elit"
};

const size_t testKeywordsSize = sizeof(testKeywords) / sizeof(const char*);


struct AminoAcidTranslator
{
    static int size() {return 26;}
    static int translate(char aa) {return aa - 'A';};
    static char translate(int index) {return static_cast<char>(index) + 'A';}
};

const char* protein = "MAKKTAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSD"
                      "MKHWPFQVVNDGDKPKVQVNYKGENRSFYPEEISSMVLTKMKEIAEAYLGHPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRI"
                      "INEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVSHFVEEFKRKHKKDISQNKR"
                      "AVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRGTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIP"
                      "KVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQTFTT"
                      "YSDNQPGVLIQVYEGERAMTRDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIE"
                      "RMVQEAERYKAEDEVQRERVAAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDSNTLAEKEEFVHKREELERV"
                      "CNPIISGLYQGAGAPGAGGFGAQAPKGGSGSGPTIEEVD";

const char* testPeptides[] =
{
    "MAKKTAIGIDLGTTYSCVGVFQHGK", // 0
    "ALRDAKLDK", // 319
    "GGSGSGPTIEEVD", // 628
    "LLL", // 348, 390, 391, 392, 454
    "LLD",
    "EEF" // 242, 589    
};

const size_t testPeptidesSize = sizeof(testPeptides) / sizeof(const char*);


void test()
{
    {
        typedef AhoCorasickTrie<> ascii_trie;
        typedef boost::shared_ptr<string> shared_string;
        vector<shared_string> keywords;
        BOOST_FOREACH(const char* keyword, boost::make_iterator_range(testKeywords, testKeywords + testKeywordsSize))
            keywords.push_back(shared_string(new string(keyword)));

        ascii_trie trie(keywords.begin(), keywords.end());
        //unit_assert(trie.find_all(text).size() == 21);

        ascii_trie::SearchResult result = trie.find_first(text);
        //unit_assert(*result.keyword() == "ipsum");
        //unit_assert(result.offset() == 6);

        trie.insert(shared_string(new string("Lorem"))); // 2 occurrences
        result = trie.find_first(text);
        //unit_assert(*result.keyword() == "Lorem");
        //unit_assert(result.offset() == 0);
        //unit_assert(trie.find_all(text).size() == 23);

        string decoyText = "A string in plain English!";
        ascii_trie::SearchResult emptyResult = trie.find_first(decoyText);
        //unit_assert(emptyResult.offset() == decoyText.length());
        //unit_assert(!emptyResult.keyword().get());
        //unit_assert(trie.find_all(decoyText).empty());
    }

    {
        typedef AhoCorasickTrie<AminoAcidTranslator, std::string> peptide_trie;
        typedef boost::shared_ptr<string> shared_string;
        vector<shared_string> peptides;
        BOOST_FOREACH(const char* peptide, boost::make_iterator_range(testPeptides, testPeptides + testPeptidesSize))
            peptides.push_back(shared_string(new string(peptide)));

        peptide_trie trie(peptides.begin(), peptides.end());
        BOOST_FOREACH(const peptide_trie::SearchResult& result, trie.find_all(protein))
            cout << result.offset() << "," << (*result.keyword()) << endl;
        unit_assert(trie.find_all(protein).size() == 8);
            
        unit_assert(trie.find_first(protein).offset() == 0);
        unit_assert(*trie.find_first(protein).keyword() == "MAKKTAIGIDLGTTYSCVGVFQHGK");

        vector<peptide_trie::SearchResult> results = trie.find_all(protein);
        unit_assert(results[0].offset() == 0);
        unit_assert(*results[0].keyword() == "MAKKTAIGIDLGTTYSCVGVFQHGK");
        unit_assert(results[1].offset() == 242);
        unit_assert(*results[1].keyword() == "EEF");
        unit_assert(results[2].offset() == 319);
        unit_assert(*results[2].keyword() == "ALRDAKLDK");
        unit_assert(results[3].offset() == 390);
        unit_assert(*results[3].keyword() == "LLL");
        unit_assert(results[4].offset() == 391);
        unit_assert(*results[4].keyword() == "LLL");
        unit_assert(results[5].offset() == 392);
        unit_assert(*results[5].keyword() == "LLD");

        // test that the shared_ptr is the same
        unit_assert(results[3].keyword() == results[4].keyword());

        // test that non-alphabet characters throw an exception
        unit_assert_throws(trie.find_all("THISISN*T A VALIDPR*TEIN"), out_of_range);
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
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
