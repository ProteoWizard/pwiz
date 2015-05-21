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

#ifndef _PROTEINSTORE_H
#define _PROTEINSTORE_H

#include "shared_defs.h"
#include "shared_funcs.h"
#include "simplethreads.h"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"

using namespace freicore;

namespace freicore
{
	struct proteinStore;


	struct proteinData
	{
        proteinData(const pwiz::proteome::ProteinPtr& proteinPtr, bool isDecoy)
            : proteinPtr(proteinPtr), isDecoy_(isDecoy)
        {}

		const string& getName() const;        /// protein's name
		const string& getDescription() const; /// description of protein
		const string& getSequence() const;    /// reads amino acid sequence if not already in memory
		bool          isDecoy() const;        /// true if the sequence is a decoy

        // implicit conversion to Peptide
        operator const Peptide& () const {return *proteinPtr;}

		// These two functions test for cleavability between offset-1 and offset
		bool testCleavage( const CleavageRule& rule, size_t offset ) const;
		CleavageRuleSet::const_iterator testCleavage( const CleavageRuleSet& rules, size_t offset ) const;

		bool operator== ( const proteinData& rhs ) { return *proteinPtr == *rhs.proteinPtr; }

		template< class Archive >
		void save( Archive& ar, const unsigned int version ) const
		{
			ar << getName() << getDescription() << getSequence() << isDecoy_;
		}

        template< class Archive >
		void load( Archive& ar, const unsigned int version )
		{
            string name, description, sequence;
			ar >> name >> description >> sequence >> isDecoy_;
            proteinPtr.reset(new pwiz::proteome::Protein(name, 0, description, sequence));
		}

        BOOST_SERIALIZATION_SPLIT_MEMBER()

		friend struct proteinStore;

	private:
        pwiz::proteome::ProteinPtr proteinPtr;
        bool isDecoy_;
	};


	struct proteinStore
	{
		proteinStore( const string& decoyPrefix = "rev_" );
        proteinStore( shared_ptr<ProteomeData> dataPtr, const string& decoyPrefix = "rev_", bool automaticDecoys = true );
        ~proteinStore();

		void readFASTA( const string& filename, const string& delimiter = " ", bool automaticDecoys = true );
		void writeFASTA( const string& filename ) const;
		//void add( const proteinData& p );

		proteinData operator[]( const string& name ) const;
		proteinData operator[]( size_t index ) const;
        string getProteinName( size_t index ) const;

		void random_shuffle();
        size_t size() const;
        size_t find( const string& name ) const;

		string decoyPrefix;
		int numReals;
		int numDecoys;

	private:
        void initialize(bool automaticDecoys);

        shared_ptr<ProteomeData> proteomeDataPtr;
        // maps indexes in the store to indexes in the underlying ProteomeData
		vector<size_t> storeIndex;
        mutable simplethread_mutex_t storeMutex;
	};
}

#endif
