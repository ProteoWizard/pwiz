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

/*
 *  Class for handling the list of spec to be added to the library.
 *  Since renamed to the SSL (scan-sequence list) file.
 */

#ifndef REFFILE_H
#define REFFILE_H

#include <algorithm>
#include <functional>
#include <iostream>
#include <fstream>
#include <string>
#include <vector>
using std::binary_function;

struct refData{ 
  string file;
  int scanNum;
  int charge;
  string seq;
  string mods;
  int annot;

  refData();
};

class RefFile
{
 private:
  ifstream input;
  refData curRef;
  bool moreRef;           //more in the file to read?
  string requiredheader;  //to compare to what is read
  string path; 
  vector<refData> refs;
  int curRefIndex;

  void checkHeader();  
  bool readNextRef();
  //for debugging
  void printRef(refData ref);

 public:
  RefFile() {requiredheader = "file\tscan\tcharge\tsequence\tmodifications\tannotation";
            curRefIndex = 0;}
  ~RefFile();

  void open(const char* name);
  bool hasRef();
  refData getNextRef();
  int getNumRef();
  static void printRefToFile(refData& ref, ofstream& file);
};

//sort by both ms2 file name and scan number
struct compRefData : public binary_function<refData, refData, bool>
{
  bool operator()(refData r1, refData r2) {
    if( r1.file == r2.file ) {
      return( r1.scanNum < r2.scanNum );
    }
    //else
    return (r1.file < r2.file);
  }
};


#endif //REFFILE_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
