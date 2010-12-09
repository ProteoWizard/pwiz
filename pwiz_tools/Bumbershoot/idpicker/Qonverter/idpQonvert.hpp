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

#include "BaseRunTimeConfig.h"
#include "Qonverter.hpp"
#include <iostream>
#include <sstream>
#include <string>

#define IDPQONVERT_RUNTIME_CONFIG \
	COMMON_RTCONFIG \
	RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
	RTCONFIG_VARIABLE( bool,			WriteQonversionDetails,		false			) \
	RTCONFIG_VARIABLE( string,			ProteinDatabase,			""				) \
	RTCONFIG_VARIABLE( string,          ProteinDatabaseSearchPath,  ""              ) \
	RTCONFIG_VARIABLE( string,			DecoyPrefix,				"rev_"			) \
	RTCONFIG_VARIABLE( double,			MaxFDR,						0.25			) \
	RTCONFIG_VARIABLE( int,				MaxResultRank,				1				) \
	RTCONFIG_VARIABLE( string,			SearchScoreWeights,			"mvh 1 xcorr 1 expect -1 ionscore 1" ) \
	RTCONFIG_VARIABLE( bool,			NormalizeSearchScores,		false			) \
    RTCONFIG_VARIABLE( string,			NormalizedSearchScores,		""              ) \
    RTCONFIG_VARIABLE( string,          NormalizationMethod,        "quantile"      ) /*\
	RTCONFIG_VARIABLE( bool,			OptimizeScoreWeights,		false			) \
	RTCONFIG_VARIABLE( int,				OptimizeScorePermutations,	200				) \
	/*RTCONFIG_VARIABLE( bool,			HasDecoyDatabase,			true			) \
	RTCONFIG_VARIABLE( bool,            PreserveInputHierarchy,     false           ) \
    RTCONFIG_VARIABLE( bool,            PercolatorScore,            false           ) \
    RTCONFIG_VARIABLE( bool,            PercolatorReranking,        false           )*/


BEGIN_IDPICKER_NAMESPACE


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

struct RunTimeConfig : public freicore::BaseRunTimeConfig
{
public:
	RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, IDPQONVERT_RUNTIME_CONFIG, "\r\n\t ", "idpQonvert.cfg", "\r\n#" )

    fileList_t inputFilepaths;
    map<string, Qonverter::Settings::ScoreInfo> scoreInfoByName;

private:
	void finalize()
	{
        // named normalized scores override the global flag
        if( !NormalizedSearchScores.empty() )
            NormalizeSearchScores = false;

        /*if( PercolatorScore && OptimizeScoreWeights )
        {
            cerr << "Warning: OptimizeScoreWeights disabled in PercolatorScore mode." << endl;
            OptimizeScoreWeights = false;
        }

		if( OptimizeScoreWeights && NormalizedSearchScores.empty() )
			cerr << "Warning: OptimizeScoreWeights without NormalizedSearchScores is not recommended." << endl;
*/
        Qonverter::Settings::NormalizationMethod normalizationMethod;
        if( NormalizationMethod == "quantile" )
            normalizationMethod = Qonverter::Settings::NormalizationMethod_Quantile;
        else if( NormalizationMethod == "linear" )
            normalizationMethod = Qonverter::Settings::NormalizationMethod_Linear;
        else
            throw runtime_error("invalid NormalizationMethod (must be 'quantile' or 'linear')");

		//permutations.clear();
		vector<string> tokens;

        set<string> normalizedSearchScores;
        split( tokens, NormalizedSearchScores, boost::is_space() );
        for( size_t i=0; i < tokens.size(); ++i)
            normalizedSearchScores.insert(to_lower_copy(tokens[i]));

		split( tokens, SearchScoreWeights, boost::is_space() );

		for( size_t i=0; i < tokens.size(); i += 2 )
		{
            to_lower(tokens[i]);
            Qonverter::Settings::ScoreInfo& scoreInfo = scoreInfoByName[tokens[i]];
            scoreInfo.weight = lexical_cast<double>(tokens[i+1]);
            scoreInfo.order = scoreInfo.weight >= 0 ? Qonverter::Settings::Order_Ascending
                                                    : Qonverter::Settings::Order_Descending;
            scoreInfo.weight = std::abs(scoreInfo.weight);
            scoreInfo.normalizationMethod = NormalizeSearchScores ||
                                            normalizedSearchScores.count(tokens[i]) ? normalizationMethod
                                                                                    : Qonverter::Settings::NormalizationMethod_Off;
		}

		/*vector<string>& permutationScoreNames = normalizedSearchScoreNames;

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
		}*/
	}
};

extern RunTimeConfig* g_rtConfig;


END_IDPICKER_NAMESPACE

#endif
