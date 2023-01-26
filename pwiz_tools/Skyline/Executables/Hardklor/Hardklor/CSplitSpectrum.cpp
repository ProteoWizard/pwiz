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
#include "CSplitSpectrum.h"
#include <iostream>

using namespace std;
using namespace MSToolkit;

CSplitSpectrum::CSplitSpectrum(Spectrum* spec, CHardklorSetting& sett) {
  wholeSpec=spec;
	userParams=sett;
	setA = new vector<CSpecAnalyze>;
	setB = new vector<CSpecAnalyze>;
	finalAnalysis = new vector<CSpecAnalyze>;
	s2n = new vector<float>;
	aIndex = new vector<int>;
	bIndex = new vector<int>;
}

CSplitSpectrum::~CSplitSpectrum(){
	delete setA;
	delete setB;
	delete finalAnalysis;
	delete s2n;
	delete aIndex;
	delete bIndex;
	wholeSpec=NULL;
	averagine=NULL;
	mercury=NULL;
}

int CSplitSpectrum::getNumWindows(){
  return (int)finalAnalysis->size();
}

//Needs function description
void CSplitSpectrum::NoSplitAnalysis(){
	int i;

	goodPeaks.clear();
	delete s2n;
	s2n = new vector<float>;

	goodPeaks.setParams(userParams);
	if(userParams.sn>0) {

		//need to add window restrictions
		if(userParams.chargeMode=='F' || 
			 userParams.chargeMode=='P' ||
			 userParams.chargeMode=='S'){
			goodPeaks.peaks = signalToNoise(*wholeSpec,0,wholeSpec->size()-1,(float)userParams.sn,&goodPeaks.S2NCutoff,true,true);
		} else {
			goodPeaks.peaks = signalToNoise(*wholeSpec,0,wholeSpec->size()-1,(float)userParams.sn,&goodPeaks.S2NCutoff);
		}
		for(i=0;i<goodPeaks.peaks.size();i++)	s2n->push_back(goodPeaks.S2NCutoff);
	} else {

		goodPeaks.S2NCutoff=0;
		for(i=0;i<wholeSpec->size();i++){
			if(userParams.window.dLower>0 || userParams.window.dUpper>0){
				if(wholeSpec->at(i).mz<userParams.window.dLower || wholeSpec->at(i).mz>userParams.window.dUpper) continue;
			}
			goodPeaks.peaks.add(wholeSpec->at(i));
			s2n->push_back(0);
		}
	}
	goodPeaks.FindPeaks();

}
  

/* 
	 This function creates two overlapping arrays of CSpecAnalyze objects with a width
   of gapSize. The overlap is exactly in the middle of each object. The two arrays are
	 stored as vectors for future analysis.
*/
void CSplitSpectrum::OverlappingAnalysis(double gapSize){

  int startA, endA;
  int startB, endB;
  double mzStartA, mzEndA;
  double mzStartB, mzEndB;
	double mz=0.0;

  int i=0;
 
	CSpecAnalyze sa;
	sa.setParams(userParams);
	sa.setAveragine(averagine);
	sa.setMercury(mercury);

	delete setA;
	delete setB;
	setA = new vector<CSpecAnalyze>;
	setB = new vector<CSpecAnalyze>;

  startA = -1;
  startB = -1;

	mzStartA = wholeSpec->at(0).mz;
  mzEndA = mzStartA + gapSize;
  mzStartB = mzStartA + (gapSize/2);
  mzEndB = mzStartB + gapSize;

  while(true){
    
    if(i==wholeSpec->size()){
      endA=i-1;
      endB=i-1;
      break;
    }

		mz = wholeSpec->at(i).mz;

    if(mz >= mzStartA && startA == -1) startA=i;
    if(mz >= mzStartB && startB == -1) startB=i;

		//Check A windows
    if(mz >= mzEndA) {
      endA=i;

			if(endA-startA>1){

				//Clear the CSpecAnalyze object and find the peaks
				sa.clear();
				if(userParams.sn>0){
					sa.S2NCutoff = findSNCutoff(*wholeSpec,startA,endA,(float)userParams.sn);
				} else {
					sa.S2NCutoff=0;
				}
				sa.FindPeaks(*wholeSpec,startA,endA);
				if(userParams.sn>0)	sa.removePeaksBelowSN();
				setA->push_back(sa);
			}

	    startA=-1;
		  endA=0;
			mzStartA = mzEndA + 0.000000001;
      mzEndA = mzEndA + gapSize;

    }

		//Check B windows
    if(wholeSpec->at(i).mz >= mzEndB) {
      endB=i;

			if(endB-startB>1){

				//Clear the CSpecAnalyze object and find the peaks
		    sa.clear();

				if(userParams.sn>0){
					sa.S2NCutoff = findSNCutoff(*wholeSpec,startB,endB,(float)userParams.sn);
				} else {
					sa.S2NCutoff=0;
				}
				sa.FindPeaks(*wholeSpec,startB,endB);
				if(userParams.sn>0)	sa.removePeaksBelowSN();
				setB->push_back(sa);
			}

      startB=-1;
      endB=0;
      mzStartB = mzEndB + 0.000000001;
      mzEndB = mzEndB + gapSize;
    }

    i++;

  }
 
}

