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
    RTCONFIG_VARIABLE( string,            OutputSuffix,                ""                ) \
    RTCONFIG_VARIABLE( string,          ProteinDatabase,            ""              ) \
    RTCONFIG_VARIABLE( string,          DecoyPrefix,                "rev_"          ) \
    RTCONFIG_VARIABLE( string,          UnimodXML,                    "unimod.xml"    ) \
    RTCONFIG_VARIABLE( string,          Blosum,                        "blosum62.fas"  ) \
    RTCONFIG_VARIABLE( double,          PrecursorMzTolerance,       1.25            ) \
    RTCONFIG_VARIABLE( double,          NTerminusMzTolerance,       1.5             ) \
    RTCONFIG_VARIABLE( double,          CTerminusMzTolerance,       1.25            ) \
    RTCONFIG_VARIABLE( double,          FragmentMzTolerance,        0.5             ) \
    RTCONFIG_VARIABLE( string,          FragmentationRule,          "cid"           ) \
    RTCONFIG_VARIABLE( bool,            FragmentationAutoRule,      true            ) \
    RTCONFIG_VARIABLE( int,                MaxResultRank,                5                ) \
    RTCONFIG_VARIABLE( int,                NumIntensityClasses,        3                ) \
    RTCONFIG_VARIABLE( int,                NumMzFidelityClasses,        3                ) \
    RTCONFIG_VARIABLE( int,                NumBatches,                    50                ) \
    RTCONFIG_VARIABLE( double,            TicCutoffPercentage,        0.98            ) \
    RTCONFIG_VARIABLE( double,            ClassSizeMultiplier,        2.0             ) \
    RTCONFIG_VARIABLE( double,          MinResultScore,             1e-7            ) \
    RTCONFIG_VARIABLE( double,          MinPeptideMass,             0.0             ) \
    RTCONFIG_VARIABLE( double,          MaxPeptideMass,             10000.0         ) \
    RTCONFIG_VARIABLE( int,             MinPeptideLength,           5               ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideLength,           75              ) \
    RTCONFIG_VARIABLE( bool,            PreferIntenseComplements,    true            ) \
    RTCONFIG_VARIABLE( int,                ProteinSamplingTime,        15                ) \
    RTCONFIG_VARIABLE( bool,            EstimateSearchTimeOnly,        false            ) \
    RTCONFIG_VARIABLE( string,            CleavageRules,                "trypsin/p"        ) \
    RTCONFIG_VARIABLE( int,                MinTerminiCleavages,        2                ) \
    RTCONFIG_VARIABLE( int,                MaxMissedCleavages,            -1                ) \
    RTCONFIG_VARIABLE( int,                MaxTagCount,                0                ) \
    RTCONFIG_VARIABLE( int,                MaxTagLength,                3                ) \
    RTCONFIG_VARIABLE( int,                BlosumThreshold,            0                ) \
    RTCONFIG_VARIABLE( bool,            MakeScoreHistograms,        false            ) \
    RTCONFIG_VARIABLE( int,                NumScoreHistogramBins,        100                ) \
    RTCONFIG_VARIABLE( int,                MaxScoreHistogramValues,    100                ) \
    RTCONFIG_VARIABLE( int,                ScoreHistogramWidth,        800                ) \
    RTCONFIG_VARIABLE( int,                ScoreHistogramHeight,        600                ) \
    RTCONFIG_VARIABLE( int,                MaxFragmentChargeState,        0                ) \
    RTCONFIG_VARIABLE( double,            MaxModificationMassPlus,    300.0            ) \
    RTCONFIG_VARIABLE( double,            MaxModificationMassMinus,    150.0            ) \
    RTCONFIG_VARIABLE( double,            MinModificationMass,        NEUTRON            ) \
    RTCONFIG_VARIABLE( bool,            MassReconMode,              false           ) \
    RTCONFIG_VARIABLE( int,                ResultsPerBatch,             200000            ) \
    RTCONFIG_VARIABLE( string,            PreferredDeltaMasses,        ""               ) \
    RTCONFIG_VARIABLE( int,             MaxNumPreferredDeltaMasses,    1               ) \
    RTCONFIG_VARIABLE( string,            ExplainUnknownMassShiftsAs,  ""               ) \
    RTCONFIG_VARIABLE( int,                MaxAmbResultsForBlindMods,  2               ) \
    RTCONFIG_VARIABLE( int,                MaxPeakCount,               200               ) \
    RTCONFIG_VARIABLE( bool,            ComputeXCorr,               true              ) \
    RTCONFIG_VARIABLE( bool,            UseNETAdjustment,           false              ) \
    RTCONFIG_VARIABLE( bool,            PenalizeUnknownMods,        false              ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,        "peakPicking true 2-"   ) \
    RTCONFIG_VARIABLE( string,          ProteinListFilters,         ""              ) \
    RTCONFIG_VARIABLE( string,          StaticMods,                 ""              ) \
    RTCONFIG_VARIABLE( string,          DynamicMods,                ""              ) \
    RTCONFIG_VARIABLE( int,             MaxDynamicMods,             2               ) \
    RTCONFIG_VARIABLE( bool,            UseAvgMassOfSequences,        false            ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideVariants,         1000000         ) \
    RTCONFIG_VARIABLE( bool,            DuplicateSpectra,            true            ) \
    RTCONFIG_VARIABLE( bool,            UseSmartPlusThreeModel,        true            ) \
    RTCONFIG_VARIABLE( bool,            UseChargeStateFromMS,        false            ) \
    RTCONFIG_VARIABLE( bool,            SearchUntaggedSpectra,        false            ) \
    RTCONFIG_VARIABLE( MZTolerance,     UntaggedSpectraPrecMZTol,   string("1.25 mz")) \
    RTCONFIG_VARIABLE( string,          BlindPTMResidues,           ""              ) 


