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

#include "CHardklor2.h"

#include <corecrt_io.h>

using namespace std;
using namespace MSToolkit;

CHardklor2::CHardklor2(CAveragine *a, CMercury8 *m, CModelLibrary *lib){
  averagine=a;
  mercury=m;
	models=lib;
	bEcho=true;
  bMem=false;
	PT=NULL;
}

CHardklor2::~CHardklor2(){
	averagine=NULL;
	mercury=NULL;
	models=NULL;
	if(PT!=NULL) {
		PT=NULL;
	}
}

hkMem& CHardklor2::operator[](const int& index){
  return vResults[index];
}

void CHardklor2::Echo(bool b){
	bEcho=b;
}

int CHardklor2::GoHardklor(CHardklorSetting sett, Spectrum* s){
	
	//Member variables
	MSReader r;
	Spectrum curSpec,c;
	vector<int> v;
	FILE* fout=NULL;
	int TotalScans;
	int manyPep, zeroPep, lowSigPep;
	int iPercent;
	int minutes, seconds;
	int i;
	vector<pepHit> vPeps;

	//initialize variables
	cs=sett;
	loadTime=0;
	analysisTime=0;
	TotalScans=0;
	manyPep=0;
  zeroPep=0;
  lowSigPep=0;
  iPercent=0;
	getTimerFrequency(timerFrequency);

	if (cs.reportAveragineAndMassOffset)
	    bShowPerformanceHints = false; // Skyline doesn't want the performance suggestions


  vResults.clear();

	//For noise reduction
	CNoiseReduction nr(&r,cs);

	//Signature
	//if(bEcho) cout << "\n\nHardklor, v2.06, Mike Hoopmann, Mike MacCoss\nCopyright 2007-2012\nUniversity of Washington\n" << endl;

	//Set the periodic table
	if(PT==NULL) PT=averagine->getPT();

	//Ouput file info to user
	if(bEcho){
		if(s==NULL) cout << "Reading from file: " << cs.inFile << endl;
		if(!bMem) cout << "Writing to file: " << cs.outFile << endl;
	}
  if(cs.fileFormat==dunno) {
    cout << "Unknown file format or bad extension." << endl;
    return -1;
  }

  if (!bMem) fout = fopen(cs.outFile.c_str(), "wt");

	//read a spectrum
	getExactTime(startTime);

	  //Read in the initial spectrum
  r.setFilter(cs.mzXMLFilter);
  r.setRawFilter(cs.rawFilter);
  if(s!=NULL){
    curSpec=*s;
  } else {
	  if(cs.boxcar==0){
      if((cs.scan.iLower>0) && (cs.scan.iLower==cs.scan.iUpper)) r.readFile(&cs.inFile[0],curSpec,cs.scan.iLower);
      else if(cs.scan.iLower>0) r.readFile(&cs.inFile[0],curSpec,cs.scan.iLower);
	    else r.readFile(&cs.inFile[0],curSpec);
	  } else {
		  if(cs.boxcarFilter==0){
        if(!nr.DeNoiseD(curSpec)) curSpec.setScanNumber(0);
			  //if(!nr.DeNoise(curSpec)) curSpec.setScanNumber(0);
			  //do something about this...

		  } else {
        if(!nr.DeNoiseC(curSpec)) curSpec.setScanNumber(0);
		  }
    }
  }

	getExactTime(stopTime);
  tmpTime1=toMicroSec(stopTime);
  tmpTime2=toMicroSec(startTime);
  loadTime+=(tmpTime1-tmpTime2);

	//Check that file was read
  if(curSpec.getScanNumber()==0) {
    if(s!=NULL) {
      cout << "Spectrum is invalid." << endl;
      return -2;
    }
    if(cs.scan.iLower>0) cout << cs.inFile << " is invalid, or requested scan number is of incorrect format." << endl;
    else cout << cs.inFile << " is invalid, or contains no spectrum." << endl;
    return -2;
  }

	//Write scan information to output file.
  if(!bMem){
    if (cs.reducedOutput) WriteScanLine(curSpec, fout, 2);
    else if (cs.xml) WriteScanLine(curSpec, fout, 1);
    else WriteScanLine(curSpec, fout, 0);
  } else {
    currentScanNumber = curSpec.getScanNumber();
  }

	//Output progress indicator
	if(bEcho) cout << iPercent;
  
  //While there is still data to read in the file.
  while(true){

		getExactTime(startTime);
		TotalScans++;
		
		//Smooth if requested
		if(cs.smooth>0) SG_Smooth(curSpec,cs.smooth,4);

		//Centroid if needed; notice that this copy wastes a bit of time.
		//TODO: make this more efficient
		if(cs.boxcar==0 && !cs.centroid) Centroid(curSpec,c);
		else c=curSpec;

		//There is a bug when using noise reduction that results in out of order m/z values
		//TODO: fix noise reduction so sorting isn't needed
		if(c.size()>0) c.sortMZ();

		//Analyze
		QuickHardklor(c,vPeps);

		//export results
		for(i=0;i<(int)vPeps.size();i++){
      if(!bMem){
			  if(cs.reducedOutput) WritePepLine(vPeps[i],c,fout,2);
			  else if(cs.xml) WritePepLine(vPeps[i],c,fout,1);
			  else WritePepLine(vPeps[i],c,fout,0);
      } else {
        ResultToMem(vPeps[i],c);
      }
		}

		//Update progress
		if(bEcho){
			if (r.getPercent() > iPercent){
				if (_isatty(_fileno(stdout)))
				{
					if(iPercent<10) cout << "\b";
					else cout << "\b\b";
					cout.flush();
				}
				iPercent=r.getPercent();
				cout << iPercent;
				cout.flush();
			}
		}

		getExactTime(stopTime);
    tmpTime1=toMicroSec(stopTime);
    tmpTime2=toMicroSec(startTime);
    analysisTime+=tmpTime1-tmpTime2;
    
    if(s!=NULL) break;

		//Check if any user limits were made and met
		if( (cs.scan.iUpper == cs.scan.iLower) && (cs.scan.iLower != 0) ){
			break;
		} else if( (cs.scan.iLower < cs.scan.iUpper) && (curSpec.getScanNumber() >= cs.scan.iUpper) ){
			break;
		}

		//Read next spectrum from file.
		getExactTime(startTime);
		if(cs.boxcar==0) {
			r.readFile(NULL,curSpec);
		} else {
			if(cs.boxcarFilter==0){
				//possible to not filter?
        nr.DeNoiseD(curSpec);
			} else {
			//case 5: nr.DeNoise(curSpec); break; //this is for filtering without boxcar
				nr.DeNoiseC(curSpec);
			}
		}

		getExactTime(stopTime);
		tmpTime1=toMicroSec(stopTime);
		tmpTime2=toMicroSec(startTime);
		loadTime+=(tmpTime1-tmpTime2);

		if(curSpec.getScanNumber()!=0){
			//Write scan information to output file.
			if(cs.reducedOutput){
				WriteScanLine(curSpec,fout,2);
			} else if(cs.xml) {
				fprintf(fout,"</Spectrum>\n");
				WriteScanLine(curSpec,fout,1);
			} else {
				WriteScanLine(curSpec,fout,0);
			}
		} else {
			break;
		}
	}

	if(!bMem) fclose(fout);

	if(bEcho) {
		cout << "\n" << endl;
		cout << "  Total number of scans analyzed: " << TotalScans << endl;

		i=(int)timeToSec(loadTime,timerFrequency);
		minutes = (int)(i/60);
		seconds = i - (60*minutes);
		cout << "\nFile access time: " << minutes << " minutes, " << seconds << " seconds." << endl;
		i=(int)timeToSec(analysisTime,timerFrequency);
		minutes = (int)(i/60);
		seconds = i - (60*minutes);
		cout << "Analysis Time:    " << minutes << " minutes, " << seconds << " seconds." << endl;
		if (bShowPerformanceHints)
		{
			if (minutes==0 && seconds==0){
				cout << "IMPOSSIBLE!!!" << endl;
			} else if(minutes <=2){
				cout << "HOLY FRIJOLE!!" << endl;
			} else if(minutes<=5) {
				cout << "Like lightning!" << endl;
			} else if(minutes<=10){
				cout << "That's pretty damn fast!" << endl;
			} else if(minutes<=20){
				cout << "Monkeys calculate faster than that!" << endl;
			} else if(minutes<=30){
				cout << "You should have taken a lunch break." << endl;
			} else if(minutes<=40){
				cout << "Oi! Too freakin' slow!!" << endl;
			} else {
				cout << "You might be able to eek out some better performance by adjusting your parameters." << endl;
			}
		}
	}
	return 1;

}

