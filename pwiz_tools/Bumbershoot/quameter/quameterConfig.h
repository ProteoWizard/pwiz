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

using namespace freicore;

#define QUAMETER_RUNTIME_CONFIG \
	COMMON_RTCONFIG MULTITHREAD_RTCONFIG \
	RTCONFIG_VARIABLE( string,          OutputFormat,               "tsv"        ) \
    RTCONFIG_VARIABLE( string,          MetricsType,                "nistms"     ) \
    RTCONFIG_VARIABLE( string,          Instrument,                 "ltq"        ) \
    RTCONFIG_VARIABLE( string,          RawDataExtension,           "mzXML"        ) \
    RTCONFIG_VARIABLE( double,			ScoreCutoff,                0.05   		 )


namespace freicore
{
namespace quameter
{

	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, QUAMETER_RUNTIME_CONFIG, "\r\n\t ", "quameter.cfg", "\r\n#" )


	private:
		void finalize()
		{
            if (!bal::iequals(OutputFormat, "tsv") && !bal::iequals(OutputFormat, "csv") && !bal::iequals(OutputFormat, "xml"))
                throw runtime_error("invalid output format");

            if (!bal::iequals(MetricsType, "nistms") && !bal::iequals(MetricsType, "pepitome") && !bal::iequals(MetricsType, "idfree"))
                throw runtime_error("invalid metrics requested");

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