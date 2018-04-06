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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#ifndef _DIRECTAGCONFIG_H
#define _DIRECTAGCONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include "directagAPIConfig.h"

using namespace freicore;

#define DIRECTAG_RUNTIME_CONFIG \
    MULTITHREAD_RTCONFIG \
    RTCONFIG_VARIABLE( string,            OutputSuffix,                ""                    ) \
    RTCONFIG_VARIABLE( int,                NumBatches,                    50                    ) \
    RTCONFIG_VARIABLE( bool,            DuplicateSpectra,            true                ) \
    RTCONFIG_VARIABLE( bool,            UseChargeStateFromMS,        true                ) \
    RTCONFIG_VARIABLE( string,          SpectrumListFilters,        "peakPicking true 2-"   ) \
    RTCONFIG_VARIABLE( bool,            WriteOutTags,                true                ) \
    RTCONFIG_VARIABLE( bool,            WriteScanRankerMetrics,        false                ) \
    RTCONFIG_VARIABLE( string,            ScanRankerMetricsFileName,    ""                    ) \
    RTCONFIG_VARIABLE( bool,            WriteHighQualSpectra,        false                ) \
    RTCONFIG_VARIABLE( string,            HighQualSpecFileName,        ""                    ) \
    RTCONFIG_VARIABLE( double,            HighQualSpecCutoff,            0.6                    ) \
    RTCONFIG_VARIABLE( string,            OutputFormat,                "mzML"                ) 


namespace freicore
{
namespace directag
{
    
    struct RunTimeConfig : public DirecTagAPIConfig
    {
    public:
        RTCONFIG_DEFINE_MEMBERS_EX( RunTimeConfig, DirecTagAPIConfig, DIRECTAG_RUNTIME_CONFIG, "directag.cfg" )

        int        SpectraBatchSize;
        int        ProteinBatchSize;

    protected:
        void finalize()
        {
            string cwd;
            cwd.resize( MAX_PATH );
            getcwd( &cwd[0], MAX_PATH );
            WorkingDirectory = cwd.c_str();
            
            DirecTagAPIConfig::finalize();
        }
    };

    extern shared_ptr<RunTimeConfig>   rtConfig;
}
}

#endif