void CSplitSpectrum::SinglePassAnalysis(double gapSize){

  int startA, endA;
  double mzStartA, mzEndA;

  int i=0;
	int j;

	CSpecAnalyze sa;
	sa.setParams(userParams);
	sa.setAveragine(averagine);
	sa.setMercury(mercury);

	goodPeaks.clear();
	delete s2n;
	s2n = new vector<float>;

  startA = -1;

  mzStartA = userParams.window.dLower;
  mzEndA = mzStartA + gapSize;

  while(true){
    
    if(i==wholeSpec->size()){
      endA=i-1;
      break;
    }

    if(wholeSpec->at(i).mz >= userParams.window.dUpper && userParams.window.dUpper>0){
      endA=i;
      break;
    }

    if(wholeSpec->at(i).mz >= mzStartA && startA == -1) startA=i;

		//Check the window
    if(wholeSpec->at(i).mz >= mzEndA) {
      endA=i;

			if(endA-startA>1){

				//Clear the CSpecAnalyze object and find the peaks
				sa.clear();
				if(userParams.chargeMode=='F' || 
					 userParams.chargeMode=='P' ||
					 userParams.chargeMode=='S'){
					sa.peaks = signalToNoise(*wholeSpec,startA,endA,(float)userParams.sn,&sa.S2NCutoff,true,true);
				} else {
					sa.peaks = signalToNoise(*wholeSpec,startA,endA,(float)userParams.sn,&sa.S2NCutoff);
				}
				sa.FindPeaks();

				for(j=0;j<sa.peaks.size();j++) {
					goodPeaks.peaks.add(sa.peaks.at(j));
					s2n->push_back(sa.S2NCutoff);
				}

			}

	    startA=-1;
		  endA=0;
			mzStartA = mzEndA + 0.000000001;
      mzEndA = mzEndA + gapSize;

    }

	}
	
	//Check last window
	if(endA-startA>1){

		//Clear the CSpecAnalyze object and find the peaks
		sa.clear();
		if(userParams.chargeMode=='F' || 
			 userParams.chargeMode=='P' ||
			 userParams.chargeMode=='S'){
			sa.peaks = signalToNoise(*wholeSpec,startA,endA,(float)userParams.sn,&sa.S2NCutoff,true,true);
		} else {
			sa.peaks = signalToNoise(*wholeSpec,startA,endA,(float)userParams.sn,&sa.S2NCutoff);
		}
		sa.FindPeaks();

		for(j=0;j<sa.peaks.size();j++) {
			goodPeaks.peaks.add(sa.peaks.at(j));
			s2n->push_back(sa.S2NCutoff);
		}

	}

}

