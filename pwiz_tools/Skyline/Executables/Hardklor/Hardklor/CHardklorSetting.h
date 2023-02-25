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
#ifndef _CHARDKLORSETTING_H
#define _CHARDKLORSETTING_H

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "HardklorTypes.h"
#include "CHardklorVariant.h"

/* 
   Defined as a class instead of a struct so
   that default parameters are used. Data access
   will be identical to that of a struct.
*/
class CHardklorSetting {

 public:
  //Constructors & Destructors:
  CHardklorSetting();
  CHardklorSetting(const CHardklorSetting&);
  ~CHardklorSetting();

  //Methods:
  CHardklorSetting& operator=(const CHardklorSetting&);
  void clearVariant();
  void out(char *s);

  //Data Mebers:
  bool centroid;			//spectrum data is centroided
  bool distArea;			//report distribution area instead of base peak intensity
  bool iAnalysis;			//intersect analysis(true) or union analysis(false)
  bool noBase;				//No base molecule - perform analysis with only averagine variant models
  bool noSplit;				//analyze entire spectrum at once
  //bool rawAvg;      //use averaged raw scans
  bool reducedOutput;	//output m/z, intensity, and charge only (de-isotoped peaks)
  bool skipZero;			//ignore zero intensity data points
  bool staticSN;			//for sna=THRASH; assume one noise level for entire spectrum
  bool xml;						//output is in xml
  bool reportAveragineAndMassOffset; //when true, add averagine formula and mass offset to output e.g. H21C14N4O4[+3.038518]
  
  int boxcar;				//number of scans to average together
  int boxcarFilter;	//value to meet or exceed to keep peak in boxcar averaged data
  int depth;        //maximum number of overlapping peptides
  int maxCharge;    //max charge state to search for
  int minCharge;    //min charge state to search for
  int minIsotopePeaks; // Must identify at least this many isotope peaks in a feature for it to be included in report
  int msLevel;      //integer representation of the scan level
  int peptide;			//maximum peptide models to analyze at a single time
  //int ppMatch;      //pre-processing matches. m/z must be observed this amount across ppWin
  //int ppWin;        //pre-processing window size (1 = +/-1 scan)
  //int noiseMatch;   //for sna=PP; number of matches required for real peaks
  //int noiseWindow;  //for sna=PP; Size of window over which scans are analyzed
  //int rawAvgCutoff; //Noise cutoff intensity for averaged raw scans
  //int rawAvgWidth;  //Number of scans on either side of target to average (1 = +/-1 scan)
  int sl;           //sensitivity level
  int smooth;       //Savitsky-Golay smoothing window size
  //int sna;          //Signal-to-noise algorithm; 0=THRASH, 1=Persistent peaks (PP)

  double maxMolMZ;  //Largest m/z to detect in the data
  double corr;      //correlation threshold
  double ppm;       //ppm tolerance of m/z values to match across scans
  double res400;    //resolution at m/z 400
  double sn;        //for sna=THRASH; signal-to-noise ratio threshold
  double snWindow;  //for sna=THRASH; bin size over which local noise is computed
  double winSize;   //maximum window size for analysis

  sInt scan;        //scan range to analyze
  sDouble window;   //m/z range to analyze

  char chargeMode;        //charge determination function to use
  char formula[64];       //non-averagine model formula
  std::string inFile;       //input file name
  std::string outFile;      //output file name
  char rawFilter[256];    //Filter which spectra from raw files are analyzed
  std::string MercuryFile;  //mercury data file to use
  std::string HardklorFile; //hardklor data file to use

  specType msType;                    //Type of mass spectrometer used to acquire data
  hkAlgorithm algorithm;              //Deconvolving algorithm to use
  std::vector<CHardklorVariant> *variant;  //Variants to make to averagine
  MSToolkit::MSFileFormat fileFormat;            //File format
  MSToolkit::MSSpectrumType mzXMLFilter;         //Filter for mzXML files

 protected:
 private:

};

#endif