int CHardklor2::BinarySearch(Spectrum& s, double mz, bool floor){

	int mid=s.size()/2;
	int upper=s.size();
	int lower=0;

	if(mz>=s[s.size()-1].mz) return s.size()-1;
	if(mz<=s[0].mz) return 0;

	while(s[mid].mz!=mz){
		if(lower>=upper) break;
		if(s[mid].mz>mz){
			upper=mid-1;
			mid=(lower+upper)/2;
		} else {
			lower=mid+1;
			mid=(lower+upper)/2;
		}
	}

	if(floor && s[mid].mz>mz && mid>0) return mid-1;
	else if(!floor && s[mid].mz<mz && mid<s.size()) return mid+1;
	else return mid;

}

//Calculates the resolution (FWHM) of a peak
double CHardklor2::CalcFWHM(double mz,double res,int iType){
	double deltaM;
	switch(iType){
	case 0: //Orbitrap
		deltaM = mz * sqrt(mz) / (20*res);  //sqare root of 400
		break;
	case 1: //TOF
		deltaM = mz / res;
		break;
	case 2: //QIT
		deltaM = res / 5000.0;
		break;
	case 3: //FTICR
	default:
		deltaM = mz * mz / (400*res);
		break;
	}
	return deltaM;
}

//First derivative method, returns base peak intensity of the set
void CHardklor2::Centroid(Spectrum& s, Spectrum& out){
  int i,j;
  float maxIntensity;
  int bestPeak;
  bool bLastPos;

	int nextBest;
	double FWHM;
	Peak_T centroid;

	out.clear();

  //Get boundaries of the spectrum. centroids must be within boundaries.
  double minMZ, maxMZ;
  if(s.size()>0){
    minMZ = s[0].mz;
    maxMZ = s[s.size()-1].mz;
  }

  bLastPos=false;
	for(i=0;i<s.size()-1;i++){

    if(s[i].intensity<s[i+1].intensity) {
      bLastPos=true;
      continue;
    } else {
      if(bLastPos){
				bLastPos=false;

				//find max and add peak
				maxIntensity=0;
				for(j=i;j<i+1;j++){
				  if (s[j].intensity>maxIntensity){
				    maxIntensity=s[j].intensity;
				    bestPeak = j;
				  }
				}

				//Best estimate of Gaussian centroid
				//Get 2nd highest point of peak
				if(bestPeak==s.size()){
					nextBest=bestPeak-1;
				} else if(s[bestPeak-1].intensity > s[bestPeak+1].intensity){
					nextBest=bestPeak-1;
				} else {
					nextBest=bestPeak+1;
				}

				//Get FWHM
				FWHM = CalcFWHM(s[bestPeak].mz,cs.res400,cs.msType);

				//Calc centroid MZ (in three lines for easy reading)
				centroid.mz = (FWHM*FWHM*log(s[bestPeak].intensity/s[nextBest].intensity));
				centroid.mz /= (GAUSSCONST*(s[bestPeak].mz-s[nextBest].mz));
				centroid.mz += ((s[bestPeak].mz+s[nextBest].mz)/2);

				//Calc centroid intensity
				centroid.intensity=(float)(s[bestPeak].intensity/exp(-pow((s[bestPeak].mz-centroid.mz)/FWHM,2)*GAUSSCONST));

				//some peaks are funny shaped and have bad gaussian fit.
				//if error is more than 10%, keep existing intensity
				if( fabs((s[bestPeak].intensity - centroid.intensity) / centroid.intensity * 100) > 10 ||
            //not a good check for infinity
            centroid.intensity>999999999999.9 ||
            centroid.intensity < 0 ) {
					centroid.intensity=s[bestPeak].intensity;
				}
				
				//Centroided peaks must fall within spectrum mass range
				if(centroid.mz<minMZ || centroid.mz>maxMZ) {
					//do nothing if invalid mz, but perhaps find a better way to handle this one day.
				} else {
					out.add(centroid);
				}
			
      }

    }
  }

}

//returns whether or not the peak is still valid. true if peak still exists, false if peak was solved already.
bool CHardklor2::CheckForPeak(vector<Result>& vMR, Spectrum& s, int index){
	double dif=100.0;
	double massDif;
	bool match=false;

	int mid=s.size()/2;
	int upper=s.size();
	int lower=0;

	double FWHM=CalcFWHM(vMR[index].mass,cs.res400,cs.msType);

	if(vMR[index].mass>=s[s.size()-1].mz) {
		mid=s.size()-1;
	} else if(vMR[index].mass<=s[0].mz) {
		mid=0;
	} else {

		while(s[mid].mz!=vMR[index].mass){
			if(lower>=upper) break;
			if(s[mid].mz>vMR[index].mass){
				upper=mid-1;
				mid=(lower+upper)/2;
			} else {
				lower=mid+1;
				mid=(lower+upper)/2;
			}
		}
	}

	dif=fabs(s[mid].mz-vMR[index].mass);
	if(mid>0){
		massDif=fabs(s[mid-1].mz-vMR[index].mass);
		if(massDif<dif) {
			dif=massDif;
			mid--;
		}
	} 
	if(mid<s.size()-1){
		massDif=fabs(s[mid+1].mz-vMR[index].mass);
		if(massDif<dif) {
			dif=massDif;
			mid++;
		}
	}

	if(dif<FWHM){
		if(mask[mid].intensity>0.1) return false;
		else return true;
	}
	return false;

}

