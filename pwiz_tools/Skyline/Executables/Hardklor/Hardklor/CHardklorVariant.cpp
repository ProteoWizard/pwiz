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
#include "CHardklorVariant.h"

using namespace std;

CHardklorVariant::CHardklorVariant(){
  ID=0;
  atoms = new vector<sInt>;
  enrich = new vector<sEnrichMercury>;
}

CHardklorVariant::CHardklorVariant(const CHardklorVariant& c){
  int i;
   
  atoms = new vector<sInt>;
  enrich = new vector<sEnrichMercury>;
  
  for(i=0;i<c.atoms->size();i++){
    atoms->push_back(c.atoms->at(i));
  }
  
  for(i=0;i<c.enrich->size();i++){
    enrich->push_back(c.enrich->at(i));
  }

  ID = c.ID;

}

CHardklorVariant::~CHardklorVariant(){
  delete atoms;
  delete enrich;
}

CHardklorVariant& CHardklorVariant::operator=(const CHardklorVariant& c){
  int i;
  if(this!=&c){
    delete atoms;
    delete enrich;
    atoms = new vector<sInt>;
    enrich = new vector<sEnrichMercury>;

    for(i=0;i<c.atoms->size();i++){
      atoms->push_back(c.atoms->at(i));
    }

    for(i=0;i<c.enrich->size();i++){
      enrich->push_back(c.enrich->at(i));
    }

    ID = c.ID;

  }
  return *this;
}

void CHardklorVariant::addAtom(const sInt& a){
  atoms->push_back(a);
}

void CHardklorVariant::addAtom(const int& a, const int& b){
  sInt s;
  s.iLower = a;
  s.iUpper = b;
  atoms->push_back(s);
}

void CHardklorVariant::addEnrich(const sEnrichMercury& a){
  enrich->push_back(a);
}

void CHardklorVariant::addEnrich(const int& a, const int& c, const double& b){
  sEnrichMercury s;
  s.atomNum = a;
  s.isotope = c;
  s.ape = b;
  enrich->push_back(s);
}

sInt& CHardklorVariant::atAtom(int i){
  return atoms->at(i);
}

sEnrichMercury& CHardklorVariant::atEnrich(int i){
  return enrich->at(i);
}

void CHardklorVariant::clear(){
  delete atoms;
  delete enrich;
  atoms = new vector<sInt>;
  enrich = new vector<sEnrichMercury>;
}

int CHardklorVariant::sizeAtom(){
  return (int)atoms->size();
}

int CHardklorVariant::sizeEnrich(){
  return (int)enrich->size();
}

