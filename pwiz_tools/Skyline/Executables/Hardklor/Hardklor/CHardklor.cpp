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

/*
   Hardklör - Thanks to Aaron Klammer (Klezmer) and 
              Greg Finney for the name suggestions.

   This program scans through high resolution mass spectra,
   predicts the monoisotopic mass and charge state of any potential
   peptides (or proteins), generates predicted isotope distributions 
	 of averagine and modified averagine molecules matching the mass 
	 of each distribution, then correlates the various combinations of each 
   distribution with the spectrum data to find a best match.

   This application was first designed to identify chlorinated peptides,
	 hence the name Hardklör. Due to the distinguishing characteristics of 
	 a chlorinated distribution, this application makes it possible to predict
   if the observed distribution is in fact chlorinated. The application
	 has since been extended to differentiate any measurable atomic or
	 enrichment modification.

   The application utilizes an object model, so components may
   be used in other applications. Furthermore, the design was made
   to be extended, or possibly embedded in other software.

   Version 1.0 is the first public release.
*/
	
#include "CHardklor.h"
#include "CAveragine.h"
#include "CMercury8.h"
#include "CSpecAnalyze.h"
#include "Smooth.h"
#include <iomanip>

using namespace std;
using namespace MSToolkit;

SSObject::SSObject(){
	pepVar = new vector<sInt>;
	corr=0;
}

SSObject::SSObject(const SSObject& c){
  corr = c.corr;
	pepVar = new vector<sInt>;
  for(unsigned int i=0;i<c.pepVar->size();i++) pepVar->push_back(c.pepVar->at(i));
}

SSObject::~SSObject(){
	delete pepVar;
}

SSObject& SSObject::operator=(const SSObject& c){
  if (this != &c) {	
    corr = c.corr;
		delete pepVar;
		pepVar = new vector<sInt>;
    for(unsigned int i=0;i<c.pepVar->size();i++)	pepVar->push_back(c.pepVar->at(i));
  }
  return *this;
}

void SSObject::addVar(int a, int b){
	sInt s;
	s.iLower = a;
	s.iUpper = b;
	pepVar->push_back(s);
}

void SSObject::clear(){
	delete pepVar;
	pepVar = new vector<sInt>;
	corr = 0;
}

CHardklor::CHardklor(){
	averagine=NULL;
	mercury=NULL;
	bEcho=true;
  bMem=false;
}

CHardklor::CHardklor(CAveragine *a, CMercury8 *m){
  averagine=a;
  mercury=m;
  sa.setAveragine(averagine);
  sa.setMercury(mercury);
	bEcho=true;
  bMem=false;
}

CHardklor::~CHardklor(){
	averagine=NULL;
	mercury=NULL;
}

void CHardklor::Echo(bool b){
	bEcho=b;
}

void CHardklor::SetAveragine(CAveragine *a){
	averagine=a;
	sa.setAveragine(averagine);
}

void CHardklor::SetMercury(CMercury8 *m){
	mercury=m;
	sa.setMercury(mercury);
}

int CHardklor::GoHardklor(CHardklorSetting sett, Spectrum* s){
	cs = sett;
  Analyze(s);
  return 0;
}

//sorts vector of Results from high to low
int CHardklor::compareData(const void *p1, const void *p2){
  const Result d1 = *(Result *)p1;
  const Result d2 = *(Result *)p2;
  if(d1.data<d2.data) return 1;
  else if(d1.data>d2.data) return -1;
  else return 0;
}

