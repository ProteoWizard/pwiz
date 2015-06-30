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

#ifndef _SEARCHRESULT_H
#define _SEARCHRESULT_H

#include "shared_defs.h"
#include "shared_funcs.h"
#include "UniModXMLParser.h"
#include "proteinStore.h"

namespace freicore
{
    typedef pair< string, double >        SearchScoreInfo;
    typedef vector< SearchScoreInfo >    SearchScoreList;
    struct BaseSearchResult : public DigestedPeptide
    {
        BaseSearchResult( const MvIntKey& k = MvIntKey(), const string& seq = "" )
            :    DigestedPeptide( seq ), rank( 0 ), mod( 0.0 ), key( k )
        {}

        BaseSearchResult( const DigestedPeptide& c )
            :    DigestedPeptide( c ), rank( 0 )
        {}

        virtual ~BaseSearchResult() {};

        size_t                rank;
        double                mod;
        MvIntKey            key;
        ProteinLociByIndex    lociByIndex;
        ProteinLociByName    lociByName;

        vector<UnimodModification>        modificationInterpretations;

        static SearchScoreList emptyScoreList;

        virtual double                    getTotalScore() const = 0;
        virtual SearchScoreList            getScoreList() const = 0;

        enum DecoyState { DecoyState_Ambiguous, DecoyState_Target, DecoyState_Decoy };
        virtual DecoyState              getDecoyState(const string& decoyPrefix) const
        {
            bool hasTarget = false, hasDecoy = false;
            BOOST_FOREACH(const ProteinLocusByName& locus, lociByName)
            {
                hasTarget = hasTarget || locus.name.find(decoyPrefix) != 0;
                hasDecoy = hasDecoy || locus.name.find(decoyPrefix) == 0;

                if( hasTarget && hasDecoy )
                    return DecoyState_Ambiguous;
            }

            if( hasTarget )
                return DecoyState_Target;
            return DecoyState_Decoy;
        }


        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< DigestedPeptide >( *this );
            ar & rank & mod;
            ar & key;
            ar & lociByIndex & lociByName;
        }
    };
}

namespace std
{
    //ostream& operator<< ( ostream& o, const SearchScoreInfo& rhs );
    //ostream& operator<< ( ostream& o, const SearchScoreList& rhs );
    ostream& operator<< ( ostream& o, const BaseSearchResult& rhs );
}

namespace freicore
{
    template< class SearchResultType >
    struct SearchResultSet : public topset< SearchResultType >
    {
        typedef SearchResultType SearchResult;
        typedef topset< SearchResultType > BaseSet;

        SearchResultSet( size_t MaxResults = 0 ) : topset< SearchResult >( MaxResults ) {}

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< topset< SearchResult > >( *this );
        }

        void add( const SearchResult& newResult )
        {
            //insert( newResult );
            //cout << "attempting to insert a search results..." << endl;
            TemplateSetInsertPair( SearchResult ) rv = insert( newResult );
            if( rv.first != BaseSet::end() )
            {
                SearchResult& r = const_cast< SearchResult& >( *rv.first );
                r.lociByIndex.insert( newResult.lociByIndex.begin(), newResult.lociByIndex.end() );
                /*if( r.sequence().find( '+' ) != string::npos || newResult.sequence().find( '+' ) != string::npos )
                {
                    SearchResult mergedResult( newResult );
                    //if( GetUnmodifiedSequence( r.sequence ) != GetUnmodifiedSequence( newResult.sequence ) )
                    //    cout << r << endl << "should not combine with" << endl << newResult << endl << *this << endl;
                    //else
                        mergedResult.sequence = MergeDeltaMasses( r.sequence, newResult.sequence );
                    erase( rv.first );
                    insert( mergedResult );
                }*/
            }
        }

        /**
            This function takes a new result, tries to insert it into the spectrum, if it fails,
            it adds the new result to the existing result as a peptide.
        */
        void update(const SearchResult& newResult) {
            
            TemplateSetInsertPair( SearchResult ) rv = insert( newResult);
            // Failed insert
            if(!rv.second) {
                // Get the existing result
                TemplateSetEqualPair(SearchResult) range = equal(newResult);
                // Check to make sure there is an existing result
                if(range.first != range.second && range.first != BaseSet::end()) {
                    // Get it by the pointer
                    SearchResult& existingResult = const_cast< SearchResult&> (*range.first);
                    // Update the alternatives by adding the newResult's peptide.
                    existingResult.alternatives.push_back(static_cast <const Peptide&>(newResult));
                }
            }
        }

