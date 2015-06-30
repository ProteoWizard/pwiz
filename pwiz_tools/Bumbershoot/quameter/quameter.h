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
// The Initial Developer of the Original Code is Ken Polzin.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include <ctime>

#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"

#include "quameterConfig.h"
#include "quameterSharedTypes.h"
#include "quameterSharedFuncs.h"
#include "quameterFileReaders.h"

#include <boost/timer.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/filesystem/operations.hpp>


#define QUAMETER_LICENSE            COMMON_LICENSE

using namespace freicore;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::proteome;
using namespace pwiz::analysis;
using namespace pwiz::util;

namespace sqlite = sqlite3pp;

namespace freicore
{
namespace quameter
{

enum InputType { NISTMS, PEPITOME, SCANRANKER, IDFREE, NONE};

struct QuameterInput 
{

    string      sourceID;
    string      sourceFile;
    string      idpDBFile;
    string      pepXMLFile;
    string      scanRankerFile;
    InputType   type;

    QuameterInput(const string& srcID,
                  const string& srcFile,
                  const string& idpDB,
                  const string& pepXML,
                  const string& scanRanker,
                  InputType inpType)
    {
        type = inpType;
            sourceFile = srcFile;
        if(type == NISTMS)
        {
            sourceID = srcID;
            idpDBFile= idpDB;
        }
        else if(type == PEPITOME)
            pepXMLFile = pepXML;
        else if(type == SCANRANKER)
            scanRankerFile = scanRanker;
    }
};

void NISTMSMetrics(const QuameterInput&);
void ScanRankerMetrics(const QuameterInput&);
void IDFreeMetrics(const QuameterInput&);
void ExecuteMetricsThread();

 /**
    * Given an idpDB file, return its RAW/mzML/etc source files.
    * Add: also accept filenames of interest (e.g. ignore all source files except -these-)
    * Add: also accept an extension type (e.g. use the .RAW source file instead of .mzML)
    */
vector<QuameterInput> GetIDPickerSpectraSources(const string& dbFilepath)
{
   
    sqlite::database db(dbFilepath);
    sqlite::query q(db, "SELECT Id, Name FROM SpectrumSource");
    vector<QuameterInput> sources;
    
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        int sourceId;
        string sourceBasename;

        row.getter() >> sourceId >> sourceBasename;

        string sourceIdStr = boost::lexical_cast<string>(sourceId);
        bfs::path sourceFilepath(sourceBasename + "." + g_rtConfig->RawDataFormat);
        if(!g_rtConfig->RawDataPath.empty())
            sourceFilepath = g_rtConfig->RawDataPath / sourceFilepath;
        else
            sourceFilepath = bfs::path(dbFilepath).parent_path() / sourceFilepath;

        if(!bfs::exists(sourceFilepath))
        {
            cerr << "Unable to find raw file at: " << sourceFilepath << endl;
            continue;
        }
        
        QuameterInput qip(sourceIdStr, sourceFilepath.string(), dbFilepath, "", "", NISTMS);
        sources.push_back(qip);
    }
    return sources;    
}

}
}