void CHardklor::Analyze(Spectrum* s) {

  //Objects
  MSReader r;
  CNoiseReduction nr(&r,cs);
  PT=averagine->getPT();

  //iterators
  unsigned int k;   //counters for loops

  int TotalIterations=0;
  int TotalScans=0;
  int i;

  //Variables for file and clock management
  int minutes, seconds;
  loadTime=0;
  analysisTime=0;
  splitTime=0;
	
  //placeholders for data output to file
  //int pepID;
  //int varID;

  //Variables for some basic information output.
  int iPercent;
  int zeroPep=0;   //Stores the number of spectra from which no potential peptides could be identified
  int manyPep=0;   //Stores the number of spectra from which the number of potential peptides 
                   //exceeds the user-defined threshold.
  int lowSigPep=0; //Stores the number of spectra whose max intensity was below the user defined threshold.

  
  //Build variants (modifications) used in the analysis
	pepVariants.clear();
  if(!cs.noBase) {
    CHardklorVariant hkv;
    pepVariants.push_back(hkv);
  }
  for(k=0;k<cs.variant->size();k++)  pepVariants.push_back(cs.variant->at(k));

  //Our temporary spectrum
  Spectrum curSpec;
  Spectrum cutSpec;

  //These are for splitting large spectra
  bool bCutMe=false;
  bool ReadScan=false;
  bool bFirst=true;
  CSplitSpectrum* cSS;
  int winCount=0;

  vResults.clear();

  //Ouput file info to user
	if(bEcho){
		if(s==NULL) cout << "Reading from file: " << cs.inFile << endl;
		if(!bMem) cout << "Writing to file: " << cs.outFile << endl;
	}

  if(cs.fileFormat==dunno) {
    cout << "Unknown file format or bad extension." << endl;
    return;
  }

  //Start of algorithm here.
  getTimerFrequency(timerFrequency);
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

		  //this is the command to filter by persistence without boxcar averaging
		  //    if(!nr.DeNoise(curSpec)) curSpec.setScanNumber(0);

	  } else {
		  if(cs.boxcarFilter>0){
			  if(!nr.DeNoiseC(curSpec)) curSpec.setScanNumber(0);
		  } else {
			  if(!nr.DeNoiseD(curSpec)) curSpec.setScanNumber(0);
		  }
    }
  }

  TotalScans++;

  getExactTime(stopTime);
  tmpTime1=toMicroSec(stopTime);
  tmpTime2=toMicroSec(startTime);
  loadTime=tmpTime1-tmpTime2;

  //Check that file was read
  if(curSpec.getScanNumber()==0) {
    if(s!=NULL) {
      cout << "Spectrum is invalid." << endl;
      return;
    }
    if(cs.scan.iLower>0) cout << cs.inFile << " is invalid, or requested scan number is of incorrect format." << endl;
    else cout << cs.inFile << " is invalid, or contains no spectrum." << endl;
    return;
  }
  currentScanNumber = curSpec.getScanNumber();

  //Open a file for output. Results will be written on the fly as they come out of the algorithm.
  if(!bMem){
    fptr.open(&cs.outFile[0],ios::out|ios::app);
    if(!fptr.good()) cout << "Output file error" << endl;
    fptr << setiosflags(ios::fixed) << setprecision(4);
  }

  if(!bMem && cs.xml){
    fptr << "<Hardklor>" << endl;
    fptr << "<File InputFilename=\"" << cs.inFile << "\" OutputFilename=\"" << cs.outFile << "\">" << endl;
    WriteParams(fptr,1);
  }
 
  //Reset some basic stat counters
  manyPep=0;
  zeroPep=0;
  lowSigPep=0;

  //Set and display on screen percentage counter
  iPercent=0;
  if(bEcho) cout << iPercent << "% ";
  
  //While there is still data to read in the file.
  while(true){

		//Load next spectrum to analyze
		if(!bFirst){
			
			//close out xml tag for previous spectrum
			if(!bMem && cs.xml) fptr << "</Spectrum>" << endl;

      if(s!=NULL) break;

		  //track load time
		  getExactTime(startTime);
		  if(cs.boxcar==0){
			  r.readFile(NULL,curSpec);
			  //case 1: nr.DeNoise(curSpec); break;
		  } else {
			  if(cs.boxcarFilter>0) nr.DeNoiseC(curSpec);
			  else nr.DeNoiseD(curSpec);
      }
		  getExactTime(stopTime);
		  tmpTime1=toMicroSec(stopTime);
		  tmpTime2=toMicroSec(startTime);
		  loadTime+=tmpTime1-tmpTime2;	

		} else {

			bFirst=false; //mark that the first scan has been processed

		}

		//Write scan information to output file.
		if(curSpec.getScanNumber()!=0){	
      if(!bMem){
			  if(cs.scan.iUpper>0 && curSpec.getScanNumber()>cs.scan.iUpper) break;
			  if(cs.reducedOutput) WriteScanLine(curSpec,fptr,2);
			  else if(cs.xml) WriteScanLine(curSpec,fptr,1);
			  else WriteScanLine(curSpec,fptr,0);
      } else {
        currentScanNumber = curSpec.getScanNumber();
      }
		} else {
			break; //exit if there is no spectrum left to analyze
		}
		TotalScans++;

		//Preprocess spectrum
		//If we have an empty spectrum, go on to the next one
    if(curSpec.size()==0) continue;

		getExactTime(startTime);
    if(cs.smooth>0) SG_Smooth(curSpec,cs.smooth,4);
		//if(curSpec.getScanNumber()==1999){
		//	for(int xx=0;xx<curSpec.size();xx++) cout << curSpec[xx].mz << "\t" << curSpec[xx].intensity << endl;
		//}

    //Check our spectrum for file type. Zoom and UltraZoom scans do not
    //need splitting.
    if(cs.fileFormat==zs || cs.fileFormat==uzs){

      sa.clear();
			sa.setParams(cs);
      sa.setSpectrum(curSpec);
			sa.FindPeaks();
			sa.PredictPeptides();

			//do not analyze spectrum with 0 predicted peaks; Keep count of these occurrances.
			if(sa.predPeak->size()==0){
				zeroPep++;
				continue;
			}

			AnalyzePeaks(sa);

    } else {
      	
			//Reinitialize our split spectrum
			cSS=new CSplitSpectrum(&curSpec,cs);
			cSS->SetAveragine(averagine);
			cSS->SetMercury(mercury);

			//Check if data already centroided
			if(cs.centroid) {
				if(cs.chargeMode=='F' || cs.chargeMode=='P' || cs.chargeMode=='S'){
					cout << "-cdm settings of F, P, and S (FFT, Patterson, Senko) only work on profile data." << endl;
					cout << "Please choose settings of Q or C (QuickCharge, Complete)" << endl;
					exit(5);
				}
				//This function directly copies the already centroided spectra to the CSS object
				cSS->Centroid(curSpec);

			//if not, process the data here (ultimately includes centroiding)
			} else {
				if(cs.boxcarFilter>0){ //TODO: Figure out how filtering interferes here
          cs.sn=0;
					cSS->Centroid(curSpec);
				} else if(cs.staticSN) {
					if(cs.sn==0) cSS->NoSplitAnalysis();
					else cSS->NewSNPass(cs.snWindow);
		  	} else {
			  	cSS->OverlappingAnalysis(cs.snWindow);
				 	if(cs.iAnalysis) cSS->IntersectionAnalysis();
				  else cSS->UnionAnalysis();
				}
			}
		
			//Split the spectrum
		  cSS->MakeAnalysis(cs.winSize);

		  //Analyze each window
		  for(winCount=0;winCount<cSS->getNumWindows();winCount++){
			  sa = cSS->getWindow(winCount);
			  sa.setParams(cs);
			  sa.PredictPeptides();	

			  //do not analyze spectrum with 0 predicted peaks; Keep count of these occurrances.
			  if(sa.predPeak->size()==0){
				  zeroPep++;
				  continue;
			  }

			  AnalyzePeaks(sa);

		  }

			//clean up cSS object
			delete cSS;

		}

    //Update the percentage indicator
		if(bEcho){
			if (r.getPercent() > iPercent){
/* BSP
				if(iPercent<10) cout << "\b";
				else cout << "\b\b";
				cout.flush();
*/
				iPercent=r.getPercent();
				cout << iPercent << "% ";
				cout.flush();
			}
		}

		if(!bMem && cs.xml) {
			fptr << "</Spectrum>" << endl;
			fptr << "</File>" << endl;
			fptr << "</Hardklor>" << endl;
		}

	} //loop to next spectrum (while)
  
  //Close the output file and clear it so it can be reused.
  if(!bMem){
    fptr.close();
    fptr.clear();
  }

	if(bEcho) {
		cout << "\n" << endl;

		switch(cs.algorithm){
		case SemiComplete:
			cout << "SemiComplete analysis:" << endl;
			break;
		case SemiCompleteFast:
			cout << "SemiCompleteFast analysis:" << endl;
			break;
		case Dynamic:
			cout << "Dynamic analysis:" << endl;
			break;
		case DynamicSemiComplete:
			cout << "DynamicSemiComplete analysis:" << endl;
			break;
		case SemiSubtractive:
			cout << "SemiSubtractive analysis:" << endl;
			break;
		case FewestPeptides:
			cout << "FewestPeptides analysis:" << endl;
			break;
		case FewestPeptidesChoice:
			cout << "FewestPeptidesChoice analysis:" << endl;
			break;
		case FastFewestPeptides:
			cout << "FastFewestPeptides analysis:" << endl;
			break;
		case FastFewestPeptidesChoice:
			cout << "FastFewestPeptidesChoice analysis:" << endl;
			break;
		case Basic:
		default:
			cout << "Basic analysis:" << endl;
			break;
		}
	}
  
  //Output the simple statistics
	if(bEcho) cout << "  Total number of scans analyzed: " << TotalScans << endl;
  //cout << "  Number of (sub)scans not analyzed:" << endl;
  //cout << "    No Peptides Predicted: " << zeroPep << endl;
  //cout << "    Intensity Below Limit: " << lowSigPep << endl;
  //cout << "  Number of scans with more predicted peptides than were analyzed: " << manyPep << endl;
	//cout << "  Total Correlations Made: " << TotalIterations << endl;

	//Output the time to analyze this file.
	if(bEcho){
		i=(int)timeToSec(loadTime,timerFrequency);
		minutes = (int)(i/60);
		seconds = i - (60*minutes);
		cout << "\nFile access time: " << minutes << " minutes, " << seconds << " seconds." << endl;
		i=(int)timeToSec(splitTime,timerFrequency);
		minutes = (int)(i/60);
		seconds = i - (60*minutes);
		cout << "Split Time:       " << minutes << " minutes, " << seconds << " seconds." << endl;
		i=(int)timeToSec(analysisTime,timerFrequency);
		minutes = (int)(i/60);
		seconds = i - (60*minutes);
		cout << "Analysis Time:    " << minutes << " minutes, " << seconds << " seconds." << endl;
/* BSP user doesn't have a lot of control over performance under Skyline invocation
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
*/
	}

	PT=NULL;

}

bool CHardklor::AnalyzePeaks(CSpecAnalyze& sa){
	
	int i;
	SSObject bsso;

	//Arrays for combinatorial analysis
  float *match;
  float *mismatch;

	//Track analysis times
	getExactTime(startTime);

  //Make an averagine distribution for every variant specified in the conf file.
	sa.MakePredictions(pepVariants);

	//if there are no possible peptides, stop the analysis here.
	if(sa.predPep->size()==0) {
		getExactTime(stopTime);
		tmpTime1=toMicroSec(stopTime);
		tmpTime2=toMicroSec(startTime);
		analysisTime+=tmpTime1-tmpTime2;
		return false;
	}

	//Restructure mismatch arrays for faster correlation analysis
	sa.BuildMismatchArrays();
 
  //Send the distributions, and the observed data points from the spectrum to a recursive
  //algorithm that will sum up every combination of every peptide in each of its chlorinated
  //forms to find the combination that best fits the data.

	//Dimension our arrays
	match = new float[sa.peaks.size()];
	for(i=0;i<sa.peaks.size();i++) match[i]=0;

	if(sa.mismatchSize>0){
		mismatch = new float[sa.mismatchSize];
		for(i=0;i<sa.mismatchSize;i++) mismatch[i]=0;
	} else {
		mismatch = new float[1];
		mismatch[0] = 0;
	}

	//clear prior data and select analysis algorithm
	bsso.clear();
	switch(cs.algorithm){
	case SemiComplete:
		SemiCompleteMethod(match,mismatch,&bsso,1,cs.depth,0);
		break;
	case SemiCompleteFast:
		SemiCompleteFastMethod(match,mismatch,&bsso,1,cs.depth,0);
		break;
	case Dynamic:
		DynamicMethod(match,mismatch,&bsso,1,cs.depth,0,0);
		break;
	case DynamicSemiComplete:
		DynamicSemiCompleteMethod(match,mismatch,&bsso,1,cs.depth,0,0);
		break;
	case SemiSubtractive:
		SemiSubtractiveMethod(&bsso,cs.depth);
		break;
	case FewestPeptides:
		FewestPeptidesMethod(&bsso,cs.depth);
		break;
	case FewestPeptidesChoice:
		FewestPeptidesChoiceMethod(&bsso,cs.depth);
		break;
	case FastFewestPeptides:
		FastFewestPeptidesMethod(&bsso,cs.depth);
		break;
	case FastFewestPeptidesChoice:
		FastFewestPeptidesChoiceMethod(&bsso,cs.depth);
		break;
	case Basic:
	default:
		BasicMethod(match,mismatch,&bsso,1,cs.depth,(int)sa.predPep->size()-1);
		break;
	}

	//Clean up memory
	delete [] match;
	delete [] mismatch;

	//Track analysis times
	getExactTime(stopTime);
  tmpTime1=toMicroSec(stopTime);
  tmpTime2=toMicroSec(startTime);
	analysisTime+=tmpTime1-tmpTime2;
		
	//if we exceeded our threshold, output the data to file or store it in memoryt
  if(bsso.corr > cs.corr) {
    if(!bMem){
		  if(cs.reducedOutput) WritePepLine(bsso,PT,fptr,2);
      else if(cs.xml) WritePepLine(bsso,PT,fptr,1);
      else WritePepLine(bsso,PT,fptr,0);
    } else {
      ResultToMem(bsso,PT);
    }
  }
    
	return true;
}

