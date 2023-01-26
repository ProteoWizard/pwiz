/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#ifndef _CHARDKLORPARSER_H
#define _CHARDKLORPARSER_H

#include <fstream>
#include <iostream>
#include <vector>

#include "CHardklorSetting.h"
#include "CPeriodicTable.h"
#include "MSToolkitTypes.h"

class CHardklorParser {

 public:
  //Constructors & Destructors
  CHardklorParser();
  ~CHardklorParser();

  //Methods
  void parse(char*);
  bool parseCMD(int argc, char* argv[]);
  bool parseConfig(char*);
  MSToolkit::MSFileFormat getFileFormat(const char* c);
  CHardklorSetting& queue(int);
  int size();

 protected:

 private:
   //Methods
   bool makeVariant(char* c);
   void warn(const char*, int);
	 
   //Data Members
   CHardklorSetting global;
   std::vector<CHardklorSetting> *vQueue;
};


#endif