int CHardklor2::CompareBPI(const void *p1, const void *p2){
  const pepHit d1 = *(pepHit *)p1;
  const pepHit d2 = *(pepHit *)p2;
	if(d1.basePeakIndex<d2.basePeakIndex) return -1;
	else if(d1.basePeakIndex>d2.basePeakIndex) return 1;
  else return 0;
}

double CHardklor2::LinReg(vector<float>& mer, vector<float>& obs){

  int i,sz;
  double sxx=0,syy=0,sxy=0;

  //Cosine angle correlation
  sxy=0;
  sxx=0;
  syy=0;
	sz=(int)mer.size();
  for(i=0;i<sz;i++){
    sxy += (mer[i]*obs[i]);
    sxx += (mer[i]*mer[i]);
    syy += (obs[i]*obs[i]);
  }

  if(sxx>0 && syy>0 && sxy>0) return sxy/sqrt(sxx*syy);
  else return 0;
    
}

bool CHardklor2::MatchSubSpectrum(Spectrum& s, int peakIndex, pepHit& pep){

	int i,k,n;
	size_t varCount;
	size_t v;
	float max=s[peakIndex].intensity;
	int maxMercuryIndex[3];
	vector<int> charges;
	double dif;
	vector<float> obs;
	vector<float> mer;
	vector<int> vMatchIndex;
	vector<float> vMatchPeak;
	vector<Result> vMR;
	Result r;
	double corr;
	double da;

	//keep track of best hits
	double bestCorr;
	double bestDA;
	int bestCharge;
	double bestMass;
	double bestZeroMass;  // Skyline wants formula for isotope envelope, needs mass offset too
	const char* bestAveragine = NULL; // Skyline wants formula for isotope envelope
	vector<int> bestMatchIndex;
	vector<float> bestMatchPeak;
	int matchCount;
	int bestMatchCount;
	int thisMaxIndex=0;
	int bestVariant;

	mercuryModel* model=NULL;

	double deltaM = CalcFWHM(s[peakIndex].mz,cs.res400,cs.msType);
	QuickCharge(s,peakIndex,charges);

	bestCorr=0.0;
	bestMatchCount=0;

	//Mark number of variants to analyze
	if(cs.noBase) varCount=cs.variant->size();
	else varCount=cs.variant->size()+1;

	//iterate through all charge states
	for(i=0;i<(int)charges.size();i++){

		for(v=0;v<varCount;v++){

			//get model from library
			dif=0;
			model=models->getModel(charges[i],(int)v,s[peakIndex].mz);
			for(k=0; k<model->size; k++) {
				if(model->peaks[k].intensity>dif){
					dif = model->peaks[k].intensity;
					maxMercuryIndex[0]=k;
				}
			}
			if(k==0) maxMercuryIndex[1]=-1;
			else maxMercuryIndex[1]=maxMercuryIndex[0]-1;		//allow right shift
			maxMercuryIndex[2]=maxMercuryIndex[0]+1;				//allow left shift

			//Apply shift and find mz boundaries
			n=0;
			while(n<3){
				
				if(maxMercuryIndex[n]<0) {
					n++;
					continue;
				}

				//Align the mercury distribution (MD) to the observed peak. The MD can shift in
				//either direction for one peak to adjust for system noise. The width of the MD
				//determines the boundaries for correlation to the observed data.
				double lower=5000.0;
				double upper=0.0;
				double shft = s[peakIndex].mz - model->peaks[maxMercuryIndex[n]].mz;
				
				vMR.clear();
				for(k=0; k<model->size; k++) {				
					r.data=model->peaks[k].intensity;
					r.mass=model->peaks[k].mz+shft;
					vMR.push_back(r);
					if(model->peaks[k].intensity>99.999) thisMaxIndex=(int)vMR.size()-1;
					
					if(r.mass<lower) lower=r.mass;
					if(r.mass>upper) upper=r.mass;
				}
				da=model->area;

				//Add a little buffer to the bounds
				lower-=0.1;
				upper+=0.1;

				//Match predictions to the observed peaks and record them in the proper array.
				corr=PeakMatcherB(vMR,s,lower,upper,deltaM/2,peakIndex,matchCount,vMatchIndex,vMatchPeak);
				//cout << "\tMSS: " << s[peakIndex].mz << " " << s[peakIndex].intensity << "\t" << charges[i] << "\t" << matchCount << "\t" << corr << endl;

				if(corr>bestCorr || (corr>cs.corr && corr+0.025*(matchCount-bestMatchCount)>bestCorr) ){
					bestMatchIndex=vMatchIndex;
					bestMatchPeak=vMatchPeak;
					bestMatchCount=matchCount;
					bestCorr=corr;
					bestMass=model->zeroMass+shft*charges[i];
					bestCharge=charges[i];
					bestAveragine = model->averagine;
					bestZeroMass = model->zeroMass;
					bestDA=da;
					bestVariant=(int)v;
				}

				n++;

			}//while

		}//for v (variant)

	}//for i (charge)
	model=NULL;

	//if above threshold, erase peaks.
	if(bestCorr>cs.corr){

		pep.area=(float)bestDA;
		pep.basePeakIndex=0;
		pep.charge=bestCharge;
		pep.corr=bestCorr;
		pep.highMZ=0;
		pep.lowMZ=0;
		pep.massShift=0;
		pep.monoMass=bestMass;
		pep.intensity=s[peakIndex].intensity;
		pep.variantIndex=bestVariant;
		pep.zeroMass = bestZeroMass;
		memmove(pep.averagine, bestAveragine, AV_FORMULA_BUFFER_LENGTH);
		pep.isotopeEvidencePeaksCount = static_cast<int>(bestMatchIndex.size());

		//mark which peaks contributed to this analysis
		for(k=0;k<(int)bestMatchIndex.size();k++){
			if(bestMatchPeak[k]*max/s[bestMatchIndex[k]].intensity>0.5) s[bestMatchIndex[k]].intensity=-s[bestMatchIndex[k]].intensity;
			else s[bestMatchIndex[k]].intensity-=bestMatchPeak[k]*max;
		}

		return true;
	}

	return false;

}