double CHardklor::LinReg(float *match, float *mismatch){

  int i,sz;
  double sxx=0,syy=0,sxy=0;
  vector<sFloat> v;
  sFloat tmp;

	//Correlate matches
	for(i=0;i<sa.peaks.size();i++){
		tmp.fLower = sa.peaks.at(i).intensity;
		tmp.fUpper = match[i];
		v.push_back(tmp);
  }

	//Correlate mismatches with 0
	for(i=0;i<sa.mismatchSize;i++){
		if(mismatch[i]>0){
			tmp.fLower = 0;
			tmp.fUpper = mismatch[i];
			v.push_back(tmp);
		}
  }

  //Cosine angle correlation
  sxy=0;
  sxx=0;
  syy=0;
	sz=(int)v.size();
  for(i=0;i<sz;i++){
    sxy += (v.at(i).fLower*v.at(i).fUpper);
    sxx += (v.at(i).fLower*v.at(i).fLower);
    syy += (v.at(i).fUpper*v.at(i).fUpper);
  }

  if(sxx>0 && syy>0 && sxy>0) return sxy/sqrt(sxx*syy);
  else return 0;
    
}


void CHardklor::BasicMethod(float *match, float *mismatch,SSObject *combo, 
			     int depth, int maxDepth, int start){

  SSObject bestCombo = *combo;
  SSObject recCombo;
  
  double RCorr;
  double bestRCorr = combo->corr;
  int a,k,n;
	float intensity;

	float *sumMatch;
	float *sumMismatch;

	sumMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
	};
	
  //Iterate through all predicted peptides
  for(k=start; k>-1; k--) {

    //check each variant
    for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
			intensity = sa.predPep->at(k).GetIntensity() - match[sa.predPep->at(k).GetMaxPeakIndex()];

			if(intensity<0) {
				//This predicted peptide cannot contribute to the analysis, so don't go
				//any deeper
				delete [] sumMatch;
				delete [] sumMismatch;
				return;
			};

			//Add the variant to the distribution being analyzed
			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity*intensity + match[a];
			};

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity*intensity + mismatch[a];
			};
      
      //Correlate this combined distribution with the mass spec data.
			//SSIterations++;
      RCorr = LinReg(sumMatch,sumMismatch);

			//cout << RCorr << endl;
      
			recCombo = *combo;
			recCombo.addVar(k,n);
			recCombo.corr = RCorr;

			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

			//Check recursions
			if(depth<maxDepth){
				BasicMethod(sumMatch,sumMismatch,&recCombo,depth+1,maxDepth,k-1);
			};

			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
    };
  };

	delete [] sumMatch;
	delete [] sumMismatch;
	*combo = bestCombo;
  
};

void CHardklor::SemiCompleteMethod(float *match, float *mismatch,SSObject *combo, 
			     int depth, int maxDepth, int start){

  SSObject bestCombo = *combo;
  SSObject recCombo;
  
  double RCorr;
  double bestRCorr = combo->corr;
  int a,n;
  unsigned int k;

	float *sumMatch;
	float *sumMismatch;

	sumMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
	};
	
  //Iterate through all predicted peptides
  for(k=start; k<sa.predPep->size(); k++) {

    //check each variant
    for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
			//Add the variant to the distribution being analyzed
			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
			};

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
			};
      
      //Correlate this combined distribution with the mass spec data.
			//SSIterations++;
      RCorr = LinReg(sumMatch,sumMismatch);
      
			recCombo = *combo;
			recCombo.addVar(k,n);
			recCombo.corr = RCorr;


			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
		
    }
  }

	//If we reached threshold, stop here without recursion
	if(bestCombo.corr>cs.corr) {
		delete [] sumMatch;
		delete [] sumMismatch;
		*combo = bestCombo;
		return;
	}

	if(depth<maxDepth){

		//Iterate through all predicted peptides
		for(k=start; k<sa.predPep->size(); k++) {

			//check each variant
			for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
				//Add the variant to the distribution being analyzed
				//Do matches first
				for(a=0;a<sa.peaks.size();a++){
					sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
				}

				//Now add mismatches
				for(a=0;a<sa.mismatchSize;a++){
					sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
				}
      
				recCombo = *combo;
				recCombo.addVar(k,n);
				recCombo.corr = RCorr;

				//Check recursions
				SemiCompleteMethod(sumMatch,sumMismatch,&recCombo,depth+1,maxDepth,k+1);

				//Check if it is the best, if so, mark it
				if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
			}
    }
  }

	delete [] sumMatch;
	delete [] sumMismatch;
	*combo = bestCombo;
  
}

void CHardklor::SemiCompleteFastMethod(float *match, float *mismatch,SSObject *combo, 
			     int depth, int maxDepth, int start){

  SSObject bestCombo = *combo;
  SSObject recCombo;
  
  double RCorr;
  double bestRCorr = combo->corr;
  int a,b,n;
  unsigned int k;

	int numArrays=0;
	for(k=start;k<sa.predPep->size();k++){
		numArrays+=sa.predPep->at(k).VariantListSize();
	}

	float **sumMatch;
	float **sumMismatch;

	sumMatch = new float* [numArrays];
	sumMismatch = new float* [numArrays];
	for(n=0;n<numArrays;n++){
		sumMatch[n] = new float[sa.peaks.size()];
		if(sa.mismatchSize>0){
			sumMismatch[n] = new float[sa.mismatchSize];
		} else {
			sumMismatch[n] = new float[1];
		}
	}
	
	b=0;
  //Iterate through all predicted peptides
  for(k=start; k<sa.predPep->size(); k++) {

    //check each variant
    for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
			//Add the variant to the distribution being analyzed
			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatch[b][a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
			}

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatch[b][a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
			}
      
      //Correlate this combined distribution with the mass spec data.
			//SSIterations++;
      RCorr = LinReg(sumMatch[b],sumMismatch[b]);
      
			recCombo = *combo;
			recCombo.addVar(k,n);
			recCombo.corr = RCorr;


			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
			b++;
    }
  }

	//If we reached threshold, stop here without recursion
	if(bestCombo.corr>cs.corr) {
		for(n=0;n<numArrays;n++) {
			delete [] sumMatch[n];
			delete [] sumMismatch[n];
		};
		delete [] sumMatch;
		delete [] sumMismatch;

		*combo = bestCombo;
		return;
	}

	if(depth<maxDepth){

		//Iterate through all predicted peptides
		b=0;
		for(k=start; k<sa.predPep->size(); k++) {

			//check each variant
			for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
				recCombo = *combo;
				recCombo.addVar(k,n);
				recCombo.corr = RCorr;

				//Check recursions
				SemiCompleteFastMethod(sumMatch[b],sumMismatch[b],&recCombo,depth+1,maxDepth,k+1);

				//Check if it is the best, if so, mark it
				if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
				b++;
			}
    }
  }

	for(n=0;n<numArrays;n++) {
		delete [] sumMatch[n];
		delete [] sumMismatch[n];
	}
	delete [] sumMatch;
	delete [] sumMismatch;
	*combo = bestCombo;
  
}


