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

#ifndef INCLUDED_CLASSIFICATION_ENGINE
#define INCLUDED_CLASSIFICATION_ENGINE

#include <string>
using std::string;

#include <stdio.h>
#include <math.h>
#include <iostream>
#include <fstream>
#include <string>
#include <vector>

class ClassifierEngine{
public:
  ClassifierEngine() 
    : classifierName_(""),
      nraw_(0),
      ninputs_(0),
      nh1_(0),
      nclasses_(0)
  {};

  void init(string externalDesignFileName){
    cout<<"reading : "<<externalDesignFileName<<endl;
    ifstream externalDesignFile(externalDesignFileName.c_str());
    if(!externalDesignFile){
      throw std::runtime_error("Unable to read externalDesignFile: " + externalDesignFileName);
    }
    string junk;
    externalDesignFile >> classifierName_;
    externalDesignFile >> junk >> nraw_;
    externalDesignFile >> junk >> ninputs_;
    externalDesignFile >> junk >> nh1_;
    externalDesignFile >> junk >> nclasses_;
    
    int feature;
    externalDesignFile >> junk;
    features_.resize(ninputs_);
    for(size_t i=0;i<ninputs_;i++){
      externalDesignFile >> feature;
      features_[i] = feature;
    }
    
    double mean;
    means_.resize(ninputs_);
    externalDesignFile >> junk;
    for(size_t i=0;i<ninputs_;i++){
      externalDesignFile >> mean;
      means_[i] = mean;
    }
    

    double sd;
    sds_.resize(ninputs_);
    externalDesignFile >> junk;
    for(size_t i=0;i<ninputs_;i++){
      externalDesignFile >> sd;
      sds_[i] = sd;
    }


    // double x_h1[NH1_CZ_ESI+1][NINPUTS_CZ_ESI+1] = {
    //    vector<double> v(ninputs_+1);
    vector<double> v(ninputs_+1);
    externalDesignFile >> junk; 
    for(size_t i=0;i<nh1_+1;i++){
      x_h1_.push_back(v);
      for(size_t j=0;j<ninputs_+1;j++){
	externalDesignFile >> x_h1_[i][j];
      }
    }


    //     double h1_y[NCLASSES_CZ_ESI+1][NH1_CZ_ESI+1] = {
    v.resize(nh1_+1);
    //    v.resize(nclasses_+1);
    externalDesignFile >> junk;
      for(size_t i=0;i<nclasses_+1;i++){
	h1_y_.push_back(v);
	for(size_t j=0;j<nh1_+1;j++){
	externalDesignFile >> h1_y_[i][j];
      }
    }


    externalDesignFile.close();
  }

  double classify(vector<double> & propertyVector) const{
    vector<double> inputs;
    //    int i;

    size_t n, j, k;
    vector<double> h1(nh1_);
    vector<double> y(nclasses_);    
    vector<double> outputs(nclasses_);

    /* normalize */
    inputs.resize(ninputs_);
    for(n = 0; n < ninputs_; n++){
      inputs[n] = (propertyVector[features_[n]] - means_[n]) / sds_[n];
    }
    
    for(j = 0; j < nh1_; j++){
      h1[j] = x_h1_[j][ninputs_];
      for(k = 0; k < ninputs_; k++){
	h1[j] += inputs[k] * x_h1_[j][k];
      }
    }
    /* take sigmoids */
    for(j = 0; j < nh1_; j++){
      h1[j] = 1. / (1. + exp(- 1.000000 * h1[j]));
    }

  /* calculate outputs */
    for(j = 0; j < nclasses_; j++){
      y[j] = h1_y_[j][nh1_];
      for(k = 0; k < nh1_; k++){
	y[j] += h1[k] * h1_y_[j][k];
      }
    }
    /* take output sigmoid */
    for(j = 0; j < nclasses_; j++){
      y[j] = 1. / (1. + exp(- 1.000000 * y[j]));
    }

    /* copy outputs */
    for(n = 0; n < nclasses_; n++)
      outputs[n] = y[n];

    //  /* find highest output */
    //    for(best = n = 0; n < nclasses_; n++)
    //      if(outputs[best] < outputs[n]) best = n;
    
    return(outputs[0]);
  }

  string classifierName() const {return classifierName_;}
    
protected:
  string classifierName_;
  size_t nraw_;
  size_t ninputs_;
  size_t nh1_;
  size_t nclasses_;
  vector<int> features_;
  vector<double> means_;
  vector<double> sds_;
  vector<vector<double> > x_h1_;
  vector<vector<double> > h1_y_;
};

#endif



