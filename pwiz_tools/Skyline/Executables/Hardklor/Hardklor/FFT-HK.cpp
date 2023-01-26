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
#include "FFT-HK.h"
#include <iostream>

using namespace std;
using namespace MSToolkit;

void FFTCharge(double *f, Spectrum& s, unsigned int start, unsigned int stop,
							 unsigned int lowCharge, unsigned int highCharge, double interval, bool bSpline){

	Spectrum fft;
	Peak_T p;
	double mz = s.at(start).mz;
	p.mz = s.at(start).mz;
	p.intensity = s.at(start).intensity;
	double maxIntensity = 0;

	int i;
	int power;
	int size = (highCharge - lowCharge + 1) * 3;
	double *in;
	complex *out;

	//Find out how many points are needed and dimension arrays.
	power=2;
	while(power<(s.size()/2)) power*=2;
	out = new complex[power];	
	in = new double[power];

	//Build the input array.
	for(i=0;i<power;i++) {
		if(i<(s.size()/2)) {
			out[i].real=s.at(i*2).intensity;
			out[i].imag=s.at(i*2+1).intensity;
		} else {
			out[i].real=0;
			out[i].imag=0;
		}

	}

	FFTreal(out,power);

	//Create a spectrum of charge states and frequencies
	for(i=0;i<power;i++){
		p.mz = (double)i/(power*2*interval);
		if(p.mz < (double)lowCharge - 0.5) continue;
		if(p.mz > (double)highCharge + 0.5) break;
		p.intensity = (float)sqrt(out[i].real*out[i].real+out[i].imag*out[i].imag);
		fft.add(p);
	}

	//Linear interpolation of charge range at 1/3 intervals.
	for(i=0;i<size;i++){
		mz = (double)(i+2)/3 + (double)(lowCharge - 1);
		f[i] = GetIntensity(fft,0,fft.size()-1,mz);
		if(f[i]>maxIntensity) maxIntensity = f[i];
	}

	//Normalize
	for(i=0;i<size;i++)	{
		f[i]/=maxIntensity;
		if(f[i]<0) f[i]=0;
		if(f[i]>1) f[i]=1;
		//cout << (double)(i+2)/3 + (double)(lowCharge - 1) << "\t" << f[i] << endl;
	}

	//Clean up memory
	delete [] out;
	delete [] in;

	//cout << "FFT done" << endl;

}

void Patterson(double *f, Spectrum& s, unsigned int start, unsigned int stop,
							 unsigned int lowCharge, unsigned int highCharge, double interval){

	int i,j;
	int size = (highCharge - lowCharge + 1) * 3;
	double ch;
	double shift = (double)(lowCharge-1);
	double maxIntensity = 0;
	
	double mz1,mz2;

	if(f==NULL) f = new double[size];

	for(i=0;i<size;i++){
		f[i]=0;
		ch = (double)(i+2)/3 + shift;
		ch = 1/ch;
		ch /= 2;
		for(j=(int)start;j<=(int)stop;j++){
				mz1 = s.at(j).mz - ch;
				mz2 = s.at(j).mz + ch;
				f[i]+= (GetIntensity(s,start,stop,mz1) * GetIntensity(s,start,stop,mz2));
		}
		if(f[i]>maxIntensity) maxIntensity = f[i];
	}

	//Normalize
	if(maxIntensity>0){
		for(i=0;i<size;i++) {
			f[i]/=maxIntensity;
			if(f[i]<0)f[i]=0;
			if(f[i]>1)f[i]=1;

			//cout << (double)(i+2)/3 + shift << "\t" << f[i] << endl;
		}
	}
	//exit(1);

}

double GetIntensity(Spectrum& s, unsigned int start, unsigned int stop, double mz){

	int index;
	int lower=start+1;
	int upper=stop;

	double slope;
	double xDif;
	float yDif;
	float intercept;
	double newX;

	if(start+1>stop) return 0;

	//binary search for correct point
	while(true){
		index = (lower + upper) / 2;
		if(index==start) return 0;

		if(s.at(index-1).mz < mz && s.at(index).mz > mz) {

			//linear interpolation
			yDif = s.at(index).intensity - s.at(index-1).intensity;
			xDif = s.at(index).mz - s.at(index-1).mz;
			slope = yDif/xDif;
			intercept = s.at(index-1).intensity;
			newX = mz - s.at(index-1).mz;
			return slope * newX + intercept;

		} else if(lower > upper) {
			return 0;
		} else {
			if(s.at(index).mz < mz) {
				lower = index + 1;			
			} else {
				upper = index - 1;
			}
		}
	}

}

void SenkoCharge(vector<int> *charges, Spectrum& s, unsigned int start, unsigned int stop, 
								 unsigned int lowCharge, unsigned int highCharge, double interval, char method) {

	charges->clear();
	double *array1;
	double *array2;
	float ch;
	float maxPeak = 0;
	int i,c;
	unsigned int j;
	int size = (highCharge - lowCharge + 1) * 3;
	bool bFound;

	/* use linear interpolation (splines do not perform well over large
		 spaces that have 0 intensity).
	*/
	Spectrum spl;
	Peak_T p;
	double mz = s.at(start).mz;
	p.mz = s.at(start).mz;
	p.intensity = s.at(start).intensity;
	spl.add(p);
	while(true){
		p.mz += interval;
		if(p.mz > s.at(stop).mz) break;
		p.intensity=(float)GetIntensity(s,start,stop,p.mz);
		if(p.intensity<0) p.intensity=0;
		spl.add(p);
	}

	switch(method){
		case 'F':
			array1 = new double[size];
			FFTCharge(array1,spl,0,spl.size()-1,lowCharge,highCharge,interval);
			break;
		case 'P':
			array1 = new double[size];
			Patterson(array1,spl,0,spl.size()-1,lowCharge,highCharge,interval);
			break;
		case 'S':
		default:
			array1 = new double[size];
			array2 = new double[size];
			Patterson(array1,spl,0,spl.size()-1,lowCharge,highCharge,interval);
			FFTCharge(array2,spl,0,spl.size()-1,lowCharge,highCharge,interval);
			for(i=0;i<size;i++) array1[i]*=array2[i];
			delete [] array2;
			break;
	}

	//keep all charges that greater than 50% max peak
	for(i=0;i<size-2;i++){
		if( (array1[i+1] > array1[i]) && (array1[i+1] > array1[i+2]) && 
			  (array1[i+1] > 0.5) ) {
			ch = (float)(i+3)/3 + (float)(lowCharge - 1);
			c = (int)(ch+0.5);

			//check if charge already exists in list. If not, add it to the list.
			bFound = false;
			for(j=0;j<charges->size();j++){
				if(charges->at(j)==c) {
					bFound = true;
					break;
				}
			}
			if(!bFound) charges->push_back(c);
		}
	}

	delete [] array1;

}
