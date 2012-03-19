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
/**
 * The MascotResultsReader collects a list of psms that should be
 * included in the library.  It passes the file object it was using to
 * the MascotSpecReader so the file only has to be opened and parsed once.
 */

#include <sys/stat.h>
#include "MascotResultsReader.h"
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

void MascotResultsReader::parseMods(PSM* psm, string modstr, 
                                    string readableModStr){
}

void MascotResultsReader::addVarMod(PSM* psm, 
                                    char varLookUpChar, 
                                    int aaPosition){
}

void MascotResultsReader::addErrorTolerantMod(PSM* psm, 
                                              string readableModStr, 
                                              int aaPosition){
}


int MascotResultsReader::getVarModIndex(const char c){
    return 0;
}

void MascotResultsReader::getIsotopeMasses(){
}

void MascotResultsReader::applyIsotopeDiffs(PSM* psm, string quantName){
}

string MascotResultsReader::getFilename(ms_inputquery& spec){
    return "";
}

string MascotResultsReader::getErrorMessage(){
    return "";
}

string MascotResultsReader::getErrorMessage(int errorCode){
    return "";
}

unsigned int MascotResultsReader::getCacheFlag(const char* filename, 
                                               int threshold){
    return 0;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