double CHardklor2::PeakMatcher(vector<Result>& vMR, Spectrum& s, double lower, double upper, double deltaM, int matchIndex, int& matchCount, int& indexOverlap, vector<int>& vMatchIndex, vector<float>& vMatchIntensity){

	vMatchIndex.clear();
	vMatchIntensity.clear();

	vector<float> obs;
	vector<float> mer;
	obs.reserve(vMR.size());
	mer.reserve(vMR.size());

	bool match;
	bool bMax=false;
	double corr=0.0;
	double dif;
	double massDif;

	matchCount=0;
	indexOverlap=-1;

	int j,k;
	for(k=0;k<(int)vMR.size();k++) {
		if(vMR[k].data>99.9) bMax=true;

		match=false;
		dif=deltaM;
						
		//look left
		j=matchIndex;
		while(j>-1 && s[j].mz>=lower){
			massDif=s[j].mz-vMR[k].mass;
			if(massDif<-deltaM) break;
			if(fabs(massDif)<dif){
				dif=fabs(massDif);
				match=true;
				matchIndex=j;
			}
			j--;
		}

		//look right
		j=matchIndex+1;
		while(j<s.size() && s[j].mz<=upper){
			massDif=s[j].mz-vMR[k].mass;
			if(massDif>deltaM) break;
			if(fabs(massDif)<dif){
				dif=fabs(massDif);
				match=true;
				matchIndex=j;
			}
			j++;
		}
	
		if(!match) {
      //if expected peak is significant (above 50 rel abun) and has no match, match it to 0.
      if(vMR[k].data>50.0) {
        //cout << "xM: " << vMR[k].mass << "\t0" << endl;
        mer.push_back((float)vMR[k].data);
        obs.push_back(0.0f);
        if(bMax) break;
      }
      
		} else {
			mer.push_back((float)vMR[k].data);
      //cout << "xM: " << vMR[k].mass << "\t" << s[matchIndex].mz << endl;
			if(mask[matchIndex].intensity>0.1 && vMR[k].data>50) {
				if(indexOverlap<0) indexOverlap=matchIndex;
			}
			if(s[matchIndex].intensity<0.1) {
				obs.push_back(0.0f);
			} else {
				matchCount++;
				obs.push_back(s[matchIndex].intensity);
			}
			vMatchIndex.push_back(matchIndex);
			vMatchIntensity.push_back((float)vMR[k].data/100.0f);
		}
	}

	if(matchCount<2) corr=0.0;
	else corr=LinReg(mer,obs);

	//for(j=0;j<mer.size();j++){
  //  cout << "M:" << mer[j] << "\t" << "O:" << obs[j] << endl;
	//}
	//cout << "Corr: " << corr << "(" << matchCount << ")" << endl;

  //remove last matched peaks (possibly overlap with other peaks) but only if they are of low abundance.
	int tmpCount=matchCount;
  while(corr<0.90 && matchCount>2 && mer[mer.size()-1]<50.0){
		mer.pop_back();
		obs.pop_back();
		matchCount--;
		double corr2=LinReg(mer,obs);
		//cout << "Old corr: " << corr << "(" << matchCount+1 << ")" << " New corr: " << corr2 << endl;
		if(corr2>corr) {
			corr=corr2;
			tmpCount=matchCount;
		}
	}
	matchCount=tmpCount;

	return corr;
}

double CHardklor2::PeakMatcherB(vector<Result>& vMR, Spectrum& s, double lower, double upper, double deltaM, int matchIndex, int& matchCount, vector<int>& vMatchIndex, vector<float>& vMatchIntensity){

	vMatchIndex.clear();
	vMatchIntensity.clear();

	vector<float> obs;
	vector<float> mer;

	obs.reserve(vMR.size());
	mer.reserve(vMR.size());

	bool match;
	bool bMax=false;
	double corr=0.0;
	double dif;
	double massDif;

	matchCount=0;

	int j,k;
	for(k=0;k<(int)vMR.size();k++) {
		if(vMR[k].data>99.9) bMax=true;
		else bMax=false;

		match=false;
		dif=deltaM;
						
		//look left
		j=matchIndex;
		while(j>-1 && s[j].mz>=lower){
			massDif=s[j].mz-vMR[k].mass;
			if(massDif<-deltaM) break;
			if(fabs(massDif)<dif){
				dif=fabs(massDif);
				match=true;
				matchIndex=j;
			}
			j--;
		}

		//look right
		j=matchIndex+1;
		while(j<s.size() && s[j].mz<=upper){
			massDif=s[j].mz-vMR[k].mass;
			if(massDif>deltaM) break;
			if(fabs(massDif)<dif){
				dif=fabs(massDif);
				match=true;
				matchIndex=j;
			}
			j++;
		}
	
		if(!match) {
			break;
		} else {
			mer.push_back((float)vMR[k].data);
			if(s[matchIndex].intensity<0.1) {
				obs.push_back(0.0f);
			} else {
				matchCount++;
				obs.push_back(s[matchIndex].intensity);
			}
			vMatchIndex.push_back(matchIndex);
			vMatchIntensity.push_back((float)vMR[k].data/100.0f);
		}
	}

	if(matchCount<2) corr=0.0;
	else corr=LinReg(mer,obs);

	int tmpCount=matchCount;
	while(corr<0.90 && matchCount>2){
		mer.pop_back();
		obs.pop_back();
		matchCount--;
		double corr2=LinReg(mer,obs);
		if(corr2>corr) {
			corr=corr2;
			tmpCount=matchCount;
		}
	}
	matchCount=tmpCount;

	return corr;
}

void CHardklor2::QuickCharge(Spectrum& s, int index, vector<int>& v){

	int i,j;
	double dif;
	double rawCh;
	double rawChR;
	int ch;
	int charge[1000];
	const Peak_T& indexPeak = s[index];
  float minIntensity=indexPeak.intensity/4;

	for(i=cs.minCharge;i<=cs.maxCharge;i++) charge[i]=0;

	//check forward
	for(j=index+1;j<s.size();j++){
		if(s[j].intensity<minIntensity) continue;
			
		dif = s[j].mz - indexPeak.mz;
		if(dif > 1.1) break;
			
		rawCh=1/dif;
		ch = (int)(rawCh+0.5);
		rawChR=rawCh-(int)rawCh;
		if(rawChR>0.2 && rawChR<0.8) continue;
		if(ch<cs.minCharge || ch>cs.maxCharge) continue;
		charge[ch]=1;
	}
  //if no forward charge, exit now.
  bool bMatch=false;
  for(i=cs.minCharge;i<=cs.maxCharge;i++){
    if(charge[i]>0) {
      bMatch=true;
      break;
    }
  }
  if(!bMatch) {
    v.clear();
    return;
  }

	//check backward
	for(j=index-1;j>=0;j--){
    if (s[j].intensity<minIntensity) continue;
			
		dif = indexPeak.mz - s[j].mz;
		if(dif > 1.1) break;
			
		rawCh=1/dif;
		ch = (int)(rawCh+0.5);
		rawChR=rawCh-(int)rawCh;
		if(rawChR>0.2 && rawChR<0.8) continue;
		if(ch<cs.minCharge || ch>cs.maxCharge) continue;
		charge[ch]=1;
	}

	v.clear();
	for(i=cs.minCharge;i<=cs.maxCharge;i++){
		if(charge[i]>0) v.push_back(i);
	}

}

