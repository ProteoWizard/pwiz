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
#include "CSpecAnalyze.h"
#include <iostream>
#include <algorithm>

using namespace std;
using namespace MSToolkit;

//Default constructor
CSpecAnalyze::CSpecAnalyze(){
  basePeak = 0;
  manyPeps = false;
  S2NCutoff = 0;
	predPeak = new vector<CPeakPrediction>;
	predPep = new vector<CPeptidePrediction>;
	charges = new vector<int>;
	mismatchSize = 0;
	averagine=NULL;
	mercury=NULL;
	spec=NULL;
}

//Copy constructor
CSpecAnalyze::CSpecAnalyze(const CSpecAnalyze& c){
	unsigned int i;

  basePeak = c.basePeak;
  manyPeps = c.manyPeps;
  peaks = c.peaks;
  peptide = c.peptide;
	userParams = c.userParams;
	mismatchSize = c.mismatchSize;
  S2NCutoff = c.S2NCutoff;
  spec = c.spec;
	averagine = c.averagine;
	mercury = c.mercury;
	spec = c.spec;

	predPeak = new vector<CPeakPrediction>;
	for(i=0;i<c.predPeak->size();i++) predPeak->push_back(c.predPeak->at(i));

	predPep = new vector<CPeptidePrediction>;
	for(i=0;i<c.predPep->size();i++) predPep->push_back(c.predPep->at(i));

	charges = new vector<int>;
	for(i=0;i<c.charges->size();i++) charges->push_back(c.charges->at(i));
}

//Destructor
CSpecAnalyze::~CSpecAnalyze(){
	delete predPeak;
	delete predPep;
	delete charges;
	averagine = NULL;
	mercury = NULL;
	spec = NULL;
}

//Overloaded operator
CSpecAnalyze& CSpecAnalyze::operator=(const CSpecAnalyze& c){
	unsigned int i;

  if(this!=&c){
    basePeak = c.basePeak;
    manyPeps = c.manyPeps;
    peaks = c.peaks;
    peptide = c.peptide;
    userParams = c.userParams;
    mismatchSize = c.mismatchSize; 
    S2NCutoff = c.S2NCutoff;
    spec = c.spec;
    averagine = c.averagine;
    mercury = c.mercury;
		spec = c.spec;
		
    delete predPeak;
    predPeak = new vector<CPeakPrediction>;
    for(i=0;i<c.predPeak->size();i++) predPeak->push_back(c.predPeak->at(i));
    
    delete predPep;
    predPep = new vector<CPeptidePrediction>;
    for(i=0;i<c.predPep->size();i++) predPep->push_back(c.predPep->at(i));

    delete charges;
    charges = new vector<int>;
    for(i=0;i<c.charges->size();i++) charges->push_back(c.charges->at(i)); 
  }
  return *this;
}

