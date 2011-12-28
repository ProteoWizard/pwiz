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

#ifndef _TAGRECONCONFIG_H
#define _TAGRECONCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"

using namespace freicore;
using namespace pwiz;

// Program parameters

#define TAGRECON_RUNTIME_CONFIG \
    COMMON_RTCONFIG MULTITHREAD_RTCONFIG \
    RTCONFIG_VARIABLE( string,          OutputFormat,               "pepXML"        ) \
    RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
    RTCONFIG_VARIABLE( string,          ProteinDatabase,            ""              ) \
    RTCONFIG_VARIABLE( string,          DecoyPrefix,                "rev_"          ) \
    RTCONFIG_VARIABLE( string,          UnimodXML,		            "unimod.xml"    ) \
    RTCONFIG_VARIABLE( string,          Blosum,						"blosum62.fas"  ) \
    RTCONFIG_VARIABLE( double,          PrecursorMzTolerance,       1.25            ) \
    RTCONFIG_VARIABLE( double,          NTerminusMzTolerance,       1.5             ) \
    RTCONFIG_VARIABLE( double,          CTerminusMzTolerance,       1.25            ) \
    RTCONFIG_VARIABLE( double,          FragmentMzTolerance,        0.5             ) \
    RTCONFIG_VARIABLE( string,          FragmentationRule,          "cid"           ) \
    RTCONFIG_VARIABLE( bool,            FragmentationAutoRule,      true            ) \
    RTCONFIG_VARIABLE( int,				MaxResultRank,	    		5				) \
    RTCONFIG_VARIABLE( int,				NumIntensityClasses,		3				) \
    RTCONFIG_VARIABLE( int,				NumMzFidelityClasses,		3				) \
    RTCONFIG_VARIABLE( int,				NumBatches,					50				) \
    RTCONFIG_VARIABLE( double,			TicCutoffPercentage,		0.98			) \
    RTCONFIG_VARIABLE( double,			ClassSizeMultiplier,		2.0 			) \
    RTCONFIG_VARIABLE( double,          MinResultScore,             1e-7            ) \
    RTCONFIG_VARIABLE( double,          MinPeptideMass,             0.0             ) \
    RTCONFIG_VARIABLE( double,          MaxPeptideMass,             10000.0         ) \
    RTCONFIG_VARIABLE( int,             MinPeptideLength,           5               ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideLength,           75              ) \
    RTCONFIG_VARIABLE( bool,			PreferIntenseComplements,	true			) \
    RTCONFIG_VARIABLE( int,				ProteinSamplingTime,		15				) \
    RTCONFIG_VARIABLE( bool,			EstimateSearchTimeOnly,		false			) \
    RTCONFIG_VARIABLE( string,			CleavageRules,				"trypsin/p"	    ) \
    RTCONFIG_VARIABLE( int,				MinTerminiCleavages,		2				) \
    RTCONFIG_VARIABLE( int,				MaxMissedCleavages,		    -1				) \
    RTCONFIG_VARIABLE( int,				MaxTagCount,				0				) \
    RTCONFIG_VARIABLE( int,				MaxTagLength,				3				) \
    RTCONFIG_VARIABLE( int,				BlosumThreshold,			0				) \
    RTCONFIG_VARIABLE( bool,			MakeScoreHistograms,		false			) \
    RTCONFIG_VARIABLE( int,				NumScoreHistogramBins,		100				) \
    RTCONFIG_VARIABLE( int,				MaxScoreHistogramValues,	100				) \
    RTCONFIG_VARIABLE( int,				ScoreHistogramWidth,		800				) \
    RTCONFIG_VARIABLE( int,				ScoreHistogramHeight,		600				) \
    RTCONFIG_VARIABLE( int,				MaxFragmentChargeState,		0				) \
    RTCONFIG_VARIABLE( double,			MaxModificationMassPlus,	300.0			) \
	RTCONFIG_VARIABLE( double,			MaxModificationMassMinus,	150.0			) \
    RTCONFIG_VARIABLE( double,			MinModificationMass,		NEUTRON			) \
    RTCONFIG_VARIABLE( bool,            MassReconMode,              false           ) \
    RTCONFIG_VARIABLE( int,				ResultsPerBatch, 		    200000		    ) \
    RTCONFIG_VARIABLE( string,			PreferredDeltaMasses,	    ""   		    ) \
    RTCONFIG_VARIABLE( int, 			MaxNumPreferredDeltaMasses,	1   		    ) \
    RTCONFIG_VARIABLE( string,			ExplainUnknownMassShiftsAs,  ""   		    ) \
    RTCONFIG_VARIABLE( int,			    MaxAmbResultsForBlindMods,  2   		    ) \
    RTCONFIG_VARIABLE( int,			    MaxPeakCount,               200   		    ) \
    RTCONFIG_VARIABLE( bool,			ComputeXCorr,               true  		    ) \
    RTCONFIG_VARIABLE( bool,			UseNETAdjustment,           true  		    ) \
    RTCONFIG_VARIABLE( bool,			PenalizeUnknownMods,        false  		    ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,        "peakPicking true 2-"   ) \
    RTCONFIG_VARIABLE( string,          ProteinListFilters,         ""              ) \
    RTCONFIG_VARIABLE( string,          StaticMods,                 ""              ) \
    RTCONFIG_VARIABLE( string,          DynamicMods,                ""              ) \
    RTCONFIG_VARIABLE( int,             MaxDynamicMods,             2               ) \
	RTCONFIG_VARIABLE( bool,			UseAvgMassOfSequences,		false			) \
    RTCONFIG_VARIABLE( int,             MaxPeptideVariants,         1000000         ) \
	RTCONFIG_VARIABLE( bool,			DuplicateSpectra,			true			) \
	RTCONFIG_VARIABLE( bool,			UseSmartPlusThreeModel,		true			) \
	RTCONFIG_VARIABLE( bool,			UseChargeStateFromMS,		false			) \
	RTCONFIG_VARIABLE( bool,			SearchUntaggedSpectra,		true			)