void CHardklor2::QuickHardklor(Spectrum& s, vector<pepHit>& vPeps) {

	//iterators
	int i,j,k,n,m=-1,x;
	size_t varCount;
	size_t v;

	//tracking spectrum peak intensities
	float maxHeight=9999999999999.9f;
	float max=0.0f;
	float lowPoint=9999999999999.9f;

	//Mercury storage and variables aligning mercury data (including 1 da shifts)
	mercuryModel* model;
	vector<Result> vMR;
	Result r;
	int maxIndex;
	int thisMaxIndex;
	int maxMercuryIndex[3];
	double da;
	double lower;
	double upper;
	double shft;

	//peak variables
	vector<int> charges;
	double deltaM;
	double dif;
	double corr;
	vector<float> obs;
	vector<float> mer;
	vector<int> vMatchIndex;
	vector<float> vMatchPeak;
	vector<int> vMatchIndex2;
	vector<float> vMatchPeak2;
	int matchCount,matchCount2;
	int indexOverlap;
  double top3[3];

	//refinement variables
	bool keepPH;
	pepHit ph2;
	//pepHit bestKeepPH;
	int lowIndex;
	int highIndex;
	bool corr2 = false;
	double corr3;

	//best hit variables
	double bestCorr;
	double bestLow;
	double bestHigh;
	double bestDA;
	char bestAveragine[AV_FORMULA_BUFFER_LENGTH]; // Skyline wants the isotope envelope info
	double bestZeroMass; // Skyline wants the isotope envelope info
	int bestCharge;
	double bestMass;
	vector<int> bestMatchIndex;
	vector<float> bestMatchPeak;
	int bestMatchCount;
	pepHit bestPH;
	bool bestKeepPH;
	int bestOverlap;
	int bestLowIndex;
	int bestHighIndex;
	int bestVariant;

	//Results
	pepHit ph;

	//Spectrum variables
	Spectrum origSpec=s;
	Spectrum refSpec=s;
	Spectrum tmpSpec;

	//create mask
	mask.clear();
	for(i=0;i<s.size();i++) mask.add(s[i].mz,0);

	//find lowest intensity;
	for(i=0;i<s.size();i++){
    //printf("%.6lf\t%.1f\n",s[i].mz, s[i].intensity);
		if(s[i].intensity<lowPoint) lowPoint=s[i].intensity;
	}

	//clear results vector
	vPeps.clear();

	//Mark number of variants to analyze
	if(cs.noBase) varCount=cs.variant->size();
	else varCount=cs.variant->size()+1;

	//start the loop through all peaks
	while(true){

		//Find most intense peak. Note that sorting is not possible because
		//peaks change in intensity as they are deconvolved. Also it is advantageous
		//to keep peaks in m/z order
		max=0.0f;
		vector<Peak_T>& peaks = *s.getPeaks(); // BSP note - swapped in a little C++ voodoo here, as this loop is a performance hotspot
		const Peak_T* peak0 = &peaks[0];
		const Peak_T* peak = peak0;
		for (size_t p = peaks.size(); p--; peak++) {
			if (peak->intensity > max && peak->intensity < maxHeight) {
				max= peak->intensity;
				maxIndex = static_cast<int>(peak - peak0);
			}
		}

		//stop searching when we reach lowest original point
		//this prevents overfitting with lots of partial noise peaks
		if(max<lowPoint) break;

		//Get the FWHM estimate for the peak we are at.
		deltaM = CalcFWHM(s[maxIndex].mz,cs.res400,cs.msType);

		//Get the charge states. Note that only remaining peaks are used in the estimate.
		//I'm not sure this is best, but it is simpler and faster
		QuickCharge(s,maxIndex,charges);

		//Reset our correlation and matchcount scores. Then iterate through each charge state and find best
		//match to the peaks.
		bestCorr=0.0;
		bestMatchCount=0;
		for(i=0;i<(int)charges.size();i++){

			//cout << s[maxIndex].mz << "\t" << charges[i] << endl;

			//check all variants
			for(v=0;v<varCount;v++){

        //cout << "Variant: " << v << endl;

				//use model library, align to top 3 peaks
				dif=0;
        top3[0]=top3[1]=top3[2]=0;
        maxMercuryIndex[0]=maxMercuryIndex[1]=maxMercuryIndex[2]=-1;
				model=models->getModel(charges[i],(int)v,s[maxIndex].mz);
				for(k=0; k<model->size; k++) {
          //cout << "i\t" << model->peaks[k].mz << "\t" << model->peaks[k].intensity << endl;
					//if(model->peaks[k].intensity>dif){
          if(model->peaks[k].intensity>top3[0]){
						//dif = model->peaks[k].intensity;
            top3[2]=top3[1];
            top3[1]=top3[0];
            top3[0]=model->peaks[k].intensity;
            maxMercuryIndex[2]=maxMercuryIndex[1];
            maxMercuryIndex[1]=maxMercuryIndex[0];
						maxMercuryIndex[0]=k;
          } else if(model->peaks[k].intensity>top3[1]) {
            top3[2]=top3[1];
            top3[1]=model->peaks[k].intensity;
            maxMercuryIndex[2]=maxMercuryIndex[1];
            maxMercuryIndex[1]=k;
          } else if(model->peaks[k].intensity>top3[2]) {
            top3[2]=model->peaks[k].intensity;
						maxMercuryIndex[2]=k;
          }
				}
				//if(k==0) maxMercuryIndex[1]=-1;
				//else maxMercuryIndex[1]=maxMercuryIndex[0]-1;		//allow right shift
				//maxMercuryIndex[2]=maxMercuryIndex[0]+1;				//allow left shift

				//Test all three positions for the model. Note that if the first peak is the base peak, then
				//no left shift is tested.
				n=0;
				while(n<3){

					//skip the left shift if already at leftmost peak.
					if(maxMercuryIndex[n]<0) {
						n++;
						continue;
					}

          //cout << "ii\tShift #" << n << endl;

					//Align the mercury distribution (MD) to the observed peak. The MD can shift in
					//either direction for one peak to adjust for system noise. The width of the MD
					//determines the boundaries for correlation to the observed data.
					lower=5000.0;
					upper=0.0;
					shft = s[maxIndex].mz - model->peaks[maxMercuryIndex[n]].mz;
					vMR.clear();
					thisMaxIndex=0;
					da=0.0f;

					//use model library
					for(k=0; k<model->size; k++) {
						const Peak_T& peak = model->peaks[k];
						r.data=peak.intensity;
						r.mass=peak.mz+shft;
						vMR.push_back(r);
						if(peak.intensity>99.999) thisMaxIndex=(int)vMR.size()-1;
					
						if(r.mass<lower) lower=r.mass;
						if(r.mass>upper) upper=r.mass;
					}
					da=model->area;

					//Add a little buffer to the m/z boundaries
					lower-=0.1;
					upper+=0.1;

					//Narrow the search to just the area of the spectrum we need
					lowIndex=BinarySearch(s,lower,true);
					highIndex=BinarySearch(s,upper,false);

					//if max peak shifts to already solved peak, skip
					if(!CheckForPeak(vMR,s,thisMaxIndex)){
						n++;
						continue;
					}

					//Match predictions to the observed peaks and record them in the proper array.
					corr=PeakMatcher(vMR,s,lower,upper,deltaM/2,maxIndex,matchCount,indexOverlap,vMatchIndex,vMatchPeak);
					//cout << "ii.i\t" << s[maxIndex].mz << " " << s[maxIndex].intensity << "\t" << charges[i] << "\t" << matchCount << "\t" << corr << "\t" << indexOverlap << "\t" << maxIndex << "\tn" << n << endl;

					//check any overlap with observed peptides. Overlap indicates deconvolution may be necessary.
					//Deconvolution is at best a rough estimate and is not used if it does not improve the correlation
					//scores.
					keepPH=false;
					if(indexOverlap>-1 /*&& indexOverlap>maxIndex*/){

            //cout << "iii\tChecking overlap: " << indexOverlap << "\t" << maxIndex << endl;

						//Find overlapping peptide
						for(m=0;m<(int)vPeps.size();m++){
							if(vPeps[m].basePeakIndex==indexOverlap) break;
						}

						//break out subspectrum; this is done using the original spectrum peak heights, not
						//the current peak heights. The peak heights are then adjusted to account for the currently
						//overlapping peptide model.
						tmpSpec.clear();
						x=0;
						int subIndex=-1;
						for(j=vPeps[m].lowIndex;j<=vPeps[m].highIndex;j++){
							
							while(x<(int)vMatchIndex.size() && j>vMatchIndex[x]) x++;

							//generate temporary subspectrum with peak heights reduced for overlapping model
							if(x<(int)vMatchIndex.size() && j==vMatchIndex[x]){
								tmpSpec.add(origSpec[j].mz,origSpec[j].intensity-vMatchPeak[x]*max);
								x++;
							} else {
								tmpSpec.add(origSpec[j]);
							}

							//get the base peak index of the subspectrum
							if(j==indexOverlap) subIndex=tmpSpec.size()-1;
						}

						//Re-Solve subspectrum and see if it has better correlation
						corr2=MatchSubSpectrum(tmpSpec,subIndex,ph2);
						//cout << "iii.i\tCorr2: " << corr2 << "\t" << ph2.corr << "\t" << origSpec[vPeps[m].basePeakIndex].mz << "\t" << vPeps[m].charge << endl;

						//If correlation is better (or close), go back and try the
						//newly adjusted peaks.
						if(corr2 && ph2.corr+0.025>vPeps[m].corr){
							x=0;

							for(j=lowIndex;j<=highIndex;j++){
								if(x<tmpSpec.size() && s[j].mz==tmpSpec[x].mz){
									refSpec[j].intensity=(origSpec[j].intensity+tmpSpec[x].intensity);
									x++;
								} else {
									refSpec[j].intensity=s[j].intensity;
								}
							}

							//solve merged models
							corr3=PeakMatcher(vMR,refSpec,lower,upper,deltaM/2,maxIndex,matchCount2,indexOverlap,vMatchIndex2,vMatchPeak2);
							//cout << "iii.ii\tCorr3: " << s[maxIndex].mz << " " << s[maxIndex].intensity << "\t" << charges[i] << "\t" << matchCount2 << "\t" << corr3 << "\t" << indexOverlap << endl;

							//keep the new model if it is better than the old one.
							if(corr3>corr) {

								corr=corr3;
								vMatchIndex=vMatchIndex2;
								vMatchPeak=vMatchPeak2;
								matchCount=matchCount2;

								//refine the overlapping one.
								keepPH=true;
								
							} else {

								//it failed, do nothing and move on.
								keepPH=false;

							}
							
						}

					}//if indexOverlap>-1
          double tCorr;
          if(bestMatchCount==0) tCorr=0;
          else tCorr=0.025*(matchCount-bestMatchCount)/bestMatchCount;
					//cout << "Old best corr: " << bestCorr << "(" << bestMatchCount << ") This corr: " << corr << "," << corr+tCorr << "(" << matchCount << ")" << endl;
					if(/*corr>bestCorr ||*/ (corr>cs.corr && corr+tCorr>bestCorr) ){
						bestMatchIndex=vMatchIndex;
						bestMatchPeak=vMatchPeak;
						bestMatchCount=matchCount;
						bestCorr=corr;
						bestMass=model->zeroMass+shft*charges[i];
						bestZeroMass = model->zeroMass;   // Skyline wants the isotope envelope info
						memmove(bestAveragine, model->averagine, AV_FORMULA_BUFFER_LENGTH);  // Skyline wants the isotope envelope info
						bestCharge=charges[i];
						bestDA=da;
						bestLow=lower;
						bestHigh=upper;
						bestKeepPH=keepPH;
						if (corr2) bestPH=ph2;
						bestOverlap=m;
						bestLowIndex=lowIndex;
						bestHighIndex=highIndex;
						bestVariant=(int)v;
					}

					n++;
				}//while

			}//for v (variants)

		}//for i (charges)

		//if above threshold, erase peaks.
		if(bestCorr>cs.corr){
			ph.area=(float)bestDA;
			ph.basePeakIndex=maxIndex;
			ph.charge=bestCharge;
			ph.corr=bestCorr;
			ph.highMZ=bestHigh;
			ph.intensity=max;
			ph.lowMZ=bestLow;
			ph.massShift=0.0;
			ph.monoMass=bestMass;
			ph.zeroMass = bestZeroMass;
			ph.lowIndex=bestLowIndex;
			ph.highIndex=bestHighIndex;
			ph.variantIndex=bestVariant;
			memmove(ph.averagine, bestAveragine, AV_FORMULA_BUFFER_LENGTH);
			ph.isotopeEvidencePeaksCount = static_cast<int>(bestMatchIndex.size());

			if(bestKeepPH){
				vPeps[bestOverlap].area=bestPH.area;
				vPeps[bestOverlap].intensity=bestPH.intensity;
				vPeps[bestOverlap].corr=bestPH.corr;
				vPeps[bestOverlap].charge=bestPH.charge;
				vPeps[bestOverlap].monoMass = bestPH.monoMass;
				vPeps[bestOverlap].zeroMass = bestPH.zeroMass;
				vPeps[bestOverlap].variantIndex=bestPH.variantIndex;
				memmove(vPeps[bestOverlap].averagine, bestPH.averagine, AV_FORMULA_BUFFER_LENGTH);
				vPeps[bestOverlap].isotopeEvidencePeaksCount = bestPH.isotopeEvidencePeaksCount;
			}
			vPeps.push_back(ph);
			mask[maxIndex].intensity=100.0f;

			for(k=0;k<(int)bestMatchIndex.size();k++){
				if(bestMatchPeak[k]*max/s[bestMatchIndex[k]].intensity>0.5){
					s[bestMatchIndex[k]].intensity=-s[bestMatchIndex[k]].intensity;
				} else {
					s[bestMatchIndex[k]].intensity-=bestMatchPeak[k]*max;
				}
        //cout << "iv\t" << s[bestMatchIndex[k]].mz << " is now " << s[bestMatchIndex[k]].intensity << endl;
			}
		}

		//set new maximum
		maxHeight=max;

	}

	//Sort results by base peak
  //This sort is expensive. Instead, try sorting before exporting to file. Make sure RefineHits below is not
  //order dependent.
	if(vPeps.size()>0) qsort(&vPeps[0],vPeps.size(),sizeof(pepHit),CompareBPI);

	//Refine overfitting based on density
	RefineHits(vPeps,origSpec);

}

