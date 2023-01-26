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
#include "CHardklorProtein.h"

using namespace std;

CHardklorProtein::CHardklorProtein(){
  enrich = new vector<sEnrichMercury>;
}

CHardklorProtein::CHardklorProtein(const CHardklorProtein& c){
  int i;

  enrich = new vector<sEnrichMercury>;
  for(i=0;i<c.enrich->size();i++) enrich->push_back(c.enrich->at(i));

  ID = c.ID;
  mz = c.mz;
  monoMass = c.monoMass;
  shft = c.shft;
  abun = c.abun;
  charge = c.charge;
  C = c.C;
  rTime = c.rTime;
  strcpy(seq,c.seq);

}

CHardklorProtein::~CHardklorProtein(){
  delete enrich;
}

CHardklorProtein& CHardklorProtein::operator=(const CHardklorProtein& c){
  int i;

  if(this != &c){
    delete enrich;
    enrich = new vector<sEnrichMercury>;
    for(i=0;i<c.enrich->size();i++) enrich->push_back(c.enrich->at(i));
    
    ID = c.ID;
    mz = c.mz;
    monoMass = c.monoMass;
    shft = c.shft;
    abun = c.abun;
    charge = c.charge;
    C = c.C;
    rTime = c.rTime;
    strcpy(seq,c.seq);
  }
  return *this;

}

void CHardklorProtein::add(int a, int c, double b){
  sEnrichMercury s;
  s.atomNum = a;
  s.isotope = c;
  s.ape = b;
  enrich->push_back(s);
}

sEnrichMercury& CHardklorProtein::at(int i){
  return enrich->at(i);
}

void CHardklorProtein::clear(){
  delete enrich;
  enrich = new vector<sEnrichMercury>;
}

int CHardklorProtein::size(){
  return (int)enrich->size();
}
