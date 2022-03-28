//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/**
 * The MascotResultsReader collects a list of psms that should be
 * included in the library.  It passes the file object it was using to
 * the MascotSpecReader so the file only has to be opened and parsed once.
 */

#include <sys/stat.h>
#include "MascotResultsReader_dummy.h"
#include "BlibUtils.h"

namespace BiblioSpec {

MascotResultsReader::MascotResultsReader(BlibBuilder& maker, 
                    const char* datFileName, 
                    const ProgressIndicator* parent_progress)
: BuildParser(maker, datFileName, parent_progress)
{
    throw BlibException(false, "Mascot support was explicitly disabled at build time.");
}


MascotResultsReader::~MascotResultsReader()
{
}

bool MascotResultsReader::parseFile(){
    return false;
}

std::vector<PSM_SCORE_TYPE> MascotResultsReader::getScoreTypes() {
    return std::vector<PSM_SCORE_TYPE>(1, MASCOT_IONS_SCORE);
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