//Builds arrays of all the modeled peaks that do not match observed peaks.
//Overlapping mismatched peaks are binned together for simplicity
void CSpecAnalyze::BuildMismatchArrays(){

  int i,j,k,m;
  bool match;
  vector<double> mz;
  double deltaM;
  double tmp;
  Peak_T extraMZ;

  //Create master mismatch set of datapoints. 
  //Essentially, this creates dynamic bins for all mismatched points

  //Iterate through each predicted peptide
  for(i=0;i<(int)predPep->size();i++) {

    //get resolution
    deltaM = calcFWHM(predPep->at(i).GetMZ());

    //Iterate through each variant
    for(j=0;j<predPep->at(i).VariantListSize();j++) {

      //Iterate through each extra data point
      for(k=0;k<predPep->at(i).GetVariant(j).SizeExtra();k++) {
        extraMZ = predPep->at(i).GetVariant(j).GetExtra(k);

        //Check mz of every extra data point against the current list
        match=false;
        for(m=0;m<(int)mz.size();m++){
          if( fabs(mz.at(m) - extraMZ.mz) < deltaM ) {
            match=true;
            break;
          }
        }
        if(!match) mz.push_back(extraMZ.mz);

      }
    }
  }
		
  //The list is built, sort it
  if(mz.size()>1) {
    for(i=0;i<(int)mz.size()-1;i++){
      for(j=i+1;j<(int)mz.size();j++){
        if(mz.at(j)<mz.at(i)){
          tmp = mz.at(i);
          mz.at(i) = mz.at(j);
          mz.at(j) = tmp;
        }
      }
    }
  }

  mismatchSize = (int)mz.size();

  //Recheck all peptide specific mismatches against the master list. This will adjust the mz values of the mismatches
  //to fit the master bins.

  //Iterate through every predicted peptide
  for(i=0;i<(int)predPep->size();i++) {

    //get resolution
    deltaM = calcFWHM(predPep->at(i).GetMZ());

    //Iterate through every variant
    for(j=0;j<predPep->at(i).VariantListSize();j++) {

      //Resize the mismatch list
      predPep->at(i).GetVariant(j).SetMismatchSize(mismatchSize);

      //Convert extra data points to mismatches
      for(k=0;k<predPep->at(i).GetVariant(j).SizeExtra();k++) {

        extraMZ = predPep->at(i).GetVariant(j).GetExtra(k);

        //Check mz of every extra data point against the current list
        match=false;
        for(m=0;m<mismatchSize;m++){
          if( fabs(mz.at(m) - extraMZ.mz) < deltaM ) {
            predPep->at(i).GetVariant(j).AddMismatch(m,extraMZ.mz,extraMZ.intensity);
            match=true;
            break;
          }
        }
				
        if(!match) {
          //This shouldn't happen. Perhaps error checking should go here.
          //cout << "Houston, we have a problem" << endl;
          //cout << extraMZ.mz << " DM:" << deltaM << endl;
          //for(m=0;m<mismatchSize;m++)  cout << mz.at(m) << endl;
          //exit(-1);
        }
      }

      //Delete extra data points
      predPep->at(i).GetVariant(j).DeleteExtra();

    }
  }

}

//Calculates the resolution (FWHM) of a peak
double CSpecAnalyze::calcFWHM(double mz){
	double deltaM;
	switch(userParams.msType){
	case OrbiTrap:
		deltaM = mz * sqrt(mz) / (20*userParams.res400);  //sqare root of 400
		break;
	case TOF:
		deltaM = mz / userParams.res400;
		break;
	case QIT:
		deltaM = userParams.res400 / 5000.0;
		break;
	case FTICR:
	default:
		deltaM = mz * mz / (400*userParams.res400);
		break;
	}
	return deltaM;
}

//The QuickCharge algorithm. Simply assign charge based on inverse distance
void CSpecAnalyze::chargeState(){
	int i,j,k;
	double dif;
	int ch;
	bool hasState;
	CPeakPrediction p;

	for(i=0;i<peaks.size()-1;i++){
		p.Clear();
		p.SetMZ(peaks.at(i).mz);
		p.SetIntensity(peaks.at(i).intensity);

		for(j=i+1;j<peaks.size();j++){
			
			dif = peaks.at(j).mz - peaks.at(i).mz;
			if(dif > 1.1) break;
			
			ch = (int)(1/dif+0.5);
			if(ch<userParams.minCharge || ch>userParams.maxCharge) continue;
			hasState=false;
			for(k=0;k<p.Size();k++){
				if(p.GetCharge(k)==ch) {
					hasState=true;
					break;
				}
			}
      if(!hasState) p.AddCharge(ch);

		}

		if(p.Size()>0) predPeak->push_back(p);
	}

}

void CSpecAnalyze::clear(){
  //spec=NULL;
  peaks.clear();
  peptide.clear();
  delete predPeak;
  delete predPep;
  predPeak = new vector<CPeakPrediction>;
  predPep = new vector<CPeptidePrediction>;
  delete charges;
  charges = new vector<int>;
  manyPeps=false;
  S2NCutoff = 0;
  mismatchSize = 0;
  basePeak = 0;
}

