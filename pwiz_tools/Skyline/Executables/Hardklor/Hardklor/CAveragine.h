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

#ifndef _CAVERAGINE_H
#define _CAVERAGINE_H

#include "CHardklorVariant.h"
#include "CPeriodicTable.h"

//#include <cstdlib>
#include <fstream>
#include <vector>
#include <string>

/*
const double AVE_MASS = 111.1254;
const double AVE_C = 4.9384;
const double AVE_H = 7.7583;
const double AVE_N = 1.3577;
const double AVE_O = 1.4773;
const double AVE_S = 0.0417;
*/

const double AVE_MASS = 111.2137;
const double AVE_C = 4.9558;
const double AVE_H = 7.8241;
const double AVE_N = 1.3571;
const double AVE_O = 1.4716;
const double AVE_S = 0.0390;

typedef struct atomInfo {
  char symbol[3];
  int numIsotopes;
  std::vector<double> *mass;
  std::vector<double> *abundance;
  atomInfo(){
    strcpy(symbol,"X");
    numIsotopes=0;
    mass = new std::vector<double>;
    abundance = new std::vector<double>;
  }
  atomInfo(const atomInfo& a){
    strcpy(symbol,a.symbol);
    numIsotopes=a.numIsotopes;
    mass = new std::vector<double>(*a.mass);
    abundance = new std::vector<double>(*a.abundance);
  }
  ~atomInfo(){
    delete mass;
    delete abundance;
  }
  atomInfo& operator=(const atomInfo& a){
    if(&a!=this){
      strcpy(symbol,a.symbol);
      numIsotopes=a.numIsotopes;
      delete mass;
      delete abundance;
      mass = new std::vector<double>(*a.mass);
      abundance = new std::vector<double>(*a.abundance);
    }
    return *this;
  }

} atomInfo;

class CAveragine {
  
 public:
  //Constructors & Destructors
  //CAveragine();
  CAveragine(const char* fn="ISOTOPE.DAT", const char* fn2="Hardklor.dat");
  ~CAveragine();

  //Methods:
  void calcAveragine(double,CHardklorVariant);
  void clear();
  void defaultValues();
  void getAveragine(char*);
  void setAveragine(int,int,int,int,int);
  int getElement(int);
  double getMonoMass();
  CPeriodicTable* getPT();
  void loadTable(const char*);

 protected:

 private:
  //Data Members:
  int *atoms;
  CPeriodicTable *PT;
  std::vector<atomInfo> *enrich;

};

#endif
