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

#ifndef _SEARCHRESULTSET_H
#define _SEARCHRESULTSET_H


#include "shared_types.h"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/proteome/Peptide.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include <boost/serialization/utility.hpp>
#include <boost/serialization/base_object.hpp>
#include <boost/serialization/level.hpp>
#include <boost/serialization/tracking.hpp>
#include <boost/serialization/vector.hpp>
#include <boost/serialization/set.hpp>
#include <boost/serialization/string.hpp>
#include <boost/serialization/shared_ptr.hpp>
#include <boost/serialization/split_member.hpp>
#include <stdexcept>


namespace freicore
{


using namespace pwiz::proteome;


struct SearchScore
{
    SearchScore(const std::string& name, double value, pwiz::cv::CVID cvid = pwiz::cv::CVID_Unknown)
        : name(name), cvid(cvid), value(value) {}

    std::string name;
    pwiz::cv::CVID cvid;
    double value;
};

typedef std::vector<SearchScore> SearchScoreList;


struct BaseSearchResult : public DigestedPeptide
{
    BaseSearchResult(const DigestedPeptide& peptide) : DigestedPeptide(peptide) {}

    PrecursorMassHypothesis precursorMassHypothesis;
    std::set<std::string> proteins;
    int fragmentsMatched;
    int fragmentsUnmatched;

    double calculatedMass() const
    {
        return precursorMassHypothesis.massType == MassType_Monoisotopic ? monoisotopicMass()
                                                                         : molecularWeight();
    }

    SearchScoreList getScoreList() const {return emptyScoreList;}
    bool isDecoy() const {return false;}

    template<typename Archive>
    void serialize(Archive& ar, const unsigned int version)
    {
        ar & boost::serialization::base_object<DigestedPeptide>(*this);
        ar & precursorMassHypothesis & proteins;
        ar & fragmentsMatched & fragmentsUnmatched;
    }

    static SearchScoreList emptyScoreList;
};


// SearchResultType should derive from BaseSearchResult
template<typename SearchResultType, typename SearchResultLessThan = std::less<SearchResultType> >
class SearchResultSet
{
    public:
    typedef SearchResultType SearchResult;
    typedef boost::shared_ptr<SearchResult> SearchResultPtr;
    typedef std::map<int, std::vector<SearchResultPtr> > RankMap;

    private:
    struct _SearchResultPtrLessThan
    {
        SearchResultLessThan searchResultLessThan;
        bool operator() (const SearchResultPtr& lhs, const SearchResultPtr& rhs) const
        {
            return searchResultLessThan(*lhs, *rhs);
        }
    };
    typedef std::multiset<SearchResultPtr, _SearchResultPtrLessThan> _MainSet;

    struct _PeptidePtrLessThan
    {
        bool operator() (const SearchResultPtr& lhs, const SearchResultPtr& rhs) const
        {
            return static_cast<Peptide>(*lhs) < static_cast<Peptide>(*rhs);
        }
    };


    public:
    typedef typename _MainSet::iterator iterator;
    typedef typename _MainSet::const_iterator const_iterator;
    typedef typename _MainSet::reverse_iterator reverse_iterator;
    typedef typename _MainSet::const_reverse_iterator const_reverse_iterator;

    SearchResultSet(size_t maxRanks = 0) : _maxRanks(maxRanks), _currentRanks(0) {}

    bool empty() const {return size() == 0;}

    size_t max_ranks() const {return _maxRanks;}
    void max_ranks(size_t maxRanks) {_max_ranks(maxRanks);}

    size_t current_ranks() const {return _currentRanks;}
    size_t size() const {return _mainSet.size();}

    iterator begin() {return _mainSet.begin();}
    iterator end() {return _mainSet.end();}

    const_iterator begin() const {return _mainSet.begin();}
    const_iterator end() const {return _mainSet.end();}

    reverse_iterator rbegin() {return _mainSet.rbegin();}
    reverse_iterator rend() {return _mainSet.rend();}

    const_reverse_iterator rbegin() const {return _mainSet.rbegin();}
    const_reverse_iterator rend() const {return _mainSet.rend();}

