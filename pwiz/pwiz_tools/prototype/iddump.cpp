//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>
#include <fstream>


using namespace std;
using namespace pwiz;
using namespace pwiz::data::pepxml;
using namespace pwiz::proteome;


int dofile(const string& filename)
{
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error(("Unable to open file" + filename).c_str());

    MSMSPipelineAnalysis anal;
    anal.read(is);

    const size_t columnWidth = 12;

    const vector<SpectrumQuery>& sq = anal.msmsRunSummary.spectrumQueries;
    cout << "# spectrumQueries: " << sq.size();
    cout << "#\n";
    cout << "# precursorNeutralMass assumedCharge retentionTimeSec calculatedMZ\n";
    for (vector<SpectrumQuery>::const_iterator it=sq.begin(); it!=sq.end(); ++it)
        cout << setprecision(6) << fixed
             << setw(columnWidth) << it->precursorNeutralMass
             << setw(columnWidth) << it->assumedCharge
             << setw(columnWidth) << it->retentionTimeSec
             << setw(columnWidth) << Ion::mz(it->precursorNeutralMass, it->assumedCharge)
             << endl;

    return 0;
}


int main(int argc, const char* argv[])
{
    try
    {
        if (argc != 2)
            throw runtime_error("Usage: iddump filename\n");
    
        const char* filename = argv[1];

        return dofile(filename);
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[msdiff] Caught unknown exception.\n";
    }

    return 1;
}

