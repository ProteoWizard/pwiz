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
#ifndef _CHARDKLORPROTEIN_H
#define _CHARDKLORPROTEIN_H

#include <vector>

#include "HardklorTypes.h"

class CHardklorProtein {
 public:
  //Constructors & Destructors:
  CHardklorProtein();
  CHardklorProtein(const CHardklorProtein&);
  ~CHardklorProtein();

  //Functions:
  CHardklorProtein& operator=(const CHardklorProtein&);
  void add(int, int, double);
  sEnrichMercury& at(int);
  void clear();
  int size();

  //Data Members:
  sInt ID;
  double mz;
  double monoMass;
  double shft;
  double abun;
  double rTime;
  int charge;
  int C;
  char seq[64];
  
 protected:
 private:
  //Data Members:
  std::vector<sEnrichMercury> *enrich;

};

#endif
