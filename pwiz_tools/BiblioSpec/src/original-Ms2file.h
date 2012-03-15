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
                                                                            
/* Class for reading and writing Ms2 files
 */

#ifndef MS2FILE_H
#define MS2FILE_H

#include <iostream>
#include <iomanip>
#include <fstream>
#include <vector>
#include <string>
#include <exception>
#include <time.h>
#include "original-Ms2Spectrum.h"
#include "original-RefSpectrum.h"

using namespace std;

enum FILE_TYPE { READ, WRITE };
enum SEQ_TYPE { SEQ, NOSEQ }; //for write lib

struct seq_tag{
  int id;
  int charge;
  int annot;
  string seq;
  string mods;
};
typedef struct seq_tag SEQ_T;

const int DEFAULT_VERBOSITY = 3;

class Ms2file
{
 private:
  const char* filename;
  bool isOpen;
  FILE_TYPE type;
  fstream file;
  bool at_eof;              
  bool noSpecLeft;          
  Ms2Spectrum currentSpec;  //to be removed
  vector<PEAK_T> peaks;     //for reading peaks from file
  int verbosity;
  //file pointers for switching between  reading and searching for spec
  long readHere;            //points to the next spec to be read, 
                            //set when nextScan encounters an S before moving past it
  long fileStart; 

  void init(int v);
  string readHeader();
  void parseHeader(string headerline);
  void readSpec(Spectrum* spec);
  int nextScanNum();

 public:
  Ms2file();
  Ms2file(int v);
  Ms2file(const char* filename, FILE_TYPE type, int verbosity);
  ~Ms2file();
  void open(const char* filename, FILE_TYPE type, int verbosity);
  void openRead(const char* filename); //make this private
  void openWrite(const char* filename);//ditto
  void close();
  void writeHeader(const char* comment);
  void copyHeader(const char* copyfilename);
  void write(string line);
  void write(Spectrum* spec);
  void write(const char* filename, vector<Spectrum> specs);
  void write(const char* filename, vector<Spectrum*> specs, bool sort);
  vector<SEQ_T> writeLib(const char* filename, vector<RefSpectrum*>& specs, SEQ_TYPE s);
  void write_I(Ms2Spectrum* spec, int scanNum, string comment);
  void nextSpec(Spectrum* spec);
  void find(int scNum, Spectrum* spec);
  bool hasSpec();
 };

#endif //MS2FILE_H define
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