/* 
	 This function sums (in m/z order) the A and B peak sets. At each peak, it records the
   signal to noise ratio. When there are two ratios, it keeps the lowest.
*/
void CSplitSpectrum::UnionAnalysis(){

	unsigned int aCount,apCount,bCount,bpCount;

	aCount=0;
	bCount=0;
	apCount=0;
	bpCount=0;

	goodPeaks.clear();
	delete s2n;
	s2n = new vector<float>;

	bool bSkip;

	while(aCount<setA->size() || bCount<setB->size()){

		//Keep iterating through sets until a peak is found
		while(true){

			//Check A set for next peak
			if(aCount<setA->size()){
				if(setA->at(aCount).peaks.size()==0 || apCount>=(unsigned int)setA->at(aCount).peaks.size()) {
					aCount++;
					apCount=0;
					continue;
				};
			};
			//Check B set for next peak
			if(bCount<setB->size()){
				if(setB->at(bCount).peaks.size()==0 || bpCount>=(unsigned int)setB->at(bCount).peaks.size()) {
					bCount++;
					bpCount=0;
					continue;
				};
			};
			break;
		};

		//Compare the peaks between sets. Either both have the same peak, or set A or set B
		//has an extra peak.
		if(aCount<setA->size() && bCount<setB->size()){
			if(setA->at(aCount).peaks.at(apCount).mz == setB->at(bCount).peaks.at(bpCount).mz){

				//check if within user specified bounds
				bSkip=false;
				if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
					if(setA->at(aCount).peaks.at(apCount).mz < userParams.window.dLower || setA->at(aCount).peaks.at(apCount).mz > userParams.window.dUpper) {
						bSkip=true;
					};
				};

				if(!bSkip){
					goodPeaks.peaks.add(setA->at(aCount).peaks.at(apCount));
					if(setA->at(aCount).S2NCutoff<=setB->at(bCount).S2NCutoff) {
						s2n->push_back(setA->at(aCount).S2NCutoff);
					} else {
						s2n->push_back(setB->at(bCount).S2NCutoff);
					};
					if(userParams.chargeMode=='F' || 
						 userParams.chargeMode=='P' ||
						 userParams.chargeMode=='S'){
						aIndex->push_back(aCount);
						bIndex->push_back(bCount);
					};
				};
				apCount++;
				bpCount++;

			} else if(setA->at(aCount).peaks.at(apCount).mz < setB->at(bCount).peaks.at(bpCount).mz) {

				//check if within user specified bounds
				bSkip=false;
				if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
					if(setA->at(aCount).peaks.at(apCount).mz < userParams.window.dLower || setA->at(aCount).peaks.at(apCount).mz > userParams.window.dUpper) {
						bSkip=true;
					};
				};

				if(!bSkip){
					goodPeaks.peaks.add(setA->at(aCount).peaks.at(apCount));
					s2n->push_back(setA->at(aCount).S2NCutoff);
					if(userParams.chargeMode=='F' || 
						 userParams.chargeMode=='P' ||
						 userParams.chargeMode=='S'){
						aIndex->push_back(aCount);
						bIndex->push_back(-1);
					};
				};
				apCount++;

			} else {

				//check if within user specified bounds
				bSkip=false;
				if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
					if(setB->at(bCount).peaks.at(bpCount).mz < userParams.window.dLower || setB->at(bCount).peaks.at(bpCount).mz > userParams.window.dUpper) {
						bSkip=true;
					};
				};

				if(!bSkip){
					goodPeaks.peaks.add(setB->at(bCount).peaks.at(bpCount));
					s2n->push_back(setB->at(bCount).S2NCutoff);
					if(userParams.chargeMode=='F' || 
						 userParams.chargeMode=='P' ||
						 userParams.chargeMode=='S'){
						aIndex->push_back(-1);
						bIndex->push_back(bCount);
					};
				};
				bpCount++;

			};
			continue;
		};

		//Add any leftover peaks from set A
		if(aCount<setA->size()){

			//check if within user specified bounds
			bSkip=false;
			if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
				if(setA->at(aCount).peaks.at(apCount).mz < userParams.window.dLower || setA->at(aCount).peaks.at(apCount).mz > userParams.window.dUpper) {
					bSkip=true;
				};
			};
		
			if(!bSkip){
				goodPeaks.peaks.add(setA->at(aCount).peaks.at(apCount));
				s2n->push_back(setA->at(aCount).S2NCutoff);
				if(userParams.chargeMode=='F' || 
					 userParams.chargeMode=='P' ||
					 userParams.chargeMode=='S'){
					aIndex->push_back(aCount);
					bIndex->push_back(-1);
				};
			};
			apCount++;

		};

		//Add any leftover peaks from set B
		if(bCount<setB->size()){

			//check if within user specified bounds
			bSkip=false;
			if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
				if(setB->at(bCount).peaks.at(bpCount).mz < userParams.window.dLower || setB->at(bCount).peaks.at(bpCount).mz > userParams.window.dUpper) {
					bSkip=true;
				};
			};

			if(!bSkip){
				goodPeaks.peaks.add(setB->at(bCount).peaks.at(bpCount));
				s2n->push_back(setB->at(bCount).S2NCutoff);
				if(userParams.chargeMode=='F' || 
					 userParams.chargeMode=='P' ||
					 userParams.chargeMode=='S'){
					aIndex->push_back(-1);
					bIndex->push_back(bCount);
				};
			};
			bpCount++;

		};

	};

};

