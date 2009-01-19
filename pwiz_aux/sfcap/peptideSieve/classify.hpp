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


#ifndef INCLUDED_CLASSIFY_BASE_HPP
#define INCLUDED_CLASSIFY_BASE_HPP

#include <stdio.h>
#include <math.h>
#include <iostream>
#include <fstream>
#include <string>
#include <map>
#include <vector>



#include "config.hpp"
using namespace boost::program_options;

#include "fasta.h" // interface to fasta file handling
using bioinfo::fasta;

#include "digest.hpp"


#define NRAW_CZ_ESI 1010
#define NINPUTS_CZ_ESI 5
#define NH1_CZ_ESI 25
#define NCLASSES_CZ_ESI 2

#define NRAW_CZ_MALDI 1010
#define NINPUTS_CZ_MALDI 5
#define NH1_CZ_MALDI 25
#define NCLASSES_CZ_MALDI 2

#define NRAW_ISB_ICAT 1010
#define NINPUTS_ISB_ICAT 5
#define NH1_ISB_ICAT 25   
#define NCLASSES_ISB_ICAT 2

#define NRAW_ISB_ESI 1010
#define NINPUTS_ISB_ESI 5
#define NH1_ISB_ESI 25   
#define NCLASSES_ISB_ESI 2

#include "proteotypicResult.hpp"

class ProteotypicClassifier{  //yes I know how this should be done and this isn't it.
public:
  ProteotypicClassifier(const Config& config){config_ = config;}
  void readPropertyMap(map<char, vector<double> > & propertyMap, string _propertyFile);

  ProteotypicResult classify(vector<double> propertyVector,string experimentalDesign);

  vector<double> convertToProperties(const string &peptide,map<char,vector<double> > & propertyMap, double mass);

  void processFASTAFile(const string& filename, map<char,vector<double> > & propertyMap);
  void processTXTFile(const string& filename, map<char,vector<double> > & propertyMap);

  int normalize_ISB_ICAT(double* inputs);
  int normalize_CZ_MALDI(double* inputs);
  int normalize_CZ_ESI(double* inputs);
  int normalize_ISB_ESI(double* inputs);
  int classify_CZ_ESI (double* inputs, double* outputs);
  int classify_CZ_MALDI (double* inputs, double* outputs);
  int classify_ISB_ICAT (double* inputs, double* outputs);
  int classify_ISB_ESI (double* inputs, double* outputs);

  void outputTXTResult(const ProteotypicResult &p,const string &experimentalDesign, double minPValue){
    if(((experimentalDesign == "PAGE_MALDI") || (experimentalDesign == "ALL")) && p._cz_maldiResult>=minPValue) {
      (*outStream_)<<"PAGE_MALDI\t";
      (*outStream_)<<p._protein<<"\t"
		  <<p._peptide<<"\t";
      (*outStream_)<<p._cz_maldiResult<<endl;
    }

    if(((experimentalDesign == "PAGE_ESI") || (experimentalDesign == "ALL")) && p._cz_esiResult>=minPValue) {
      (*outStream_)<<"PAGE_ESI\t";
      (*outStream_)<<p._protein<<"\t"
	     <<p._peptide<<"\t";
      (*outStream_)<<p._cz_esiResult<<endl;      
    }
    if(((experimentalDesign == "MUDPIT_ESI") || (experimentalDesign == "ALL")) && p._isb_esiResult>=minPValue) {
      (*outStream_)<<"MUDPIT_ESI\t";
      (*outStream_)<<p._protein<<"\t"
	     <<p._peptide<<"\t";
      (*outStream_)<<p._isb_esiResult<<endl;
    }
    if(((experimentalDesign == "ICAT_ESI") || (experimentalDesign == "ALL")) && p._isb_icatResult>=minPValue) {
      (*outStream_)<<"ICAT_ESI\t";
      (*outStream_)<<p._protein<<"\t"
	     <<p._peptide<<"\t";
      (*outStream_)<<p._isb_icatResult<<endl;
    }
  }
	
  void outputFASTAResult(const vector<ProteotypicResult> &resultVector,const string &experimentalDesign, double minPValue){

    for(vector<ProteotypicResult>::const_iterator q = resultVector.begin();q!=resultVector.end();q++){
      outputTXTResult(*q, experimentalDesign, minPValue);
    }
  }
  ~ProteotypicClassifier(){;}

protected:
  string outputFile_;
  string propertyOutputFile_;
  ostream* outStream_;
  ostream* propertyOutStream_;
  Config config_;

  void getOutputStream(const string &filename) {
    outputFile_ = config_.outputFileName(filename);
    if(outputFile_.size() == 0){
      outStream_ =  &std::cout;
    }
    else{
      outStream_ = new ofstream(outputFile_.c_str(),ios_base::trunc);
    }
  }

  void getPropertyStream(const string &filename) {
    propertyOutputFile_ = config_.propertyFileName(filename);
    if(propertyOutputFile_.size() == 0){
      propertyOutStream_ =  NULL;
    }
    else{
      propertyOutStream_ = new ofstream(propertyOutputFile_.c_str(),ios_base::trunc);
    }
  }


  void releasePropertyStream(){
    if(propertyOutStream_ != NULL){
      delete propertyOutStream_;
    }
  }

  void releaseOutputStream(){
    if(outputFile_.size() != 0){
      delete outStream_;
    }
  }

  void printPropertyVector(const string &shortHeader, const string &peptide, const vector<double> &propertyVector){
    if(propertyOutStream_ != NULL){
      (*propertyOutStream_)<<shortHeader<<"\t"<<peptide;
      for(size_t prop=0;prop<propertyVector.size();prop++){
	(*propertyOutStream_)<<"\t"<<propertyVector[prop];
      }
    (*propertyOutStream_)<<std::endl;
  }
}


};

#endif
