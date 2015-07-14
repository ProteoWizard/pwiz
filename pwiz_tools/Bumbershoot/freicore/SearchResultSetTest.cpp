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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "SearchResultSet.h"
#include <boost/foreach.hpp>


using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace freicore;


struct TestSearchResult
{
    int score;
    int expectedRank;
    const char* sequence;
    bool nTerminusIsSpecific;
    bool cTerminusIsSpecific;
    bool isDecoy;
};

const TestSearchResult testSearchResults[] =
{
    {25, 1, "THEQUICKRWNFX", true, true, false},
    {15, 2, "UMPSVERTHELZYDG", true, true, false},
    {12, 3, "THEQUICQRWNFX", true, false, true},
    {10, 4, "SHESELLS", false, true, false},
    {10, 4, "SHESEIIS", false, true, false},
    {5,  5, "SEASHELLS", false, true, false},
    {5,  5, "SEASHELIS", false, false, true},
    {5,  5, "SEASHEIIS", true, true, true},
    {3,  6, "BYTHESEA", false, false, true},
    {1,  7, "SHRE", false, false, false}
};

const size_t testSearchResultsSize = sizeof(testSearchResults) / sizeof(TestSearchResult);


struct SearchResult : public BaseSearchResult
{
    SearchResult(const TestSearchResult& result)
    : BaseSearchResult(DigestedPeptide(Peptide(result.sequence, ModificationParsing_Auto),
                                       0, 0,
                                       result.nTerminusIsSpecific,
                                       result.cTerminusIsSpecific)),
      score(result.score),
      _isDecoy(result.isDecoy)
    {}

    int score;
    bool _isDecoy;

    SearchScoreList getScoreList() const
    {
        SearchScoreList result;
        result.push_back(SearchScore("score", (double) score));
        return result;
    }

    bool isDecoy() const {return _isDecoy;}

    bool operator< (const SearchResult& rhs) const
    {
        return score < rhs.score;
    }
};

typedef boost::shared_ptr<SearchResult> SearchResultPtr;

struct SearchResultReverseLessThan
{
    bool operator() (const SearchResult& lhs, const SearchResult& rhs) const
    {
        return lhs.score > rhs.score;
    }
};

struct SearchResultPtrReverseLessThan
{
    bool operator() (const SearchResultPtr& lhs, const SearchResultPtr& rhs) const
    {
        return lhs->score > rhs->score;
    }
};

