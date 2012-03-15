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
/*
 *  BiblioSpec Version 1.0
 *  Copyright 2006 University of Washington. All rights reserved.
 *  Written by Barbara Frewen, Michael J. MacCoss, William Stafford Noble
 *  in the Department of Genome Sciences at the University of Washington.
 *  http://proteome.gs.washington.edu/
 *
 */
                                                                             
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

using namespace std;

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
