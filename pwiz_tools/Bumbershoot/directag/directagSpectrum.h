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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris, Zeqiang Ma
//

#ifndef _DIRECTAGSPECTRUM_H
#define _DIRECTAGSPECTRUM_H

#include "stdafx.h"
#include "freicore.h"
#include "directagConfig.h"
#include "SearchSpectrum.h"
#include "tagsFile.h"
#include "PeakSpectrum.h"

using namespace freicore;

namespace freicore
{
namespace directag
{
	typedef map< double, double >						MzFidelityErrorBins, MzFEBins;
	typedef map< double, double >						ComplementErrorBins, CEBins;
	typedef vector< CEBins >						CEBinsList;
	typedef vector< double >							IntensityRanksumBins, IRBins;
	typedef vector< vector< IRBins > >				IntensityRanksumBinsByPeakCountAndTagLength, IRBinsTable;

	typedef set< double >						nodeSet_t;
	typedef MvhTable				bgProbabilities_t, bgComplements_t, bgFidelities_t;
	struct GapInfo;
	typedef vector< GapInfo >					gapVector_t;
	typedef map< double, gapVector_t >			gapMap_t;		// peakMz -> vector of TagInfos for individual peaks

	typedef float								sgNode;
	struct sgNodeInfo
	{
		sgNodeInfo() : nPathSize(0), cPathSize(0) {}
		vector< GapInfo >	nEdges;			// edges in the N terminus direction
		vector< GapInfo >	cEdges;			// edges in the C terminus direction
		set< string >		nPathSequences;
		set< string >		cPathSequences;
		set< string >		fullPathSequences;
		int					nPathSize;
		int					cPathSize;
		int					longestPath;
	};

	typedef map< sgNode, sgNodeInfo >			spectrumGraph;

	struct PeakInfo
	{
		PeakInfo()
			:	hasComplementAsCharge( g_rtConfig->NumChargeStates-1, false )
		{}

		template< class Archive >
		void serialize( Archive& ar, const int unsigned version )
		{
			ar & intensityRank;
			//ar & longestPathRank;
			ar & hasSomeComplement;
			ar & hasComplementAsCharge;
		}

		int		intensityRank;
		//int		longestPathRank;
		char	hasSomeComplement;
		double	intensity;

		vector< bool > hasComplementAsCharge;
	};

	typedef BasePeakData< PeakInfo > PeakData;

	struct SearchResult : public GenericSearchResult
	{
		SearchResult( const GenericSearchResult& r = GenericSearchResult() )
			:	GenericSearchResult( r ), fdr( 1 ), pScoreWeights(NULL), isScoreCalculated( false ), score( 0 )
		{}

		void setScoreWeights( const map< string, double >& scoreWeights )
		{
			pScoreWeights = &scoreWeights;
			isScoreCalculated = false;
			calculateTotalScore();
		}

		double fdr;

		inline double getTotalScore() const
		{
			if( pScoreWeights == NULL )
				throw runtime_error( "Error: score weights not set, cannot calculate total score for results!" );
			return score;
		}

		bool operator< ( const SearchResult& rhs ) const
		{
			if( pScoreWeights == NULL )
			{
				if( rank == rhs.rank )
                    return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
				else
					return rank < rhs.rank;
			}

			if( score == rhs.score )
				if( mod == rhs.mod )
                    return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
				else
					return mod > rhs.mod;
			else
				return score < rhs.score;
		}

		bool operator== ( const SearchResult& rhs ) const
		{
			if( pScoreWeights == NULL )
				return ( rank == rhs.rank && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
														         static_cast<const Peptide&>(rhs)) );
			return ( score == rhs.score && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
														       static_cast<const Peptide&>(rhs)) );
		}

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

	struct Spectrum : public PeakSpectrum< PeakInfo >, TaggingSpectrum, SearchSpectrum<SearchResult>
	{
		Spectrum();
		Spectrum( const Spectrum& old );

		void initialize( int numIntenClasses, int NumMzFidelityClasses )
		{
			complementClassCounts.resize( 2 /* binary */, 0 );
		}

		void Parse( bool intenAsClasses = false );
		void ClassifyPeakIntensities();
		double FindComplements( double complementMzTolerance );
		size_t MakeTagGraph();
		void MakeProbabilityTables();
		void FilterPeaks();
		void Preprocess();
		size_t Score();

