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
#include "proteinStore.h"
#include "Profiler.h"
#include "simplethreads.h"
#include "pwiz/analysis/proteome_processing/ProteinList_DecoyGenerator.hpp"
#include "pwiz/data/proteome/ProteinListCache.hpp"

using namespace freicore;
using namespace pwiz::analysis;

namespace freicore
{
    const string& proteinData::getName() const        { return proteinPtr->id; }
    const string& proteinData::getDescription() const { return proteinPtr->description; }
    const string& proteinData::getSequence() const    { return proteinPtr->sequence(); }
    bool          proteinData::isDecoy() const        { return isDecoy_; }

	bool proteinData::testCleavage( const CleavageRule& rule, size_t offset ) const
	{
        const string& data = getSequence();

		size_t n_count = offset+1; // # of residues N terminal to the cleavage site (+1 for the N terminus)
		size_t c_count = data.length()-offset+1; // # of residues C terminal to the cleavage site (+1 for the C terminus)

		const CleavageHalfRule& n_rules = rule.first;
		bool n_passes = n_rules.hasWildcard;
		for( CleavageHalfRule::const_iterator itr = n_rules.begin(); !n_passes && itr != n_rules.end(); ++itr )
		{
			const string& n_rule = *itr;

			if( n_rule.length() > n_count ) // rule cannot pass if it needs more residues than there are to test
				continue;

			n_passes = true;
			for( size_t i=0; n_passes && i < n_rule.length(); ++i )
			{
				char protein_residue;
				if( offset-i == 0 ) // at the N terminus, check for PROTEIN_N_TERMINUS_SYMBOL
					protein_residue = PROTEIN_N_TERMINUS_SYMBOL;
				else
					protein_residue = data[offset-1-i];

				if( *(n_rule.rbegin()+i) != protein_residue )
					n_passes = false;
			}
		}

		if( !n_passes )
			return false;

		const CleavageHalfRule& c_rules = rule.second;
		bool c_passes = c_rules.hasWildcard;
		for( CleavageHalfRule::const_iterator itr = c_rules.begin(); !c_passes && itr != c_rules.end(); ++itr )
		{
			const string& c_rule = *itr;

			if( c_rule.length() > c_count ) // rule cannot pass if it needs more residues than there are to test
				continue;

			c_passes = true;
			for( size_t i=0; c_passes && i < c_rule.length(); ++i )
			{
				char protein_residue;
				if( offset+i == data.length() ) // at the C terminus, check for PROTEIN_C_TERMINUS_SYMBOL
					protein_residue = PROTEIN_C_TERMINUS_SYMBOL;
				else
					protein_residue = data[offset+i];

				if( c_rule[i] != protein_residue )
					c_passes = false;
			}
		}

		if( c_passes )
			return true;
		else
			return false;
	}

	CleavageRuleSet::const_iterator proteinData::testCleavage( const CleavageRuleSet& rules, size_t offset ) const
	{
		if( offset > getSequence().length() ) // the offset at m_data.length() will test for cleavage at the protein's C terminus
			throw out_of_range( "offset parameter to ProteinData::testCleavage" );

		CleavageRuleSet::const_iterator itr;
		for( itr = rules.begin(); itr != rules.end(); ++itr )
		{
			if( testCleavage( *itr, offset ) )
				return itr; // this cleavage rule passed the test
		}
		return itr; // no cleavage rule passed the test
	}

    proteinStore::proteinStore( const string& decoyPrefix )
			:	decoyPrefix(decoyPrefix), numReals(0), numDecoys(0)
    {
        simplethread_create_mutex(&storeMutex);
    }

    proteinStore::proteinStore( shared_ptr<ProteomeData> dataPtr, const string& decoyPrefix, bool automaticDecoys )
			:	decoyPrefix(decoyPrefix), proteomeDataPtr(dataPtr)
	{
        simplethread_create_mutex(&storeMutex);

        initialize(automaticDecoys);
    }

    proteinStore::~proteinStore()
    {
        simplethread_destroy_mutex(&storeMutex);
    }

