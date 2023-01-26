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
#include "S2N.h"
#include <iostream>

using namespace std;
using namespace MSToolkit;

// Adapted from THRASH
Spectrum signalToNoise(Spectrum& s, int start, int stop, float sig, float* cutoff, bool skipZero, bool subtract){

  int i;
  int c;
  double d;
  int a[100];
  float b[100];
  float FWHM;
  float maxIntensity;
  float baseIntensity;
  Spectrum newSpec;

  if(s.size()==0) return newSpec;

	if(skipZero){
		//count only non-zero values in spectrum
		c=0;
		for(i=start;i<stop+1;i++){
			if(s.at(i).intensity>0)c++;
		};
	} else {
		//accept all values
		c=stop-start+1;
	};

	if(c==0) return newSpec;

  float *sortD = new float[c];

  //Set initial array values
  for(i=0;i<100;i++) {
    a[i]=0;
    b[i]=0;
  };

  //Copy abundances to an array that can be sorted
  maxIntensity=0;
  c=0;
  for(i=start;i<stop+1;i++) {
		if(skipZero) {
			if(s.at(i).intensity > 0) sortD[c++] = s.at(i).intensity;
		} else {
			sortD[c++] = s.at(i).intensity;
		};
    if(s.at(i).intensity > maxIntensity) maxIntensity = s.at(i).intensity;
  };

  //Sort the array
  sort(sortD,sortD+c);
  
  //Find the number of relative abundances at 1% intervals
  c=0;
  for(i=0;i<100;i++){
    while(sortD[c]*100/maxIntensity < i) c++;
    a[i] = c;
  };

  //Get the derivative
  for(i=1;i<100;i++) {
    b[i]=(float)(a[i]-a[i-1]);
  };
    
  //Smooth the derivative
  SG_SmoothD(&b[0],100,20,2);

  //Find the most intense point in the derivative
  c=0;
  for(i=0;i<100;i++){
    if(b[i]>=b[c]) c=i;
  };
  baseIntensity=(float)c;

  //find FWHM (this is a hack - I don't know the proper way to do this)
  i=c;
  d=b[c]/2;
  while(i<100){
    if(b[i]>d)i++;
    else break;
  };
  FWHM = (float)(i-c);

  i=c;
  while(i>0){
    if(b[i]>d)i--;
    else break;
  };

  FWHM += (float)(c-i);


  //Clean up the memory
  delete [] sortD;

  //Go find the peaks using the baseIntensity and FWHM
	if(subtract) newSpec = SNSubtracted(s,start,stop,sig,FWHM,maxIntensity,baseIntensity);
  else newSpec = SNPeaks(s,start,stop,sig,FWHM,maxIntensity,baseIntensity);

  *cutoff = sig*FWHM+baseIntensity;

  return newSpec;

};

Spectrum SNPeaks(Spectrum& s, int start, int stop, float SN, float FWHM, float max, float base){
  int i;
  double sig;
  Spectrum newSpec;

  //Scan all data. Keep data that exceed the signal to noise threshold.
  for(i=start;i<stop+1;i++){
    sig = (s.at(i).intensity*100/max - base) / FWHM;
    if(sig > SN) newSpec.add(s.at(i));
  };

  return newSpec;

};

Spectrum SNSubtracted(Spectrum& s, int start, int stop, float SN, float FWHM, float max, float base){
  int i;
  double sig;
  Spectrum newSpec;
	Peak_T p;

  //Scan all data. Keep data that exceed the signal to noise threshold.
  for(i=start;i<stop+1;i++){
    sig = (s.at(i).intensity*100/max - base) / FWHM;
    if(sig > SN) newSpec.add(s.at(i));
		else {
			p=s.at(i);
			p.intensity=0;
			newSpec.add(p);
		};
  };

  return newSpec;

};