//Method for predicting peptides to analyze
// 5/1/09 Most likely not used - commenting
/*
void CSpecAnalyze::FindPeptides(){
  // split the results into overlapping ranges
  peaks = signalToNoise(spec,0,spec.size()-1,userParams.sn,&S2NCutoff);
  FirstDerivativePeaks(peaks,1);  //10 for zoomscan, 4 for UZS
 
  //if there is more than two peaks, find a charge state.
	if(peaks.size()>2) chargeState();

}
*/

//Quickest ways to find all spectrum peaks without making any predictions
int CSpecAnalyze::FindPeaks(){

	/*
  if(userParams.chargeMode == 'F' ||
		 userParams.chargeMode == 'P' ||
		 userParams.chargeMode == 'S') TraditionalCharges();
	*/

	if(userParams.fileFormat == zs) {
		peaks = *spec;
		FirstDerivativePeaks(peaks,10);
	} else if(userParams.fileFormat == uzs) {
		peaks = *spec;
		FirstDerivativePeaks(peaks,4);
	} else {
		FirstDerivativePeaks(peaks,1);  //10 for zoomscan, 4 for UZS
	}

  return peaks.size();

}

//Find all spectrum peaks in a given range (by data point array index)
int CSpecAnalyze::FindPeaks(Spectrum& s, int start, int stop){

	/*
  if(userParams.chargeMode == 'F' ||
		 userParams.chargeMode == 'P' ||
		 userParams.chargeMode == 'S') TraditionalCharges();
	*/

	if(userParams.fileFormat == zs) {
		FirstDerivativePeaks(s,start,stop,10);
	} else if(userParams.fileFormat == uzs) {
		FirstDerivativePeaks(s,start,stop,4);
	} else {
		//intercept low res TOF data here?
		FirstDerivativePeaks(s,start,stop,1);  //10 for zoomscan, 4 for UZS
	}

  return peaks.size();

}

//First derivative method, returns base peak intensity of the set
void CSpecAnalyze::FirstDerivativePeaks(Spectrum& s, int winSize){
  FirstDerivativePeaks(s,0,s.size()-1,winSize);
}

//First derivative method, returns base peak intensity of the set
void CSpecAnalyze::FirstDerivativePeaks(Spectrum& s, int start, int stop, int winSize){
  //cout << "Start centroid " << s.getScanNumber() << " " << start << " " << stop << " " << winSize << endl;
  int i,j;
  float maxIntensity;
  int bestPeak;
  bool bLastPos;
  Spectrum gp;

	int nextBest;
	double FWHM;
	Peak_T centroid;

	basePeak=0;
  bLastPos=false;
  for(i=start;i<stop+1-winSize;i++){

    if(s.at(i).intensity<s.at(i+winSize).intensity) {
      bLastPos=true;
      continue;
    } else {
      if(bLastPos){
				bLastPos=false;
	
        //Possible ways to improve this:
				//1. check FWHM - arg! what a computational disaster.
				//2. account for noise - another disaster.

				//find max and add peak
				maxIntensity=0;
				for(j=i;j<i+winSize;j++){
				  if (s.at(j).intensity>maxIntensity){
				    maxIntensity=s.at(j).intensity;
				    bestPeak = j;
				  }
				}

				//Best estimate of Gaussian centroid
				//Get 2nd highest point of peak
				if(bestPeak==stop){
					nextBest=bestPeak-1;
				} else if(s.at(bestPeak-1).intensity > s.at(bestPeak+1).intensity){
					nextBest=bestPeak-1;
				} else {
					nextBest=bestPeak+1;
				}

				//Get FWHM
				FWHM = calcFWHM(s.at(bestPeak).mz);

				//Calc centroid MZ (in three lines for easy reading)
				centroid.mz = pow(FWHM,2)*log(s.at(bestPeak).intensity/s.at(nextBest).intensity);
				centroid.mz /= GAUSSCONST*(s.at(bestPeak).mz-s.at(nextBest).mz);
				centroid.mz += (s.at(bestPeak).mz+s.at(nextBest).mz)/2;

				//Calc centroid intensity
				centroid.intensity=(float)(s.at(bestPeak).intensity/exp(-pow((s.at(bestPeak).mz-centroid.mz)/FWHM,2)*GAUSSCONST));

				//some peaks are funny shaped and have bad gaussian fit.
				//if error is more than 10%, keep existing intensity
				if( fabs((s.at(bestPeak).intensity - centroid.intensity) / centroid.intensity * 100) > 10 ||
            //not a good check for infinity
            centroid.intensity>999999999999.9 ||
            centroid.intensity < 0 ) {
					centroid.intensity=s.at(bestPeak).intensity;
				}

				//Hack until I put in mass ranges
				if(centroid.mz<0 || centroid.mz>2000) {
					//do nothing if invalid mz
				} else {
					gp.add(centroid);
					if(centroid.intensity > basePeak) {
						basePeak = centroid.intensity;
					}
				}
				i+=winSize-1;
      }

    }
  }

  //cout << "centroid ok" << endl;
  peaks = gp;

}

