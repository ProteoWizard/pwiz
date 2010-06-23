//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

#ifndef _IDPQONVERT_H
#define _IDPQONVERT_H

#define IDPQONVERT_LICENSE				COMMON_LICENSE

#include "expat_xml.h"
#include "SimpleXMLWriter.h"
#include <iostream>
#include <sstream>
#include <string>

#define IDPQONVERT_RUNTIME_CONFIG \
	COMMON_RTCONFIG \
	RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
	RTCONFIG_VARIABLE( string,			ProteinDatabase,			""				) \
	RTCONFIG_VARIABLE( string,          ProteinDatabaseSearchPath,  ""              ) \
	RTCONFIG_VARIABLE( string,			DecoyPrefix,				"rev_"			) \
	RTCONFIG_VARIABLE( double,			MaxFDR,						0.25			) \
	RTCONFIG_VARIABLE( int,				MaxResultRank,				1				) \
	RTCONFIG_VARIABLE( string,			SearchScoreWeights,			"mvh 1 xcorr 1 expect -1 ionscore 1" ) \
	RTCONFIG_VARIABLE( bool,			NormalizeSearchScores,		false			) \
    RTCONFIG_VARIABLE( string,			NormalizedSearchScores,		""              ) \
    RTCONFIG_VARIABLE( string,          NormalizationMethod,        "quantile"      ) \
	RTCONFIG_VARIABLE( bool,			OptimizeScoreWeights,		false			) \
	RTCONFIG_VARIABLE( int,				OptimizeScorePermutations,	200				) \
	RTCONFIG_VARIABLE( bool,			WriteQonversionDetails,		false			) \
	RTCONFIG_VARIABLE( bool,			HasDecoyDatabase,			true			) \
	RTCONFIG_VARIABLE( bool,            PreserveInputHierarchy,     false           ) \
    RTCONFIG_VARIABLE( bool,            PercolatorScore,            false           ) \
    RTCONFIG_VARIABLE( bool,            PercolatorReranking,        false           )


namespace freicore
{
namespace idpicker
{
    struct PWIZ_API_DECL Version
    {
        static int Major();
        static int Minor();
        static int Revision();
        static std::string str();
        static std::string LastModified();
    };

	enum QonvertErrorCode
	{
		QONVERT_SUCCESS,
		QONVERT_ERROR_UNHANDLED_EXCEPTION,
		QONVERT_ERROR_FASTA_FILE_FAILURE,
		QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE,
		QONVERT_ERROR_RESIDUE_CONFIG_FILE_FAILURE,
		QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS,
		QONVERT_ERROR_RUNTIME_CONFIG_OVERRIDE_FAILURE,
		QONVERT_ERROR_NO_INPUT_FILES_FOUND,
		QONVERT_ERROR_NO_TARGET_PROTEINS,
        QONVERT_ERROR_NO_DECOY_PROTEINS
	};

	static float EPSILON = 0.0001f;

	struct SearchResult : public GenericSearchResult
	{

		SearchResult( const GenericSearchResult& r = GenericSearchResult() )
			:	GenericSearchResult( r ), fdr( 1 ), pScoreWeights(NULL), isScoreCalculated( false ), score( 0 )
		{}

		SearchResult(const SearchResult& res) : GenericSearchResult(res), fdr(res.fdr), pScoreWeights(res.pScoreWeights), isScoreCalculated(res.isScoreCalculated), score(res.score), alternatives(res.alternatives) {}

		void setScoreWeights( const map< string, double >& scoreWeights )
		{
			pScoreWeights = &scoreWeights;
			isScoreCalculated = false;
			calculateTotalScore();
		}

		double fdr;
		// Stores all the alternative interpretations to the same
		// modification mass.
		vector <Peptide> alternatives;

		inline double getTotalScore() const
		{
			if( pScoreWeights == NULL )
				throw runtime_error( "Error: score weights not set, cannot calculate total score for results!" );
			return score;
		}

