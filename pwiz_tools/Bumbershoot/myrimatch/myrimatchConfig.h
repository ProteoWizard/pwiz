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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _MYRIMATCHCONFIG_H
#define _MYRIMATCHCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include "pwiz/data/identdata/IdentDataFile.hpp"


using namespace freicore;


#define MYRIMATCH_RUNTIME_CONFIG \
    COMMON_RTCONFIG MULTITHREAD_RTCONFIG \
    RTCONFIG_VARIABLE( string,          OutputFormat,                   "pepXML"                ) \
    RTCONFIG_VARIABLE( string,          OutputSuffix,                   ""                      ) \
    RTCONFIG_VARIABLE( string,          ProteinDatabase,                ""                      ) \
    RTCONFIG_VARIABLE( string,          DecoyPrefix,                    "rev_"                  ) \
    RTCONFIG_VARIABLE( string,          PrecursorMzToleranceRule,       "auto"                  ) \
    RTCONFIG_VARIABLE( IntegerSet,      MonoisotopeAdjustmentSet,       string("[-1,2]")        ) \
    RTCONFIG_VARIABLE( MZTolerance,     MonoPrecursorMzTolerance,       string("10 ppm")        ) \
    RTCONFIG_VARIABLE( MZTolerance,     AvgPrecursorMzTolerance,        string("1.5 mz")        ) \
    RTCONFIG_VARIABLE( MZTolerance,     FragmentMzTolerance,            string("0.5 mz")        ) \
    RTCONFIG_VARIABLE( string,          FragmentationRule,              "cid"                   ) \
    RTCONFIG_VARIABLE( bool,            FragmentationAutoRule,          true                    ) \
    RTCONFIG_VARIABLE( int,             MaxResultRank,                  2                       ) \
    RTCONFIG_VARIABLE( int,             NumIntensityClasses,            3                       ) \
    RTCONFIG_VARIABLE( int,             NumMzFidelityClasses,           3                       ) \
    RTCONFIG_VARIABLE( int,             NumBatches,                     50                      ) \
    RTCONFIG_VARIABLE( double,          TicCutoffPercentage,            0.98                    ) \
    RTCONFIG_VARIABLE( double,          ClassSizeMultiplier,            2.0                     ) \
    RTCONFIG_VARIABLE( double,          MinResultScore,                 1e-7                    ) \
    RTCONFIG_VARIABLE( int,             MinMatchedFragments,            5                       ) \
    RTCONFIG_VARIABLE( double,          MinPeptideMass,                 0.0                     ) \
    RTCONFIG_VARIABLE( double,          MaxPeptideMass,                 10000.0                 ) \
    RTCONFIG_VARIABLE( int,             MinPeptideLength,               5                       ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideLength,               75                      ) \
    RTCONFIG_VARIABLE( bool,            PreferIntenseComplements,       true                    ) \
    RTCONFIG_VARIABLE( int,             ProteinSamplingTime,            15                      ) \
    RTCONFIG_VARIABLE( bool,            EstimateSearchTimeOnly,         false                   ) \
    RTCONFIG_VARIABLE( string,          CleavageRules,                  "trypsin/p"             ) \
    RTCONFIG_VARIABLE( int,             MinTerminiCleavages,            2                       ) \
    RTCONFIG_VARIABLE( int,             MaxMissedCleavages,             -1                      ) \
    RTCONFIG_VARIABLE( int,             MaxFragmentChargeState,         0                       ) \
    RTCONFIG_VARIABLE( int,             ResultsPerBatch,                200000                  ) \
    RTCONFIG_VARIABLE( bool,            ComputeXCorr,                   true                    ) \
    RTCONFIG_VARIABLE( int,             MaxPeakCount,                   300                     ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,            "peakPicking true 2-"   ) \
    RTCONFIG_VARIABLE( string,          ProteinListFilters,             ""                      ) \
    RTCONFIG_VARIABLE( bool,            UseSmartPlusThreeModel,         true                    ) \
    RTCONFIG_VARIABLE( string,          StaticMods,                     ""                      ) \
    RTCONFIG_VARIABLE( string,          DynamicMods,                    ""                      ) \
    RTCONFIG_VARIABLE( int,             MaxDynamicMods,                 2                       ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideVariants,             1000000                 ) \
    RTCONFIG_VARIABLE( bool,            KeepUnadjustedPrecursorMz,      false                   )


namespace freicore
{
namespace myrimatch
{
    // TODO: move to its own class?
    enum MzToleranceRule { MzToleranceRule_Auto, MzToleranceRule_Mono, MzToleranceRule_Avg };

    struct RunTimeConfig : public BaseRunTimeConfig
    {

    public:
        RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, MYRIMATCH_RUNTIME_CONFIG, "myrimatch.cfg" )

        string decoyPrefix;
        bool automaticDecoys;

        CVID cleavageAgent;
        string cleavageAgentRegex;
        Digestion::Config digestionConfig;

        FragmentTypesBitset defaultFragmentTypes;

        DynamicModSet   dynamicMods;
        StaticModSet    staticMods;
        double          largestNegativeDynamicModMass;
        double          largestPositiveDynamicModMass;