double CSpecAnalyze::InterpolateMZ(Peak_T& p1, Peak_T& p2, double halfMax){

	double slope;
	double xDif;
	double yDif;
	double intercept;

	//linear interpolation
	yDif = (double)(p2.intensity - p1.intensity);
	xDif = (double)(p2.mz - p1.mz);
	slope = yDif/xDif;
	intercept = (double)p1.intensity;

	return (halfMax - intercept)/slope+p1.mz;

}

//5/1/09 - No longer used? Commenting out
/*
void CSpecAnalyze::FindCharge(){
	int i,j,k,n;
	double monoMass;
	double shft;
	double closest=0.0;
	int closestIndex;
	double dif=0.0;
	double deltaM;
	int maxPeak;

	CPeakPrediction p;
	CHardklorVariant var;
	char v[64];

	if(predPeak->size()==0) return;

	//Sort peaks from high to low intensity;
	for(i=0;i<predPeak->size()-1;i++){
		for(j=i+1;j<predPeak->size();j++){
			if(predPeak->at(j).GetIntensity() > predPeak->at(i).GetIntensity()) {
				p = predPeak->at(j);
				predPeak->at(j) = predPeak->at(i);
				predPeak->at(i) = p;
			};
		};
	};

	//Profile every charge state of every peak, from most to least intense
	for(i=0;i<predPeak->size();i++){

    deltaM = calcFWHM(predPeak->at(i).GetMZ());

		for(j=0;j<predPeak->at(i).Size();j++) {

			//Calc monoisotopic mass
			monoMass = predPeak->at(i).GetMZ() * predPeak->at(i).GetCharge(j) -
								 predPeak->at(i).GetCharge(j) * (ProtonMass);

			//Create averagine
			averagine->clear();
			averagine->calcAveragine(monoMass,var);
			averagine->getAveragine(&v[0]);

			//Make mercury distribution
			mercury->GoMercury(&v[0],predPeak->at(i).GetCharge(j));

			//Align to max peak
			dif=0;
			for(k=0; k<mercury->FixedData.size(); k++) {
				if(mercury->FixedData.at(k).data>dif){
					dif = mercury->FixedData.at(k).data;
					maxPeak=k;
				};
			};
			shft = predPeak->at(i).GetMZ() - mercury->FixedData.at(maxPeak).mass;
			for(k=0; k<mercury->FixedData.size(); k++) mercury->FixedData.at(k).mass += shft;

			//Remove charge states of peaks where mercury intensity > 10%
			for(k=0;k<mercury->FixedData.size();k++){
				closest=userParams.winSize;
				closestIndex=-1;
				if(mercury->FixedData.at(k).data < 10) continue;
				if(k == maxPeak) continue;
				for(n=0;n<predPeak->size();n++){
					if(n == i) continue;
					dif = (double)fabs(predPeak->at(n).GetMZ()-mercury->FixedData.at(k).mass);
					if(dif < closest) {
						closest = dif;
						closestIndex = n;
					};
				};

				//If matching peak was found, erase same charge state at that peak
				//But only if intensity is lower!!!
				if(closestIndex > -1 && closest < deltaM) {
					for(n=0;n<predPeak->at(closestIndex).Size();n++){
						if(predPeak->at(closestIndex).GetCharge(n) == predPeak->at(i).GetCharge(j) &&
							 predPeak->at(closestIndex).GetIntensity() < predPeak->at(i).GetIntensity()){
							//cout << "Remove: " << predPeak->at(closestIndex).GetMZ() << " +" << predPeak->at(closestIndex).GetCharge(n) << endl;
							predPeak->at(closestIndex).EraseCharge(n);
							break;
						};
					};
				};

			};

		};

		//Iterate predictions
	};
};
*/