void testSimpleSet()
{
    vector<SearchResultPtr> searchResultSortedList;
    SearchResultSet<SearchResult> searchResultSet;
    map<int, vector<SearchResultPtr> > expectedResultsByRank;

    for (size_t i=0; i < testSearchResultsSize; ++i)
    {
        const TestSearchResult& t = testSearchResults[i];
        SearchResultPtr result(new SearchResult(t));
        searchResultSortedList.push_back(result);
        searchResultSet.add(result);
        expectedResultsByRank[t.expectedRank].push_back(result);
    }

    sort(searchResultSortedList.begin(), searchResultSortedList.end(), SearchResultPtrReverseLessThan());

    {
        unit_assert(searchResultSet.size() == testSearchResultsSize);
        unit_assert(searchResultSet.current_ranks() == 7);

        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 1);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (int i=1; i <= testSearchResultsSize; ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    // should have no effect
    searchResultSet.max_ranks(7);

    {
        unit_assert(searchResultSet.size() == testSearchResultsSize);
        unit_assert(searchResultSet.current_ranks() == 7);

        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 1);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (int i=1; i <= testSearchResultsSize; ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    // cut ranks down to 5
    searchResultSet.max_ranks(5);

    {
        unit_assert(searchResultSet.size() == 8);
        unit_assert(searchResultSet.current_ranks() == 5);

        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 1);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (size_t i=1; i <= searchResultSet.current_ranks(); ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    // cut ranks down to 3
    searchResultSet.max_ranks(3);

    {
        unit_assert(searchResultSet.size() == 3);
        unit_assert(searchResultSet.current_ranks() == 3);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (size_t i=1; i <= searchResultSet.current_ranks(); ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    const TestSearchResult bestTestResult = {100, 1, "BEST", false, false, false};
    SearchResultPtr bestResult(new SearchResult(bestTestResult));
    searchResultSet.add(bestResult);

    // bump the ranks up
    expectedResultsByRank.clear();
    expectedResultsByRank[1].push_back(bestResult);
    expectedResultsByRank[2].push_back(searchResultSortedList[0]);
    expectedResultsByRank[3].push_back(searchResultSortedList[1]);

    {
        // size should still be 3
        unit_assert(searchResultSet.size() == 3);
        unit_assert(searchResultSet.current_ranks() == 3);

        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 100);
        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 5);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (size_t i=1; i <= searchResultSet.current_ranks(); ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    const TestSearchResult secondBestTestResult = {90, 2, "BETTER", false, false, true};
    SearchResultPtr secondBestResult(new SearchResult(secondBestTestResult));
    searchResultSet.add(secondBestResult);
    
    // bump the ranks up
    expectedResultsByRank.clear();
    expectedResultsByRank[1].push_back(bestResult);
    expectedResultsByRank[2].push_back(secondBestResult);
    expectedResultsByRank[3].push_back(searchResultSortedList[0]);

    {
        // size should still be 3
        unit_assert(searchResultSet.size() == 3);
        unit_assert(searchResultSet.current_ranks() == 3);

        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 100);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 90);
        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (size_t i=1; i <= searchResultSet.current_ranks(); ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }

    // bump max_ranks up to 4
    searchResultSet.max_ranks(4);

    const TestSearchResult worstTestResult = {0, 2, "WRST", false, false, true};
    SearchResultPtr worstResult(new SearchResult(worstTestResult));
    searchResultSet.add(worstResult);

    expectedResultsByRank[4].push_back(worstResult);

    {
        // size should now be 4
        unit_assert(searchResultSet.size() == 4);
        unit_assert(searchResultSet.current_ranks() == 4);

        unit_assert(searchResultSet.bestNonSpecificTarget()->score == 100);
        unit_assert(searchResultSet.bestNonSpecificDecoy()->score == 90);
        unit_assert(searchResultSet.bestFullySpecificTarget()->score == 25);
        unit_assert(searchResultSet.bestSemiSpecificDecoy()->score == 12);
        unit_assert(searchResultSet.bestSemiSpecificTarget()->score == 10);
        unit_assert(searchResultSet.bestFullySpecificDecoy()->score == 5);

        map<int, vector<SearchResultPtr> > actualResultsByRank = searchResultSet.byRank();

        for (size_t i=1; i <= searchResultSet.current_ranks(); ++i)
            BOOST_FOREACH(const SearchResultPtr& item, expectedResultsByRank[i])
            {
                vector<SearchResultPtr>& actualRank = actualResultsByRank[i];
                unit_assert(find(actualRank.begin(), actualRank.end(), item) != actualRank.end());
            }
    }
}


/*void testSimpleReverseSet()
{
    topset<SearchResult, SearchResultReverseLessThan> searchResultSet;
    for (size_t i=0; i < testSearchResultsSize; ++i)
        searchResultSet.insert(searchResults[i]);

    {
        unit_assert(searchResultSet.size() == testSearchResultsSize);

        int actualRank = testSearchResultsSize + 1;
        BOOST_REVERSE_FOREACH(const SearchResult& item, searchResultSet)
            unit_assert(item.expectedRank == --actualRank);
    }

    // should have no effect
    searchResultSet.max_size(testSearchResultsSize);

    {
        unit_assert(searchResultSet.size() == testSearchResultsSize);

        int actualRank = testSearchResultsSize + 1;
        BOOST_REVERSE_FOREACH(const SearchResult& item, searchResultSet)
            unit_assert(item.expectedRank == --actualRank);
    }

    // cut size down to 2
    searchResultSet.max_size(3);
    
    {
        unit_assert(searchResultSet.size() == 3);

        int actualRank = testSearchResultsSize + 1;
        BOOST_REVERSE_FOREACH(const SearchResult& item, searchResultSet)
            unit_assert(item.expectedRank == --actualRank);
    }
}*/

const TestSearchResult testSearchResults2[] =
{
    {1, 2, "KVKPSFVCLRC(57)", false, false, true},
    {2, 1, "KVKPSFVC(57)LRCK", false, true, true},
    {2, 1, "VKPSFVCLRC(57)K", true, true, true}
};

const size_t testSearchResultsSize2 = sizeof(testSearchResults2) / sizeof(TestSearchResult);

void test2()
{
    SearchResultSet<SearchResult> searchResultSet(5);

    for (size_t i=0; i < testSearchResultsSize2; ++i)
    {
        const TestSearchResult& t = testSearchResults2[i];
        SearchResultPtr result(new SearchResult(t));
        searchResultSet.add(result);
    }

    unit_assert(searchResultSet.size() == 3);
    unit_assert(searchResultSet.current_ranks() == 2);
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testSimpleSet();
        //testSimpleReverseSet();
        test2();
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