namespace freicore
{
namespace tagrecon
{
    enum UnknownMassShiftSearchMode {BLIND_PTMS, MUTATIONS, PREFERRED_DELTA_MASSES, INACTIVE};

    struct RunTimeConfig : public BaseRunTimeConfig
    {
    public:
        RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, TAGRECON_RUNTIME_CONFIG, "tagrecon.cfg" )

        path executableFilepath; // path to tagrecon executable (to look for unimod and blosum)

        string decoyPrefix;
        bool automaticDecoys;

        CVID cleavageAgent;
        string cleavageAgentRegex;
        Digestion::Config digestionConfig;

        PreferredDeltaMassesList preferredDeltaMasses;

        FragmentTypesBitset defaultFragmentTypes;

        DynamicModSet   dynamicMods;
        StaticModSet    staticMods;
        double          largestNegativeDynamicModMass;
        double          largestPositiveDynamicModMass;

        UnknownMassShiftSearchMode unknownMassShiftSearchMode;

        int                SpectraBatchSize;
        int                ProteinBatchSize;
        int                ProteinIndexOffset;
        double          curMinPeptideMass;
        double          curMaxPeptideMass;
        int                minIntensityClassCount;
        int                minMzFidelityClassCount;

        vector<double>  PrecursorMassTolerance;
        vector<double>  NTerminalMassTolerance;
        vector<double>  CTerminalMassTolerance;
        
        int             maxFragmentChargeState;
        int             maxChargeStateFromSpectra;
        vector<double>  NETRewardVector;

        bool            restrictBlindPTMLocality;
        vector<bool>    blindPTMLocalities;
        
        vector<MZTolerance> untaggedSpectraPrecMassTolerance;

        pwiz::identdata::IdentDataFile::Format outputFormat;

