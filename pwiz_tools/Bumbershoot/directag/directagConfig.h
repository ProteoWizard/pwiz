//
// $Id: directagConfig.h 11 2009-10-12 17:22:20Z chambm $
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
// Contributor(s): Surendra Dasaris
//

#ifndef _DIRECTAGCONFIG_H
#define _DIRECTAGCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"

using namespace freicore;

#define TAG_ONLY_HITS	0
#define TAG_ALWAYS		1
#define TAG_ONLY_MISSES	2

#define DIRECTAG_RUNTIME_CONFIG \
	COMMON_RTCONFIG SPECTRUM_RTCONFIG SEQUENCE_RTCONFIG MULTITHREAD_RTCONFIG VALIDATION_RTCONFIG \
	RTCONFIG_VARIABLE( string,			OutputSuffix,				"-tags"			) \
	RTCONFIG_VARIABLE( string,			InlineValidationFile,		""				) \
	RTCONFIG_VARIABLE( int,				InlineValidationMode,		TAG_ONLY_HITS	) \
	RTCONFIG_VARIABLE( bool,			AlwaysKeepValidTags,		true			) \
	RTCONFIG_VARIABLE( int,				MaxTagCount,				50				) \
	RTCONFIG_VARIABLE( double,			MaxTagScore,				20.0			) \
	RTCONFIG_VARIABLE( int,				NumIntensityClasses,		3				) \
	RTCONFIG_VARIABLE( int,				NumMzFidelityClasses,		3				) \
	RTCONFIG_VARIABLE( int,				TagLength,					3				) \
	RTCONFIG_VARIABLE( int,				StartSpectraScanNum,		0				) \
	RTCONFIG_VARIABLE( int,				EndSpectraScanNum,			-1				) \
	RTCONFIG_VARIABLE( int,				NumBatches,					50				) \
	RTCONFIG_VARIABLE( double,			TicCutoffPercentage,		1.0			) \
	RTCONFIG_VARIABLE( size_t,			MaxPeakCount,				100				) \
	RTCONFIG_VARIABLE( double,			ClassSizeMultiplier,		2.0			) \
	RTCONFIG_VARIABLE( double,			MinPrecursorAdjustment,		-2.5			) \
	RTCONFIG_VARIABLE( double,			MaxPrecursorAdjustment,		2.5			) \
	RTCONFIG_VARIABLE( double,			PrecursorAdjustmentStep,	0.1			) \
	RTCONFIG_VARIABLE( bool,			NormalizeOnMode,			true			) \
	RTCONFIG_VARIABLE( bool,			AdjustPrecursorMass,		false			) \
	RTCONFIG_VARIABLE( int,				DeisotopingMode,			1				) \
	RTCONFIG_VARIABLE( bool,			MakeSpectrumGraphs,			false			) \
	RTCONFIG_VARIABLE( int,				MzFidelityErrorBinsSize,	20				) \
	RTCONFIG_VARIABLE( int,				MzFidelityErrorBinsSamples,	10000		) \
	RTCONFIG_VARIABLE( double,			MzFidelityErrorBinsLogMin,	-5.0			) \
	RTCONFIG_VARIABLE( double,			MzFidelityErrorBinsLogMax,	1.0			) \
	\
	RTCONFIG_VARIABLE( double,			IntensityScoreWeight,		1.0			) \
	RTCONFIG_VARIABLE( double,			MzFidelityScoreWeight,		1.0			) \
	RTCONFIG_VARIABLE( double,			ComplementScoreWeight,		1.0			) \
	RTCONFIG_VARIABLE( double,			RandomScoreWeight,			0.0			)

namespace freicore
{
namespace directag
{

	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		BOOST_PP_SEQ_FOR_EACH( RTCONFIG_DECLARE_VAR, ~, DIRECTAG_RUNTIME_CONFIG )

		RunTimeConfig() : BaseRunTimeConfig(), rng(0), RandomScoreRange( 0.0, 0.99999999999 ), GetRandomScore( rng, RandomScoreRange )
		{
			BOOST_PP_SEQ_FOR_EACH( RTCONFIG_INIT_DEFAULT_VAR, ~, DIRECTAG_RUNTIME_CONFIG )
		}

		void initializeFromBuffer( string& cfgStr, const string& delim = "\r\n\t " )
		{
			BaseRunTimeConfig::initializeFromBuffer( cfgStr, delim );
			string strVal;
			BOOST_PP_SEQ_FOR_EACH( RTCONFIG_PARSE_BUFFER, ~, DIRECTAG_RUNTIME_CONFIG )
			finalize();
		}

		RunTimeVariableMap getVariables( bool hideDefaultValues = false )
		{
			BaseRunTimeConfig::getVariables();
			BOOST_PP_SEQ_FOR_EACH( RTCONFIG_FILL_MAP, m_variables, DIRECTAG_RUNTIME_CONFIG )
			return m_variables;
		}

		void setVariables( RunTimeVariableMap& vars )
		{
			BaseRunTimeConfig::setVariables( vars );
			BOOST_PP_SEQ_FOR_EACH( RTCONFIG_READ_MAP, vars, DIRECTAG_RUNTIME_CONFIG )
			finalize();
		}

		int initializeFromFile( const string& rtConfigFilename = "directag.cfg" )
		{
			return BaseRunTimeConfig::initializeFromFile( rtConfigFilename );
		}

		vector< float >			scoreThresholds;

		ResidueMap				inlineValidationResidues;
		tagMetaIndex_t			tagMetaIndex;

		map< string, float >	compositionScoreMap;

		boost::mt19937 rng;
		boost::uniform_real<> RandomScoreRange;
		boost::variate_generator< boost::mt19937&, boost::uniform_real<> > GetRandomScore;

		int		SpectraBatchSize;
		int		ProteinBatchSize;
		int		minIntensityClassCount;
		int		minMzFidelityClassCount;
		int		tagPeakCount;
		double	MzFidelityErrorBinsScaling;
		double	MzFidelityErrorBinsOffset;

	protected:
		void finalize()
		{
			BaseRunTimeConfig::finalize();

			string cwd;
			cwd.resize( MAX_PATH );
			getcwd( &cwd[0], MAX_PATH );
			WorkingDirectory = cwd.c_str();

			if( TicCutoffPercentage > 1.0f )
			{
				TicCutoffPercentage /= 100.0f;
				if( g_pid == 0 )
					cerr << g_hostString << ": TicCutoffPercentage > 1.0 (100%) corrected, now at: " << TicCutoffPercentage << endl;
			}

			if( !DynamicMods.empty() )
			{
				DynamicMods = TrimWhitespace( DynamicMods );
				g_residueMap->setDynamicMods( DynamicMods );
			}

			if( !StaticMods.empty() )
			{
				StaticMods = TrimWhitespace( StaticMods );
				g_residueMap->setStaticMods( StaticMods );
			}

			//if( g_residueMap )
			//	inlineValidationResidues = *g_residueMap;

			tagPeakCount = TagLength + 1;

			double m = ClassSizeMultiplier;
			if( m > 1 )
			{
				minIntensityClassCount = int( ( pow( m, NumIntensityClasses ) - 1 ) / ( m - 1 ) );
				minMzFidelityClassCount = int( ( pow( m, NumMzFidelityClasses ) - 1 ) / ( m - 1 ) );
			} else
			{
				minIntensityClassCount = NumIntensityClasses;
				minMzFidelityClassCount = NumMzFidelityClasses;
			}
		}
	};

	extern RunTimeConfig*					g_rtConfig;
}
}

#endif