/* 
	 This function is similar to UnionAnalysis, but differs in that it only accepts peaks
	 when found in both sets. The lowest signal to noise cutoff is kept.
*/
void CSplitSpectrum::IntersectionAnalysis(){
	unsigned int aCount,apCount,bCount,bpCount;

	aCount=0;
	bCount=0;
	apCount=0;
	bpCount=0;

	goodPeaks.clear();
	delete s2n;
	s2n = new vector<float>;

	bool bSkip;

	while(aCount<setA->size() && bCount<setB->size()){

		//Keep iterating through sets until a peak is found
		while(true){
			//Check set A
			if(aCount<setA->size()){
				if(setA->at(aCount).peaks.size()==0 || apCount>=(unsigned int)setA->at(aCount).peaks.size()) {
					aCount++;
					apCount=0;
					continue;
				}
			}
			//Check set B
			if(bCount<setB->size()){
				if(setB->at(bCount).peaks.size()==0 || bpCount>=(unsigned int)setB->at(bCount).peaks.size()) {
					bCount++;
					bpCount=0;
					continue;
				}
			}
			break;
		}

		//Keep peaks found in both sets only.
		if(aCount<setA->size() && bCount<setB->size()){
			if(setA->at(aCount).peaks.at(apCount).mz == setB->at(bCount).peaks.at(bpCount).mz){
				
				//check if within user specified bounds
				bSkip=false;
				if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
					if(setA->at(aCount).peaks.at(apCount).mz < userParams.window.dLower || setA->at(aCount).peaks.at(apCount).mz > userParams.window.dUpper) {
						bSkip=true;
					}
				}

				if(!bSkip){
					goodPeaks.peaks.add(setA->at(aCount).peaks.at(apCount));
					if(setA->at(aCount).S2NCutoff<=setB->at(bCount).S2NCutoff) {
						s2n->push_back(setA->at(aCount).S2NCutoff);
					} else {
						s2n->push_back(setB->at(bCount).S2NCutoff);
					}
					if(userParams.chargeMode=='F' || 
						 userParams.chargeMode=='P' ||
						 userParams.chargeMode=='S'){
						aIndex->push_back(aCount);
						bIndex->push_back(bCount);
					}
				}

				apCount++;
				bpCount++;
			} else if(setA->at(aCount).peaks.at(apCount).mz < setB->at(bCount).peaks.at(bpCount).mz) {
				apCount++;
			} else {
				bpCount++;
			}
		}
	}

}

