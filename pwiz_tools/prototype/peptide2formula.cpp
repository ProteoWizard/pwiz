//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/proteome/Peptide.hpp"

using namespace pwiz::util;
using namespace pwiz::proteome;

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        cerr << "Usage: peptide2formula <text file with one peptide per line>" << endl;
        return 1;
    }

    if (!bfs::exists(argv[1]))
    {
        cerr << "File \"" << argv[1] << "\" does not exist." << endl;
        return 1;
    }

    ifstream peptideFile(argv[1]);
    string sequence;
    while (getlinePortable(peptideFile, sequence))
    {
        Peptide peptide(sequence);
        cout << sequence << '\t' << peptide.formula() << '\n';
    }

    return 0;
}