    SearchResultPtr bestFullySpecificTarget() const {return _bestFullySpecificTarget;}
    SearchResultPtr bestFullySpecificDecoy() const {return _bestFullySpecificDecoy;}
    SearchResultPtr bestSemiSpecificTarget() const {return _bestSemiSpecificTarget;}
    SearchResultPtr bestSemiSpecificDecoy() const {return _bestSemiSpecificDecoy;}
    SearchResultPtr bestNonSpecificTarget() const {return _bestNonSpecificTarget;}
    SearchResultPtr bestNonSpecificDecoy() const {return _bestNonSpecificDecoy;}

    void erase(const SearchResultPtr& resultPtr)
    {
        if(_bestFullySpecificTarget == resultPtr)
            _bestFullySpecificTarget.reset();
        else if(_bestFullySpecificDecoy == resultPtr)
            _bestFullySpecificDecoy.reset();
        else if(_bestSemiSpecificTarget == resultPtr)
            _bestSemiSpecificTarget.reset();
        else if(_bestSemiSpecificDecoy == resultPtr)
            _bestSemiSpecificDecoy.reset();
        else if(_bestNonSpecificTarget == resultPtr)
            _bestNonSpecificTarget.reset();
        else if(_bestNonSpecificDecoy == resultPtr)
            _bestNonSpecificDecoy.reset();
        _mainSet.erase(resultPtr);
    }

    void add(const SearchResultPtr& result)
    {
        if(!result.get())
            throw runtime_error("result pointer is null");

        // find results with the same score
        pair<typename _MainSet::iterator, typename _MainSet::iterator> range = _mainSet.equal_range(result);

        bool isNewResult = true;
        for (typename _MainSet::iterator itr = range.first; itr != range.second; ++itr)
        {
            const SearchResultPtr& existingResult = *itr;

            // compare peptide sequence and modifications
            if (existingResult->Peptide::operator==(*result))
            {
                isNewResult = false;

                // peptide/mods are the same, but the flanking residues and terminal specificity could be different
                bool sameDigestion = existingResult->specificTermini() == result->specificTermini() &&
                                     existingResult->NTerminusPrefix() == result->NTerminusPrefix() &&
                                     existingResult->CTerminusSuffix() == result->CTerminusSuffix();

                if (!sameDigestion)
                {
                    // if the new result is more specific, the categories have to be updated
                    if (existingResult->specificTermini() < result->specificTermini())
                    {
                        SearchResultPtr* existingResultCategory = _getResultCategory(existingResult);

                        // reset the existingResult's category
                        if (*existingResultCategory == existingResult)
                            existingResultCategory->reset();

                        SearchResultPtr* newResultCategory = _getResultCategory(result);

                        // if the current result for that category is less than the new result, replace it
                        if (!newResultCategory->get() || _searchResultPtrLessThan(*newResultCategory, result))
                            *newResultCategory = result;
                    }

                    // replace the flanking residues and specificity with the new result
                    if (existingResult->specificTermini() <= result->specificTermini() &&
                        existingResult->NTerminusPrefix() < result->NTerminusPrefix() &&
                        existingResult->CTerminusSuffix() < result->CTerminusSuffix())
                        static_cast<DigestedPeptide&>(const_cast<SearchResult&>(*existingResult)) = *result;
                }

                // add the new result's protein to the existing proteins
                const_cast<SearchResult&>(*existingResult).proteins.insert(*result->proteins.begin());
            }
        }

        if (isNewResult)
        {
            // if range is empty, the current result will be a new rank
            if (range.first == range.second)
            {
                // if _maxRanks is not set or the set is not full, insert and increment _currentRanks
                if (_maxRanks == 0 || _currentRanks < _maxRanks)
                {
                    ++_currentRanks;
                    _mainSet.insert(result);
                }
                // otherwise compare the new rank to the worst existing rank
                else
                {
                    // because range is empty, we know that (new rank != worst rank)

                    // if worst rank < new rank, insert the new rank and erase the worst rank
                    SearchResultPtr worstResult = *_mainSet.begin();
                    if (_searchResultPtrLessThan(worstResult, result))
                    {
                        _mainSet.insert(result);

                        // erase all the results tied with the worst result
                        while (!_searchResultPtrLessThan(worstResult, *_mainSet.begin()))
                            _mainSet.erase(_mainSet.begin());
                    }
                }
            }
            // a new result in an existing rank is simply inserted
            else
                _mainSet.insert(result);
        }
        // existing results are not inserted


        // compare the new result to the top hit in its category
        // NOTE: we do this even if the result was not inserted because that
        // category may not be in the main set; the new result may have a better
        // score or have the same sequence() but different specificTermini() or isDecoy()
        SearchResultPtr* resultCategory = _getResultCategory(result);

        // if the current result for that category is less than the new result, replace it
        if (!resultCategory->get() || _searchResultPtrLessThan(*resultCategory, result))
            *resultCategory = result;
    }