/* 
	 This funciton scans through all the good peaks and divides them into sets with a maximum
	 distance between the first and last peak of winSize. When winSize is exceeded, the set
	 of peaks is cut between the two peaks that have a maximum distance from each other.
*/
void CSplitSpectrum::MakeAnalysis(double winSize){
	int i,j;
	CSpecAnalyze sa;

	double startMZ;
	double maxDif;
	int peakCount;
	int cutPoint;
	int startPoint;
	float lowS2N;

	sa.setAveragine(averagine);
	sa.setMercury(mercury);

  //For optimum window splitting
  vector<sSplit> split;
  sSplit tmpSplit;
  //double tmpDouble;
  float lowIntensity;

	//For testing purposes:
	/*
	cout << "Winsize: " << winSize << endl;
	for(i=0;i<goodPeaks.peaks.size();i++){
		cout << goodPeaks.peaks.at(i).mz << "  " << goodPeaks.peaks.at(i).intensity << endl;
	};
	*/

	cutPoint=0;
	peakCount=0;
	while(peakCount<goodPeaks.peaks.size()-1){
		startPoint=peakCount;
		startMZ=goodPeaks.peaks.at(peakCount).mz;
		maxDif=0.0;

		//Continue while we are less than winSize and there are still peaks
		while( ((goodPeaks.peaks.at(peakCount).mz - startMZ) < winSize) && (peakCount<goodPeaks.peaks.size()-1) && (maxDif<1.05) ) {
			peakCount++;
			
			//keep track of the maximum distance between consecutive peaks in this window
			if( (goodPeaks.peaks.at(peakCount).mz - goodPeaks.peaks.at(peakCount-1).mz) > maxDif){
				cutPoint = peakCount-1;
				maxDif = goodPeaks.peaks.at(peakCount).mz - goodPeaks.peaks.at(peakCount-1).mz;
			}
		}

    //cout << "The CutPoint: " << goodPeaks.peaks[cutPoint].mz << "  " << maxDif << endl;

    //if the cutpoint might sever a valid distribution, find a less damaging
    //location to cut the data.
    if(maxDif<0.525) {
      split.clear();
      for(i=startPoint;i<=peakCount;i++){
        tmpSplit.index=i;
        tmpSplit.mz=goodPeaks.peaks.at(i).mz;
        tmpSplit.intensity=goodPeaks.peaks.at(i).intensity;
        split.push_back(tmpSplit);
        //cout << tmpSplit.mz << "," << tmpSplit.intensity << endl;
      }

      //cout << "Would be CutPoint: " << goodPeaks.peaks[cutPoint].mz << "  " << maxDif << endl;
      
      while(maxDif<0.53) { 

        //remove the least intensity peak;
        lowIntensity=9999999999999.0f;
        for(i=1;i<(int)split.size();i++){
          if(split[i].intensity<lowIntensity){
            lowIntensity=split[i].intensity;
            j=i;
          }
        }

        //Slow method - think of better approach
        //cout << "Erase: " << split[j].mz << "," << split[j].intensity << endl;
        split.erase(split.begin()+j);

        //if we trimmed the end of the window, stop now.
        if( (split[split.size()-1].mz-split[0].mz) < (winSize-1.03)){
          maxDif=666;
          cutPoint=split[split.size()-1].index;
          break;
        }
        

        //compute the new max difference between peaks
        maxDif=0.0;
        for(i=1;i<(int)split.size();i++){
          if( (split[i].mz-split[i-1].mz) > maxDif){
				    maxDif = split[i].mz-split[i-1].mz;
            cutPoint = split[i-1].index;
          }
			  }

        //cout << "New Best dif: " << maxDif << " - " << cutPoint << "," << goodPeaks.peaks[cutPoint].mz << endl;

      }

    }

		/* For checking which peaks appear in a window
		cout << "For: " << goodPeaks.peaks[startPoint].mz << " - " << goodPeaks.peaks[peakCount].mz << endl;
		for(i=startPoint;i<=peakCount;i++){
			cout << "  Peak: " << goodPeaks.peaks[i].mz << " " << goodPeaks.peaks[i].intensity << endl;
		}
    cout << "CutPoint: " << goodPeaks.peaks[cutPoint].mz << "  " << maxDif << endl;
    */

    //I think this sanity check is for when the distance to the last peak is less than the window size.
		//if(goodPeaks.peaks.at(peakCount).mz - startMZ < winSize) cutPoint=goodPeaks.peaks.size()-1;
    if(peakCount==goodPeaks.peaks.size()-1) cutPoint=goodPeaks.peaks.size()-1;

		//Create our optimal window that is less than winsize
		if(startPoint!=cutPoint) {
			sa.clear();
			lowS2N=100;

			//create peaks and find lowest signal to noise cutoff across those peaks
			for(i=startPoint;i<=cutPoint;i++){
				sa.peaks.add(goodPeaks.peaks.at(i));
				if(s2n->at(i)<lowS2N) lowS2N=s2n->at(i);
			}
			sa.S2NCutoff=lowS2N;

			//For checking what the cut window is
			//cout << "  Cut point: " << goodPeaks.peaks.at(startPoint).mz << " - " << goodPeaks.peaks.at(cutPoint).mz << endl;
			
			//Find charges (FFT & Patterson ONLY)
			/*
			if(userParams.chargeMode=='F' || 
				 userParams.chargeMode=='P' ||
				 userParams.chargeMode=='S') {

					 cout << "hehe: " << startPoint << " " << cutPoint << endl;
					 cout << aIndex->size() << endl;
					 cout << setA->size() << endl;

				//Iterate through all peak indeces in this analysis
				for(i=startPoint;i<=cutPoint;i++){

					//only add charges from setA if it contributed to the peak
					if(aIndex->at(i) != -1) {

						for(k=0;k<(int)setA->at(aIndex->at(i)).charges->size();k++){
							bFound=false;
							for(j=0;j<(int)sa.charges->size();j++){
								if(sa.charges->at(j) == setA->at(aIndex->at(i)).charges->at(k)){
									bFound=true;
									break;
								}
							}
							if(!bFound) {
								sa.charges->push_back(setA->at(aIndex->at(i)).charges->at(k));
							}
						}
					}

					//only add charges from SetB if it contributed to the peak
					if(bIndex->at(i) != -1) {
						for(k=0;k<(int)setB->at(bIndex->at(i)).charges->size();k++){
							bFound=false;
							for(j=0;j<(int)sa.charges->size();j++){
								if(sa.charges->at(j) == setB->at(bIndex->at(i)).charges->at(k)){
									bFound=true;
									break;
								}
							}
							if(!bFound) {
								sa.charges->push_back(setB->at(bIndex->at(i)).charges->at(k));
							}
						}
					}

				}//i
			
			}//end charge mode block
			*/

			sa.setSpectrum(*wholeSpec);
			finalAnalysis->push_back(sa);

		}
		peakCount=cutPoint+1;
	}

}

