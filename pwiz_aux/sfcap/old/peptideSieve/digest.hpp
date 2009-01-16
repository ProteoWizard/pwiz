//
// Original Author: Parag Mallick
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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



#ifndef INCLUDE_DIGEST_HPP
#define INClUDE_DIGEST_HPP

#include <stdio.h>
#include <math.h>
#include <iostream>
#include <fstream>
#include <string>
#include <algorithm>
#include <map>

#include "config.hpp"

class Digest{
public:
  Digest(const string& sequence,const Config& config = Config()){
    sequence_ = sequence;
    config_ = config;
    initMassMap();
    createPeptides();
    currentPep_ = peptides_.begin();
    currentPepMass_ = massVector_.begin();
  }

  Digest(){
    initMassMap();
  }


  size_t numPeptides(){
    return peptides_.size();
  }

  string currentPeptide(){
    return *currentPep_;
  }

  double currentMass(){
    return *currentPepMass_;
  }

  void next(){
    currentPep_++;
    currentPepMass_++;
  }

  void createPeptides();
  double computeMass(string pep);
  void initMassMap();

  ~Digest(){;}
private:

  vector<string>::iterator currentPep_;
  vector<double>::iterator currentPepMass_;
  vector<string> peptides_;
  vector<double> massVector_;
  void setAminoAcidMasses();
  Config config_;
  string sequence_;
  map<char, double> massMap_;

};

#endif