//Builds Mercury distribution models for all predicted peptides. Keeps only
//the ones that align to a user defined (sensitivity level param) number of observed peaks
void CSpecAnalyze::MakePredictions(vector<CHardklorVariant>& var){
  int i,j,k,n,q;
  bool match;
  double monoMass;
  double shft;
  double deltaM;
  double dif=0;
  double fracAbun;
  char v[64];
  float intensity;

  int maxPeak;
  int matchMaxPeak;
  int matchIndex;

  int matchCount;
  int mismatchCount;

  double distArea;
  double predMonoMass;

  CPeakPrediction pk;
  CPeptidePrediction p;
  CPeptideVariant pv;

  i=peaks.size();
  pv.SetMatchSize(i);

  //Sort peaks from high to low intensity;
  for(i=0;i<(int)predPeak->size()-1;i++){
    for(j=i+1;j<(int)predPeak->size();j++){
      if(predPeak->at(j).GetIntensity() > predPeak->at(i).GetIntensity()) {
        pk = predPeak->at(j);
        predPeak->at(j) = predPeak->at(i);
        predPeak->at(i) = pk;
      }
    }
  }

  //Go through every peak prediction
  for(i=0;i<(int)predPeak->size();i++) {

    //Clear any prior peptide prediction data
    p.Clear();

    //Set some basic data
    p.SetMZ(predPeak->at(i).GetMZ());
    p.SetIntensity(predPeak->at(i).GetIntensity());

    //Get the resolution
    deltaM = calcFWHM(predPeak->at(i).GetMZ());

    //Iterate through every charge state
    for(j=0;j<predPeak->at(i).Size();j++){

      //Iterate through every variant
      for(q=0;q<(int)var.size();q++){

        //Clear prior data
        pv.Clear();
		
        //Calc monoisotopic mass
        monoMass = predPeak->at(i).GetMZ() * predPeak->at(i).GetCharge(j) - predPeak->at(i).GetCharge(j) * (ProtonMass);

        //Create averagine
        if(strcmp(userParams.formula,"")==0){
          averagine->clear();
          averagine->calcAveragine(monoMass,var.at(q));
          averagine->getAveragine(&v[0]);
        } else {
          strcpy(v,userParams.formula);
        }

        //Make mercury distribution	
        for(k=0;k<var.at(q).sizeEnrich();k++){
          mercury->Enrich(var.at(q).atEnrich(k).atomNum,var.at(q).atEnrich(k).isotope,var.at(q).atEnrich(k).ape);
        }
        mercury->GoMercury(&v[0],predPeak->at(i).GetCharge(j));

        //Align to max peak
        dif=0;
        for(k=0; k<(int)mercury->FixedData.size(); k++) {
          if(mercury->FixedData.at(k).data>dif){
            dif = mercury->FixedData.at(k).data;
            maxPeak=k;
          }
        }

        shft = predPeak->at(i).GetMZ() - mercury->FixedData.at(maxPeak).mass;
        //for fixed formulas, skip large mass differences.
        //currently set at 10 ppm, this is only good for FT-Ultra and Orbitrap
        if(strcmp(userParams.formula,"")!=0){
          if( fabs(shft/mercury->FixedData[maxPeak].mass*1000000) > 10) continue;
        }
        for(k=0; k<(int)mercury->FixedData.size(); k++) {
          mercury->FixedData.at(k).mass += shft;
        }

        matchCount=0;
        mismatchCount=0;
        fracAbun=0;
        distArea=0;

        //Match predictions to the observed peaks and record them in the proper array.
        for(k=0;k<(int)mercury->FixedData.size();k++) {
          if(mercury->FixedData.at(k).data<S2NCutoff) continue;
          if(mercury->FixedData.at(k).data<0.1) continue;

          match=false;
          dif=userParams.winSize;
          for(n=0;n<peaks.size();n++){
            if(fabs(peaks.at(n).mz-mercury->FixedData.at(k).mass)<dif){
              dif=fabs(peaks.at(n).mz-mercury->FixedData.at(k).mass);
              matchIndex=n;
            }
          }
          if(dif<deltaM){
            if(k==maxPeak) matchMaxPeak=matchIndex;
            intensity = (float)mercury->FixedData.at(k).data/100;
            pv.AddMatch(matchIndex,mercury->FixedData.at(k).mass,intensity);
            fracAbun+=mercury->FracAbunData.at(k).data;
            //distArea+=peaks.at(matchIndex).intensity;
            distArea+=mercury->FixedData.at(k).data/100;
            matchCount++;
          } else {
            intensity = (float)mercury->FixedData.at(k).data/100;
            distArea+=mercury->FixedData.at(k).data/100;
            pv.AddExtra(mercury->FixedData.at(k).mass,intensity);
            mismatchCount++;
          }
        }

        if(strcmp(userParams.formula,"")==0) predMonoMass=averagine->getMonoMass();
        else predMonoMass=mercury->getZeroMass();

        //Record additional information
        pv.SetCharge(predPeak->at(i).GetCharge(j));
        pv.SetMonoMass(predMonoMass+(shft*predPeak->at(i).GetCharge(j)));
        pv.SetFormula(v);
        pv.SetVariant(var.at(q));
        pv.SetArea(distArea);

        //Add this variant to our list for this peptide.
        switch(userParams.sl){
        case 1:
          if(fracAbun>0.75) p.AddVariant(pv);
          break;
        case 2:
          if(fracAbun>0.50) p.AddVariant(pv);
          break;
        case 3:
          if(matchCount>1) p.AddVariant(pv);
          break;
        default:
          if(matchCount>mismatchCount) p.AddVariant(pv);
          break;
        }

      }
    }

    //Add peptide prediction to the list.
    if(p.VariantListSize()>0) {
      p.SetIntensity(predPeak->at(i).GetIntensity());
      p.SetMaxPeakIndex(matchMaxPeak);
      predPep->push_back(p);
    }
		
    //if we reach the maximum peptides allowed, stop making predictions
    if(predPep->size() == userParams.peptide) {
      return;
    }
  }

  return;

}

