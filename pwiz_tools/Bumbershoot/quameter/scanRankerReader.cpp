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

#include "scanRankerReader.h"

namespace freicore
{
namespace quameter
{

    void ScanRankerReader::extractData()
    {
        ifstream reader(srTextFile.c_str());
        
        string input;
        getline(reader,input);
        while(boost::starts_with(input,"H"))
            getline(reader,input);
        do
        {
            if(!input.empty())
            {
                tokenizer parser(input, tabDelim);
                tokenizer::iterator itr = parser.begin();
                // Parse the columns
                int spectrumIndex = boost::lexical_cast<int>(*(++itr));
                string nativeID = *(++itr);
                //cout << nativeID << ",";
                double precMZ = boost::lexical_cast<double>(*(++itr));
                //cout << precMZ << ",";
                int charge = boost::lexical_cast<int>(*(++itr));
                //cout << charge << ",";
                double precMass = boost::lexical_cast<double>(*(++itr));
                //cout << precMass << ",";
                double bestTagScore = boost::lexical_cast<double>(*(++itr));
                //cout << bestTagScore << ",";
                double bestTagTIC = boost::lexical_cast<double>(*(++itr));
                //cout << bestTagTIC << ",";
                double tagMzRange = boost::lexical_cast<double>(*(++itr));
                //cout << tagMzRange << ",";
                double srScore = boost::lexical_cast<double>(*(++itr));
                //cout << srScore << endl;
                
                ScanRankerMS2PrecInfo scanInfo;
                scanInfo.nativeID = nativeID;
                scanInfo.precursorMZ = precMZ;
                scanInfo.precursorMass = precMass;
                scanInfo.charge = charge;
                precursorInfos.insert(make_pair<string,ScanRankerMS2PrecInfo>(nativeID,scanInfo));
                bestTagScores.insert(make_pair<ScanRankerMS2PrecInfo,double>(scanInfo,bestTagScore));
                tagMzRanges.insert(make_pair<ScanRankerMS2PrecInfo,double>(scanInfo,tagMzRange));
                scanRankerScores.insert(make_pair<ScanRankerMS2PrecInfo,double>(scanInfo,srScore));
                bestTagTics.insert(make_pair<ScanRankerMS2PrecInfo,double>(scanInfo,bestTagTIC));
            }

        }while(getline(reader,input));
    }

}
}
