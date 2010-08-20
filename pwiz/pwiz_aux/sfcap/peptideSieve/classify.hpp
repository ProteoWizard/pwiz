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
#include <sstream>

#include "config.hpp"
using namespace boost::program_options;

//#include "fasta.h" // interface to fasta file handling
//using bioinfo::fasta;

#include "digest.hpp"
#include "classificationEngine.hpp"

/*
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
*/

#include "proteotypicResult.hpp"

class ProteotypicClassifier{  //yes I know how this should be done and this isn't it.
public:
  ProteotypicClassifier(const Config& config, const string& experimentalDesigns){config_ = config;readClassifiers(experimentalDesigns);}
  void readPropertyMap(map<char, vector<double> > & propertyMap, string _propertyFile);
  void readClassifiers(const string& experimentalDesigns){
    vector<string> edVector;
    _Tokenize(experimentalDesigns, edVector,",");
    classifiers_.resize(edVector.size());
    for(size_t i = 0;i<edVector.size();i++){
      classifiers_[i].init(edVector[i]);
    }
  }


  vector<double> convertToProperties(const string &peptide,map<char,vector<double> > & propertyMap, double mass);

  void processFASTAFile(const string& filename, map<char,vector<double> > & propertyMap);
  void processTXTFile(const string& filename, map<char,vector<double> > & propertyMap);
  
  void classify(vector<double> propertyVector, ProteotypicResult & classifierResult);

  /*
    int normalize_ISB_ICAT(double* inputs);
    int normalize_CZ_MALDI(double* inputs);
    int normalize_CZ_ESI(double* inputs);
    int normalize_ISB_ESI(double* inputs);
    int classify_CZ_ESI (double* inputs, double* outputs);
    int classify_CZ_MALDI (double* inputs, double* outputs);
    int classify_ISB_ICAT (double* inputs, double* outputs);
    int classify_ISB_ESI (double* inputs, double* outputs);
  */

  void outputTXTResult(const ProteotypicResult &p,double minPValue){

    for(vector<ClassifierEngine>::const_iterator i = classifiers_.begin();i!=classifiers_.end();i++){

      string nm = i->classifierName();
      map<string,double>::const_iterator r = p._results.find(nm);

      if(r->second > minPValue){
	(*outStream_)<<nm<<"\t";
	(*outStream_)<<p._protein<<"\t"
		     <<p._peptide<<"\t";
	(*outStream_)<<r->second<<endl;      
      }
    }
  }

	
  void outputFASTAResult(const vector<ProteotypicResult> &resultVector, double minPValue){
    for(vector<ProteotypicResult>::const_iterator q = resultVector.begin();q!=resultVector.end();q++){
      outputTXTResult(*q, minPValue);
    }
  }
  ~ProteotypicClassifier(){;}

protected:
  string outputFile_;
  string propertyOutputFile_;
  ostream* outStream_;
  ostream* propertyOutStream_;
  Config config_;
  vector<ClassifierEngine> classifiers_;

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

  void _Tokenize(const string& str,
		vector<string>& tokens,
		const string& delimiters = " "){
    // Skip delimiters at beginning.
    string::size_type lastPos = str.find_first_not_of(delimiters, 0);
    // Find first "non-delimiter".
    string::size_type pos     = str.find_first_of(delimiters, lastPos);
    
    while (string::npos != pos || string::npos != lastPos){
        // Found a token, add it to the vector.
      tokens.push_back(str.substr(lastPos, pos - lastPos));
      // Skip delimiters.  Note the "not_of"
      lastPos = str.find_first_not_of(delimiters, pos);
      // Find next "non-delimiter"
      pos = str.find_first_of(delimiters, lastPos);
    }
  }


};

#endif