void CHardklor::DynamicMethod(float *match, float *mismatch,SSObject *combo, 
			     int depth, int maxDepth, int start, double corr){

  SSObject bestCombo = *combo;
  SSObject recCombo;
  
  double RCorr;
  double bestRCorr = combo->corr;
  int a,n;
  unsigned int k;

	float *sumMatch;
	float *sumMismatch;

	sumMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
	};
	
  //Iterate through all predicted peptides
  for(k=start; k<sa.predPep->size(); k++) {

    //check each variant
    for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
			//Add the variant to the distribution being analyzed
			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
			};

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
			};
      
      //Correlate this combined distribution with the mass spec data.
			//SSIterations++;
      RCorr = LinReg(sumMatch,sumMismatch);

			//cout << "RCorr = " << RCorr << endl;
      
			recCombo = *combo;
			recCombo.addVar(k,n);
			recCombo.corr = RCorr;

			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

			//Check recursions if better than previously
			if(depth < maxDepth && RCorr > corr){
				DynamicMethod(sumMatch,sumMismatch,&recCombo,depth+1,maxDepth,k+1,RCorr);
				if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
			};
      
    };
  };

	delete [] sumMatch;
	delete [] sumMismatch;
	*combo = bestCombo;
  
};


void CHardklor::DynamicSemiCompleteMethod(float *match, float *mismatch,SSObject *combo, 
			     int depth, int maxDepth, int start, double corr){

  SSObject bestCombo = *combo;
  SSObject recCombo;
  
  double RCorr;
  double bestRCorr = combo->corr;
  int a,b,n;
  unsigned int k;

	vector<double> vecCorr;

	float *sumMatch;
	float *sumMismatch;

	sumMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
	}
	
  //Iterate through all predicted peptides
	b=0;
  for(k=start; k<sa.predPep->size(); k++) {

    //check each variant
    for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
			//Add the variant to the distribution being analyzed
			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
			}

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
			}
      
      //Correlate this combined distribution with the mass spec data.
			//SSIterations++;
      RCorr = LinReg(sumMatch,sumMismatch);
      
			recCombo = *combo;
			recCombo.addVar(k,n);
			recCombo.corr = RCorr;

			vecCorr.push_back(RCorr);
			b++;

			//Check if it is the best, if so, mark it
			if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
    }
  }

	//If we reached threshold, stop here without recursion
	if(bestCombo.corr>cs.corr) {
		delete [] sumMatch;
		delete [] sumMismatch;
		*combo = bestCombo;
		return;
	}


	//Otherwise, if we're not at the maximum depth, iterate
	b=0;
	if(depth < maxDepth) {

		//Iterate through all predicted peptides
		for(k=start; k<sa.predPep->size(); k++) {

			//check each variant
			for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){

				if(vecCorr.at(b) > corr) {
      
					//Add the variant to the distribution being analyzed
					//Do matches first
					for(a=0;a<sa.peaks.size();a++){
						sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + match[a];
					}

					//Now add mismatches
					for(a=0;a<sa.mismatchSize;a++){
						sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + mismatch[a];
					}
      
					recCombo = *combo;
					recCombo.addVar(k,n);
					recCombo.corr = vecCorr.at(b);

					//Check recursions
					DynamicSemiCompleteMethod(sumMatch,sumMismatch,&recCombo,depth+1,maxDepth,k+1,vecCorr.at(b));

					//Check if it is the best, if so, mark it
					if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

				}
				b++;
      
			}
		}

	}

	delete [] sumMatch;
	delete [] sumMismatch;
	*combo = bestCombo;
  
};

void CHardklor::SemiSubtractiveMethod(SSObject *combo, int maxDepth){

  SSObject bestCombo = *combo;
  SSObject recCombo;
	SSObject depthCombo;
  
  double RCorr;
  int a,n;
  unsigned int k;
	bool bSkip;

	int depth=0;

	float *sumMatch;
	float *sumMismatch;
	float *sumMatchMem;
	float *sumMismatchMem;

	sumMatch = new float[sa.peaks.size()];
	sumMatchMem = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
		sumMismatchMem = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
		sumMismatchMem = new float[1];
	}

	for(a=0;a<sa.peaks.size();a++){
		sumMatchMem[a]=0;
	}
	for(a=0;a<sa.mismatchSize;a++){
		sumMismatchMem[a]=0;
	}

	while(depth < maxDepth) {

		cout << "Depth: " << depth << " of " << maxDepth << endl;

		depthCombo = bestCombo;

		//Iterate through all predicted peptides
		for(k=0; k<sa.predPep->size(); k++) {

			//Skip peptides we have already analyzed.
			bSkip=false;
			for(n=0;n<(int)bestCombo.pepVar->size();n++){
				if(bestCombo.pepVar->at(n).iLower==k) {
					bSkip=true;
					break;
				}
			}
			if(bSkip) continue;

			//check each variant
			for(n=0; n<sa.predPep->at(k).VariantListSize(); n++){
      
				//Add the variant to the distribution being analyzed
				//Do matches first
				for(a=0;a<sa.peaks.size();a++){
					sumMatch[a]=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity + sumMatchMem[a];
				}

				//Now add mismatches
				for(a=0;a<sa.mismatchSize;a++){
					sumMismatch[a]=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity + sumMismatchMem[a];
				}
      
				//Correlate this combined distribution with the mass spec data.
				//SSIterations++;
		    RCorr = LinReg(sumMatch,sumMismatch);
				cout << RCorr << endl;
      
				recCombo = depthCombo;
				recCombo.addVar(k,n);
				recCombo.corr = RCorr;

				//Check if it is the best, if so, mark it
				if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;
      
			}

		}

		//If we reached threshold, stop here without recursion
		if(bestCombo.corr>cs.corr) {
			delete [] sumMatch;
			delete [] sumMismatch;
			delete [] sumMatchMem;
			delete [] sumMismatchMem;
			*combo = bestCombo;
			return;
		}

		if(bestCombo.corr>0) {
			//Copy the best correlated data for the next iteration
			k = bestCombo.pepVar->at(bestCombo.pepVar->size()-1).iLower;
			n = bestCombo.pepVar->at(bestCombo.pepVar->size()-1).iUpper;

			//Do matches first
			for(a=0;a<sa.peaks.size();a++){
				sumMatchMem[a]+=sa.predPep->at(k).GetVariant(n).GetMatch(a).intensity;
			}

			//Now add mismatches
			for(a=0;a<sa.mismatchSize;a++){
				sumMismatchMem[a]+=sa.predPep->at(k).GetVariant(n).GetMismatch(a).intensity;
			}
		}

		depth++;

	}

	delete [] sumMatch;
	delete [] sumMismatch;
	delete [] sumMatchMem;
	delete [] sumMismatchMem;
	*combo = bestCombo;
  
}


