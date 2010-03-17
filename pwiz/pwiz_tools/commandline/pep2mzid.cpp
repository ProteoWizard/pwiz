//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/mziddata/Pep2MzIdent.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/data/proteome/Peptide.hpp"

#include <iostream>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/tokenizer.hpp>

using boost::shared_ptr;
using boost::tokenizer;
using boost::lexical_cast;

using namespace std;
using namespace boost::filesystem;

using namespace pwiz::data;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;

int main(int argc, char* argv[])
{
    namespace pepxml = pwiz::data::pepxml;
    
    string inFile, outFile;
    
    if (argc<3)
    {
        string usage = "usage: ";
        usage += argv[0];
        usage += " <in> <out>";
        throw runtime_error(usage.c_str());
    }

    inFile = argv[1];
    outFile = argv[2];

    ifstream in(inFile.c_str());
    
    MSMSPipelineAnalysis msmsPA;
    msmsPA.read(in);

    Pep2MzIdent p2m(msmsPA);

    MzIdentMLFile::write(*p2m.getMzIdentML(), outFile);
    
    return 0;
}
