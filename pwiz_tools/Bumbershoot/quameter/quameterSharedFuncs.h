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

#ifndef _QUAMETERSHAREDFUNCS_H
#define _QUAMETERSHAREDFUNCS_H

#include <vector>

using namespace std;

namespace freicore
{
namespace quameter
{
    /**
        * Return the first quartile of a vector<double>
        */
        inline double Q1(vector<double> dataSet) {
            double quartile1;
            int dataSize = (int)dataSet.size();
            if ( dataSize % 4 == 0 ) {
                int index1 = (dataSize/4)-1;
                int index2 = (dataSize/4);
                quartile1 = (dataSet[index1] + dataSet[index2]) / 2;
            }
            else {
                int index1 = (dataSize)/4;
                quartile1 = dataSet[index1];
            }
            return quartile1;
        }

        /**
        * Return the second quartile (aka median) of a vector<double>
        */
        inline double Q2(vector<double> dataSet) {
            double quartile2;
            int dataSize = (int)dataSet.size();
            if ( dataSize % 2 == 0 ) {
                int index1 = (dataSize/2)-1;
                int index2 = (dataSize/2);
                quartile2 = (dataSet[index1] + dataSet[index2]) / 2;
            }
            else {
                int index1 = (dataSize)/2;
                quartile2 = dataSet[index1];
            }
            return quartile2;
        }

        /**
        * Overload second quartile function to int
        */
        inline int Q2(vector<int> dataSet) {
            int quartile2;
            int dataSize = (int)dataSet.size();
            if ( dataSize % 2 == 0 ) {
                int index1 = (dataSize/2)-1;
                int index2 = (dataSize/2);
                quartile2 = (dataSet[index1] + dataSet[index2]) / 2;
            }
            else {
                int index1 = (dataSize)/2;
                quartile2 = dataSet[index1];
            }
            return quartile2;
        }

        /**
        * Return the third quartile of a vector<double>
        */
        inline double Q3(vector<double> dataSet) {
            double quartile3;
            int dataSize = (int)dataSet.size();
            if ( (dataSize * 3) % 4 == 0 ) {
                int index1 = (3*dataSize/4)-1;
                int index2 = (3*dataSize/4);
                quartile3 = (dataSet[index1] + dataSet[index2]) / 2;
            }
            else {
                int index1 = (3*dataSize)/4;
                quartile3 = dataSet[index1];
            }
            return quartile3;
        }

}
}

#endif