		inline float hasFScore() const {
			return isScoreCalculated;
		}

		/**
			This function returns the search scores a string representation.
			The function looks at the search score weights strings and arranges
			the scores in the returning string in that order.
		**/
		inline string getSearchScores(const vector<string>& scoreNames) const 
		{
			stringstream scoreString;
			for(size_t i = 0; i < scoreNames.size(); ++i) {
				for(size_t j = 0; j < scoreList.size(); ++j) {
					if(scoreList[j].first == scoreNames[i]) {
						if(i>0) 
							scoreString << " ";
						scoreString << scoreList[j].second;
					}
				}
			}
			return scoreString.str();
		}

        /* This function computes the f-score from a set of features. The weights of these features 
           are determined by the ScoreDiscriminant data structure. 
        */
        void computeDiscriminantScore(const map<string, double>& featureWeights, const set<string>& normalizedScoreNames)
        {
            double discriminantScore = 0.0;

            map<string, double> scoreMap;
            typedef pair<string, double> StringDoublePair;
            BOOST_FOREACH(const StringDoublePair& itr, scoreList)
                scoreMap.insert(itr);

            scoreMap["NET"] = specificTermini();
            scoreMap["NMC"] = missedCleavages();

            BOOST_FOREACH(const StringDoublePair& itr, featureWeights)
            {
                if (itr.first == "m0") // skip constant?
                    continue;

                double score = normalizedScoreNames.count(itr.first) > 0 ? scoreMap[itr.first + "_norm"] : scoreMap[itr.first];
                discriminantScore += score * itr.second;
            }

            scoreList.push_back(make_pair("f-score", discriminantScore));
        }

		/**
		operator< compares two search results based on rank (or score) first,
		peptide sequence, and modification mass.
		*/	
		bool operator< (const SearchResult& rhs) const {
			//cout << score << "," << sequence() << "," << modifications().monoisotopicDeltaMass() << "->" << rhs.score << "," << rhs.sequence() << "," << rhs.modifications().monoisotopicDeltaMass() << endl;
			float floatingPointDifference = fabs(modifications().monoisotopicDeltaMass()-rhs.modifications().monoisotopicDeltaMass());
			if( pScoreWeights == NULL ) {
				if(rank == rhs.rank) {
					if(sequence().length() == rhs.sequence().length() && sequence() == rhs.sequence()) {
						if(floatingPointDifference <= EPSILON) {
							return false;
						} else {
							return modifications().monoisotopicDeltaMass() < rhs.modifications().monoisotopicDeltaMass();
						}
					} else {
						return sequence() < rhs.sequence();
					}
				} else {
					return rank > rhs.rank;
				}
			}

			if(score == rhs.score) {
				if(sequence().length() == rhs.sequence().length() && sequence() == rhs.sequence()) {
					if(floatingPointDifference <= EPSILON) {
						return false;
					} else {
						return modifications().monoisotopicDeltaMass() < rhs.modifications().monoisotopicDeltaMass();
					}
				} else {
					return sequence() < rhs.sequence();
				}
			} else {
				return score < rhs.score;
			}
		}

		/**
		operator== compares two search results based on rank (or score) first,
		peptide sequence, and modification mass. It returns true iff all the
		above attribures between two different search results are equal.
		*/
		bool operator== ( const SearchResult& rhs ) const {
			float floatingPointDifference = fabs(modifications().monoisotopicDeltaMass()-rhs.modifications().monoisotopicDeltaMass());
			if( pScoreWeights == NULL ) {
				return (rank == rhs.rank && floatingPointDifference <= EPSILON && 
					sequence().length()== rhs.sequence().length() && sequence() == rhs.sequence());
			}

			return (score == rhs.score && floatingPointDifference <= EPSILON 
				&& sequence().length()== rhs.sequence().length() && sequence() == rhs.sequence());
		}