    RankMap byRank() const
    {
        RankMap rankMap;

        SearchResultPtr lastResult;
        int lastRank = 1;
        _PeptidePtrLessThan peptideLessThan;

        BOOST_REVERSE_FOREACH(const SearchResultPtr& result, _mainSet)
        {
            if (lastResult.get() && _searchResultPtrLessThan(result, lastResult))
            {
                // sort the vector of results by peptide
                std::sort(rankMap[lastRank].begin(), rankMap[lastRank].end(), peptideLessThan);
                ++lastRank;
            }
            rankMap[lastRank].push_back(result);
            lastResult = result;
        }

        return rankMap;
    }

    RankMap byRankAndCategory() const
    {
        RankMap rankMap = byRank();

        _MainSet outrankedResults;
        _insertOutrankedCategory(outrankedResults, _bestFullySpecificTarget);
        _insertOutrankedCategory(outrankedResults, _bestFullySpecificDecoy);
        _insertOutrankedCategory(outrankedResults, _bestSemiSpecificTarget);
        _insertOutrankedCategory(outrankedResults, _bestSemiSpecificDecoy);
        _insertOutrankedCategory(outrankedResults, _bestNonSpecificTarget);
        _insertOutrankedCategory(outrankedResults, _bestNonSpecificDecoy);

        SearchResultPtr lastResult;
        int lastRank = 1;
        _PeptidePtrLessThan peptideLessThan;
        
        if (!rankMap.empty())
        {
            lastResult = rankMap.rbegin()->second.front();
            lastRank = rankMap.rbegin()->first;
        }

        BOOST_REVERSE_FOREACH(const SearchResultPtr& result, outrankedResults)
        {
            if (lastResult.get() && _searchResultPtrLessThan(result, lastResult))
            {
                // sort the vector of results by peptide
                std::sort(rankMap[lastRank].begin(), rankMap[lastRank].end(), peptideLessThan);
                ++lastRank;
            }
            rankMap[lastRank].push_back(result);
            lastResult = result;
        }

        return rankMap;
    }

    template<typename Archive>
    void serialize(Archive& ar, const unsigned int version)
    {
        ar & _maxRanks & _currentRanks;
        ar & _mainSet;
        ar & _bestFullySpecificTarget;
        ar & _bestFullySpecificDecoy;
        ar & _bestSemiSpecificTarget;
        ar & _bestSemiSpecificDecoy;
        ar & _bestNonSpecificTarget;
        ar & _bestNonSpecificDecoy;
    }


    private:

    size_t _maxRanks, _currentRanks;

    // result comparator
    _SearchResultPtrLessThan _searchResultPtrLessThan;

    // top hits from all categories
    _MainSet _mainSet;

    // top hits by category
    SearchResultPtr _bestFullySpecificTarget;
    SearchResultPtr _bestFullySpecificDecoy;
    SearchResultPtr _bestSemiSpecificTarget;
    SearchResultPtr _bestSemiSpecificDecoy;
    SearchResultPtr _bestNonSpecificTarget;
    SearchResultPtr _bestNonSpecificDecoy;

    inline SearchResultPtr* _getResultCategory(const SearchResultPtr& result)
    {
        switch (result->specificTermini())
        {
            case 2: return result->isDecoy() ? &_bestFullySpecificDecoy : &_bestFullySpecificTarget; break;
            case 1: return result->isDecoy() ? &_bestSemiSpecificDecoy : &_bestSemiSpecificTarget; break;
            case 0: return result->isDecoy() ? &_bestNonSpecificDecoy : &_bestNonSpecificTarget; break;
            default: throw runtime_error("invalid value from specificTermini()");
        }
    }

    inline void _insertOutrankedCategory(_MainSet& outrankedResults, const SearchResultPtr& result) const
    {
        // if result is worse than the worst result in the main set, insert it
        if (result.get() && _searchResultPtrLessThan(result, *_mainSet.begin()))
            outrankedResults.insert(result);
    }