        int             SpectraBatchSize;
        int             ProteinBatchSize;
        int             ProteinIndexOffset;
        double          curMinPeptideMass;
        double          curMaxPeptideMass;
        int             minIntensityClassCount;
        int             minMzFidelityClassCount;
        int             maxFragmentChargeState;
        int             maxChargeStateFromSpectra;

        MzToleranceRule precursorMzToleranceRule;

        vector<MZTolerance> avgPrecursorMassTolerance;
        vector<MZTolerance> monoPrecursorMassTolerance;

        // Compute the fragment mass error bins and their associated log odds scores
        vector<double>  massErrors;
        vector<double>  mzFidelityLods;

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

            decoyPrefix = DecoyPrefix.empty() ? "rev_" : DecoyPrefix;
            automaticDecoys = DecoyPrefix.empty() ? false : true;

            if (MonoisotopeAdjustmentSet.empty())
                MonoisotopeAdjustmentSet.insert(0);

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

            if( ProteinSamplingTime == 0 )
            {
                if( EstimateSearchTimeOnly )
                    m_warnings << "ProteinSamplingTime = 0 disables EstimateSearchTimeOnly.\n";
                EstimateSearchTimeOnly = 0;
            }

            // TODO: move mzToleranceRule to its own class
            bal::to_lower(PrecursorMzToleranceRule);
            if( PrecursorMzToleranceRule == "auto" )
                precursorMzToleranceRule = MzToleranceRule_Auto;
            else if( PrecursorMzToleranceRule == "mono" )
                precursorMzToleranceRule = MzToleranceRule_Mono;
            else if( PrecursorMzToleranceRule == "avg" )
                precursorMzToleranceRule = MzToleranceRule_Avg;
            else
                m_warnings << "Invalid mode \"" << PrecursorMzToleranceRule << "\" for PrecursorMzToleranceRule.\n";

            if (MonoisotopeAdjustmentSet.size() > 1 && (1000.0 + MonoPrecursorMzTolerance) - 1000.0 > 0.2)
                m_warnings << "MonoisotopeAdjustmentSet should be set to 0 when the MonoPrecursorMzTolerance is wide.\n";

            ProteinIndexOffset = 0;

            string cwd;
            cwd.resize( MAX_PATH );
            getcwd( &cwd[0], MAX_PATH );
            WorkingDirectory = cwd.c_str();

            if( TicCutoffPercentage > 1.0 )
            {
                TicCutoffPercentage /= 100.0;
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

            if( ClassSizeMultiplier > 1 )
            {
                minIntensityClassCount = int( ( pow( ClassSizeMultiplier, NumIntensityClasses ) - 1 ) / ( ClassSizeMultiplier - 1 ) );
                minMzFidelityClassCount = int( ( pow( ClassSizeMultiplier, NumMzFidelityClasses ) - 1 ) / ( ClassSizeMultiplier - 1 ) );
            } else
            {
                minIntensityClassCount = NumIntensityClasses;
                minMzFidelityClassCount = NumMzFidelityClasses;
            }
            
            maxChargeStateFromSpectra = 0;
            maxFragmentChargeState = ( MaxFragmentChargeState > 0 ? MaxFragmentChargeState+1 : NumChargeStates );
            
            vector<double> insideProbs;
            int numBins = 5;
            // Divide the fragment mass error into half and use it as standard deviation
            double stdev = FragmentMzTolerance.value*0.5;
            massErrors.clear();
            insideProbs.clear();
            mzFidelityLods.clear();
            // Divide the mass error distributions into 10 bins.
            for(int j = 1; j <= numBins; ++j) 
            {
                // Compute the mass error associated with each bin.
                double massError = FragmentMzTolerance.value * ((double)j/(double)numBins);
                // Compute the cumulative distribution function of massError 
                // with mu=0 and sig=stdev
                double errX = (massError-0)/(stdev*sqrt(2.0));
                double cdf = 0.5 * (1.0+pwiz::math::erf(errX));
                // Compute the gaussian inside probability
                double insideProb = 2.0*cdf-1.0;
                // Save the mass errors and inside probabilities
                massErrors.push_back(massError);
                insideProbs.push_back(insideProb);
            }
            // mzFidelity bin probablities are dependent on the number of bin. So,
            // compute the probabilities only once.
            // Compute the probability associated with each mass error bin
            double denom = insideProbs.back();
            for(int j = 0; j < numBins; ++j) 
            {
                double prob;
                if(j==0) {
                    prob = insideProbs[j]/denom;
                } else {
                    prob = (insideProbs[j]-insideProbs[j-1])/denom;
                }
                // Compute the log odds ratio of GaussianProb to Uniform probability and save it
                mzFidelityLods.push_back(log(prob*(double)numBins));
            }
            /*cout << "Error-Probs:" << endl;
            for(int j = 0; j < numBins; ++j) 
            {
                cout << massErrors[j] << ":" << mzFidelityLods[j] << " ";
            }
            cout << endl;*/
            //exit(1);

            BaseRunTimeConfig::finalize();
        }
    };

    extern RunTimeConfig* g_rtConfig;
}
}

#endif