		/*bool operator< ( const SearchResult& rhs ) const
		{
			if( pScoreWeights == NULL )
			{
				if( rank == rhs.rank )
					return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
				else
					return rank < rhs.rank;
			}

			if( score == rhs.score )
				return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
			else
			return score < rhs.score;
			}

		bool operator== ( const SearchResult& rhs ) const
		{
			if( pScoreWeights == NULL ) {
				return ( rank == rhs.rank && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
						static_cast<const Peptide&>(rhs)));
			}

			return ( score == rhs.score && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
					static_cast<const Peptide&>(rhs)));
		}*/

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< GenericSearchResult >( *this );
			ar & fdr;
		}

	protected:
		void calculateTotalScore()
		{
			if( pScoreWeights == NULL )
				throw runtime_error( "Error: score weights not set, cannot calculate total score for results!" );

			if( !isScoreCalculated )
			{
				score = 0;
				for( size_t i=0; i < scoreList.size(); ++i )
				{
					map< string, double >::const_iterator itr = pScoreWeights->find( scoreList[i].first );
					if( itr != pScoreWeights->end() )
						score += scoreList[i].second * itr->second;
				}
				isScoreCalculated = true;
			}
		}

		
		const map< string, double >* pScoreWeights;
		bool isScoreCalculated;
		double score;
	};

	typedef map< string, string > permutation;

	struct ValueVectorLessThan
	{
		bool operator() ( const vector<double>& l, const vector<double>& r ) const
		{
			for( size_t i=0; i < l.size(); ++i )
			{
				if( l[i] != r[i] )
					return l[i] < r[i];
			}
			return false;
		}
	};

	struct permutable
	{
		virtual ~permutable() {}
		size_t baseCfgPos;

		virtual void initialize( permutation& currentState )
		{}

		virtual bool permute( permutation& currentState )
		{
			return false;
		}
	};

	struct permutable_d : public permutable
	{
		permutable_d( double _total, double _step, const vector<string>& _vars, size_t _precision )
			: total(_total), step(_step), vars(_vars), values( _vars.size(), 0 ), precision( _precision )
		{}

		void initialize( permutation& currentState )
		{
			pending.clear();
			applied.clear();
			for( size_t i=1; i < vars.size(); ++i )
				values[i] = 0;
			values[0] = total;
			for( size_t i=0; i < vars.size(); ++i ) {
				stringstream ss;
				ss.precision(6);
				ss << values[i];
				currentState[ vars[i] ] = ss.str();
				//currentState[ vars[i] ] = lexical_cast<string>( values[i] );
			}
		}

		bool permute( permutation& currentState )
		{
			for( size_t i=0; i < values.size()-1; ++i )
			{
				if( values[i] > 0 )
				{
					values[i] = round( values[i] - step, precision );
					for( size_t j=i+1; j < vars.size(); ++j )
					{
						double tmp = values[j];
						values[j] = round( values[j] + step, precision );
						vector<double> possible( values.begin(), values.end() );
						if( applied.count( possible ) == 0 )
							pending.insert( possible );
						values[j] = tmp;
					}
					break;
				}
			}

			if( pending.empty() )
				return false;

			const vector<double>& newValues = *applied.insert( *pending.begin() ).first;
			pending.erase( pending.begin() );
			for( size_t i=0; i < vars.size(); ++i )
			{
				values[i] = newValues[i];
				stringstream ss;
				ss.precision(14);
				ss << values[i];
				currentState[ vars[i] ] = ss.str();
				//currentState[ vars[i] ] = lexical_cast<string>( values[i] );
			}
			return true;
		}