//Reduces the number of features (cs.depth) per 1 Da window. This removes a lot
//of false hits resulting from jagged tails on really large peaks. Criteria for
//removal is lowest peak intensity
void CHardklor2::RefineHits(vector<pepHit>& vPeps, Spectrum& s){

	unsigned int i;
	int j;
	double lowp,highp;
	bool bRestart=true;
	vector<pepHit> vTmpHit;
	vector<int> vPepMask;
	list<int> vList;
	list<int>::iterator it;

	//generate an index of the peptides to keep or throw away
	for(i=0;i<vPeps.size();i++) vPepMask.push_back(0);

	//iterate through all hits
	for(i=0;i<vPeps.size();i++){

		//skip anything already marked for removal
		if(vPepMask[i]>0)	continue;

		//put a tolerance around each peak
		lowp=s[vPeps[i].basePeakIndex].mz-0.5;
		highp=s[vPeps[i].basePeakIndex].mz+0.5;

		//put the current hit in the list
		vList.clear();
		vList.push_front(i);

		//find all other hits in the tolerance window
		//look left first
		j=i-1;
		while(j>-1){

			//break out when boundary is reached
			if(s[vPeps[j].basePeakIndex].mz<lowp) break;

			//skip anything marked for removal.
			if(vPepMask[j]>0){
				j--;
				continue;
			}

			//add to list from high to low
			for(it=vList.begin();it!=vList.end();it++){
				if(s[vPeps[j].basePeakIndex].intensity > s[vPeps[*it].basePeakIndex].intensity) break;
			}
			vList.insert(it,j);

			j--;
		}

		//look right
		j=i+1;
		while(j<(int)vPeps.size()){

			//break out when boundary is reached
			if(s[vPeps[j].basePeakIndex].mz>highp) break;

			//skip anything marked for removal.
			if(vPepMask[j]>0){
				j++;
				continue;
			}

			//add to list from high to low
			for(it=vList.begin();it!=vList.end();it++){
				if(s[vPeps[j].basePeakIndex].intensity > s[vPeps[*it].basePeakIndex].intensity) break;
			}
			vList.insert(it,j);

			j++;
		}

		//remove the lowest peptides below threshold (user specified depth)
		if((int)vList.size()>cs.depth){
			it=vList.begin();
			for(j=0;j<cs.depth;j++)	it++;
			for(it=it;it!=vList.end();it++)	vPepMask[*it]=1;
		}

	}

	//copy over the keepers
	for(i=0;i<vPeps.size();i++){
		if(vPepMask[i]) continue;
		vTmpHit.push_back(vPeps[i]);
	}
	vPeps.clear();
	for(i=0;i<vTmpHit.size();i++)vPeps.push_back(vTmpHit[i]);

}