        double getBestTotalScore() const
        {
            if( BaseSet::empty() )
                return 0;
            return const_cast< SearchResultType& >( *BaseSet::rbegin() ).getTotalScore();
        }

        string MergeDeltaMasses( const string& seq1, const string& seq2 )
        {
            string out;
            size_t maxLength = seq1.length() * 2;
            out.reserve( maxLength );
            size_t i1 = 0, i2 = 0;
            while( i1 < seq1.length() || i2 < seq2.length() )
            {
                if( i1 < seq1.length() && i2 < seq2.length() && seq1[i1] == seq2[i2] )
                {
                    out.push_back( seq1[i1] );
                    ++i1; ++i2;
                } else
                {
                    if( i1 < seq1.length() && seq1[i1] == '+' )
                    {
                        out.push_back( '+' );
                        ++i1;
                    } else if( i2 < seq2.length() && seq2[i2] == '+' )
                    {
                        out.push_back( '+' );
                        ++i2;
                    }
                }
            }
            return out;
        }

        /*void convertProteinNamesToIndexes( const proteinStore::ProteinIndexMap& nameToIndex )
        {
            for( typename BaseSet::iterator itr = BaseSet::begin(); itr != BaseSet::end(); ++itr )
                for( ProteinLociByName::iterator itr2 = itr->lociByName.begin(); itr2 != itr->lociByName.end(); ++itr2 )
                {
                    ProteinIndex index;
                    shared_ptr<string> tmp( new string(itr2->name) ); // unnamed temporary shared_ptrs are leaky
                    proteinStore::ProteinIndexMap::const_iterator itr3 = nameToIndex.find( tmp );
                    if( itr3 == nameToIndex.end() )
                        index = 0;
                    else
                        index = itr3->second;
                    const_cast< SearchResult& >( *itr ).lociByIndex.insert( ProteinLocusByIndex( index, itr2->offset ) );
                }
        }*/

        void convertProteinIndexesToNames( const proteinStore& proteins )
        {
            for( typename BaseSet::iterator itr = BaseSet::begin(); itr != BaseSet::end(); ++itr )
                for( ProteinLociByIndex::iterator itr2 = itr->lociByIndex.begin(); itr2 != itr->lociByIndex.end(); ++itr2 )
                {
                    ProteinName name;
                    try {
                        name = proteins.getProteinName( itr2->index );
                    } catch(exception&)
                    {
                        name = lexical_cast<string>( itr2->index );
                    }
                    const_cast< SearchResult& >( *itr ).lociByName.insert( ProteinLocusByName( name, itr2->offset ) );
                }
        }

        void calculateRanks()
        {
            if( BaseSet::empty() )
                return;

            size_t rank = 1;
            double lastScore = BaseSet::rbegin()->getTotalScore();
            for( typename BaseSet::reverse_iterator itr = BaseSet::rbegin(); itr != BaseSet::rend(); ++itr )
            {
                const_cast< SearchResult& >( *itr ).rank = ( lastScore != itr->getTotalScore() ? ++rank : rank );
                lastScore = itr->getTotalScore();
            }
        }

        /*void calculateDeltaCn()
        {
            if( empty() )
                return;

            size_t numScores = scoreIndexMap.size();
            if( !scoreIndexMap.count("deltacn") )
                scoreIndexMap["deltacn"] = numScores;
            size_t deltaCnScoreIndex = scoreIndexMap["deltacn"];

            for( reverse_iterator itr = rbegin(); itr != rend(); ++itr )
            {
                SearchResult& r = const_cast< SearchResultT& >( *itr );
                if( r.scores.size() <= deltaCnScoreIndex )
                    r.scores.push_back(0);
                r.scores[deltaCnScoreIndex] = ( r.scores[0] != 0 ? ( rbegin()->scores[0] - r.scores[0] ) / rbegin()->scores[0] : 0 );
            }
        }

        map< string, size_t > scoreIndexMap;*/
    };
}
BOOST_CLASS_IMPLEMENTATION( freicore::BaseSearchResult, boost::serialization::object_serializable )
//BOOST_CLASS_IMPLEMENTATION( freicore::SearchResultSet, boost::serialization::object_serializable )
BOOST_CLASS_TRACKING( freicore::BaseSearchResult, boost::serialization::track_never )
//BOOST_CLASS_TRACKING( freicore::SearchResultSet, boost::serialization::track_never )

namespace std
{
    template< class SearchResultType >
    ostream& operator<< ( ostream& o, const SearchResultSet< SearchResultType >& rhs )
    {
        return ( o << static_cast< set< SearchResultType > >( rhs ) );
    }
}

#endif
