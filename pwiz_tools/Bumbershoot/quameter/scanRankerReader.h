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

#ifndef _SCANRANKERREADER_H
#define _SCANRANKERREADER_H

#include "quameterSharedTypes.h"

#include <boost/tokenizer.hpp>
#include <boost/assign.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/algorithm/string/find.hpp>
#include <boost/algorithm/string/trim.hpp>
#include <boost/algorithm/string/split.hpp>
#include <boost/algorithm/string/predicate.hpp>
#include <boost/algorithm/string/compare.hpp>

#include <boost/accumulators/accumulators.hpp>
#include <boost/accumulators/statistics/stats.hpp>
#include <boost/accumulators/statistics/mean.hpp>
#include <boost/accumulators/statistics/median.hpp>
#include <boost/accumulators/framework/accumulator_set.hpp>
#include <boost/lexical_cast.hpp>

#include <iostream>
#include <fstream>

using namespace std;

namespace accs = boost::accumulators;
using namespace boost::assign;
using namespace boost::algorithm;

namespace freicore
{
namespace quameter
{
    static const boost::char_separator<char> delim(" =\r\n");
    static const boost::char_separator<char> tabDelim("\t");

    typedef boost::tokenizer<boost::char_separator<char> > tokenizer;

    struct ScanRankerReader
    {
        string srTextFile;
        multimap<string,ScanRankerMS2PrecInfo> precursorInfos;
        map<ScanRankerMS2PrecInfo,double> bestTagScores;
        map<ScanRankerMS2PrecInfo,double> bestTagTics;
        map<ScanRankerMS2PrecInfo,double> tagMzRanges;
        map<ScanRankerMS2PrecInfo,double> scanRankerScores;

        ScanRankerReader(const string& file)
        {
            srTextFile = file;
        }

        void extractData();
    };

}
}
#endif
