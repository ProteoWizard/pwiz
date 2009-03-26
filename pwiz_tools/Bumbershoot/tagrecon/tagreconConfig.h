#ifndef _TAGRECONCONFIG_H
#define _TAGRECONCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"

using namespace freicore;

// Program parameters

#define DMM_MATCH_KNOWN_MASSES	0
#define DMM_MATCH_EXACT_MASSES	1
#define DMM_MATCH_ALL_MASSES		2

#define TAGRECON_RUNTIME_CONFIG \
    COMMON_RTCONFIG SPECTRUM_RTCONFIG SEQUENCE_RTCONFIG MULTITHREAD_RTCONFIG \
    RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
    RTCONFIG_VARIABLE( string,          ProteinDatabase,            ""              ) \
    RTCONFIG_VARIABLE( string,          UnimodXML,		            ""              ) \
    RTCONFIG_VARIABLE( string,          Blosum,						""              ) \
    RTCONFIG_VARIABLE( string,          FragmentationRule,          "cid"           ) \
    RTCONFIG_VARIABLE( bool,            FragmentationAutoRule,      true            ) \
    RTCONFIG_VARIABLE( int,				MaxResults,					5				) \
    RTCONFIG_VARIABLE( int,				NumIntensityClasses,		3				) \
    RTCONFIG_VARIABLE( int,				NumMzFidelityClasses,		3				) \
    RTCONFIG_VARIABLE( int,				StartSpectraScanNum,		0				) \
    RTCONFIG_VARIABLE( int,				EndSpectraScanNum,			-1				) \
    RTCONFIG_VARIABLE( int,				NumBatches,					50				) \
    RTCONFIG_VARIABLE( float,			TicCutoffPercentage,		0.98f			) \
    RTCONFIG_VARIABLE( float,			ClassSizeMultiplier,		2.0f			) \
    RTCONFIG_VARIABLE( float,			IntensityScoreWeight,		1.0f			) \
    RTCONFIG_VARIABLE( float,			MinSequenceMass,			0.0f			) \
    RTCONFIG_VARIABLE( float,			MaxSequenceMass,			10000.0f 		) \
    RTCONFIG_VARIABLE( int,				MaxSequenceLength,	        75				) \
    RTCONFIG_VARIABLE( bool,			PreferIntenseComplements,	true			) \
    RTCONFIG_VARIABLE( bool,			AdjustPrecursorMass,		false			) \
    RTCONFIG_VARIABLE( float,			MinPrecursorAdjustment,		-2.5f			) \
    RTCONFIG_VARIABLE( float,			MaxPrecursorAdjustment,		2.5f			) \
    RTCONFIG_VARIABLE( float,			PrecursorAdjustmentStep,	0.1f			) \
    RTCONFIG_VARIABLE( int,				NumSearchBestAdjustments,	1				) \
    RTCONFIG_VARIABLE( int,				DeisotopingMode,			0				) \
    RTCONFIG_VARIABLE( int,				ProteinSamplingTime,		15				) \
    RTCONFIG_VARIABLE( bool,			EstimateSearchTimeOnly,		false			) \
    RTCONFIG_VARIABLE( int,				ProteinSampleSize,			100				) \
    RTCONFIG_VARIABLE( int,				StartProteinIndex,			0				) \
    RTCONFIG_VARIABLE( int,				EndProteinIndex,			-1				) \
    RTCONFIG_VARIABLE( string,			CleavageRules,				"[|K|R . . ]"	) \
    RTCONFIG_VARIABLE( string,          DigestionRules,             "[KR]|"         ) \
    RTCONFIG_VARIABLE( int,				NumMinTerminiCleavages,		2				) \
    RTCONFIG_VARIABLE( int,				NumMaxMissedCleavages,		-1				) \
    RTCONFIG_VARIABLE( int,				MinCandidateLength,			5				) \
    RTCONFIG_VARIABLE( float,			NTerminusMassTolerance,		2.5f			) \
    RTCONFIG_VARIABLE( float,			CTerminusMassTolerance,		1.0f			) \
    RTCONFIG_VARIABLE( bool,			AllowPartialSequences,		true			) \
    RTCONFIG_VARIABLE( int,				MaxTagCount,				0				) \
    RTCONFIG_VARIABLE( int,				MaxTagLength,				3				) \
    RTCONFIG_VARIABLE( bool,			FindUnknownMods,			false			) \
    RTCONFIG_VARIABLE( bool,			FindSequenceVariations,		false			) \
    RTCONFIG_VARIABLE( int,				BlosumThreshold,			0				) \
    RTCONFIG_VARIABLE( bool,			CalculateRelativeScores,	false			) \
    RTCONFIG_VARIABLE( bool,			MakeSpectrumGraphs,			false			) \
    RTCONFIG_VARIABLE( bool,			MakeScoreHistograms,		false			) \
    RTCONFIG_VARIABLE( int,				NumScoreHistogramBins,		100				) \
    RTCONFIG_VARIABLE( int,				MaxScoreHistogramValues,	100				) \
    RTCONFIG_VARIABLE( int,				ScoreHistogramWidth,		800				) \
    RTCONFIG_VARIABLE( int,				ScoreHistogramHeight,		600				) \
    RTCONFIG_VARIABLE( int,				MaxFragmentChargeState,		0				) \
    RTCONFIG_VARIABLE( double,			MaxTagMassDeviation,		300.0			) \
    RTCONFIG_VARIABLE( double,			NTerminusMzTolerance,		0.75 			) \
	RTCONFIG_VARIABLE( double,			CTerminusMzTolerance,		0.5	    		) 
    