void CHardklor::FewestPeptidesMethod(SSObject *combo, int maxDepth){

  SSObject recCombo;
	SSObject bestCombo = *combo;
  
  double RCorr;
  int a,b,j,k,n,pos;
	int pepID,varID;

	int depth=0;
	int countDown=0;
	int comboListCounter=0;

	float intensity;

	float *sumMatch;
	float *sumMismatch;
	float *priorMatch;
	float *priorMismatch;

	sumMatch = new float[sa.peaks.size()];
	priorMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
		priorMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
		priorMismatch = new float[1];
	};

	vector<SSObject> comboList;
	comboList.push_back(*combo);
	comboListCounter++;

	/*
	for(j=0;j<sa.peaks.size();j++){
		cout << sa.peaks.at(j).mz << " " << sa.peaks.at(j).intensity << endl;
	};
	*/

	while(depth < maxDepth && depth < (int)sa.predPep->size()) {

		countDown=comboListCounter;
		comboListCounter=0;
		pos=0;

		//scan through current list of combos
		while(pos < countDown){

			//cout << depth+1 << " of " << maxDepth << "\t" << pos << " of " << countDown << " with " << sa.predPep->size() << endl;

			//clear old priors data 
			for(a=0;a<sa.peaks.size();a++) priorMatch[a]=0;
			for(a=0;a<sa.mismatchSize;a++) priorMismatch[a]=0;

			//recreate prior match and mismatch data
			for(n=0;n<(int)comboList.at(pos).pepVar->size();n++){
				pepID = comboList.at(pos).pepVar->at(n).iLower;
				varID = comboList.at(pos).pepVar->at(n).iUpper;

				intensity = sa.predPep->at(pepID).GetIntensity() - priorMatch[sa.predPep->at(pepID).GetMaxPeakIndex()];

				for(a=0;a<sa.peaks.size();a++){
					priorMatch[a]+=sa.predPep->at(pepID).GetVariant(varID).GetMatch(a).intensity*intensity;
				};
				for(a=0;a<sa.mismatchSize;a++){
					priorMismatch[a]+=sa.predPep->at(pepID).GetVariant(varID).GetMismatch(a).intensity*intensity;
				};
			};

			if(comboList.at(pos).pepVar->size()>0){
				b = comboList.at(pos).pepVar->at(comboList.at(pos).pepVar->size()-1).iLower - 1;
			} else {
				b = (int)sa.predPep->size()-1;
			};

			//add every peptide & every variant
			for(j=b;j>-1;j--){
	
				intensity = sa.predPep->at(j).GetIntensity() - priorMatch[sa.predPep->at(j).GetMaxPeakIndex()];

				if(intensity<0) {
					//This predicted peptide cannot contribute to the analysis, so don't go
					//any deeper
					continue;
				};

				//iterate through variants now
				for(k=0;k<sa.predPep->at(j).VariantListSize();k++){

					//sum matches and mismatches
					for(a=0;a<sa.peaks.size();a++){
						sumMatch[a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + priorMatch[a];
					};

					//Now add mismatches
					for(a=0;a<sa.mismatchSize;a++){
						sumMismatch[a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + priorMismatch[a];
					};

					//Correlate this combined distribution with the mass spec data.
					//SSIterations++;
					RCorr = LinReg(sumMatch,sumMismatch);
					//cout << RCorr << endl;
      
					recCombo = comboList.at(0);
					recCombo.addVar(j,k);
					recCombo.corr = RCorr;

					//Check if it is the best, if so, mark it
					if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

					//Add to our list of combos to analyze in the future
					if(depth<maxDepth-1){
						comboList.push_back(recCombo);
						comboListCounter++;
					};

				};//for k

			};//for j

			//delete our prior combo from the list
			//comboList.erase(comboList.begin());
			//countDown--;
			pos++;

		};//while countDown

		comboList.erase(comboList.begin(),comboList.begin()+countDown);

		//check best to see if it exceeded threshold
		if(bestCombo.corr>cs.corr) {
			delete [] sumMatch;
			delete [] sumMismatch;
			delete [] priorMatch;
			delete [] priorMismatch;
			*combo = bestCombo;
			return;
		};

		//otherwise, we iterate
		depth++;

	};//while depth

	//return the best we got
	delete [] sumMatch;
	delete [] sumMismatch;
	delete [] priorMatch;
	delete [] priorMismatch;
	*combo = bestCombo;
  
};

void CHardklor::FewestPeptidesChoiceMethod(SSObject *combo, int maxDepth){

  SSObject recCombo;
	SSObject bestCombo = *combo;
  
  double RCorr;
  int a,j,k,n;
	int pepID,varID;

	int depth=0;
	int countDown=0;
	int comboListCounter=0;
	int start;

	float intensity;

	float *sumMatch;
	float *sumMismatch;
	float *priorMatch;
	float *priorMismatch;

	sumMatch = new float[sa.peaks.size()];
	priorMatch = new float[sa.peaks.size()];
	if(sa.mismatchSize>0){
		sumMismatch = new float[sa.mismatchSize];
		priorMismatch = new float[sa.mismatchSize];
	} else {
		sumMismatch = new float[1];
		priorMismatch = new float[1];
	};

	vector<SSObject> comboList;
	comboList.push_back(*combo);
	comboListCounter++;

	while(depth < maxDepth && depth < (int)sa.predPep->size()) {

		countDown=comboListCounter;
		comboListCounter=0;

		//scan through current list of combos
		while(countDown>0){

			//clear old priors data
			for(a=0;a<sa.peaks.size();a++) priorMatch[a]=0;
			for(a=0;a<sa.mismatchSize;a++) priorMismatch[a]=0;

			//recreate prior match and mismatch data
			for(n=0;n<(int)comboList.at(0).pepVar->size();n++){
				pepID = comboList.at(0).pepVar->at(n).iLower;
				varID = comboList.at(0).pepVar->at(n).iUpper;

				intensity = sa.predPep->at(pepID).GetIntensity() - priorMatch[sa.predPep->at(pepID).GetMaxPeakIndex()];

				for(a=0;a<sa.peaks.size();a++){
					priorMatch[a]+=sa.predPep->at(pepID).GetVariant(varID).GetMatch(a).intensity*intensity;
				};
				for(a=0;a<sa.mismatchSize;a++){
					priorMismatch[a]+=sa.predPep->at(pepID).GetVariant(varID).GetMismatch(a).intensity*intensity;
				};
			};

			if(comboList.at(0).pepVar->size()>0) start = comboList.at(0).pepVar->at(comboList.at(0).pepVar->size()-1).iLower-1;
			else start = (int)sa.predPep->size()-1;

			//add every peptide & every variant
			for(j=start;j>-1;j--){
			
				intensity = sa.predPep->at(j).GetIntensity() - priorMatch[sa.predPep->at(j).GetMaxPeakIndex()];
				if(intensity<0) {
					//This predicted peptide cannot contribute to the analysis, so don't go
					//any deeper
					continue;
				};

				//iterate through variants now
				for(k=0;k<sa.predPep->at(j).VariantListSize();k++){

					//Add the variant to the distribution being analyzed
					//Do matches first
					for(a=0;a<sa.peaks.size();a++){
						sumMatch[a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + priorMatch[a];
					};

					//Now add mismatches
					for(a=0;a<sa.mismatchSize;a++){
						sumMismatch[a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + priorMismatch[a];
					};

					//Correlate this combined distribution with the mass spec data.
					//SSIterations++;
					RCorr = LinReg(sumMatch,sumMismatch);
					//cout << RCorr << endl;
      
					recCombo = comboList.at(0);
					recCombo.addVar(j,k);
					recCombo.corr = RCorr;

					//Check if it is the best, if so, mark it
					if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

					//Add to our list of combos to analyze in the future if above a lower threshhold
					if(RCorr > (cs.corr/2)) {
						comboList.push_back(recCombo);
						comboListCounter++;
					};

				};//for k

			};//for j

			//delete our prior combo from the list
			comboList.erase(comboList.begin());
			countDown--;

		};//while countDown

		//check best to see if it exceeded threshold
		if(bestCombo.corr>cs.corr) {
			delete [] sumMatch;
			delete [] sumMismatch;
			delete [] priorMatch;
			delete [] priorMismatch;
			*combo = bestCombo;
			return;
		};

		//otherwise, we iterate
		depth++;

	};//while depth

	//return the best we got
	delete [] sumMatch;
	delete [] sumMismatch;
	delete [] priorMatch;
	delete [] priorMismatch;
	*combo = bestCombo;
  
};


void CHardklor::FastFewestPeptidesMethod(SSObject *combo, int maxDepth){

  SSObject recCombo;
	SSObject bestCombo = *combo;

	vector<SSObject> *comboListA;
	vector<SSObject> *comboListB;
	comboListA = new vector<SSObject>;
  comboListB = new vector<SSObject>;
	comboListA->push_back(*combo);
	comboListB->push_back(*combo);

  double RCorr;
  int a,b,j,k;
	//int x;

	int depth=0;
	float intensity=0.0f;

	float **sumMatchA;
	float **sumMismatchA;
	float **sumMatchB;
	float **sumMismatchB;

	float *sumMatchC;
	float *sumMismatchC;

	//Initialize arrays
	sumMatchA = new float* [1];
	sumMismatchA = new float* [1];
	sumMatchA[0] = new float[sa.peaks.size()];
	if(sa.mismatchSize>0) sumMismatchA[0] = new float[sa.mismatchSize];
	else sumMismatchA[0] = new float[1];

	sumMatchB = new float* [1];
	sumMismatchB = new float* [1];
	sumMatchB[0] = new float[sa.peaks.size()];
	if(sa.mismatchSize>0) sumMismatchB[0] = new float[sa.mismatchSize];
	else sumMismatchB[0] = new float[1];

	sumMatchC = new float[sa.peaks.size()];
	if(sa.mismatchSize>0) sumMismatchC = new float[sa.mismatchSize];
	else sumMismatchC = new float[1];

	for(a=0;a<sa.peaks.size();a++) sumMatchB[0][a]=0;
	for(a=0;a<sa.mismatchSize;a++) sumMismatchB[0][a]=0;

	bool storeA=true;
	int widthA, widthB, widthC, sizeA, sizeB;
	int count,lastCount;
	widthA=1;
	widthB=1;
	widthC=1;
	sizeA=1;
	sizeB=1;

	//cout << "In method, everything initialized" << endl;

	while(depth < maxDepth && depth < sa.predPep->size()) {

		if(storeA){
			if(depth < maxDepth-1) {
				for(a=0;a<widthA;a++){
					delete [] sumMatchA[a];
					delete [] sumMismatchA[a];
				}
				delete [] sumMatchA;
				delete [] sumMismatchA;
				widthA=calcDepth(0,depth+1);
				sumMatchA = new float* [widthA];
				sumMismatchA = new float* [widthA];
				for(a=0;a<widthA;a++){
					sumMatchA[a] = new float[sa.peaks.size()];
					if(sa.mismatchSize>0) sumMismatchA[a] = new float[sa.mismatchSize];
					else sumMismatchA[a] = new float[1];
				}
				delete comboListA;
				comboListA = new vector<SSObject>;
				//cout << "Done re-initializing A" << endl;
			} else {
				widthC=calcDepth(0,depth+1);
			}
		} else {
			if(depth < maxDepth-1) {
				for(a=0;a<widthB;a++){
					delete [] sumMatchB[a];
					delete [] sumMismatchB[a];
				}
				delete [] sumMatchB;
				delete [] sumMismatchB;
				//cout << "Deleted B ok" << endl;
				widthB=calcDepth(0,depth+1);
				//cout << "WidthB: " << widthB << endl;
				sumMatchB = new float* [widthB];
				sumMismatchB = new float* [widthB];
				for(a=0;a<widthB;a++){
					sumMatchB[a] = new float[sa.peaks.size()];
					if(sa.mismatchSize>0) sumMismatchB[a] = new float[sa.mismatchSize];
					else sumMismatchB[a] = new float[1];
				}
				delete comboListB;
				comboListB = new vector<SSObject>;
				//cout << "Done re-initializing B" << endl;
			} else {
				widthC=calcDepth(0,depth+1);
			}
		}

		//scan through current list of combos
		lastCount=0;
		count=0;
		while(true){
			
			if(storeA){
				if(depth==maxDepth-1)	{
					if(count==widthC) break;
				} else {
					if(count==widthA) break;
				}
				if(lastCount==sizeB) break;
				if(comboListB->at(lastCount).pepVar->size()==0) {
					b=(int)sa.predPep->size()-1;
				} else {
					b = comboListB->at(lastCount).pepVar->at(comboListB->at(lastCount).pepVar->size()-1).iLower-1;
					//cout << "B: " << b << endl;
				}
			} else {
				if(depth==maxDepth-1)	{
					if(count==widthC) break;
				} else {
					if(count==widthB) break;
				}
				if(lastCount==sizeA) break;
				if(comboListA->at(lastCount).pepVar->size()==0) {
					b=(int)sa.predPep->size()-1;
				}	else {
					b = comboListA->at(lastCount).pepVar->at(comboListA->at(lastCount).pepVar->size()-1).iLower-1;
					//cout << "B: " << b << endl;
				}
			}

			//add every peptide & every variant
			for(j=b;j>-1;j--){
				
				if(storeA){
					//cout << sumMatchB[lastCount][sa.predPep->at(j).GetMaxPeakIndex()] << ", " << sa.predPep->at(j).GetIntensity() << ", " << sa.predPep->at(j).GetMaxPeakIndex() << endl;
					intensity = sa.predPep->at(j).GetIntensity() - sumMatchB[lastCount][sa.predPep->at(j).GetMaxPeakIndex()];
				} else {
					//cout << sumMatchA[lastCount][sa.predPep->at(j).GetMaxPeakIndex()] << ", " << sa.predPep->at(j).GetIntensity() << ", " << sa.predPep->at(j).GetMaxPeakIndex() << endl;
					intensity = sa.predPep->at(j).GetIntensity() - sumMatchA[lastCount][sa.predPep->at(j).GetMaxPeakIndex()];
				}

				//cout << j << ": " << intensity << endl;

				//if intensity is less than zero, this is an invalid summation
				if(intensity>0) {

					//iterate through variants now
					for(k=0;k<sa.predPep->at(j).VariantListSize();k++){

						//cout << sa.predPep->at(j).GetMZ() << "  " << sa.predPep->at(j).GetVariant(k).GetFormula() << " " << sa.predPep->at(j).GetVariant(k).GetCharge() << endl;
						//cout << "MISMATCH SIZE: " << sa.mismatchSize << endl;

						//sum matches and mismatches
						if(storeA){
							for(a=0;a<sa.peaks.size();a++){
								//cout << "Match: " << sa.peaks.at(a).mz << " " << sa.peaks.at(a).intensity << "    " << sa.predPep->at(j).GetVariant(k).GetMatch(a).mz << " " << sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity << endl;
								if(depth == maxDepth-1){
									sumMatchC[a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchB[lastCount][a];
								} else {
									sumMatchA[count][a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchB[lastCount][a];
								}
							}
							for(a=0;a<sa.mismatchSize;a++){
								//cout << "Mismatch: " << sa.predPep->at(j).GetVariant(k).GetMismatch(a).mz << "  " << sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity << endl;
								if(depth == maxDepth-1) {
									sumMismatchC[a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchB[lastCount][a];
								} else {
									sumMismatchA[count][a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchB[lastCount][a];
								}
							}
						} else {
							for(a=0;a<sa.peaks.size();a++){
								if(depth == maxDepth-1){
									sumMatchC[a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchA[lastCount][a];
								} else {
									sumMatchB[count][a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchA[lastCount][a];
								}
							}
							for(a=0;a<sa.mismatchSize;a++){
								if(depth == maxDepth-1){
									sumMismatchC[a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchA[lastCount][a];
								} else {
									sumMismatchB[count][a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchA[lastCount][a];
								}
							}
						}

						//Correlate this combined distribution with the mass spec data.
						//SSIterations++;
						if(depth == maxDepth-1){
							RCorr = LinReg(sumMatchC,sumMismatchC);
						} else {
							if(storeA) RCorr = LinReg(sumMatchA[count],sumMismatchA[count]);
							else RCorr = LinReg(sumMatchB[count],sumMismatchB[count]);
						}

						//cout << RCorr << endl;

						if(storeA) recCombo = comboListB->at(lastCount);
						else recCombo = comboListA->at(lastCount);
						recCombo.addVar(j,k);
						recCombo.corr = RCorr;

						/*
						for(x=0;x<recCombo.pepVar->size();x++) {

							a=recCombo.pepVar->at(x).iLower;
							b=recCombo.pepVar->at(x).iUpper;

							cout << "(" << a << "," << b << ")" << sa.predPep->at(a).GetMZ() << "\t";
							cout << sa.predPep->at(a).GetVariant(b).GetCharge() << "\t";
						};
						cout << "\n--------" << endl;
						*/

						//Check if it is the best, if so, mark it
						if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

						count++;
						if(depth < maxDepth-1){
							if(storeA) comboListA->push_back(recCombo);
							else comboListB->push_back(recCombo);
						}



					}//for k

				}//end if(intensity>0)

			}//for j

			//iterate to the next stored array
			lastCount++;

		}//while(true)

		if(storeA) sizeA=count;
		else sizeB=count;

		//check best to see if it exceeded threshold
		if(bestCombo.corr>cs.corr) {
			for(a=0;a<widthA;a++){
				delete [] sumMatchA[a];
				delete [] sumMismatchA[a];
			}
			delete [] sumMatchA;
			delete [] sumMismatchA;
			for(a=0;a<widthB;a++){
				delete [] sumMatchB[a];
				delete [] sumMismatchB[a];
			}
			delete [] sumMatchB;
			delete [] sumMismatchB;
			delete [] sumMatchC;
			delete [] sumMismatchC;
			delete comboListA;
			delete comboListB;
			*combo = bestCombo;
			return;
		}

		//otherwise, we iterate
		if(storeA) storeA=false;
		else storeA=true;
		depth++;

		//This is here for the complete method. It is a failsafe to kill the routine
		//if there is little hope that combining distributions will produce a real result
		if(depth==1 && bestCombo.corr<0.05) break;

	}//while depth

	//return the best we got
	for(a=0;a<widthA;a++){
		delete [] sumMatchA[a];
		delete [] sumMismatchA[a];
	}
	delete [] sumMatchA;
	delete [] sumMismatchA;
	for(a=0;a<widthB;a++){
		delete [] sumMatchB[a];
		delete [] sumMismatchB[a];
	}
	delete [] sumMatchB;
	delete [] sumMismatchB;
	delete [] sumMatchC;
	delete [] sumMismatchC;
	delete comboListA;
	delete comboListB;
	*combo = bestCombo;
	
	//cout << "Done! " << bestCombo.corr << "   " << SSIterations << endl;
  
}

void CHardklor::FastFewestPeptidesChoiceMethod(SSObject *combo, int maxDepth){

  SSObject recCombo;
	SSObject bestCombo = *combo;

	vector<SSObject> *comboListA;
	vector<SSObject> *comboListB;
	comboListA = new vector<SSObject>;
  comboListB = new vector<SSObject>;
	comboListA->push_back(*combo);
	comboListB->push_back(*combo);

  double RCorr;
  int a,b,j,k;

	int depth=0;
	float intensity;

	float **sumMatchA;
	float **sumMismatchA;
	float **sumMatchB;
	float **sumMismatchB;

	//Initialize arrays
	sumMatchA = new float* [1];
	sumMismatchA = new float* [1];
	sumMatchA[0] = new float[sa.peaks.size()];
	if(sa.mismatchSize>0) sumMismatchA[0] = new float[sa.mismatchSize];
	else sumMismatchA[0] = new float[1];

	sumMatchB = new float* [1];
	sumMismatchB = new float* [1];
	sumMatchB[0] = new float[sa.peaks.size()];
	if(sa.mismatchSize>0) sumMismatchB[0] = new float[sa.mismatchSize];
	else sumMismatchB[0] = new float[1];

	for(a=0;a<sa.peaks.size();a++) sumMatchB[0][a]=0;
	for(a=0;a<sa.mismatchSize;a++) sumMismatchB[0][a]=0;

	bool storeA=true;
	int widthA, widthB, sizeA, sizeB;
	int count,lastCount;
	widthA=1;
	widthB=1;
	sizeA=1;
	sizeB=1;

	//cout << "In method, everything initialized" << endl;

	while(depth < maxDepth && depth < sa.predPep->size()) {

		if(storeA){
			for(a=0;a<widthA;a++){
				delete [] sumMatchA[a];
				delete [] sumMismatchA[a];
			};
			delete [] sumMatchA;
			delete [] sumMismatchA;
			//cout << "Deleted A ok" << endl;
			widthA=calcDepth(0,depth+1);
			//cout << "WidthA: " << widthA << endl;
			sumMatchA = new float* [widthA];
			sumMismatchA = new float* [widthA];
			for(a=0;a<widthA;a++){
				sumMatchA[a] = new float[sa.peaks.size()];
				if(sa.mismatchSize>0) sumMismatchA[a] = new float[sa.mismatchSize];
				else sumMismatchA[a] = new float[1];
			};
			delete comboListA;
			comboListA = new vector<SSObject>;
			//cout << "Done re-initializing A" << endl;
		} else {
			for(a=0;a<widthB;a++){
				delete [] sumMatchB[a];
				delete [] sumMismatchB[a];
			};
			delete [] sumMatchB;
			delete [] sumMismatchB;
			//cout << "Deleted B ok" << endl;
			widthB=calcDepth(0,depth+1);
			//cout << "WidthB: " << widthB << endl;
			sumMatchB = new float* [widthB];
			sumMismatchB = new float* [widthB];
			for(a=0;a<widthB;a++){
				sumMatchB[a] = new float[sa.peaks.size()];
				if(sa.mismatchSize>0) sumMismatchB[a] = new float[sa.mismatchSize];
				else sumMismatchB[a] = new float[1];
			};
			delete comboListB;
			comboListB = new vector<SSObject>;
			//cout << "Done re-initializing B" << endl;
		};

		//scan through current list of combos
		count=0;
		lastCount=0;
		while(true){

			if(storeA){
				if(lastCount==sizeB) break;
				if(count==widthA) break;
				if(comboListB->at(lastCount).pepVar->size()==0) b=(int)sa.predPep->size()-1;
				else b = comboListB->at(lastCount).pepVar->at(comboListB->at(lastCount).pepVar->size()-1).iLower-1;
				//cout << "Checking ";
				//for(a=0;a<comboListB->at(lastCount).pepVar->size();a++){
				//	cout << comboListB->at(lastCount).pepVar->at(a).iLower << "." << comboListB->at(lastCount).pepVar->at(a).iUpper << "-";
				//};
				//cout << endl;
			} else {
				if(lastCount==sizeA) break;
				if(count==widthB) break;
				if(comboListA->at(lastCount).pepVar->size()==0) b=(int)sa.predPep->size()-1;
				else b = comboListA->at(lastCount).pepVar->at(comboListA->at(lastCount).pepVar->size()-1).iLower-1;
				//cout << "Checking ";
				//for(a=0;a<comboListA->at(lastCount).pepVar->size();a++){
				//	cout << comboListA->at(lastCount).pepVar->at(a).iLower << "." << comboListA->at(lastCount).pepVar->at(a).iUpper << "-";
				//};
				//cout << endl;
			};

			//add every peptide & every variant
			for(j=b;j>-1;j--){
				
				if(storeA){
					intensity = sa.predPep->at(j).GetIntensity() - sumMatchB[lastCount][sa.predPep->at(j).GetMaxPeakIndex()];
				} else {
					intensity = sa.predPep->at(j).GetIntensity() - sumMatchA[lastCount][sa.predPep->at(j).GetMaxPeakIndex()];
				};

				//if intensity is less than zero, this is an invalid summation
				if(intensity>0) {	
						
					//iterate through variants now
					for(k=0;k<sa.predPep->at(j).VariantListSize();k++){

						//sum matches and mismatches
						if(storeA){
							for(a=0;a<sa.peaks.size();a++){
								sumMatchA[count][a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchB[lastCount][a];
							};
							for(a=0;a<sa.mismatchSize;a++){
								sumMismatchA[count][a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchB[lastCount][a];
							};
						} else {
							for(a=0;a<sa.peaks.size();a++){
								sumMatchB[count][a]=sa.predPep->at(j).GetVariant(k).GetMatch(a).intensity*intensity + sumMatchA[lastCount][a];
							};
							for(a=0;a<sa.mismatchSize;a++){
								sumMismatchB[count][a]=sa.predPep->at(j).GetVariant(k).GetMismatch(a).intensity*intensity + sumMismatchA[lastCount][a];
							};
						};

						//Correlate this combined distribution with the mass spec data.
						//SSIterations++;
						if(storeA) RCorr = LinReg(sumMatchA[count],sumMismatchA[count]);
						else RCorr = LinReg(sumMatchB[count],sumMismatchB[count]);
      
						if(storeA) recCombo = comboListB->at(lastCount);
						else recCombo = comboListA->at(lastCount);
						recCombo.addVar(j,k);
						recCombo.corr = RCorr;

						//Check if it is the best, if so, mark it
						if(recCombo.corr>bestCombo.corr) bestCombo = recCombo;

						if(RCorr>cs.corr/2){
							count++;
							if(storeA) comboListA->push_back(recCombo);
							else comboListB->push_back(recCombo);
						};

					};//for k

				};//end if(intensity>0)

			};//for j

			//iterate to the next stored array
			lastCount++;

		};//while countDown

		if(storeA) sizeA=count;
		else sizeB=count;

		//check best to see if it exceeded threshold
		if(bestCombo.corr>cs.corr) {
			for(a=0;a<widthA;a++){
				delete [] sumMatchA[a];
				delete [] sumMismatchA[a];
			};
			delete [] sumMatchA;
			delete [] sumMismatchA;
			for(a=0;a<widthB;a++){
				delete [] sumMatchB[a];
				delete [] sumMismatchB[a];
			};
			delete [] sumMatchB;
			delete [] sumMismatchB;
			delete comboListA;
			delete comboListB;
			*combo = bestCombo;
      return;
    }

    //otherwise, we iterate
    if(storeA) storeA=false;
    else storeA=true;
    depth++;

  }//while depth

  //return the best we got
  for(a=0;a<widthA;a++){
    delete [] sumMatchA[a];
    delete [] sumMismatchA[a];
  }
  delete [] sumMatchA;
  delete [] sumMismatchA;
  for(a=0;a<widthB;a++){
    delete [] sumMatchB[a];
    delete [] sumMismatchB[a];
  }
  delete [] sumMatchB;
  delete [] sumMismatchB;
  delete comboListA;
  delete comboListB;
  *combo = bestCombo;
	
  //cout << "Done! " << bestCombo.corr << "   " << SSIterations << endl;
  
}

int CHardklor::calcDepth(int start, int max, int depth, int count) {
	int i,j,n;
	int total=0;
	int sz=(int)sa.predPep->size();
	
	if(max==1) {
		for(i=start;i<sz;i++) total+=sa.predPep->at(i).VariantListSize();
		return total;
	}

	for(i=start;i<sz-max+depth;i++) {

		if(depth==max) {
			return sa.predPep->at(i).VariantListSize()*count;
		} else {
			for(j=i+1;j<sz;j++){
				n = calcDepth(j,max,depth+1,sa.predPep->at(i).VariantListSize()*count);
				total+=n;
			}
		}
		if(depth>1) return total;
	}

	return total;

}

void CHardklor::ResultToMem(SSObject& obj, CPeriodicTable* PT){
  int j,k;
  int pepID;
  int varID;
	int sz=(int)obj.pepVar->size();
  char mods[32];
  char tmp[16];

  //Each peptide in a window is designated with a P 
  for(k=0;k<sz;k++) {

    pepID=obj.pepVar->at(k).iLower;
    varID=obj.pepVar->at(k).iUpper;

    hkm.monoMass = sa.predPep->at(pepID).GetVariant(varID).GetMonoMass();
    hkm.charge = sa.predPep->at(pepID).GetVariant(varID).GetCharge();
    if(cs.distArea) hkm.intensity = sa.predPep->at(pepID).GetIntensity()*(float)sa.predPep->at(pepID).GetVariant(varID).GetArea();
    else hkm.intensity = sa.predPep->at(pepID).GetIntensity();
    hkm.scan = currentScanNumber;
    hkm.mz = sa.predPep->at(pepID).GetMZ();
    hkm.corr = obj.corr;

		//Add mods
		strcpy(mods,"");
		for(j=0;j<sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().sizeAtom();j++){
			strcat(mods,PT->at(sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atAtom(j).iLower).symbol);
      sprintf(tmp,"%d",sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atAtom(j).iUpper);
			strcat(mods,tmp);
		}
		strcat(mods,"_");
		for(j=0;j<sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().sizeEnrich();j++){
      sprintf(tmp,"%.2lf",sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).ape);
      strcat(mods,tmp);
      strcat(mods,PT->at(sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).atomNum).symbol);
			sprintf(tmp,"%d",sa.predPep->at(pepID).GetVariant(varID).GetHKVariant().atEnrich(j).isotope);
			strcat(mods,tmp);
    }
    strcpy(hkm.mods,mods);
    vResults.push_back(hkm);

  } 

}

void CHardklor::WritePepLine(SSObject& obj, CPeriodicTable* PT, fstream& fptr, int format){
  int j,k;
  int pepID;
  int varID;
	int sz=(int)obj.pepVar->size();

  //Each peptide in a window is designated with a P 
  for(k=0;k<sz;k++) {

    pepID=obj.pepVar->at(k).iLower;
    varID=obj.pepVar->at(k).iUpper;

    if(format==0){
			fptr << "P\t" << sa.predPep->at(pepID).GetVariant(varID).GetMonoMass();
			fptr << "\t" << sa.predPep->at(pepID).GetVariant(varID).GetCharge();
			if(cs.distArea) fptr << "\t" << sa.predPep->at(pepID).GetIntensity()*sa.predPep->at(pepID).GetVariant(varID).GetArea();
		  else fptr << "\t" << sa.predPep->at(pepID).GetIntensity();
			fptr << "\t" << sa.predPep->at(pepID).GetMZ();
			fptr << "\t" << sa.peaks.at(0).mz << "-" << sa.peaks.at(sa.peaks.size()-1).mz;
			fptr << "\t" << sa.S2NCutoff;

			//Add mods
			fptr << "\t";
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

			fptr << "\t" << obj.corr;
			fptr << endl;

    } else if(format==1) {
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
    } else if(format==2){
			fptr << (sa.predPep->at(pepID).GetVariant(varID).GetMonoMass()+sa.predPep->at(pepID).GetVariant(varID).GetCharge()*1.007276466)/sa.predPep->at(pepID).GetVariant(varID).GetCharge();
			if(cs.distArea) fptr << "\t" << sa.predPep->at(pepID).GetIntensity()*sa.predPep->at(pepID).GetVariant(varID).GetArea();
			else fptr << "\t" << sa.predPep->at(pepID).GetIntensity();
			fptr << "\t" << sa.predPep->at(pepID).GetVariant(varID).GetCharge();
			fptr << endl;
		}
  }
}

void CHardklor::WriteScanLine(Spectrum& s, fstream& fptr, int format){
  //int i;

  if(format==0) {
    fptr << "S\t" << s.getScanNumber() << "\t" << s.getRTime() << "\t" << cs.inFile;

		//For Alex Panchaud, special ZS case
		if(s.getFileType()==ZS || s.getFileType()==UZS){
			if(s.sizeZ()>0){
				for(int i=0;i<s.sizeZ();i++) fptr << "\t" << s.atZ(i).z << "," << s.atZ(i).mh;
			}
		} else {

			//otherwise output precursor info if it exists
			if(s.sizeZ()==1){
				fptr << "\t" << s.atZ(0).mh-1.00727649 << "\t" << s.atZ(0).z << "\t" << s.getMZ();
			} else if(s.sizeZ()>1){
				fptr << "\t0.0\t0\t" << s.getMZ();
			} else {
				fptr << "\t0.0\t0\t0.0";
			}
		}
    fptr << endl;
  } else if(format==1){
    fptr << "<Spectrum Scan=\"" << s.getScanNumber() << "\" ";
		fptr << "RetentionTime=\"" << s.getRTime() << "\" "; 
		fptr << "Filename=\"" << cs.inFile << "\"";
		if(s.getFileType()==ZS || s.getFileType()==UZS){
			if(s.sizeZ()>0){
				for(int i=0;i<s.sizeZ();i++) fptr << " PeptideSignal" << i << "=\"" << s.atZ(i).z << "," << s.atZ(i).mh << "\"";
			}
		} else {
			if(s.sizeZ()==1){
				fptr << " AccMonoMass=\"" << s.atZ(0).mh-1.00727649 << "\" PrecursorCharge=\"" << s.atZ(0).z << "\" PrecursorMZ=\"" << s.getMZ() << "\"";
			} else if(s.sizeZ()>1){
				fptr << " AccMonoMass=\"0.0\" PrecursorCharge=\"0\" PrecursorMZ=\"" << s.getMZ() << "\"";
			} else {
				fptr << " AccMonoMass=\"0.0\" PrecursorCharge=\"0\" PrecursorMZ=\"0.0\"";
      }
    }
    fptr << ">" << endl;
  } else if(format==2) {
    fptr << "Scan = " << s.getScanNumber();
    fptr << endl;
  }
}

void CHardklor::WriteParams(fstream& fptr, int format){

}

int CHardklor::Size(){
  return (int)vResults.size();
}

void CHardklor::SetResultsToMemory(bool b){
  bMem=b;
}

hkMem& CHardklor::operator[](const int& index){
  return vResults[index];
}