    private:
        void finalize()
        {
            if (bal::iequals(OutputFormat, "pepXML"))
                outputFormat = pwiz::identdata::IdentDataFile::Format_pepXML;
            else if (bal::iequals(OutputFormat, "mzIdentML"))
                outputFormat = pwiz::identdata::IdentDataFile::Format_MzIdentML;
            else
                m_warnings << "Invalid value \"" << OutputFormat << "\" for OutputFormat\n";


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

            // TODO: move CleavageRules parsing to its own class
            trim(CleavageRules); // trim flanking whitespace

            if (bal::iequals(CleavageRules, "NoEnzyme"))
                m_warnings << "NoEnzyme is not supported. If you want non-specific digestion, set CleavageRules to the enzyme that digested your sample and set MinTerminiCleavages to 0.\n";
            else if (CleavageRules.empty())
                m_warnings << "Blank value for CleavageRules is invalid.\n";
            else
            {
                // first try to parse the token as the name of an agent
                cleavageAgent = Digestion::getCleavageAgentByName(CleavageRules);
                cleavageAgentRegex.clear();

                if (cleavageAgent != CVID_Unknown || CleavageRules.find(' ') == string::npos)
                {
                    // a single token must be either a cleavage agent name or regex
                    // multiple tokens could be a cleavage agent or an old-style cleavage rule set

                    if (bal::iequals(CleavageRules, "unspecific cleavage"))
                    {
                        m_warnings << "Unspecific cleavage is not recommended. For a non-specific search, you should almost always set CleavageRules to the enzyme that digested your sample and set MinTerminiCleavages to 0.\n";
                        MinTerminiCleavages = 0;

                        // there is no regex
                    }
                    else if (bal::iequals(CleavageRules, "no cleavage"))
                    {
                        // there is no regex
                    }
                    else if (cleavageAgent == CVID_Unknown)
                    {
                        // next try to parse the token as a Perl regex
                        // regex must be zero width, so it must use at least one parenthesis;
                        // this will catch most bad cleavage agent names (e.g. "tripsen")
                        if (CleavageRules.find('(') != string::npos)
                            cleavageAgentRegex = CleavageRules;
                        else
                            m_warnings << "Invalid cleavage agent name or regex \"" << CleavageRules << "\"\n";
                    }
                    else
                    {
                        // use regex for predefined cleavage agent
                        cleavageAgentRegex = Digestion::getCleavageAgentRegex(cleavageAgent);
                    }
                }
                else if (cleavageAgent == CVID_Unknown)
                {
                    // multiple tokens must be a CleavageRuleSet
                    CleavageRuleSet tmpRuleSet;
                    stringstream CleavageRulesStream( CleavageRules );
                    CleavageRulesStream >> tmpRuleSet;
                    cleavageAgentRegex = tmpRuleSet.asCleavageAgentRegex();
                }
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
                m_warnings << "Blank value for FragmentationRule is invalid.\n";
            else
            {
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
                        m_warnings << "Manual FragmentationRule setting requires comma-separated list of ion series, e.g. 'manual:b,y'\n";
                    else
                    {
                        vector<string> fragmentTypeTokens;
                        split( fragmentTypeTokens, fragmentationRuleTokens[1], is_any_of(",") );
                
                        if( fragmentTypeTokens.empty() )
                            m_warnings << "Manual FragmentationRule setting requires comma-separated list of ion series, e.g. 'manual:b,y'\n";

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
                    }
                } else
                    m_warnings << "Invalid mode \"" << mode << "\" for FragmentationRule.\n";
            }

            ProteinIndexOffset = 0;

            string cwd;
            cwd.resize( MAX_PATH );
            getcwd( &cwd[0], MAX_PATH );
            WorkingDirectory = cwd.c_str();

            if( TicCutoffPercentage > 1.0f )
            {
                TicCutoffPercentage /= 100.0f;
                m_warnings << "TicCutoffPercentage must be between 0 and 1 (100%)\n";
            }
            
            if( !DynamicMods.empty() )
            {
                try {dynamicMods = DynamicModSet( DynamicMods );}
                catch (exception& e) {m_warnings << "Unable to parse DynamicMods \"" << DynamicMods << "\": " << e.what() << "\n";}
            }

            if( !StaticMods.empty() )
            {
                try {staticMods = StaticModSet( StaticMods );}
                catch (exception& e) {m_warnings << "Unable to parse StaticMods \"" << StaticMods << "\": " << e.what() << "\n";}
            }
            
            BOOST_FOREACH(const DynamicMod& mod, dynamicMods)
            {
                largestPositiveDynamicModMass = max(largestPositiveDynamicModMass, mod.modMass * MaxDynamicMods);
                largestNegativeDynamicModMass = min(largestNegativeDynamicModMass, mod.modMass * MaxDynamicMods);
            }

            // Check to see if the user wants to restrict the blind PTMs to particular residues.
            restrictBlindPTMLocality = false;
            if(BlindPTMResidues.size()>0)
            {
                // Split the residue locality rule, get the index of the residues to be used for
                // blind PTM localization, and set the flag.
                vector<string> residues;
                split( residues, BlindPTMResidues, is_any_of(",") );
                if( residues.empty() )
                    m_warnings << "Invalid blind ptm residue restriction rule.\n";
                else
                {
                    restrictBlindPTMLocality = true;
                    // Reset the ascii indexed array for residues.
                    blindPTMLocalities.resize(29, false);

                    BOOST_FOREACH(const string& res, residues)
                    {
                        size_t index = static_cast<size_t>(std::toupper(res.at(0)));
                        index -= 65;
                        if(index >= 0 && index <= 29)
                            blindPTMLocalities[index] = true;
                    }
                }
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

            BaseRunTimeConfig::finalize();
        }
    };

    extern RunTimeConfig* g_rtConfig;
}
}

#endif