// Adapted from THRASH
float findSNCutoff(Spectrum& s, int start, int stop, float sig, bool skipZero){

  int i;
  int c;
  double d;
  int a[100];
  float b[100];
  float FWHM;
  float maxIntensity;
  float baseIntensity;
  Spectrum newSpec;

  if(s.size()==0) 0;

	if(skipZero){
		//count only non-zero values in spectrum
		c=0;
		for(i=start;i<stop+1;i++){
			if(s.at(i).intensity>0)c++;
		};
	} else {
		//accept all values
		c=stop-start+1;
	};

	if(c==0) return 0;

  float *sortD = new float[c];

  //Set initial array values
  for(i=0;i<100;i++) {
    a[i]=0;
    b[i]=0;
  };

  //Copy abundances to an array that can be sorted
  maxIntensity=0;
  c=0;
  for(i=start;i<stop+1;i++) {
		if(skipZero) {
			if(s.at(i).intensity > 0) sortD[c++] = s.at(i).intensity;
		} else {
			sortD[c++] = s.at(i).intensity;
		};
    if(s.at(i).intensity > maxIntensity) maxIntensity = s.at(i).intensity;
  };

  //Sort the array
  sort(sortD,sortD+c);
  
  //Find the number of relative abundances at 1% intervals
  c=0;
  for(i=0;i<100;i++){
    while(sortD[c]*100/maxIntensity < i) c++;
    a[i] = c;
  };

  //Get the derivative
  for(i=1;i<100;i++) {
    b[i]=(float)(a[i]-a[i-1]);
  };
    
  //Smooth the derivative
  SG_SmoothD(&b[0],100,20,2);

  //Find the most intense point in the derivative
  c=0;
  for(i=0;i<100;i++){
    if(b[i]>=b[c]) c=i;
  };
  baseIntensity=(float)c;

  //find FWHM (this is a hack - I don't know the proper way to do this)
  i=c;
  d=b[c]/2;
  while(i<100){
    if(b[i]>d)i++;
    else break;
  };
  FWHM = (float)(i-c);

  i=c;
  while(i>0){
    if(b[i]>d)i--;
    else break;
  };

  FWHM += (float)(c-i);


  //Clean up the memory
  delete [] sortD;

  return sig*FWHM+baseIntensity;

};

float findSNCutoff2(Spectrum& s, int start, int stop, float sig, double& max, bool skipZero){

  int i;
  int c;
  double d;
  int a[100];
  float b[100];
  float FWHM;
  float maxIntensity;
  float baseIntensity;
  Spectrum newSpec;

  if(s.size()==0) 0;

	if(skipZero){
		//count only non-zero values in spectrum
		c=0;
		for(i=start;i<stop+1;i++){
			if(s.at(i).intensity>0)c++;
		};
	} else {
		//accept all values
		c=stop-start+1;
	};

	if(c==0) return 0;

  float *sortD = new float[c];

  //Set initial array values
  for(i=0;i<100;i++) {
    a[i]=0;
    b[i]=0;
  };

  //Copy abundances to an array that can be sorted
  maxIntensity=0;
  c=0;
  for(i=start;i<stop+1;i++) {
		if(skipZero) {
			if(s.at(i).intensity > 0) sortD[c++] = s.at(i).intensity;
		} else {
			sortD[c++] = s.at(i).intensity;
		};
    if(s.at(i).intensity > maxIntensity) maxIntensity = s.at(i).intensity;
  };

  //Sort the array
  sort(sortD,sortD+c);
  
  //Find the number of relative abundances at 1% intervals
  c=0;
  for(i=0;i<100;i++){
    while(sortD[c]*100/maxIntensity < i) c++;
    a[i] = c;
  };

  //Get the derivative
  for(i=1;i<100;i++) {
    b[i]=(float)(a[i]-a[i-1]);
  };
    
  //Smooth the derivative
  SG_SmoothD(&b[0],100,20,2);

  //Find the most intense point in the derivative
  c=0;
  for(i=0;i<100;i++){
    if(b[i]>=b[c]) c=i;
  };
  baseIntensity=(float)c;

  //find FWHM (this is a hack - I don't know the proper way to do this)
  i=c;
  d=b[c]/2;
  while(i<100){
    if(b[i]>d)i++;
    else break;
  };
  FWHM = (float)(i-c);

  i=c;
  while(i>0){
    if(b[i]>d)i--;
    else break;
  };

  FWHM += (float)(c-i);


  //Clean up the memory
  delete [] sortD;

	max = maxIntensity;
  return sig*FWHM+baseIntensity;

};