namespace freicore
{
namespace tagrecon
{
    enum UnknownMassShiftSearchMode {BLIND_PTMS, MUTATIONS, PREFERRED_DELTA_MASSES, INACTIVE};

    struct RunTimeConfig : public BaseRunTimeConfig
    {
    public:
        RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, TAGRECON_RUNTIME_CONFIG, "\r\n\t ", "tagrecon.cfg", "\r\n#" )

        path executableFilepath; // path to tagrecon executable (to look for unimod and blosum)

        string decoyPrefix;
        bool automaticDecoys;

        boost::regex cleavageAgentRegex;
        Digestion::Config digestionConfig;

        PreferredDeltaMassesList preferredDeltaMasses;

        FragmentTypesBitset defaultFragmentTypes;

        DynamicModSet   dynamicMods;
        StaticModSet    staticMods;
        double          largestNegativeDynamicModMass;
        double          largestPositiveDynamicModMass;

        UnknownMassShiftSearchMode unknownMassShiftSearchMode;

        int				SpectraBatchSize;
        int				ProteinBatchSize;
        int				ProteinIndexOffset;
        double          curMinPeptideMass;
        double          curMaxPeptideMass;
        int				minIntensityClassCount;
        int				minMzFidelityClassCount;

        vector<double>  PrecursorMassTolerance;
        vector<double>  NTerminalMassTolerance;
        vector<double>  CTerminalMassTolerance;
        
        int             maxFragmentChargeState;
        int             maxChargeStateFromSpectra;
        vector<double>  NETRewardVector;

        pwiz::identdata::IdentDataFile::Format outputFormat;