void CHardklor2::ResultToMem(pepHit& ph, Spectrum& s){
  int i,j;
  char mods[32];
  char tmp[16];

  hkm.monoMass = ph.monoMass;
  hkm.charge = ph.charge;
  if(cs.distArea) hkm.intensity = ph.area*ph.intensity;
  else hkm.intensity = ph.intensity;
  hkm.scan = currentScanNumber;
  hkm.mz = s[ph.basePeakIndex].mz;
  hkm.corr = ph.corr;

  //Add mods
  if(!cs.noBase) i=ph.variantIndex-1;
	else i=ph.variantIndex;
	strcpy(mods,"");
  if(i<0) {
	  strcat(mods,"_");
	} else {
    for(j=0;j<cs.variant->at(i).sizeAtom();j++){
		  strcat(mods,PT->at(cs.variant->at(i).atAtom(j).iLower).symbol);
		  sprintf(tmp,"%d",cs.variant->at(i).atAtom(j).iUpper);
      strcat(mods,tmp);
		}
		strcat(mods,"_");
		for(j=0;j<cs.variant->at(i).sizeEnrich();j++){
		  sprintf(tmp,"%.2lf",cs.variant->at(i).atEnrich(j).ape);
      strcat(mods,tmp);
			strcat(mods,PT->at(cs.variant->at(i).atEnrich(j).atomNum).symbol);
      sprintf(tmp,"%d_",cs.variant->at(i).atEnrich(j).isotope);
      strcat(mods,tmp);
		}
	}
  strcpy(hkm.mods,mods);
  vResults.push_back(hkm);
}

void CHardklor2::SetResultsToMemory(bool b){
  bMem=b;
}

int CHardklor2::Size(){
  return (int)vResults.size();
}

