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