//This function looks at the peaks, determines which ones might be part of a distribution
//and creates an array of m/z values, intensities, and charges that will later be made
//into peptide models
int CSpecAnalyze::PredictPeptides(){
	int i,j;
	CPeakPrediction p;
  Spectrum s;

	if(peaks.size()>1){

    /*
    if(peaks.size()>25){
      peaks.sortIntensity();
      for(i=0;i<25;i++) s.add(peaks.at(i));
      peaks=s;
      peaks.sortMZ();
    }
    */    

		switch(userParams.chargeMode){
		case 'C':
			for(i=0;i<peaks.size();i++){
				if(peaks.at(i).intensity==0) continue;
				p.SetMZ(peaks.at(i).mz);
				p.SetIntensity(peaks.at(i).intensity);
				for(j=userParams.minCharge;j<=userParams.maxCharge;j++) p.AddCharge(j);
				predPeak->push_back(p);
			}
			break;
		case 'F':
		case 'P':
		case 'S':
			TraditionalCharges();
			if(charges->size()==0) break;
			for(i=0;i<peaks.size();i++){
				if(peaks.at(i).intensity==0) continue;
				p.SetMZ(peaks.at(i).mz);
				p.SetIntensity(peaks.at(i).intensity);
				for(j=0;j<(int)charges->size();j++) p.AddCharge(charges->at(j));
				predPeak->push_back(p);
			}
			break;
		case 'Q':
		default:
			chargeState();
			break;
		}
	}

  return (int)predPeak->size();

}

