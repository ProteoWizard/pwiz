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

/*
 * A little reader to look up the parentFileName attribute from the
 * ScanOrigin sub element of the scan element of an mxXML file.
 */
#include <map>
#include <iostream>
#include "Verbosity.h"
#include "saxhandler.h"

using namespace std;

namespace BiblioSpec {

class mzxmlFinder : public SAXHandler{

 public:
 mzxmlFinder(const char* filename) :
    curNum_(-1), curIndex_(-1), returnType_(SCAN_NUM) {
    setFileName(filename); // for the saxhandler
  };

  ~mzxmlFinder() {
  };

  void findScanNumFromName(map<string, int>* nameNumTable) {
    nameNumTable_ = nameNumTable;
    returnType_ = SCAN_NUM;
    parse();
  };

  void findScanIndexFromName(map<string, int>* nameNumTable) {
    nameNumTable_ = nameNumTable;
    returnType_ = INDEX;
    parse();
  }
 private:
  int curNum_;
  int curIndex_;
  enum RETURN_TYPE { SCAN_NUM, INDEX };
  RETURN_TYPE returnType_;
  map<string, int>* nameNumTable_;

  virtual void startElement(const XML_Char* name, const XML_Char** attr) {
    if(isElement("scan", name)) {
      curNum_ = atoi( getAttrValue("num", attr));
      curIndex_ += 1;
    } else if(isElement("scanOrigin", name)) {
      string specName = getAttrValue("parentFileName", attr);

      // look up name
      map<string, int>::iterator found = nameNumTable_->find(specName);
      if( found != nameNumTable_->end() ) {
          if( returnType_ == SCAN_NUM ){
              found->second = curNum_;
              curNum_ = -1;
          } else { // index
              found->second = curIndex_;
          }
          Verbosity::comment(V_ALL, "Scan %s has %s %d.", specName.c_str(),
                             (returnType_ == INDEX ? "index" : "scan number"),
                             found->second);
      }
    }
  };
  //  virtual void endElement(const XML_Char* name);
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