		void findTags_R(	gapMap_t::iterator gapInfoItr,
							int tagIndex,
							string& tag,
							vector< double >& peakErrors,
							vector< PeakData::iterator >& peakList,
							int peakChargeState,
							size_t& numTagsGenerated,
							IRBins& irBins );
		size_t findTags();

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< BaseSpectrum >( *this );
			ar & boost::serialization::base_object< PeakSpectrum< PeakInfo > >( *this );
			ar & boost::serialization::base_object< TaggingSpectrum >( *this );
			ar & boost::serialization::base_object< SearchSpectrum< SearchResult > >( *this );

			ar & complementClassCounts & scoreWeights;
			ar & complementScoreWeight & intensityScoreWeight & mzFidelityScoreWeight;
		}

		TagList					validTagList;

		map< string, double >	scoreWeights;
		float					complementScoreWeight;
		float					intensityScoreWeight;
		float					mzFidelityScoreWeight;

		vector< int >			complementClassCounts;
	
		//Histogram< int >		intensityScoreHistogram;

        double                   complementaryTIC;
        int                     tagGraphPeakCount;
        double                   tagGraphTIC;

		vector< gapMap_t >		gapMaps;			// a graph of peaks to peaks that are a residue's width away
		nodeSet_t				nodeSet;			// the set of peaks used to build the residue-width graph
		TagList					interimTagList;
		vector< spectrumGraph >	tagGraphs;
		map< int, double >		complementPDF;

		bgComplements_t			bgComplements;

		// code for ScanRanker
		float					bestTagScore;
		float					bestTagTIC;
		float					tagMzRange;
		float					bestTagScoreNorm;
		float					bestTagTICNorm;
		float					tagMzRangeNorm;
		float					qualScore;
	};

	struct SpectraList : public	PeakSpectraList< Spectrum, SpectraList >,
								TaggingSpectraList< Spectrum, SpectraList >,
								SearchSpectraList< Spectrum, SpectraList >
	{
		typedef BaseSpectraList<Spectrum, SpectraList>::ListConstIterator	ListConstIterator;
		typedef BaseSpectraList<Spectrum, SpectraList>::ListIterator			ListIterator;

		static void					InitCEBins();

		static void					InitIRBins();
		static void					PrecacheIRBins( SpectraList& instance );
		static void					CalculateIRBins( int tagLength, int numPeaks );
		static void					CalculateIRBins_R( IRBins& theseIRBins, int tagLength, int numPeaks, int curRanksum, int curRank, int loopDepth );

		static void					InitMzFEBins();

		static CEBinsList			complementErrorBinsList;
		static IRBinsTable			intensityRanksumBinsTable;
		static MzFEBins				mzFidelityErrorBins;

		using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
		using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;
	};

	typedef vector< TagInfo >					tagNode_t;

	struct GapInfo
	{
		GapInfo( PeakData::iterator itr1, PeakData::iterator itr2, gapMap_t::iterator itr3, double a=0, char r='Z', double b=0, double c=0, double d=0 )
			: fromPeakItr(itr1), peakItr(itr2), nextGapInfo(itr3), gapMass(a), gapRes(r), error(b), nTermMz(c), cTermMz(d) {}
		PeakData::iterator fromPeakItr;
		PeakData::iterator peakItr;
		gapMap_t::iterator nextGapInfo;
		double	gapMass;		// Set while processing (for findTags)
		char	gapRes;
		double	error;				// Set while processing (for findTags)
		double	nTermMz, cTermMz;
	};
}
}

namespace std
{
	ostream& operator<< ( ostream& o, const freicore::directag::PeakInfo& rhs );
	ostream& operator<< ( ostream& o, const freicore::directag::GapInfo& rhs );
	ostream& operator<< ( ostream& o, const freicore::directag::gapVector_t& rhs );
	ostream& operator<< ( ostream& o, const freicore::directag::gapMap_t& rhs );
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::directag::PeakInfo, boost::serialization::object_serializable )
BOOST_CLASS_IMPLEMENTATION( freicore::directag::PeakData, boost::serialization::object_serializable )
BOOST_CLASS_IMPLEMENTATION( freicore::directag::Spectrum, boost::serialization::object_serializable )

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::directag::PeakInfo, boost::serialization::track_never )
BOOST_CLASS_TRACKING( freicore::directag::PeakData, boost::serialization::track_never )
BOOST_CLASS_TRACKING( freicore::directag::Spectrum, boost::serialization::track_never )

#endif