    void proteinStore::initialize(bool automaticDecoys)
    {
        // default scheme maps 0->0, 1->1, etc.
        storeIndex.resize(proteomeDataPtr->proteinListPtr->size(), 0);

        numReals = numDecoys = 0;
        if( !automaticDecoys )
        {
            storeIndex.resize(proteomeDataPtr->proteinListPtr->size());
            for (int i=0; i < size(); ++i)
                storeIndex[i] = i;
        }
        else
        {
            for (size_t i=0; i < size(); ++i)
            {
                storeIndex[i] = i;
                if (bal::starts_with(proteomeDataPtr->proteinListPtr->protein(i, false)->id, decoyPrefix))
                    ++numDecoys;
                else
                    ++numReals;
            }

            if( numDecoys == 0 )
            {
                proteomeDataPtr->proteinListPtr = ProteinListPtr(
                    new ProteinList_DecoyGenerator(proteomeDataPtr->proteinListPtr,
                                                   ProteinList_DecoyGenerator::PredicatePtr(
                                                    new ProteinList_DecoyGeneratorPredicate_Reversed(decoyPrefix))));
    
                storeIndex.resize(proteomeDataPtr->proteinListPtr->size());
                for (int i=0; i < numReals; ++i)
                    storeIndex[i] = i;
    
                for (size_t i = (size_t) numReals; i < proteomeDataPtr->proteinListPtr->size(); ++i)
                {
                    storeIndex[i] = i;
                    ++numDecoys;
                }
            }
        }

        proteomeDataPtr->proteinListPtr = ProteinListPtr(new ProteinListCache(proteomeDataPtr->proteinListPtr, ProteinListCacheMode_MetaDataAndSequence, 10000));
    }

	void proteinStore::readFASTA( const string& filename, const string& delimiter, bool automaticDecoys )
	{
        proteomeDataPtr.reset(new ProteomeDataFile(filename, true));

        initialize(automaticDecoys);
	}

	void proteinStore::writeFASTA( const string& filename ) const
	{
        ProteomeDataFile::write(*proteomeDataPtr, filename);
	}

	/*void proteinStore::add( const proteinData& p )
	{
		const_iterator itr = std::find( begin(), end(), p );
		if( itr == end() )
		{
			push_back( p );
			nameToIndex[ p.m_name ] = size()-1;
			indexToName[ size()-1 ] = p.m_name;
		}
	}*/

	proteinData proteinStore::operator[]( const string& name ) const
    {
        size_t index = proteomeDataPtr->proteinListPtr->find(name);
        if (index == proteomeDataPtr->proteinListPtr->size())
            throw runtime_error("protein \"" + name + "\" not found in database");

        simplethread_lock_mutex(&storeMutex);
        ProteinPtr proteinPtr = proteomeDataPtr->proteinListPtr->protein(index);
        simplethread_unlock_mutex(&storeMutex);
        return proteinData(proteinPtr, bal::starts_with(proteinPtr->id, decoyPrefix));
    }

	proteinData proteinStore::operator[]( size_t index ) const
    {
        simplethread_lock_mutex(&storeMutex);
        ProteinPtr proteinPtr = proteomeDataPtr->proteinListPtr->protein(storeIndex[index]);
        simplethread_unlock_mutex(&storeMutex);
        return proteinData(proteinPtr, bal::starts_with(proteinPtr->id, decoyPrefix));
    }

    string proteinStore::getProteinName( size_t index ) const
    {
        simplethread_lock_mutex(&storeMutex);
        ProteinPtr proteinPtr = proteomeDataPtr->proteinListPtr->protein(storeIndex[index], false);
        simplethread_unlock_mutex(&storeMutex);
        return proteinPtr->id;
    }

	void proteinStore::random_shuffle()
	{
		std::random_shuffle( storeIndex.begin(), storeIndex.end() );
	}

    size_t proteinStore::size() const {return storeIndex.size();}

    size_t proteinStore::find( const string& name ) const
    {
        size_t index = proteomeDataPtr->proteinListPtr->find(name);
        if (index == proteomeDataPtr->proteinListPtr->size())
            return index;

        return *std::find(storeIndex.begin(), storeIndex.end(), index);
    }
}
