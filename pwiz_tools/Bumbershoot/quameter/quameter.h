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


#include <iostream>
#include <fstream>
#include <sstream>
#include <map>
#include <math.h>
#include <time.h>

#include "sqlite/sqlite3pp.h"

#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"

#include "crawdad/SimpleCrawdad.h"
#include "quameterConfig.h"
#include "quameterSharedTypes.h"
#include "quameterSharedFuncs.h"
#include "idpDBReader.h"
#include "scanRankerReader.h"

#include <boost/timer.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/filesystem/operations.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/algorithm/string/predicate.hpp>
#include <boost/algorithm/string/compare.hpp>


#define QUAMETER_LICENSE			COMMON_LICENSE

using boost::shared_ptr;

using namespace std;
using namespace freicore;
using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::proteome;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace SimpleCrawdad;
using namespace crawpeaks;

namespace sqlite = sqlite3pp;

namespace freicore
{
namespace quameter
{

enum InputType { NISTMS, PEPITOME, SCANRANKER, NONE};

struct QuameterInput 
{

    string      sourceID;
	string      sourceFile;
	string      idpDBFile;
    string      pepXMLFile;
    string      scanRankerFile;
    InputType   type;

    QuameterInput(string srcID, string srcFile, string idpDB, string pepXML, string scanRanker, InputType inpType)
    {
        type = inpType;
        if(type == NISTMS)
        {
            sourceID = srcID;
            sourceFile = srcFile;
            idpDBFile= idpDB;
        } else if(type == PEPITOME)
        {
            sourceFile = srcFile;
            pepXMLFile = pepXML;
        } else if(type == SCANRANKER)
        {
            sourceFile = srcFile;
            scanRankerFile = scanRanker;
        }
    }
};

void NISTMSMetrics(QuameterInput, FullReaderList);
void ScanRankerMetrics(QuameterInput, FullReaderList);
void ExecuteMetricsThread();
vector<string> GetNativeId(const string&, const string&);
multimap<int, string> GetDuplicateID(const string&, const string&);

bool compareByPeak(const IntensityPair& pair1, const IntensityPair& pair2) 
{ 
    return pair1.peakIntensity < pair2.peakIntensity; 
}

 /**
    * Given an idpDB file, return its RAW/mzML/etc source files.
    * Add: also accept filenames of interest (e.g. ignore all source files except -these-)
    * Add: also accept an extension type (e.g. use the .RAW source file instead of .mzML)
    */
vector<QuameterInput> GetIDPickerSpectraSources(const string& dbFilename)
{
   
    sqlite::database db(dbFilename);
    string s = "select Id, Name from SpectrumSource";
    sqlite::query qry(db, s.c_str() );
    vector<QuameterInput> sources;
	
    for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
        int intSrcId;
        string srcFilename;

        (*i).getter() >> intSrcId >> srcFilename;

        string sourceId = boost::lexical_cast<string>(intSrcId);
        bfs::path p = dbFilename;
        bfs::path srcPath(p.parent_path() / (srcFilename + "."+g_rtConfig->RawDataFormat));
        if(!bfs::exists(srcPath))
            continue; // if this source file doesn't exist we can't do anything, so let's try for the next source file
        
        QuameterInput qip(sourceId,srcPath.string(),dbFilename,"","",NISTMS);
        sources.push_back(qip);
    }
    return sources;	
}

}
}
