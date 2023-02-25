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
#include "CHardklorSetting.h"

using namespace std;
using namespace MSToolkit;

CHardklorSetting::CHardklorSetting(){
  noBase=false;
	iAnalysis=false;
  maxCharge=5;
	minCharge=1;
  msLevel=1;
  depth=3;
  peptide=10;
  smooth=0;
  corr=0.85;
  sn=1.0;
  scan.iLower=0;
  scan.iUpper=0;
  window.dLower=0;
  window.dUpper=0;
	algorithm=FastFewestPeptides;
	variant = new vector<CHardklorVariant>;

	msType=FTICR;
	res400=100000;
	winSize=4.0;

	chargeMode='Q';
	snWindow=250.0;
	skipZero=true;
	noSplit=false;
	sl=2;
	fileFormat=dunno;
	mzXMLFilter=MS1;
	distArea=false;
	strcpy(formula,"");

	centroid=false;
	staticSN=true;
  xml=false;
	reducedOutput=false;

  //ppMatch=1;
  //ppWin=1;
  //noiseMatch=1;
  //noiseWindow=3;
  ppm=10.0;
  //sna=0;
	boxcar=0;
	boxcarFilter=0;

  //rawAvg=false;
  //rawAvgWidth=1;
  //rawAvgCutoff=1000;

  maxMolMZ=5000;

  minIsotopePeaks = 1; // Don't report features unless they have at least this many isotope peaks identified

  strcpy(rawFilter,"");
}

CHardklorSetting::CHardklorSetting(const CHardklorSetting& c){
  int i;

	//Copy variant list
  variant = new vector<CHardklorVariant>;
  for(i=0;i<c.variant->size();i++) variant->push_back(c.variant->at(i));
	
  //Copy other data memebers
  noBase=c.noBase;
	iAnalysis=c.iAnalysis;
  maxCharge=c.maxCharge;
	minCharge=c.minCharge;
  msLevel=c.msLevel;
  depth=c.depth;
  peptide=c.peptide;
  smooth=c.smooth;
  corr=c.corr;
  sn=c.sn;
  scan.iLower=c.scan.iLower;
  scan.iUpper=c.scan.iUpper;
  window.dLower=c.window.dLower;
  window.dUpper=c.window.dUpper;
  inFile=c.inFile;
  outFile=c.outFile;
  MercuryFile=c.MercuryFile;
  HardklorFile=c.HardklorFile;
	algorithm=c.algorithm;
	msType=c.msType;
	res400=c.res400;
	winSize=c.winSize;

	chargeMode=c.chargeMode;
	snWindow=c.snWindow;
	skipZero=c.skipZero;
	noSplit=c.noSplit;
	sl=c.sl;
	fileFormat=c.fileFormat;
	mzXMLFilter=c.mzXMLFilter;
	distArea=c.distArea;
	strcpy(formula,c.formula);

	centroid = c.centroid;
	staticSN = c.staticSN;
  xml = c.xml;
	reducedOutput = c.reducedOutput;

  //ppMatch=c.ppMatch;
  //ppWin=c.ppWin;
  //noiseMatch=c.noiseMatch;
  //noiseWindow=c.noiseWindow;
  ppm=c.ppm;
  //sna=c.sna;
	boxcar=c.boxcar;
	boxcarFilter=c.boxcarFilter;

  //rawAvg=c.rawAvg;
  //rawAvgWidth=c.rawAvgWidth;
  //rawAvgCutoff=c.rawAvgCutoff;

  maxMolMZ=c.maxMolMZ;

  strcpy(rawFilter,c.rawFilter);
}
  
CHardklorSetting::~CHardklorSetting(){
	delete variant;
}

CHardklorSetting& CHardklorSetting::operator=(const CHardklorSetting& c){
  int i;
  if (this!=&c){
		delete variant;
    variant = new vector<CHardklorVariant>;
    for(i=0;i<c.variant->size();i++){
      variant->push_back(c.variant->at(i));
    }
    noBase=c.noBase;
		iAnalysis=c.iAnalysis;
    maxCharge=c.maxCharge;
		minCharge=c.minCharge;
    msLevel=c.msLevel;
    depth=c.depth;
		peptide=c.peptide;
    smooth=c.smooth;
    corr=c.corr;
    sn=c.sn;
    scan.iLower=c.scan.iLower;
    scan.iUpper=c.scan.iUpper;
    window.dLower=c.window.dLower;
    window.dUpper=c.window.dUpper;
	inFile = c.inFile;
	outFile = c.outFile;
	MercuryFile = c.MercuryFile;
	HardklorFile = c.HardklorFile;
		algorithm=c.algorithm;
		msType=c.msType;
		res400=c.res400;
		winSize=c.winSize;

		chargeMode=c.chargeMode;
		snWindow=c.snWindow;
		skipZero=c.skipZero;
		noSplit=c.noSplit;
		sl=c.sl;
		fileFormat=c.fileFormat;
		mzXMLFilter=c.mzXMLFilter;
		distArea=c.distArea;
		strcpy(formula,c.formula);

		centroid = c.centroid;
		staticSN = c.staticSN;
    xml = c.xml;
		reducedOutput = c.reducedOutput;

    //ppMatch=c.ppMatch;
    //ppWin=c.ppWin;
    //noiseMatch=c.noiseMatch;
    //noiseWindow=c.noiseWindow;
    ppm=c.ppm;
    //sna=c.sna;
		boxcar=c.boxcar;
		boxcarFilter=c.boxcarFilter;

    //rawAvg=c.rawAvg;
    //rawAvgWidth=c.rawAvgWidth;
    //rawAvgCutoff=c.rawAvgCutoff;
    maxMolMZ = c.maxMolMZ;

    strcpy(rawFilter,c.rawFilter);
  }
  return *this;
}

void CHardklorSetting::clearVariant(){
  delete variant;
  variant = new vector<CHardklorVariant>;
}

void CHardklorSetting::out(char *s){
  sprintf(s,"minCh:%d maxCh:%d d:%d p:%d s:%d corr:%lf sn:%lf res:%d,%lf win:%lf sl:%d v:%zu\n",minCharge,maxCharge,depth,peptide,smooth,corr,sn,msType,res400,winSize,sl,variant->size());
}
