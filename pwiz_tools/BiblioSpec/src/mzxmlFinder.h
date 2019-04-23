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
#include <cmath>
#include <cstdlib>
#include <map>
#include <iostream>
#include <vector>
#include "Verbosity.h"
#include "saxhandler.h"


namespace BiblioSpec {

class mzxmlFinder : public SAXHandler{

 public:
 mzxmlFinder(const char* filename) :
    curNum_(-1), curIndex_(-1), curPrecursor_(0.0), returnType_(SCAN_NUM), readingPrecursor_(false) {
    setFileName(filename); // for the saxhandler
  };

  ~mzxmlFinder() {
  };

  class SpecInfo {
   public:
    SpecInfo(double precursor, SpecInfo* next): scan_(-1), precursor_(precursor), next_(next) {}
    ~SpecInfo() {
      SpecInfo* info = next_;
      while (info != NULL)
      {
        SpecInfo* tmp = info;
        info = info->next_;
        delete tmp;
      }
    }
    int getScan() const { return scan_; }
    void setScan(int scan) { scan_ = scan; }
    SpecInfo* getMatch(double precursor) {
      SpecInfo* info = this;
      while (info != NULL && !info->precursorMatch(precursor)) info = info->next_;
      return info;
    }
    bool precursorMatch(double precursor) const { return abs(precursor_ - precursor) <= 0.001; }
   private:
    int scan_;
    double precursor_;
    SpecInfo* next_;
  };

  void findScanNumFromName(map<string, SpecInfo*>* nameNumTable) {
    nameNumTable_ = nameNumTable;
    returnType_ = SCAN_NUM;
    parse();
  };

  void findScanIndexFromName(map<string, SpecInfo*>* nameNumTable) {
    nameNumTable_ = nameNumTable;
    returnType_ = INDEX;
    parse();
  }
 private:
  int curNum_;
  int curIndex_;
  string curName_;
  double curPrecursor_;
  enum RETURN_TYPE { SCAN_NUM, INDEX };
  RETURN_TYPE returnType_;
  map<string, SpecInfo*>* nameNumTable_;
  bool readingPrecursor_;
  string charBuf_;

  virtual void startElement(const XML_Char* name, const XML_Char** attr) {
    if(isElement("scan", name)) {
      curNum_ = atoi( getAttrValue("num", attr));
      curIndex_ += 1;
    } else if(isElement("scanOrigin", name)) {
      curName_ = getAttrValue("parentFileName", attr);
    } else if (isElement("precursorMz", name)) {
      readingPrecursor_ = true;
    }
  }
  virtual void endElement(const XML_Char* name) {
    if (isElement("precursorMz", name)) {
      readingPrecursor_ = false;
      curPrecursor_ = atof(charBuf_.c_str());
      charBuf_.clear();
    } else if (isElement("scan", name)) {
      // look up name
      map<string, SpecInfo*>::iterator i = nameNumTable_->find(curName_);
      if (i != nameNumTable_->end()) {
        SpecInfo* info = i->second->getMatch(curPrecursor_);
        if (info != NULL) {
          info->setScan(returnType_ == SCAN_NUM ? curNum_ : curIndex_);
          Verbosity::comment(V_ALL, "Scan %s has %s %d.", curName_.c_str(),
                             (returnType_ == INDEX ? "index" : "scan number"),
                             i->second->getScan());
          return;
        }
      }
      Verbosity::warn("Couldn't find '%s' (%.3f)", curName_.c_str(), curPrecursor_);
    }
  }
  virtual void characters(const XML_Char *s, int len) {
    if (readingPrecursor_) {
      charBuf_.append(s, len);
    }
  }
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