CSpecAnalyze CSplitSpectrum::getWindow(int index){
	return finalAnalysis->at(index);
}

void CSplitSpectrum::SetAveragine(CAveragine *a){
	averagine=a;
	goodPeaks.setAveragine(averagine);
}

void CSplitSpectrum::SetMercury(CMercury8 *m){
	mercury=m;
	goodPeaks.setMercury(mercury);
}

void CSplitSpectrum::Centroid(Spectrum& s){
	goodPeaks.clear();
	goodPeaks.setParams(userParams);
	delete s2n;
	s2n = new vector<float>;
	for(int i=0;i<s.size();i++){
		goodPeaks.peaks.add(s.at(i));
		s2n->push_back(0);
	}
}

//This funciton is for calculating S/N thresholds when using Noise reduced data,
//such as data found on Thermo FT or Orbitrap instruments
//Make only one pass and set the noise threshold at the lowest point
void CSplitSpectrum::NewSNPass(double gapSize){
	int startA, endA;
	float S2NCutoff;
  double mzStartA, mzEndA;
	double mz=0.0;
	double maxIntensity=0.0;
	double max;
	double lowPoint=99999999999;

  unsigned int i=0;

	CSpecAnalyze sa;
	sa.setParams(userParams);
	sa.setAveragine(averagine);
	sa.setMercury(mercury);

	delete setA;
	setA = new vector<CSpecAnalyze>;

  startA = -1;

  mzStartA = wholeSpec->at(0).mz;
  mzEndA = mzStartA + gapSize;

  while(true){
    
    if(i==wholeSpec->size()){
      endA=i-1;
      break;
    }

		mz = wholeSpec->at(i).mz;

    if(mz >= mzStartA && startA == -1) startA=i;

    if(mz >= mzEndA) {
      endA=i;

			if(endA-startA>1){

				S2NCutoff = findSNCutoff2(*wholeSpec,startA,endA,(float)userParams.sn,max);
				
				if( (S2NCutoff/100*max) < lowPoint ) lowPoint = S2NCutoff/100*max;
				if( max > maxIntensity ) maxIntensity = max;

			}

			startA=-1;
		  endA=0;
			mzStartA = mzEndA + 0.000000001;
      mzEndA = mzEndA + gapSize;

		}

    i++;  

  }

	sa.clear();
	sa.S2NCutoff = (float)(lowPoint/maxIntensity*100);
	sa.FindPeaks(*wholeSpec,0,wholeSpec->size()-1);

	if(userParams.sn>0)	sa.removePeaksBelowSN();

	goodPeaks.clear();
	delete s2n;
	s2n = new vector<float>;
	for(i=0;i<(unsigned int)sa.peaks.size();i++){
		if(userParams.window.dLower>0 || userParams.window.dUpper > 0){
			if(sa.peaks.at(i).mz < userParams.window.dLower || sa.peaks.at(i).mz > userParams.window.dUpper) continue;
		}
		goodPeaks.peaks.add(sa.peaks.at(i));
		s2n->push_back(sa.S2NCutoff);
	}

}