		double total;
		double step;
		vector<string> vars;
		vector<double> values;
		set< vector<double>, ValueVectorLessThan > pending;
		set< vector<double>, ValueVectorLessThan > applied;
		size_t precision;
	};

	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, IDPQONVERT_RUNTIME_CONFIG, "\r\n\t ", "idpQonvert.cfg", "\r\n#" )

        fileList_t inputFilepaths;
        vector< permutation > permutations;
        vector< string > searchScoreNames;
        set<string> normalizedSearchScores;

        enum NormalizationMethodEnum
        {
            NormalizationMethod_Quantile,
            NormalizationMethod_Linear
        };
        NormalizationMethodEnum normalizationMethod;

		void transmogrify( permutable* p, permutation& currentState )
		{
			p->initialize( currentState );
			permutations.push_back( currentState );
			while( p->permute( currentState ) )
				permutations.push_back( currentState );
		}

	private:
		void finalize()
		{
            // named normalized scores override the global flag
            if( !NormalizedSearchScores.empty() )
                NormalizeSearchScores = false;

            if( PercolatorScore && OptimizeScoreWeights )
            {
                cerr << "Warning: OptimizeScoreWeights disabled in PercolatorScore mode." << endl;
                OptimizeScoreWeights = false;
            }

			if( OptimizeScoreWeights && NormalizedSearchScores.empty() )
				cerr << "Warning: OptimizeScoreWeights without NormalizedSearchScores is not recommended." << endl;

            if( NormalizationMethod == "quantile" )
                normalizationMethod = NormalizationMethod_Quantile;
            else if( NormalizationMethod == "linear" )
                normalizationMethod = NormalizationMethod_Linear;
            else
                throw runtime_error("invalid NormalizationMethod (must be 'quantile' or 'linear')");

			permutations.clear();
			vector<string> tokens;

            split( tokens, NormalizedSearchScores, boost::is_space() );
            for( size_t i=0; i < tokens.size(); ++i)
                normalizedSearchScores.insert(to_lower_copy(tokens[i]));

			split( tokens, SearchScoreWeights, boost::is_space() );
			searchScoreNames.resize( tokens.size() / 2 );

            vector<string> normalizedSearchScoreNames;
			for( size_t i=0; i < searchScoreNames.size(); ++i )
			{
				searchScoreNames[i] = to_lower_copy( tokens[i*2] );

                if (NormalizeSearchScores)
                    normalizedSearchScores.insert(searchScoreNames[i]);

                if (normalizedSearchScores.find(searchScoreNames[i]) != normalizedSearchScores.end())
				    normalizedSearchScoreNames.push_back(searchScoreNames[i] + "_norm");
                else
                    normalizedSearchScoreNames.push_back(searchScoreNames[i]);
			}

			vector<string>& permutationScoreNames = normalizedSearchScoreNames;

			permutation initialState;
			permutable_d scorePermutable( 1.0f, 0.1f, permutationScoreNames, 1 );
			transmogrify( &scorePermutable, initialState );

			// for each score, make its score weights negative if its input weight was negative
			for( size_t i=0; i < permutationScoreNames.size(); ++i )
			{
				if( lexical_cast< float >( tokens[i*2+1] ) < 0 )
					for( size_t j=0; j < permutations.size(); ++j )
					{
						permutation::iterator itr = permutations[j].find( permutationScoreNames[i] );
						itr->second = string("-") + itr->second;
					}
			}
		}
	};

	extern RunTimeConfig* g_rtConfig;

	struct AlphaIndex : public string
	{
		AlphaIndex( const string& a = "A" ) : string(a) {}
		AlphaIndex( size_t n ) : string("A")
		{
			for( ; n > 0; --n )
				this->operator++();
		}

		AlphaIndex& operator++()
		{
			incrementChar( --end() );
			return *this;
		}

		AlphaIndex& operator=( const string& a )
		{
			this->string::operator=(a);
			return *this;
		}

	private:
		void incrementChar( string::iterator itr )
		{
			if( *itr == 'Z' )
			{
				*itr = 'A';
				if( itr == begin() )
					push_back('A');
				else
					incrementChar( --itr );
			} else
				++ *itr;
		}
	};

	struct ProteinInfo;
	struct PeptideInfo;
	struct Spectrum;

	struct PeptideInfo : public DigestedPeptide
	{
		PeptideInfo( const string& seq = "" )
			:	DigestedPeptide( seq ), id(0) {}

		PeptideInfo( const DigestedPeptide& c )
			:	DigestedPeptide( c ), id(0) {}

		size_t					id;
		set< ProteinInfo* >		proteins;
		set< Spectrum* >		spectra;

		bool operator< ( const PeptideInfo& rhs ) const
		{
			return sequence() < rhs.sequence();
		}
	};

	struct ProteinInfo : public proteinData
	{
		ProteinInfo( const string& name )
			: proteinData(ProteinPtr(new Protein(name, 0, "", "")), false), pIndex(-1), totalMass(0), totalLength(0)
		{}

		ProteinInfo( const proteinData& baseProtein = proteinData(ProteinPtr(new Protein("", 0, "", "")), false) )
			: proteinData(baseProtein), pIndex(-1), totalMass(0), totalLength(0)
		{
		}

		int						pIndex;
		double					totalMass;
		int						totalLength;
		set< PeptideInfo* >		peptides;
		set< Spectrum* >		spectra;

		bool operator< ( const ProteinInfo& rhs ) const
		{
			return getName() < rhs.getName();
		}
	};

	struct Spectrum : public SearchSpectrum< SearchResult >
	{
		Spectrum() : BaseSpectrum(), SearchSpectrum< SearchResult >() {}
		string group;
	};

	struct SpectraList : public	SearchSpectraList< Spectrum, SpectraList >
	{
		using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
		using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;

		XML_Parser parser;
		RunTimeVariableMap	analysisParameters;
		size_t curId;
		size_t curCluster;
		size_t curOffset;
		vector< size_t > variantIds;
		set< size_t > locusIds;
		string curSource;
		string setGroup;
		string curGroup;
		Spectrum* curSpectrum;
		double maxFDR;
		size_t maxRank;

		PeptideInfo* curPeptide;

		typedef map< size_t, ProteinInfo* > ProteinIndex;
		typedef map< size_t, PeptideInfo* > PeptideIndex;

		ProteinIndex proteinIndex;
		PeptideIndex peptideIndex;

		struct SourceIndexInfo
		{
			SourceIndexInfo( const string& n, const string& g ) : name(n), group(g) {}
			string name;
			string group;
		};

		typedef map< size_t, SourceIndexInfo >	SourceIndex;

		SourceIndex sourceIndex;

		map< string, ProteinInfo >	proteins;
		map< string, PeptideInfo >	peptides;

		typedef map< string, set< Spectrum*, spectraSortByID > >				SourceReverseIndex;
		typedef map< string, pair< size_t, PeptideInfo* > >	PeptideReverseIndex;
		typedef map< string, pair< size_t, ProteinInfo* > >	ProteinReverseIndex;

		void generateReverseIndex( SourceReverseIndex& src, PeptideReverseIndex& pep, ProteinReverseIndex& pro )
		{
			for( map< string, PeptideInfo >::iterator itr = peptides.begin(); itr != peptides.end(); ++itr )
			{
				for( set< Spectrum* >::iterator itr2 = itr->second.spectra.begin(); itr2 != itr->second.spectra.end(); ++itr2 )
				{
					Spectrum* s = *itr2;
					s->resultSet.calculateRanks();
					src[ s->id.source ].insert(s);
				}

				size_t id = pep.size() + 1;
				pep[ itr->first ].first = id;
				pep[ itr->first ].second = &itr->second;
			}

			for( map< string, ProteinInfo >::iterator itr = proteins.begin(); itr != proteins.end(); ++itr )
			{
				size_t id = pro.size() + 1;
				pro[ itr->first ].first = id;
				pro[ itr->first ].second = &itr->second;
			}
		}

		void normalizeSearchScores( const set<string>& scoreNames, int numChargeStates );
		string writePeptides(	const string& header,
			const RunTimeVariableMap& idVars,
			const string& sourceHeader = "",
			const string& validationStartTime = "",
			const string& validationEndTime = "",
			size_t maxRank = 1 );
	};
}
}

#endif
