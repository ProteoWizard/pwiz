/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
#pragma once

#include "BuildParser.h"
#include "Verbosity.h"
#include "pwiz/data/identdata/IdentDataFile.hpp"

namespace BiblioSpec{
    
    /**
     * Class for parsing mzIdentML files.
     */
    class MzIdentMLReader : public BuildParser {
        
    public:
        MzIdentMLReader(BlibBuilder& maker,
                        const char* mzidFileName,
                        const ProgressIndicator* parent_progress);
        ~MzIdentMLReader();
        
        bool parseFile();
        
    private:
        pwiz::identdata::IdentDataFile* pwizReader_;
        map< string, vector<PSM*> > fileMap_; // vector of PSMs for each file
        double scoreThreshold_;

        // name some file accessors to make the code more readable
        vector<pwiz::identdata::SpectrumIdentificationListPtr>::const_iterator list_iter_;
        vector<pwiz::identdata::SpectrumIdentificationListPtr>::const_iterator list_end_;
        vector<pwiz::identdata::SpectrumIdentificationResultPtr>::const_iterator result_iter_;
        vector<pwiz::identdata::SpectrumIdentificationItemPtr>::const_iterator item_iter_;


        void collectPsms();
        void extractModifications(string modPepSeq, PSM* psm);
        double getScore(const pwiz::identdata::SpectrumIdentificationItem& item);
    };
} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

