//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// MSIAMTDataTest.cpp 
///

#include "MSIAMTData.hpp"
#include <iostream>
#include <fstream>
#include <string>

using namespace std;
using namespace pwiz;
using namespace eharmony;

int main()
{
    ifstream ifs("msi_aligned_database.xml");
    MSIAMTData data;
    data.read(ifs);

    /*
    ostringstream oss;
    XMLWriter writer(oss);
    data.write(writer);
    cout << oss.str() << endl;
    */
    
    ofstream ofs("msi_aligned_database.tsv");
    ofstream r("r_aligned.tsv");
    vector<PeptideEntry>::iterator it = data.peptideEntries.begin();
    for(; it != data.peptideEntries.end(); ++it)
        {
            vector<ModificationStateEntry>::iterator jt = it->modificationStateEntries.begin();
            for(; jt != it->modificationStateEntries.end(); ++jt)
                {
                    vector<Observation>::iterator kt = jt->observations.begin();
                    for(; kt != jt->observations.end(); ++kt)
                        {
                            ofs << jt->modifiedMass << "\t" << kt->observedHydrophobicity << "\t" << it->peptideSequence << "\n";
                            r << jt->modifiedMass << "\t" << kt->observedHydrophobicity << "\n";
                        }

                }

        }

    return 0;

}
