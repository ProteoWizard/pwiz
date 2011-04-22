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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _PEPITOMECONFIG_H
#define _PEPITOMECONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/utility/math/erf.hpp"
#include <boost/math/distributions/chi_squared.hpp>
using boost::math::chi_squared_distribution;

using namespace freicore;
using namespace pwiz;
using namespace pwiz::math;

#define PEPITOME_RUNTIME_CONFIG \
	COMMON_RTCONFIG MULTITHREAD_RTCONFIG \
	RTCONFIG_VARIABLE( string,          OutputFormat,               "pepXML"        ) \
    RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
    RTCONFIG_VARIABLE( string,          ProteinDatabase,            ""              ) \
    RTCONFIG_VARIABLE( string,          SpectralLibrary,            ""              ) \
    RTCONFIG_VARIABLE( string,          DecoyPrefix,                "DECOY_"        ) \
    RTCONFIG_VARIABLE( string,          PrecursorMzToleranceRule,   "auto"          ) \
    RTCONFIG_VARIABLE( IntegerSet,      MonoisotopeAdjustmentSet,   string("[-1,2]")) \
    RTCONFIG_VARIABLE( MZTolerance,     MonoPrecursorMzTolerance,   string("10 ppm")) \
    RTCONFIG_VARIABLE( MZTolerance,     AvgPrecursorMzTolerance,    string("1.5 mz")) \
    RTCONFIG_VARIABLE( MZTolerance,     FragmentMzTolerance,        string("0.5 mz")) \
    RTCONFIG_VARIABLE( string,          FragmentationRule,          "cid"           ) \
    RTCONFIG_VARIABLE( bool,            FragmentationAutoRule,      true            ) \
	RTCONFIG_VARIABLE( int,				MaxResultRank,				5   			) \
	RTCONFIG_VARIABLE( int,				NumIntensityClasses,		3				) \
	RTCONFIG_VARIABLE( int,				NumMzFidelityClasses,		3				) \
    RTCONFIG_VARIABLE( double,			ClassSizeMultiplier,		2.0			    ) \
    RTCONFIG_VARIABLE( double,          MinResultScore,             1e-7            ) \
    RTCONFIG_VARIABLE( double,          MinPeptideMass,             0.0             ) \
    RTCONFIG_VARIABLE( double,          MaxPeptideMass,             10000.0         ) \
    RTCONFIG_VARIABLE( int,             MinPeptideLength,           5               ) \
    RTCONFIG_VARIABLE( int,             MaxPeptideLength,           75              ) \
	RTCONFIG_VARIABLE( int,				NumBatches,					50				) \
	RTCONFIG_VARIABLE( double,			TicCutoffPercentage,		0.98			) \
	RTCONFIG_VARIABLE( double,			LibTicCutoffPercentage,		0.98			) \
	RTCONFIG_VARIABLE( int,			    LibMaxPeakCount,    		150 			) \
	RTCONFIG_VARIABLE( bool,			CleanLibSpectra,    		true			) \
	RTCONFIG_VARIABLE( bool,			PreferIntenseComplements,	true			) \
	RTCONFIG_VARIABLE( int,				ProteinSamplingTime,		15				) \
	RTCONFIG_VARIABLE( bool,			EstimateSearchTimeOnly,		false			) \
	RTCONFIG_VARIABLE( string,			CleavageRules,				"trypsin/p"     ) \
    RTCONFIG_VARIABLE( int,             MinTerminiCleavages,        2               ) \
    RTCONFIG_VARIABLE( int,             MaxMissedCleavages,         -1              ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,        "peakPicking true 2-"   ) \
    RTCONFIG_VARIABLE( string,          ProteinListFilters,         ""              ) \
	RTCONFIG_VARIABLE( int,				MaxFragmentChargeState,		0				) \
    RTCONFIG_VARIABLE( int,				ResultsPerBatch, 		    200000		    ) \
    RTCONFIG_VARIABLE( bool,			RecalculateLibPepMasses,    true			) \
    RTCONFIG_VARIABLE( bool,			FASTARefreshResults,        true			) \
    RTCONFIG_VARIABLE( int,			    MaxPeakCount,               150   		    ) \
    RTCONFIG_VARIABLE( int,			    IRSPeakCount,               50   		    ) \
    RTCONFIG_VARIABLE( string,          StaticMods,                 ""              ) \
    RTCONFIG_VARIABLE( string,          DynamicMods,                ""              ) \
    RTCONFIG_VARIABLE( int,             MaxDynamicMods,             2               ) 


namespace freicore
{
namespace pepitome
{
    // TODO: move to its own class?
    enum MzToleranceRule { MzToleranceRule_Auto, MzToleranceRule_Mono, MzToleranceRule_Avg };

	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, PEPITOME_RUNTIME_CONFIG, "\r\n\t ", "pepitome.cfg", "\r\n#" )

        boost::regex cleavageAgentRegex;
        Digestion::Config digestionConfig;

        FragmentTypesBitset defaultFragmentTypes;

        DynamicModSet   dynamicMods;
        StaticModSet    staticMods;
        double          largestNegativeDynamicModMass;
        double          largestPositiveDynamicModMass;

		int				SpectraBatchSize;
		int				ProteinBatchSize;
		int				ProteinIndexOffset;
		double			curMinPeptideMass;
		double			curMaxPeptideMass;
		int				minIntensityClassCount;
		int				minMzFidelityClassCount;
		int				maxFragmentChargeState;
		int             maxChargeStateFromSpectra;

        MzToleranceRule precursorMzToleranceRule;

        vector<MZTolerance> avgPrecursorMassTolerance;
        vector<MZTolerance> monoPrecursorMassTolerance;

		// Compute the fragment mass error bins and their associated log odds scores
		vector<double> massErrors;
		vector<double> mzFidelityLods;

        pwiz::mziddata::MzIdentMLFile::Format outputFormat;

        shared_ptr<boost::math::chi_squared> chiDist;

	private:
		void finalize()
		{
            if (bal::iequals(OutputFormat, "pepXML"))
                outputFormat = pwiz::mziddata::MzIdentMLFile::Format_pepXML;
            else if (bal::iequals(OutputFormat, "mzIdentML"))
                outputFormat = pwiz::mziddata::MzIdentMLFile::Format_MzIdentML;
            else
                throw runtime_error("invalid output format");

            // TODO: move CleavageRules parsing to its own class
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

			if( ProteinSamplingTime == 0 )
            {
                EstimateSearchTimeOnly = 0;
                if( g_pid == 0 )
                    cerr << g_hostString << ": ProteinSamplingTime = 0 disables EstimateSearchTimeOnly" << endl;
            }

            // TODO: move mzToleranceRule to its own class
            bal::to_lower(PrecursorMzToleranceRule);
            if( PrecursorMzToleranceRule == "auto" )
                precursorMzToleranceRule = MzToleranceRule_Auto;
            else if( PrecursorMzToleranceRule == "mono" )
                precursorMzToleranceRule = MzToleranceRule_Mono;
            else if( PrecursorMzToleranceRule == "avg" )
                precursorMzToleranceRule = MzToleranceRule_Avg;

			ProteinIndexOffset = 0;

			string cwd;
			cwd.resize( MAX_PATH );
			getcwd( &cwd[0], MAX_PATH );
			WorkingDirectory = cwd.c_str();

			if( TicCutoffPercentage > 1.0 )
			{
				TicCutoffPercentage /= 100.0;
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
            chiDist.reset(new boost::math::chi_squared(4));
		}
	};

	extern RunTimeConfig* g_rtConfig;
}
}

#endif
