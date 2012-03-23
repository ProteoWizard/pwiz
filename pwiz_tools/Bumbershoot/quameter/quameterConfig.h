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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef _QUAMETERCONFIG_H
#define _QUAMETERCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>

using namespace freicore;
using boost::icl::interval_set;
using boost::icl::continuous_interval;

#define QUAMETER_RUNTIME_CONFIG \
	COMMON_RTCONFIG MULTITHREAD_RTCONFIG \
	RTCONFIG_VARIABLE( string,          OutputFormat,                "tsv"        ) \
    RTCONFIG_VARIABLE( string,          MetricsType,                 "nistms"     ) \
    RTCONFIG_VARIABLE( string,          Instrument,                  "ltq"        ) \
    RTCONFIG_VARIABLE( IntegerSet,      MonoisotopeAdjustmentSet,    string("0")) \
    RTCONFIG_VARIABLE( string,          RawDataFormat,               "RAW"        ) \
    RTCONFIG_VARIABLE( string,          RawDataPath,                 ""           ) \
    RTCONFIG_VARIABLE( double,          ScoreCutoff,                 0.05         ) \
    RTCONFIG_VARIABLE( MZTolerance,     ChromatogramMzLowerOffset,   "0.5mz"      ) \
    RTCONFIG_VARIABLE( MZTolerance,     ChromatogramMzUpperOffset,   "1.0mz"      ) \
	RTCONFIG_VARIABLE( bool,            ChromatogramOutput,          false        ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,         "peakPicking true 1-" )



namespace freicore
{
namespace quameter
{

	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, QUAMETER_RUNTIME_CONFIG, "\r\n\t ", "quameter.cfg", "\r\n#" )

        interval_set<double> chromatogramScanTimeWindow(double centerTime) const
        {
            return interval_set<double>(continuous_interval<double>::closed(centerTime-300, centerTime+300));
        }

        interval_set<double> chromatogramMzWindow(double centerMz, int charge) const
        {
            double mzLower = centerMz - MZTolerance(ChromatogramMzLowerOffset.value * charge, ChromatogramMzLowerOffset.units);
            double mzUpper = centerMz + MZTolerance(ChromatogramMzUpperOffset.value * charge, ChromatogramMzLowerOffset.units);
            return interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
        }

        bool useAvgMass;

	private:
		void finalize()
		{
            bal::to_lower(OutputFormat);
            bal::to_lower(MetricsType);
            bal::to_lower(Instrument);

            useAvgMass = Instrument == "ltq";

            if (OutputFormat != "tsv" && OutputFormat != "csv" && OutputFormat != "xml")
                throw runtime_error("invalid output format");

            if (!bal::starts_with(MetricsType, "nistms") && MetricsType != "pepitome" && MetricsType != "scanranker" && MetricsType != "idfree")
                throw runtime_error("invalid metrics type");

            bal::trim_right_if(RawDataPath, is_any_of("/\\"));

			string cwd;
			cwd.resize( MAX_PATH );
			getcwd( &cwd[0], MAX_PATH );
			WorkingDirectory = cwd.c_str();
		}
	};

	extern RunTimeConfig* g_rtConfig;
}
}

#endif
