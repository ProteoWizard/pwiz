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
#include <ctime>
#include "original-Ms2Spectrum.h"
#include "original-RefSpectrum.h"


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