void CHardklor2::WritePepLine(pepHit& ph, Spectrum& s, FILE* fptr, int format){
  int i,j;

    // Reduce output by rejecting anything with less than minIsotopePeaksCount isotope peaks
	if (ph.isotopeEvidencePeaksCount < cs.minIsotopePeaks)
	{
		return;
	}

  if(format==0){
		fprintf(fptr,"P\t%.4lf",ph.monoMass);
		fprintf(fptr,"\t%d",ph.charge);
		if(cs.distArea) fprintf(fptr,"\t%.0f",ph.area*ph.intensity);
		else fprintf(fptr,"\t%.0f",ph.intensity);
		fprintf(fptr,"\t%.4lf",s[ph.basePeakIndex].mz);
		fprintf(fptr,"\t%.4lf-%.4lf",s[ph.lowIndex].mz,s[ph.highIndex].mz);
		fprintf(fptr,"\t0.0000");

		//Add mods
		if(!cs.noBase) i=ph.variantIndex-1;
		else i=ph.variantIndex;
		if(i<0) {
			fprintf(fptr,"\t_");
		} else {
			fprintf(fptr,"\t");
			for(j=0;j<cs.variant->at(i).sizeAtom();j++){
				fprintf(fptr,"%s",PT->at(cs.variant->at(i).atAtom(j).iLower).symbol);
				fprintf(fptr,"%d",cs.variant->at(i).atAtom(j).iUpper);
			}
			fprintf(fptr,"_");
			for(j=0;j<cs.variant->at(i).sizeEnrich();j++){
				fprintf(fptr,"%.2lf",cs.variant->at(i).atEnrich(j).ape);
				fprintf(fptr,"%s",PT->at(cs.variant->at(i).atEnrich(j).atomNum).symbol);
				fprintf(fptr,"%d_",cs.variant->at(i).atEnrich(j).isotope);
			}
		}

		fprintf(fptr, "\t%.4lf", ph.corr);

		 if (cs.reportAveragineAndMassOffset)
		 {
		 	// Skyline wants to know the chemical formula and mass offset so it can use the same isotope envelope as Hardklor
		 	// Encode this as a formula with a mass modification e.g. "H21C14N4O4[+3.038518]", "H21C14N4O4[-0.085518]" etc
	 	 	double massOffset = ph.monoMass - ph.zeroMass;
		 	char massShiftStr[60];
		 	if (fabs(massOffset) < .001)
				 massShiftStr[0] = 0;
			else if (massOffset < 0)
				 snprintf(massShiftStr, 60, "[%.6f]", massOffset);
			else if (massOffset > 0)
				snprintf(massShiftStr, 60, "[+%.6f]", massOffset);
			 fprintf(fptr, "\t%s%s", ph.averagine, massShiftStr);
		 }
		fprintf(fptr, "\n");

  } else if(format==1) {
		/*
      fptr << "<Peak Mass=\"" << sa.predPep->at(pepID).GetVariant(varID).GetMonoMass() << "\" ";
      fptr << "ChargeState=\"" << sa.predPep->at(pepID).GetVariant(varID).GetCharge() << "\" ";
      if(cs.distArea) fptr << "Area=\"" << sa.predPep->at(pepID).GetIntensity()*sa.predPep->at(pepID).GetVariant(varID).GetArea() << "\" ";
		  else fptr << "Intensity=\"" << sa.predPep->at(pepID).GetIntensity() << "\" ";
			fptr << "MZ=\"" << sa.predPep->at(pepID).GetMZ() << "\" ";
			fptr << "Window=\"" << sa.peaks.at(0).mz << "-" << sa.peaks.at(sa.peaks.size()-1).mz << "\" ";
			fptr << "SN=\"" << sa.S2NCutoff << "\" ";

      //Add mods
      fptr << "Mod=\"";
			for(j=0;j<sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().sizeAtom();j++){
				fptr << PT->at(sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atAtom(j).iLower).symbol;
				fptr << sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atAtom(j).iUpper;
			}
			fptr << "_";
			for(j=0;j<sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().sizeEnrich();j++){
				fptr << setiosflags(ios::fixed) << setprecision(2);
				fptr << sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).ape;
				fptr << setiosflags(ios::fixed) << setprecision(4);
				fptr << PT->at(sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).atomNum).symbol;
				fptr << sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).isotope;
				fptr << "_";
			}
      fptr << "\" ";

			fptr << "Score=\"" << obj.corr << "\"/>" << endl;
			*/

		//reduced output
  } else if(format==2){
		fprintf(fptr,"%.4lf",(ph.monoMass+ph.charge*1.007276466)/ph.charge);
		if(cs.distArea) fprintf(fptr,"\t%.0f",ph.area*ph.intensity);
		else fprintf(fptr,"\t%.0f",ph.intensity);
		fprintf(fptr,"\t%d\n",ph.charge);
	}
}

void CHardklor2::WriteScanLine(Spectrum& s, FILE* fptr, int format){

  if(format==0) {
    fprintf(fptr,"S\t%d\t%.4f\t%s",s.getScanNumber(),s.getRTime(),cs.inFile.c_str());

		//For Alex Panchaud, special ZS case
		if(s.getFileType()==ZS || s.getFileType()==UZS){
			if(s.sizeZ()>0){
				for(int i=0;i<s.sizeZ();i++) fprintf(fptr,"\t%d,%.6lf",s.atZ(i).z,s.atZ(i).mh);
			}
		} else {

			//otherwise output precursor info if it exists
			if(s.sizeZ()==1){
				fprintf(fptr,"\t%.4lf\t%d\t%.4lf",s.atZ(0).mh-1.00727649,s.atZ(0).z,s.getMZ());
			} else if(s.sizeZ()>1){
				fprintf(fptr,"\t0.0\t0\t%.4lf",s.getMZ());
			} else {
				fprintf(fptr,"\t0.0\t0\t0.0");
			}
		}
    fprintf(fptr,"\n");

		//For XML output
  } else if(format==1){
    fprintf(fptr,"<Spectrum Scan=\"%d\" ",s.getScanNumber());
		fprintf(fptr,"RetentionTime=\"%.4f\" ",s.getRTime()); 
		fprintf(fptr,"Filename=\"%s\"",cs.inFile.c_str());
		if(s.getFileType()==ZS || s.getFileType()==UZS){
			if(s.sizeZ()>0){
				for(int i=0;i<s.sizeZ();i++) fprintf(fptr," PeptideSignal%d=\"%d,%.4lf\"",i,s.atZ(i).z,s.atZ(i).mh);
			}
		} else {
			if(s.sizeZ()==1){
				fprintf(fptr," AccMonoMass=\"%.4lf\" PrecursorCharge=\"%d\" PrecursorMZ=\"%.4lf\"",s.atZ(0).mh-1.00727649,s.atZ(0).z,s.getMZ());
			} else if(s.sizeZ()>1){
				fprintf(fptr," AccMonoMass=\"0.0\" PrecursorCharge=\"0\" PrecursorMZ=\"%.4lf\"",s.getMZ());
			} else {
				fprintf(fptr," AccMonoMass=\"0.0\" PrecursorCharge=\"0\" PrecursorMZ=\"0.0\"");
			}
		}
    fprintf(fptr,">\n");

		//For reduced output
	} else if(format==2) {
		fprintf(fptr, "Scan=%d	RT=%.4f\n", s.getScanNumber(),s.getRTime());
	}
}