//Reduces the peaks to only those we will be using
void CSpecAnalyze::removePeaksBelowSN(){
	int i;

	//cout << "BasePeakSN: " << basePeak << endl;
	//Note: The FirstDerivativePeaks function sets a new basePeak intensity based on a gaussian fit
	//The SNCutoff, however, is based on the RAW value.
	//They are different (by no more than 10%), and thus the cutoff has changed.

  /* Old method uses erase function. New method avoids this to gain a little speed
	for(i=0;i<peaks.size();i++){
		if( (peaks.at(i).intensity / basePeak * 100) < S2NCutoff) {
			peaks.erase(i);
			i--;
		}
	}
  */

  Spectrum s;
  for(i=0;i<peaks.size();i++){
		if( (peaks.at(i).intensity / basePeak * 100) >= S2NCutoff) s.add(peaks.at(i));
	}
  peaks=s;
}
void CSpecAnalyze::setAveragine(CAveragine *a){
	averagine=a;
}

void CSpecAnalyze::setMercury(CMercury8 *m){
	mercury=m;
}

void CSpecAnalyze::setParams(const CHardklorSetting& sett){
	userParams=sett;
}

void CSpecAnalyze::setSpectrum(Spectrum& s){
  spec=&s;
}

//For using FFT or Patterson for charge states
void CSpecAnalyze::TraditionalCharges(){

	if(spec->size()==0) {
		cout << "Cannot find charge state(s) because spectrum is empty." << endl;
		exit(6);
	}

	//Find left and right boundaries in original spectrum
	double lowMz=peaks[0].mz-0.25;
	double highMz=peaks[peaks.size()-1].mz+0.25;
	int lowIndex=binarySearch(lowMz);
	int highIndex=binarySearch(highMz);

	//fix resolution
	double mz = peaks.at(peaks.size()/2).mz;
  double deltaM = calcFWHM(mz);
	deltaM/=4;

	//Find the charges
	SenkoCharge(charges,*spec,lowIndex,highIndex,userParams.minCharge,userParams.maxCharge,deltaM,userParams.chargeMode);

}

int CSpecAnalyze::binarySearch(double mz){
	int lower,mid,upper;
	int sz=spec->size();
	double dif;

	mid=sz/2;
	lower=0;
	upper=sz;
	
	while(true){
		if(lower>=upper) break;
		if(mz<spec->at(mid).mz){
			upper=mid-1;
			mid=(lower+upper)/2;
		} else {
			lower=mid+1;
			mid=(lower+upper)/2;
		}
		if(mid==sz) {
			mid--;
			break;
		}
	}

	if(mid==0 || mid==sz-1) return mid;
	if(mz<spec->at(mid).mz){
		dif=spec->at(mid).mz-mz;
		if(mz-spec->at(mid-1).mz < dif) return mid-1;
	} else {
		dif=mz-spec->at(mid).mz;
		if(spec->at(mid+1).mz-mz < dif) return mid+1;
	}
	return mid;
}