namespace freicore
{
    namespace tagrecon
    {

        struct TagreconRunTimeConfig : public BaseRunTimeConfig
        {
        public:
            RTCONFIG_DEFINE_MEMBERS( TagreconRunTimeConfig, TAGRECON_RUNTIME_CONFIG, "\r\n\t ", "tagrecon.cfg", "\r\n#" )

                CleavageRuleSet	_CleavageRules;
            vector<Digestion::Motif> digestionMotifs;
            Digestion::Config digestionConfig;

            FragmentTypesBitset defaultFragmentTypes;

            int				SpectraBatchSize;
            int				ProteinBatchSize;
            int				ProteinIndexOffset;
            float			curMinSequenceMass;
            float			curMaxSequenceMass;
            int				minIntensityClassCount;
            int				minMzFidelityClassCount;
            bool			hasDynamicMods;
            // Mass tolerances according to the charge state.
            vector<float>	PrecursorMassTolerance;
            vector<float>   NTerminalMassTolerance;
            vector<float>   CTerminalMassTolerance;
            bool			tagMutexesInitialized;

        private:
            void finalize()
            {
                tagMutexesInitialized = false;

                //cout << "ProteinDatabase:" << ProteinDatabase << "\n";

                stringstream CleavageRulesStream( CleavageRules );
                _CleavageRules.clear();
                CleavageRulesStream >> _CleavageRules;

                vector<string> motifs;
                boost::split(motifs, DigestionRules, boost::is_space());
                digestionMotifs.clear();
                digestionMotifs.insert(digestionMotifs.end(), motifs.begin(), motifs.end());


                int maxMissedCleavages = NumMaxMissedCleavages < 0 ? 100000 : NumMaxMissedCleavages;
                Digestion::Specificity specificity = (Digestion::Specificity) NumMinTerminiCleavages;
                // maxLength of a peptide is the max_sequence_mass/mass_of_averagine_molecule
                int maxLength = (int) (MaxSequenceMass/111.1254f);
                digestionConfig = Digestion::Config( maxMissedCleavages,
                    MinCandidateLength,
                    maxLength,
                    specificity );

                vector<string> fragmentationRuleTokens;
                split( fragmentationRuleTokens, FragmentationRule, is_any_of(":") );
                if( fragmentationRuleTokens.empty() )
                    throw runtime_error("invalid blank fragmentation rule");

                const string& mode = fragmentationRuleTokens[0];
                if( mode == "cid" )
                {
                    defaultFragmentTypes[FragmentType_B] = true;
                    defaultFragmentTypes[FragmentType_Y] = true;
                } else if( mode == "etd" )
                {
                    defaultFragmentTypes[FragmentType_C] = true;
                    defaultFragmentTypes[FragmentType_Z_Radical] = true;
                } else if( mode == "manual" )
                {
                    if( fragmentationRuleTokens.size() != 2 )
                        throw runtime_error("manual fragmentation mode requires comma-separated list, e.g. 'manual:b,y'");

                    vector<string> fragmentTypeTokens;
                    split( fragmentTypeTokens, fragmentationRuleTokens[1], is_any_of(",") );

                    if( fragmentTypeTokens.empty() )
                        throw runtime_error("no fragment types specified for manual fragmentation mode");

                    for( size_t i=0; i < fragmentTypeTokens.size(); ++i )
                    {
                        string fragmentType = to_lower_copy(fragmentTypeTokens[i]);
                        if( fragmentType == "a" )
                            defaultFragmentTypes[FragmentType_A] = true;
                        else if( fragmentType == "b" )
                            defaultFragmentTypes[FragmentType_B] = true;
                        else if( fragmentType == "c" )
                            defaultFragmentTypes[FragmentType_C] = true;
                        else if( fragmentType == "x" )
                            defaultFragmentTypes[FragmentType_X] = true;
                        else if( fragmentType == "y" )
                            defaultFragmentTypes[FragmentType_Y] = true;
                        else if( fragmentType == "z" )
                            defaultFragmentTypes[FragmentType_Z] = true;
                        else if( fragmentType == "z*" )
                            defaultFragmentTypes[FragmentType_Z_Radical] = true;
                    }
                } else
                    throw runtime_error("invalid fragmentation mode \"" + mode + "\"");

                ProteinIndexOffset = 0;

                string cwd;
                cwd.resize( MAX_PATH );
                getcwd( &cwd[0], MAX_PATH );
                WorkingDirectory = cwd.c_str();

                if( AdjustPrecursorMass )
                {
                    UseAvgMassOfSequences = false;
                }

                if( TicCutoffPercentage > 1.0f )
                {
                    TicCutoffPercentage /= 100.0f;
                    if( g_pid == 0 )
                        cerr << g_hostString << ": TicCutoffPercentage > 1.0 (100%) corrected, now at: " << TicCutoffPercentage << endl;
                }

                hasDynamicMods = false;
                if( !DynamicMods.empty() )
                {
                    DynamicMods = TrimWhitespace( DynamicMods );
                    g_residueMap->setDynamicMods( DynamicMods );
                    if(g_residueMap->dynamicMods.size()>0) {
                        hasDynamicMods = true;
                    }
                }

                if( !StaticMods.empty() )
                {
                    StaticMods = TrimWhitespace( StaticMods );
                    g_residueMap->setStaticMods( StaticMods );
                }

                //Set the mass tolerances according to the charge state.
                vector<float>& precursorMassTolerance = PrecursorMassTolerance;
                vector<float>& nterminalMassTolerance = NTerminalMassTolerance;
                vector<float>& cterminalMassTolerance = CTerminalMassTolerance;
                precursorMassTolerance.clear();
                nterminalMassTolerance.clear();
                cterminalMassTolerance.clear();
                for( int z=1; z <= NumChargeStates; ++z ) {
                    precursorMassTolerance.push_back( PrecursorMzTolerance * z );
                    nterminalMassTolerance.push_back( NTerminusMzTolerance * z );
                    cterminalMassTolerance.push_back( CTerminusMzTolerance * z );
                }

                if( ClassSizeMultiplier > 1 )
                {
                    minIntensityClassCount = int( ( pow( ClassSizeMultiplier, NumIntensityClasses ) - 1 ) / ( ClassSizeMultiplier - 1 ) );
                    minMzFidelityClassCount = int( ( pow( ClassSizeMultiplier, NumMzFidelityClasses ) - 1 ) / ( ClassSizeMultiplier - 1 ) );
                } else
                {
                    minIntensityClassCount = NumIntensityClasses;
                    minMzFidelityClassCount = NumMzFidelityClasses;
                }
            }
        };

        extern TagreconRunTimeConfig* g_rtConfig;
    }
}

#endif