    private:
        void finalize()
        {
            if (bal::iequals(OutputFormat, "pepXML"))
                outputFormat = pwiz::identdata::IdentDataFile::Format_pepXML;
            else if (bal::iequals(OutputFormat, "mzIdentML"))
                outputFormat = pwiz::identdata::IdentDataFile::Format_MzIdentML;
            else
                throw runtime_error("invalid output format");


            path pathToUnimodXML(UnimodXML);
            if( !pathToUnimodXML.has_parent_path() )
                UnimodXML = (executableFilepath.parent_path() / pathToUnimodXML).string();
            if( !exists(UnimodXML) )
                throw runtime_error("unable to find Unimod XML \"" + UnimodXML + "\"");

            path pathToBlosum(Blosum);
            if( !pathToBlosum.has_parent_path() )
                Blosum = (executableFilepath.parent_path() / pathToBlosum).string();
            if( !exists(Blosum) )
                throw runtime_error("unable to find Blosum matrix \"" + Blosum + "\"");

            decoyPrefix = DecoyPrefix.empty() ? "rev_" : DecoyPrefix;
            automaticDecoys = DecoyPrefix.empty() ? false : true;

            trim(CleavageRules); // trim flanking whitespace
            if( CleavageRules.find(' ') == string::npos )
            {
                // a single token must be either a cleavage agent name or regex
                // first try to parse the token as the name of an agent
                CVID cleavageAgent = Digestion::getCleavageAgentByName(CleavageRules);
                if( cleavageAgent == CVID_Unknown )
                {
                    // next try to parse the token as a Perl regex
                    try
                    {
                        // regex must be zero width, so it must use at least one parenthesis;
                        // this will catch most bad cleavage agent names (e.g. "tripsen")
                        if( CleavageRules.find('(') == string::npos )
                            throw boost::bad_expression(boost::regex_constants::error_bad_pattern);
                        cleavageAgentRegex = boost::regex(CleavageRules);
                    }
                    catch (boost::bad_expression&)
                    {
                        // a bad regex or agent name is fatal
                        throw runtime_error("invalid cleavage agent name or regex: " + CleavageRules);
                    }
                }
                else
                {
                    // use regex for predefined cleavage agent
                    cleavageAgentRegex = boost::regex(Digestion::getCleavageAgentRegex(cleavageAgent));
                }
            }
            else
            {
                // multiple tokens must be a CleavageRuleSet
                CleavageRuleSet tmpRuleSet;
                stringstream CleavageRulesStream( CleavageRules );
                CleavageRulesStream >> tmpRuleSet;
                cleavageAgentRegex = boost::regex(tmpRuleSet.asCleavageAgentRegex());
            }

            MaxMissedCleavages = MaxMissedCleavages < 0 ? 100000 : MaxMissedCleavages;

            // Preferred mass shifts to be used in the unknown PTM search mode
            preferredDeltaMasses = PreferredDeltaMassesList( PreferredDeltaMasses, MaxNumPreferredDeltaMasses );

            // Set the unknown mass shift search mode
            unknownMassShiftSearchMode = INACTIVE;
            if(ExplainUnknownMassShiftsAs.size()>0)
            {
                to_lower(ExplainUnknownMassShiftsAs);
                if(ExplainUnknownMassShiftsAs.compare("blindptms")==0)
                    unknownMassShiftSearchMode = BLIND_PTMS;
                else if(ExplainUnknownMassShiftsAs.compare("mutations")==0)
                    unknownMassShiftSearchMode = MUTATIONS;
                else if(ExplainUnknownMassShiftsAs.compare("preferredptms")==0)
                {
                    if(preferredDeltaMasses.size()==0)
                        throw runtime_error("Preferred mass shifts must be defined when \"PreferredPTMs\" search mode is selected.");
                    unknownMassShiftSearchMode = PREFERRED_DELTA_MASSES;
                }
            }

            if( unknownMassShiftSearchMode != BLIND_PTMS )
                PenalizeUnknownMods = false;

            int maxMissedCleavages = MaxMissedCleavages < 0 ? 100000 : MaxMissedCleavages;
            Digestion::Specificity specificity = (Digestion::Specificity) MinTerminiCleavages;
            // maxLength of a peptide is the max_sequence_mass/mass_of_averagine_molecule
            int maxLength = (int) (MaxPeptideMass/111.1254f);
            digestionConfig = Digestion::Config( maxMissedCleavages,
                                                 MinPeptideLength,
                                                 maxLength,
                                                 specificity );

            // We do not have to use NET based adjustment
            // to the rank score when we are searching for
            // fully enzymatic peptides.
            if(MinTerminiCleavages == 2)
                UseNETAdjustment = false;

            // TODO: move fragmentation rule parsing to its own class
            vector<string> fragmentationRuleTokens;
            split( fragmentationRuleTokens, FragmentationRule, is_any_of(":") );
            if( fragmentationRuleTokens.empty() )
                throw runtime_error("invalid blank fragmentation rule");

            const string& mode = fragmentationRuleTokens[0];
            defaultFragmentTypes.reset();
            if( mode.empty() || mode == "cid" )
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

            if( TicCutoffPercentage > 1.0f )
            {
                TicCutoffPercentage /= 100.0f;
                if( g_pid == 0 )
                    cerr << g_hostString << ": TicCutoffPercentage > 1.0 (100%) corrected, now at: " << TicCutoffPercentage << endl;
            }

            if( !DynamicMods.empty() )
            {
                DynamicMods = TrimWhitespace( DynamicMods );
                dynamicMods = DynamicModSet( DynamicMods );
            }

            if( !StaticMods.empty() )
            {
                StaticMods = TrimWhitespace( StaticMods );
                staticMods = StaticModSet( StaticMods );
            }
            
            BOOST_FOREACH(const DynamicMod& mod, dynamicMods)
            {
                largestPositiveDynamicModMass = max(largestPositiveDynamicModMass, mod.modMass * MaxDynamicMods);
                largestNegativeDynamicModMass = min(largestNegativeDynamicModMass, mod.modMass * MaxDynamicMods);
            }

            maxChargeStateFromSpectra = 1;
            maxFragmentChargeState = ( MaxFragmentChargeState > 0 ? MaxFragmentChargeState+1 : NumChargeStates );

            PrecursorMassTolerance.push_back(PrecursorMzTolerance);

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

    extern RunTimeConfig* g_rtConfig;
}
}

#endif