    void _max_ranks(size_t maxRanks)
    {
        _maxRanks = maxRanks;

        if (_maxRanks == 0 || _currentRanks < _maxRanks)
            return;

        _currentRanks = std::min(_currentRanks, _maxRanks);

        RankMap rankMap = byRank();
        int currentRank = maxRanks + 1;
        while (true)
        {
            if (!rankMap.count(currentRank))
                break;
            BOOST_FOREACH(const SearchResultPtr& result, rankMap[currentRank])
                _mainSet.erase(result);
            ++currentRank;
        }
    }
};


} // namespace freicore


namespace std
{
    ostream& operator<< (ostream& o, const freicore::SearchScore& rhs);

    template<typename SearchResultType, typename SearchResultLessThan>
    ostream& operator<< (ostream& o, const freicore::SearchResultSet<SearchResultType, SearchResultLessThan>& rhs)
    {
        typedef typename freicore::SearchResultSet<SearchResultType, SearchResultLessThan>::RankMap RankMap;
        RankMap rankMap = rhs.byRank();
        BOOST_FOREACH(const typename RankMap::value_type& rank, rankMap)
        BOOST_FOREACH(const boost::shared_ptr<SearchResultType>& resultPtr, rank.second)
        {
            string sequence = resultPtr->sequence();
            BOOST_REVERSE_FOREACH(const ModificationMap::value_type& modSite, resultPtr->modifications())
            BOOST_FOREACH(const Modification& mod, modSite.second)
            sequence.insert(min((int) sequence.length(), max(0, modSite.first+1)), "(" + boost::lexical_cast<string>(freicore::round(mod.monoisotopicDeltaMass())) + ")");
            o << "(" << rank.first << ": " << sequence << " " << resultPtr->getScoreList() << " " << resultPtr->protein << ")\n";
        }
        return o;
    }
}


namespace boost {
namespace serialization {

using namespace pwiz::proteome;

template<class Archive>
void save(Archive& ar, const Modification& m, const unsigned int version)
{
    double mono = m.monoisotopicDeltaMass(), avg = m.averageDeltaMass();
    ar << mono << avg;
}

template<class Archive>
void load(Archive& ar, Modification& m, const unsigned int version)
{
    double mono, avg;
    ar >> mono >> avg;
    m = Modification(mono, avg);
}

template<class Archive>
void save(Archive& ar, const Peptide& p, const unsigned int version)
{
    ar << p.sequence();
    ar << p.modifications().base();
}

template<class Archive>
void load(Archive& ar, Peptide& p, const unsigned int version)
{
    string sequence;
    ar >> sequence;
    p = Peptide(sequence);
    ar >> p.modifications().base();
}

template<class Archive>
void save(Archive& ar, const DigestedPeptide& p, const unsigned int version)
{
    ar << p.sequence();

    size_t offset = p.offset(), missedCleavages = p.missedCleavages();
    bool N = p.NTerminusIsSpecific(), C = p.CTerminusIsSpecific();
    string pre = p.NTerminusPrefix(), post = p.CTerminusSuffix();
    ar << offset << missedCleavages;
    ar << N << C;
    ar << pre << post;

    const ModificationMap& modMap = p.modifications();
    size_t modMapSize = modMap.size();
    ar << modMapSize;
    for( ModificationMap::const_iterator itr = modMap.begin(); itr != modMap.end(); ++itr )
        ar << itr->first << static_cast< const vector<Modification> >(itr->second);
}

template<class Archive>
void load(Archive& ar, DigestedPeptide& p, const unsigned int version)
{
    string sequence;
    ar >> sequence;

    size_t offset, missedCleavages;
    bool N, C;
    string pre, post;
    ar >> offset >> missedCleavages;
    ar >> N >> C;
    ar >> pre >> post;

    p = DigestedPeptide(sequence.begin(), sequence.end(), offset, missedCleavages, N, C, pre, post);

    size_t modMapSize;
    ar >> modMapSize;
    ModificationMap& modMap = p.modifications();
    int position;
    Modification mod;
    for( size_t i=0; i < modMapSize; ++i )
    {
        vector<Modification> modList;
        ar >> position >> modList;
        modMap.insert( modMap.end(), make_pair(position, modList) );
    }
}

} // namespace serialization
} // namespace boost

BOOST_SERIALIZATION_SPLIT_FREE(pwiz::proteome::Modification)
BOOST_SERIALIZATION_SPLIT_FREE(pwiz::proteome::Peptide)
BOOST_SERIALIZATION_SPLIT_FREE(pwiz::proteome::DigestedPeptide)


#endif // _SEARCHRESULTSET_H
