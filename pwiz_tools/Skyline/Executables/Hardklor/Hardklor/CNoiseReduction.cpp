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
#include "CNoiseReduction.h"

using namespace std;
using namespace MSToolkit;

CNoiseReduction::CNoiseReduction(){
  pos=0;
  posA=0;
  lastFile="";
}

CNoiseReduction::CNoiseReduction(MSReader* msr, CHardklorSetting& hs){
  r=msr;
  cs=hs;
  pos=0;
  posA=0;
  lastFile="";
}

CNoiseReduction::~CNoiseReduction(){
  r=NULL;
}

//Calculates the resolution (FWHM) of a peak
double CNoiseReduction::calcFWHM(double mz){
	double deltaM;
	switch(cs.msType){
	case OrbiTrap:
		deltaM = mz * sqrt(mz) / (20*cs.res400); //(20*userParams.res400);  //sqare root of 400
		break;
	case TOF:
		deltaM = mz / cs.res400;
		break;
	case QIT:
		deltaM = cs.res400 / 5000.0;
		break;
	case FTICR:
	default:
		deltaM = mz * mz / (400*cs.res400);
		break;
	}
	return deltaM;
}

bool CNoiseReduction::DeNoise(Spectrum& sp){

  double ppm;
  int i,j,k;
  int index;
  int matchCount;
  char cFilter1[256];
  char cFilter2[256];

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  if(pos==0){
    if((cs.scan.iLower>0)) {
      k=cs.scan.iLower; 
      r->readFile(&cs.inFile[0],tmpSpec,cs.scan.iLower);
    } else {
      r->readFile(&cs.inFile[0],tmpSpec);
      k=tmpSpec.getScanNumber();
    }
    if(tmpSpec.getScanNumber()==0) return false;
    tmpSpec.getRawFilter(cFilter1,256);
      
    //Gather left side of scan
    i=1;
    j=0;
    while( k-i > 0){
      r->readFile(&cs.inFile[0],tmpSpec,cs.scan.iLower-i);
      if(tmpSpec.getScanNumber()==0) {
        i++;
        continue;
      }
      tmpSpec.getRawFilter(cFilter2,256);
      
      //Assume High resolution data at all times
      if(!cs.centroid) {
        FirstDerivativePeaks(tmpSpec,1);
        tmpSpec.setRawFilter(cFilter2);
      }

      s.push_front(tmpSpec);
      if(strcmp(cFilter1,cFilter2)==0){
        j++;
				if(j==(int)(cs.boxcar/2)) break;
      }
      i++;
    }

    //cout << "Done left " << s.size() << " " << cs.rawAvgWidth << endl;

    //Get our position again
    r->readFile(&cs.inFile[0],tmpSpec,k);

    //Assume High resolution data at all times
    if(!cs.centroid) {
      FirstDerivativePeaks(tmpSpec,1);
      tmpSpec.setRawFilter(cFilter1);
    }
    
    //Add first target scan
    s.push_back(tmpSpec);
    pos=(int)s.size()-1; 
    
    //Add right side of scan
    i=1;
    j=0;
    while(true){
      r->readFile(NULL,tmpSpec);
      if(tmpSpec.getScanNumber()==0) break;
      tmpSpec.getRawFilter(cFilter2,256);

      //Assume High resolution data at all times
      if(!cs.centroid) {
        FirstDerivativePeaks(tmpSpec,1);
        tmpSpec.setRawFilter(cFilter2);
      }

      s.push_back(tmpSpec);
      //cout << tmpSpec.getScanNumber() << " " << cFilter1 << " xx " << cFilter2 << endl;
      if(strcmp(cFilter1,cFilter2)==0){  
        j++;
				if(j==(int)(cs.boxcar/2)) break;
      }
      i++;
    }

  }

  //return false if we reached the end
  if(pos>=(int)s.size()) return false;

  //Because Deque may have mixed spectra, create list of Deque indexes to compare
  //Shrink Deque on the left if some spectra are not needed
  //Expand Deque to the right if needed and possible
  //cout << "Checking scan " << s[pos].getScanNumber() << endl;
  v.clear();
  s[pos].getRawFilter(cFilter1,256);

  //look left
  for(i=pos-1;i>=0;i--){
    s[i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      v.push_back(i);
			if(v.size()==(int)(cs.boxcar/2)) break;
    }
  }

  //erase unneeded left items
  while(i>0){
    s.pop_front();
    i--;
    for(j=0;j<(int)v.size();j++) v[j]--;
    pos--;
  }

  //look right
  j=0;
  for(i=pos+1;i<(int)s.size();i++){
    s[i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      v.push_back(i);
      j++;
      if(j==(int)(cs.boxcar/2)) break;
    }
  }

  //extend right side if needed
  while(j<(int)(cs.boxcar/2)){
    r->readFile(NULL,tmpSpec);
    if(tmpSpec.getScanNumber()==0) break;    
    tmpSpec.getRawFilter(cFilter2,256);

    //Assume High resolution data at all times
    if(!cs.centroid) {
      FirstDerivativePeaks(tmpSpec,1);
      tmpSpec.setRawFilter(cFilter2);
    }

    s.push_back(tmpSpec);
    if(strcmp(cFilter1,cFilter2)==0) {
      v.push_back((int)s.size()-1);
      j++;
    }
  }

  //if there is nothing to compare to, exit now
  if(v.size()==0) return false;

  //compare peaks
  //cout << "Checking " << s[pos].size() << " peaks." << endl;
  for(i=0;i<s[pos].size();i++){
    matchCount=1;
    for(j=0;j<(int)v.size();j++){
      if(s[v[j]].size()<1) continue;
      index = NearestPeak(s[v[j]],s[pos].at(i).mz);
      ppm=fabs( (s[v[j]].at(index).mz-s[pos].at(i).mz)/s[pos].at(i).mz * 1000000);
      if(ppm<cs.ppm) matchCount++;
    }
		if(matchCount>=cs.boxcarFilter || matchCount==v.size()) sp.add(s[pos].at(i));
  }
  sp.setScanNumber(s[pos].getScanNumber());
  sp.setScanNumber(s[pos].getScanNumber(true),true);
  sp.setRTime(s[pos].getRTime());
  pos++;

  //cout << sp.getScanNumber() << endl;

  //cout << "Done! " << sp.size() << " " << pos << endl;
  return true;

}

//Overloaded function allows user to just pass an array of spectra to be compared.
//No file reading/parsing is performed.
/*
bool CNoiseReduction::DeNoise(Spectrum& sp, vector<Spectrum>& vs, int pivot, bool findPeaks){

  double ppm;
  int i,j;
  int index;
  int matchCount;

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  //Find peaks (centroid) for all scans (assume high res data)
  if(findPeaks) {
    for(i=0;i<vs.size();i++) FirstDerivativePeaks(vs[i],1);
  }

  //compare peaks
  for(i=0;i<vs[pivot].size();i++){
    matchCount=1;
    for(j=0;j<vs.size();j++){
      if(j==pivot) continue;
      if(vs[j].size()<1) continue;
      index = NearestPeak(vs[j],vs[pivot].at(i).mz);
      ppm=fabs( (vs[j].at(index).mz-vs[pivot].at(i).mz)/vs[pivot].at(i).mz * 1000000);
      if(ppm<cs.ppm) matchCount++;
    }
    if(matchCount>=cs.ppMatch || matchCount==v.size()) sp.add(vs[pivot].at(i));
  }
  sp.setScanNumber(vs[pivot].getScanNumber());
  sp.setScanNumber(vs[pivot].getScanNumber(true),true);
  sp.setRTime(vs[pivot].getRTime());

  //cout << "Done! " << sp.size() << " " << pivot << endl;
  return true;

}

//Overloaded function allows user to just pass an array of spectra to be compared.
//No file reading/parsing is performed.
bool CNoiseReduction::DeNoise(Spectrum& sp, deque<Spectrum>& vs, int pivot, bool findPeaks){

  double ppm;
  int i,j;
  int index;
  int matchCount;

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  //Find peaks (centroid) for all scans (assume high res data)
  if(findPeaks) {
    for(i=0;i<vs.size();i++) FirstDerivativePeaks(vs[i],1);
  }

  //compare peaks
  for(i=0;i<vs[pivot].size();i++){
    matchCount=1;
    for(j=0;j<vs.size();j++){
      if(j==pivot) continue;
      if(vs[j].size()<1) continue;
      index = NearestPeak(vs[j],vs[pivot].at(i).mz);
      ppm=fabs( (vs[j].at(index).mz-vs[pivot].at(i).mz)/vs[pivot].at(i).mz * 1000000);
      if(ppm<cs.ppm) matchCount++;
    }
    if(matchCount>=cs.ppMatch || matchCount==v.size()) sp.add(vs[pivot].at(i));
  }
  sp.setScanNumber(vs[pivot].getScanNumber());
  sp.setScanNumber(vs[pivot].getScanNumber(true),true);
  sp.setRTime(vs[pivot].getRTime());

  //cout << "Done! " << sp.size() << " " << pos << endl;
  return true;

}
*/

//First derivative method taken from CSpecAnalyze, returns base peak intensity of the set
void CNoiseReduction::FirstDerivativePeaks(Spectrum& sp, int winSize){
  int i,j;
  float maxIntensity;
  int bestPeak;
  bool bLastPos;
  Spectrum gp;

	int nextBest;
	double FWHM;
	Peak_T centroid;

  bLastPos=false;
  for(i=0;i<sp.size()-winSize;i++){

    if(sp.at(i).intensity<sp.at(i+winSize).intensity) {
      bLastPos=true;
      continue;
    } else {
      if(bLastPos){
				bLastPos=false;
	
				//find max and add peak
				maxIntensity=0;
				for(j=i;j<i+winSize;j++){
				  if (sp.at(j).intensity>maxIntensity){
				    maxIntensity=sp.at(j).intensity;
				    bestPeak = j;
				  }
				}

				//Best estimate of Gaussian centroid
				//Get 2nd highest point of peak
				if(bestPeak==sp.size()-1){
					nextBest=bestPeak-1;
				} else if(sp.at(bestPeak-1).intensity > sp.at(bestPeak+1).intensity){
					nextBest=bestPeak-1;
				} else {
					nextBest=bestPeak+1;
				}

				//Get FWHM
				FWHM = calcFWHM(sp.at(bestPeak).mz);

				//Calc centroid MZ (in three lines for easy reading)
				centroid.mz = pow(FWHM,2)*log(sp.at(bestPeak).intensity/sp.at(nextBest).intensity);
				centroid.mz /= GC*(sp.at(bestPeak).mz-sp.at(nextBest).mz);
				centroid.mz += (sp.at(bestPeak).mz+sp.at(nextBest).mz)/2;

				//Calc centroid intensity
				centroid.intensity=(float)(sp.at(bestPeak).intensity/exp(-pow((sp.at(bestPeak).mz-centroid.mz)/FWHM,2)*GC));

				//some peaks are funny shaped and have bad gaussian fit.
				//if error is more than 10%, keep existing intensity
				if( fabs((sp.at(bestPeak).intensity - centroid.intensity) / centroid.intensity * 100) > 10 ||
            //not a good check for infinity
            centroid.intensity>999999999999.9 ||
            centroid.intensity < 0 ) {
					centroid.intensity=sp.at(bestPeak).intensity;
				}

				//Hack until I put in mass ranges
				if(centroid.mz<0 || centroid.mz>2000) {
					//do nothing if invalid mz
				} else {
					gp.add(centroid);
				}
				i+=winSize-1;
      }

    }
  }
  
  int scanNumber=sp.getScanNumber();
  int scanNumber2=sp.getScanNumber(false);
  float rTime=sp.getRTime();
  sp = gp;
  sp.setRTime(rTime);
  sp.setScanNumber(scanNumber);
  sp.setScanNumber(scanNumber2,true);

}



//Binary search to quickly find the nearest peak
int CNoiseReduction::NearestPeak(Spectrum& sp, double mz){
  int pivot=0;
  int width=0;
  int lastWidth=0;
  int best=0;
  double dif=9999999.9;
  double d;

  pivot=sp.size()/2;
  width=(int)(pivot/2.0+0.5);
  while(width!=lastWidth && pivot<sp.size() && pivot>-1){
    d=fabs(sp.at(pivot).mz-mz);
    if(d<dif){
      dif=d;
      best=pivot;
    }
    if(sp.at(pivot).mz==mz){
      return pivot;
    } else if(sp.at(pivot).mz > mz){
      pivot-=width;
      lastWidth=width;
      width=(int)(width/2.0+0.5);
    } else {
      pivot+=width;
      lastWidth=width;
      width=(int)(width/2.0+0.5);
    }
  }
  if(pivot<sp.size() && pivot>-1){
    d=fabs(sp.at(pivot).mz-mz);
    if(d<dif) best=pivot;
  }
  return best;
}

bool CNoiseReduction::ScanAverage(Spectrum& sp, std::string file, int width, float cutoff){
  
  Spectrum ts;
  Spectrum ps=sp;
  MSReader r;
 
  int i;
  int j;
  int k;
  int widthCount=0;
  int numScans=1;
  double dif;
  double prec;
  double dt;
  double c=CParam(ps,3);

  bool bLeft=true;
  int posLeft=ps.getScanNumber()-1;
  int posRight=ps.getScanNumber()+1;
  char cFilter1[256];
  char cFilter2[256];

  ps.getRawFilter(cFilter1,256);

  while(widthCount<(width*2)){

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      widthCount++;
      while(posLeft>0){
        r.readFile(file.c_str(),ts,posLeft);
        if(ts.getScanNumber()==0) break;
        ts.getRawFilter(cFilter2,256);
        if(strcmp(cFilter1,cFilter2)==0) break;
        posLeft--;
      }
    } else {
      bLeft=true;
      widthCount++;
      while(true){
        r.readFile(file.c_str(),ts,posRight);
        if(ts.getScanNumber()==0) break;
        ts.getRawFilter(cFilter2,256);
        if(strcmp(cFilter1,cFilter2)==0) break;
        posRight++;
      }
    }
    if(ts.getScanNumber()==0) continue;

    numScans++;

    //Match peaks between pivot scan and temp scan
    k=0;
    for(i=0;i<ps.size();i++){
      dif=100000.0;
      prec = c * ps.at(i).mz * ps.at(i).mz / 2;

      for(j=k;j<ts.size();j++){
        dt=fabs(ps.at(i).mz-ts.at(j).mz);
        if(dt<=dif) {
          if(dt<prec) {
            ps.at(i).intensity+=ts.at(j).intensity;
            ts.at(j).mz=-1.0;
            k=j+1;
            break;
          }
          dif=dt;
        } else {
          k=j-1;
          break;
        }
      }
    }

    //Add unmatched peaks from temp scan
    for(i=0;i<ts.size();i++){
      if(ts.at(i).mz>-1.0) ps.add(ts.at(i));
    }

    //Sort pivot scan peaks for fast traversal against next temp scan
    ps.sortMZ();

  }

  //Average points and apply cutoff
  sp.clear();
  for(i=0;i<ps.size();i++) {
    ps.at(i).intensity/=numScans;
    if(ps.at(i).intensity>=cutoff) sp.add(ps.at(i));
  }

  sp.setScanNumber(ps.getScanNumber());
  sp.setScanNumber(ps.getScanNumber(true),true);
  sp.setRTime(ps.getRTime());

  return true;
}

bool CNoiseReduction::NewScanAverage(Spectrum& sp, std::string file, int width, float cutoff, int scanNum){
  
  Spectrum ts;

  vector<int> vPos;
   
  int i;
  int j;
  int k;
  int m;
  int widthCount=0;
  int numScans=1;
  double dif;
  double prec=0;
  double dt;
  double c=0;
  double ppm;

  bool bLeft=true;
  int posLeft;
  int posRight;
  int index;
  char cFilter1[256];
  //char cFilter2[256];

  //double slope;
  //double intercept;

  sp.clear();
  Spectrum* specs;
  specs = new Spectrum[width*2+1];

  //if file is not null, create new buffer
  if(file.length()){
    lastFile = file;
    bs.clear();
    if(scanNum>0) r->readFile(file.c_str(),ts,scanNum);
    else r->readFile(file.c_str(), ts);
    if(ts.getScanNumber()==0) {
      delete [] specs;
      return false;
    }
    bs.push_back(ts);
    specs[0]=bs[0];
    if (!cs.centroid) c = CParam(specs[0], 3);
    posA=0;
  } else {
    posA++;
    if(posA>=(int)bs.size()) { //end of buffer, no more data
      delete [] specs;
      return false; 
    }
    specs[0]=bs[posA];
    if (!cs.centroid) c = CParam(specs[0], 3);
  }

  specs[0].getRawFilter(cFilter1,256);

  posLeft=posA;
  posRight=posA;
  while(widthCount<(width*2)){

    index=-1;

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      widthCount++;
      
      while(true){
        posLeft--;
        if(posLeft<0) { //buffer is too short on left, add spectra
          i=bs[0].getScanNumber();
          while(true){
            i--;
            if(i==0) break;
            r->readFile(lastFile.c_str(),ts,i);
            if(ts.getScanNumber()==0) continue;
            else break;
          }
          if(i==0) break;
          bs.push_front(ts);
          posA++;
          posRight++;
          posLeft=0;
          //ts.getRawFilter(cFilter2,256);
          if(ts.getMsLevel()==cs.msLevel) {
            index=posLeft;
            break;
          }
        } else {
          //bs[posLeft].getRawFilter(cFilter2,256);
          if(bs[posLeft].getMsLevel()==cs.msLevel) {
            index=posLeft;
            break;
          }
        }
      }

    } else {
      bLeft=true;
      widthCount++;

      while(true){
        posRight++;
        if(posRight>=(int)bs.size()) { //buffer is too short on right, add spectra
          r->readFile(lastFile.c_str(),ts,bs[bs.size()-1].getScanNumber());
          r->readFile(NULL,ts);
          if(ts.getScanNumber()==0) {
            posRight--;
            break;
          }
          bs.push_back(ts);
          //ts.getRawFilter(cFilter2,256);
          if(ts.getMsLevel()==cs.msLevel) {
            index=posRight;
            break;
          }
        } else {
          //bs[posRight].getRawFilter(cFilter2,256);
          if(bs[posRight].getMsLevel()==cs.msLevel) {
            index=posRight;
            break;
          }
        }
      }
    }

    if(index==-1)  continue;
    specs[numScans++]=bs[index];

  }
  
  double tmz;
  int mzcount=0;

  /* Ledford equation correction
  double freq;
  double conA=0.0;
  double conB=0.0;

  for(m=0;m<numScans;m++){
    conA+=specs[m].getConversionA();
    conB+=specs[m].getConversionB();
  }
  conA/=numScans;
  conB/=numScans;

  printf("%.10lf, %.10lf\n",conA,conB);
  for(k=0;k<numScans;k++){
    for(j=0;j<specs[k].size();j++){
      freq = sqrt(specs[k].getConversionA()*specs[k].getConversionA() - (4*specs[k].at(j).mz*(-specs[k].getConversionB())));
      freq += specs[k].getConversionA();
      freq /= (2*specs[k].at(j).mz);

      specs[k].at(j).mz = conA/freq + conB/(freq*freq);
    }
  }
  */

  //Match peaks between pivot scan (0) and neighbors (the rest)
  for(m=0;m<numScans;m++){
    
    vPos.clear();
    for(i=0;i<numScans;i++) vPos.push_back(0);

    for(i=0;i<specs[m].size();i++){ //iterate all points
      if(specs[m].at(i).intensity<0.1) continue;
      tmz=specs[m].at(i).mz;
      mzcount=1;
      if (!cs.centroid) prec = c * tmz * tmz / 2;
      
      for(k=m+1;k<numScans;k++){ //iterate all neighbors
        dif=100000.0;

        for(j=vPos[k];j<specs[k].size();j++){ //check if point is a match
          if(specs[k].at(j).intensity<0.1) continue; //skip meaningless datapoints to speed along
          dt=fabs(tmz-specs[k].at(j).mz);

          if(dt<=dif) {

            if (cs.centroid){
              ppm = dt / specs[k].at(j).mz*1e6;
              if (ppm<cs.ppm){
                specs[m].at(i).intensity += specs[k].at(j).intensity;
                specs[m].at(i).mz += specs[k].at(j).mz;
                vPos[k] = j + 1;
                specs[k].at(j).intensity = -1.0;
                mzcount++;
                break;
              }
            } else if(dt<prec) {
              //linear interpolate
              //if(specs[k].at(j).mz<tmz && j<specs[k].size()-1){
              //  slope=(specs[k].at(j+1).intensity-specs[k].at(j).intensity)/(specs[k].at(j+1).mz-specs[k].at(j).mz);
              //  intercept=specs[k].at(j).intensity-specs[k].at(j).mz*slope;
              //  specs[m].at(i).intensity+=(tmz*slope+intercept);
              //} else if(specs[k].at(j).mz>tmz && j>0) {
              //  slope=(specs[k].at(j).intensity-specs[k].at(j-1).intensity)/(specs[k].at(j).mz-specs[k].at(j-1).mz);
              //  intercept=specs[k].at(j).intensity-specs[k].at(j).mz*slope;
              //  specs[m].at(i).intensity+=(tmz*slope+intercept);
              //} else {
              specs[m].at(i).intensity += specs[k].at(j).intensity;
              //}

              //Averaging the mz values appears equivalent to realigning all spectra against
              //an average Ledford correction.
              specs[m].at(i).mz += specs[k].at(j).mz;
              vPos[k]=j+1;
              specs[k].at(j).intensity=-1.0;
              mzcount++;
              break;
            }
            dif=dt;
          } else {
            vPos[k]=j-1;
            break;
          }
        }
      }//for k

      sp.add(specs[m].at(i).mz/mzcount,specs[m].at(i).intensity/numScans);

    } //next i
  } //next m

  if(sp.size()>0) sp.sortMZ();
  sp.setScanNumber(specs[0].getScanNumber());
  sp.setScanNumber(specs[0].getScanNumber(true),true);
  sp.setRTime(specs[0].getRTime());
  sp.setRawFilter(cFilter1);

  if(posLeft>0){
    while(posLeft>0){
      bs.pop_front();
      posLeft--;
      posA--;
    }
  }
  delete [] specs;
  return true;
}

/*
bool CNoiseReduction::ScanAverage(Spectrum& sp, vector<Spectrum>& vs, int pivot, float cutoff, double cp){
 
  int i;
  int j;
  int k;
  int ts;
  int widthCount=0;
  int numScans=1;
  double dif;
  double prec;
  double dt;
  double c;
  if(cp==0.0) c=CParam(vs[pivot],1);
  else c=cp;

  bool bLeft=true;
  int posLeft=pivot-1;
  int posRight=pivot+1;


  cout << "PIVOT: " << vs[pivot].getScanNumber() << endl;
  cout << c << endl;

  while(numScans<vs.size()){

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      if(posLeft<0) continue;
      ts=posLeft;
      posLeft--;
    } else {
      bLeft=true;
      if(posRight>=vs.size()) continue;
      ts=posRight;
      posRight++;
    }

    numScans++;

    cout << "TS: " << ts << " " << vs[ts].getScanNumber() << endl;
    for(i=0;i<vs[ts].size();i++) cout << vs[ts].at(i).mz << " " << vs[ts].at(i).intensity << endl;

    //Match peaks between pivot scan and temp scan
    k=0;
    for(i=0;i<vs[pivot].size();i++){
      dif=100000.0;
      prec = c * vs[pivot].at(i).mz * vs[pivot].at(i).mz / 2;

      for(j=k;j<vs[ts].size();j++){
        dt=fabs(vs[pivot].at(i).mz-vs[ts].at(j).mz);
        if(dt<=dif) {
          if(dt<prec) {
            cout << "MATCH: " << vs[pivot].at(i).mz << " to " << vs[ts].at(j).mz << " was " << vs[pivot].at(i).intensity;
            vs[pivot].at(i).intensity+=vs[ts].at(j).intensity;
            cout << " is now " << vs[pivot].at(i).intensity << endl;
            vs[ts].at(j).mz=-1.0;
            k=j+1;
            break;
          }
          dif=dt;
        } else {
          k=j-1;
          break;
        }
      }
    }

    //Add unmatched peaks from temp scan
    for(i=0;i<vs[ts].size();i++){
      if(vs[ts].at(i).mz>-1.0) {
        vs[pivot].add(vs[ts].at(i));
        cout << "Adding " << vs[ts].at(i).mz << endl;
      }
    }

    //Sort pivot scan peaks for fast traversal against next temp scan
    vs[pivot].sortMZ();

  }

  cout << "NUMSCANS: " << numScans << endl;

  //Average points and apply cutoff
  sp.clear();
  for(i=0;i<vs[pivot].size();i++) {
    vs[pivot].at(i).intensity/=numScans;
    if(vs[pivot].at(i).intensity>=cutoff) sp.add(vs[pivot].at(i));
  }

  sp.setScanNumber(vs[pivot].getScanNumber());
  sp.setScanNumber(vs[pivot].getScanNumber(true),true);
  sp.setRTime(vs[pivot].getRTime());

  return true;
}

bool CNoiseReduction::ScanAverage(Spectrum& sp, deque<Spectrum>& vs, int pivot, float cutoff){
 
  int i;
  int j;
  int k;
  int ts;
  int widthCount=0;
  int numScans=1;
  double dif;
  double prec;
  double dt;
  double c=CParam(vs[pivot],1);

  bool bLeft=true;
  int posLeft=pivot-1;
  int posRight=pivot+1;

  while(numScans<vs.size()){

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      if(posLeft<0) continue;
      ts=posLeft;
      posLeft--;
    } else {
      bLeft=true;
      if(posRight>=vs.size()) continue;
      ts=posRight;
      posRight++;
    }

    numScans++;

    //Match peaks between pivot scan and temp scan
    k=0;
    for(i=0;i<vs[pivot].size();i++){
      dif=100000.0;
      prec = c * vs[pivot].at(i).mz * vs[pivot].at(i).mz / 2;

      for(j=k;j<vs[ts].size();j++){
        dt=fabs(vs[pivot].at(i).mz-vs[ts].at(j).mz);
        if(dt<=dif) {
          if(dt<prec) {
            vs[pivot].at(i).intensity+=vs[ts].at(j).intensity;
            vs[ts].at(j).mz=-1.0;
            k=j+1;
            break;
          }
          dif=dt;
        } else {
          k=j-1;
          break;
        }
      }
    }

    //Add unmatched peaks from temp scan
    for(i=0;i<vs[ts].size();i++){
      if(vs[ts].at(i).mz>-1.0) vs[pivot].add(vs[ts].at(i));
    }

    //Sort pivot scan peaks for fast traversal against next temp scan
    vs[pivot].sortMZ();

  }

  //Average points and apply cutoff
  sp.clear();
  for(i=0;i<vs[pivot].size();i++) {
    vs[pivot].at(i).intensity/=numScans;
    if(vs[pivot].at(i).intensity>=cutoff) sp.add(vs[pivot].at(i));
  }

  sp.setScanNumber(vs[pivot].getScanNumber());
  sp.setScanNumber(vs[pivot].getScanNumber(true),true);
  sp.setRTime(vs[pivot].getRTime());

  return true;
}
*/

/*
bool CNoiseReduction::ScanAverageBuffered(Spectrum& sp, char* file, int width, float cutoff, int scanNum){
  
  Spectrum ts;
  Spectrum ps;
  //MSReader r;
 
  int i;
  int j;
  int k;
  int widthCount=0;
  int numScans=1;
  double dif;
  double prec;
  double dt;
  double c;

  bool bLeft=true;
  int posLeft=ps.getScanNumber()-1;
  int posRight=ps.getScanNumber()+1;
  int index;
  char cFilter1[256];
  char cFilter2[256];

  sp.clear();

  //if file is not null, create new buffer
  if(file!=NULL){
    strcpy(lastFile,file);
    bs.clear();
    if(scanNum>0) r->readFile(file,ts,scanNum);
    else r->readFile(file,ts);
    if(ts.getScanNumber()==0) return false;
    bs.push_back(ts);
    ps=bs[0];
    c=CParam(ps,3);
    posA=0;
  } else {
    posA++;
    //cout << "ER: " << posA << " " << bs.size() << endl;
    if(posA>=bs.size()) return false; //end of buffer, no more data
    ps=bs[posA];
    c=CParam(ps,3);
  }

  //set our pivot spectrum
  //ps=bs[posA];
  ps.getRawFilter(cFilter1,256);
  //cout << "Averaging: " << ps.getScanNumber() << endl;

  posLeft=posA;
  posRight=posA;
  while(widthCount<(width*2)){

    index=-1;

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      widthCount++;
      
      while(true){
        posLeft--;
        if(posLeft<0) { //buffer is too short on left, add spectra
          i=bs[0].getScanNumber()-1;
          if(i==0) break;
          r->readFile(lastFile,ts,i);
          if(ts.getScanNumber()==0) continue;
          bs.push_front(ts);
          posLeft=0;
          posA++;
          posRight++;
          ts.getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posLeft;
            break;
          }
        } else {
          bs[posLeft].getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posLeft;
            break;
          }
        }
      }

    } else {
      bLeft=true;
      widthCount++;

      while(true){
        posRight++;
        //cout << "PR: " << posRight << " " << bs.size() << endl;
        if(posRight>=bs.size()) { //buffer is too short on right, add spectra
          r->readFile(lastFile,ts,bs[bs.size()-1].getScanNumber());
          //cout << ts.getScanNumber() << endl;
          r->readFile(NULL,ts);
          //cout << ts.getScanNumber() << endl;
          //cout << "Full on right: next scan is " << ts.getScanNumber() << " after " << bs[bs.size()-1].getScanNumber() << endl;
          if(ts.getScanNumber()==0) {
            posRight--;
            break;
          }
          bs.push_back(ts);
          ts.getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posRight;
            break;
          }
        } else {
          bs[posRight].getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posRight;
            break;
          }
        }
      }
    }

    if(index==-1) {
      //cout << "No match " << widthCount << endl;
      continue;
    }
    ts=bs[index];
    //cout << "  with: " << ts.getScanNumber() << "   " << numScans+1 << endl;

    numScans++;

    //Match peaks between pivot scan and temp scan
    k=0;
    for(i=0;i<ps.size();i++){
      dif=100000.0;
      prec = c * ps.at(i).mz * ps.at(i).mz / 2;

      for(j=k;j<ts.size();j++){
        dt=fabs(ps.at(i).mz-ts.at(j).mz);
        if(dt<=dif) {
          if(dt<prec) {
            ps.at(i).intensity+=ts.at(j).intensity;
            ts.at(j).mz=-1.0;
            k=j+1;
            break;
          }
          dif=dt;
        } else {
          k=j-1;
          break;
        }
      }
    }

    //Add unmatched peaks from temp scan
    for(i=0;i<ts.size();i++){
      if(ts.at(i).mz>-1.0) ps.add(ts.at(i));
    }

    //Sort pivot scan peaks for fast traversal against next temp scan
    ps.sortMZ();

  }

  //Average points and apply cutoff
  sp.clear();
  for(i=0;i<ps.size();i++) {
    ps.at(i).intensity/=numScans;
    if(ps.at(i).intensity>=cutoff) sp.add(ps.at(i));
  }

  sp.setScanNumber(ps.getScanNumber());
  sp.setScanNumber(ps.getScanNumber(true),true);
  sp.setRTime(ps.getRTime());
  sp.setRawFilter(cFilter1);

  //clear unused buffer
  if(posLeft>0){
    while(posLeft>0){
      bs.pop_front();
      posLeft--;
      posA--;
    }
  }

  //cout << "Done averaging" << endl;

  return true;
}
*/

bool CNoiseReduction::ScanAveragePlusDeNoise(Spectrum& sp, std::string file, int width, float cutoff, int scanNum){
  
  Spectrum ts;
  Spectrum ps;
  //MSReader r;

  vector<int> v;
  vector<int> vPos;
 
  int i;
  int j;
  int k;
  int widthCount=0;
  int numScans=1;
  int match;
  double dif;
  double prec;
  double dt;
  double c;

  bool bLeft=true;
  int posLeft=ps.getScanNumber()-1;
  int posRight=ps.getScanNumber()+1;
  int index;
  char cFilter1[256];
  //char cFilter2[256];

  sp.clear();

  //if file is not null, create new buffer
  if(file.length()){
    lastFile = file;
    bs.clear();
    if(scanNum>0) r->readFile(file.c_str(),ts,scanNum);
    else r->readFile(file.c_str(),ts);
    if(ts.getScanNumber()==0) return false;
    bs.push_back(ts);
    ps=bs[0];
    c=CParam(ps,3);
    posA=0;
  } else {
    posA++;
    //cout << "ER: " << posA << " " << bs.size() << endl;
    if(posA>=(int)bs.size()) return false; //end of buffer, no more data
    ps=bs[posA];
    c=CParam(ps,3);
  }

  //set our pivot spectrum
  //ps=bs[posA];
  ps.getRawFilter(cFilter1,256);
  //cout << "Averaging: " << ps.getScanNumber() << endl;

  posLeft=posA;
  posRight=posA;
  while(widthCount<(width*2)){

    index=-1;

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      widthCount++;
      
      while(true){
        posLeft--;
        //cout << posLeft << endl;
        if(posLeft<0) { //buffer is too short on left, add spectra
          i=bs[0].getScanNumber();
          while(true){
            i--;
            //cout << "I: " << i << endl;
            if(i==0) break;
            r->readFile(lastFile.c_str(),ts,i);
            if(ts.getScanNumber()==0) continue;
            else break;
          }
          if(i==0) break;
          bs.push_front(ts);
          for(i=0;i<(int)v.size();i++)v[i]++;
          posA++;
          posRight++;
          posLeft=0;
          //ts.getRawFilter(cFilter2,256);
          if(ts.getMsLevel()==cs.msLevel) {
            index=posLeft;
            break;
          }
        } else {
          //bs[posLeft].getRawFilter(cFilter2,256);
          if(bs[posLeft].getMsLevel()==cs.msLevel) {
            index=posLeft;
            break;
          }
        }
      }

    } else {
      bLeft=true;
      widthCount++;

      while(true){
        posRight++;
        if(posRight>=(int)bs.size()) { //buffer is too short on right, add spectra
          r->readFile(lastFile.c_str(),ts,bs[bs.size()-1].getScanNumber());
          r->readFile(NULL,ts);
          if(ts.getScanNumber()==0) {
            posRight--;
            break;
          }
          bs.push_back(ts);
          //ts.getRawFilter(cFilter2,256);
          if(ts.getMsLevel()==cs.msLevel) {
            index=posRight;
            break;
          }
        } else {
          //bs[posRight].getRawFilter(cFilter2,256);
          if(bs[posRight].getMsLevel()==cs.msLevel) {
            index=posRight;
            break;
          }
        }
      }
    }

    if(index==-1)  continue;
   
    //ts=bs[index];
    v.push_back(index);

    numScans++;
  }

  //cout << "Still Averaging: " << ps.getScanNumber() << endl;
  //cout << " with: ";
  //for(i=0;i<v.size();i++) cout << bs[v[i]].getScanNumber() << " ";
  //cout << endl;

  //cout << numScans << " " << v.size() << endl;

  //Match peaks between pivot scan and neighbors
  for(i=0;i<(int)v.size();i++) vPos.push_back(0);
  for(i=0;i<(int)ps.size();i++){ //iterate all points
    prec = c * ps.at(i).mz * ps.at(i).mz / 2;
    match=1;

    for(k=0;k<(int)v.size();k++){ //iterate all neighbors
      dif=100000.0;
      //cout << "Checking " << bs[v[k]].getScanNumber() << " pos " << vPos[k] << endl;

      for(j=vPos[k];j<bs[v[k]].size();j++){ //check if point is a match
        dt=fabs(ps.at(i).mz-bs[v[k]].at(j).mz);
        if(dt<=dif) {
          if(dt<prec) {
            ps.at(i).intensity+=bs[v[k]].at(j).intensity;
            vPos[k]=j+1;
            match++;
            break;
          }
          dif=dt;
        } else {
          vPos[k]=j-1;
          break;
        }
      }

    }

    //if data point was not visible across enough scans, set it to 0
		if(match<cs.boxcarFilter && match<(int)v.size()) ps.at(i).intensity=0.0;

  }


  //Average points and apply cutoff
  for(i=0;i<ps.size();i++) {
    ps.at(i).intensity/=numScans;
    sp.add(ps.at(i));
    //if(ps.at(i).intensity>=cutoff) sp.add(ps.at(i));
  }

  sp.setScanNumber(ps.getScanNumber());
  sp.setScanNumber(ps.getScanNumber(true),true);
  sp.setRTime(ps.getRTime());
  sp.setRawFilter(cFilter1);

  //clear unused buffer
  if(posLeft>0){
    while(posLeft>0){
      bs.pop_front();
      posLeft--;
      posA--;
    }
  }

  //cout << "Done averaging" << endl;

  return true;
}

bool CNoiseReduction::DeNoiseB(Spectrum& sp){

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  if(pos==0){
    if((cs.scan.iLower>0)) {
      if(!ScanAveragePlusDeNoise(sp,&cs.inFile[0],(int)(cs.boxcar/2),0.1f,cs.scan.iLower)) return false;
    } else {
      ScanAveragePlusDeNoise(sp,&cs.inFile[0],(int)(cs.boxcar/2),0.1f);
    }
    pos=1;
  } else {
    ScanAveragePlusDeNoise(sp,NULL,(int)(cs.boxcar/2),0.1f);
  }

  if(sp.getScanNumber()==0) return false;
  if (!cs.centroid) FirstDerivativePeaks(sp, 1);
  return true;

}

bool CNoiseReduction::DeNoiseC(Spectrum& sp){

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  if(pos==0){
    if(cs.scan.iLower>0) {
      if(!NewScanAveragePlusDeNoise(sp,cs.inFile,(int)(cs.boxcar/2),0.1f,cs.scan.iLower)) return false;
    } else {
      NewScanAveragePlusDeNoise(sp,cs.inFile,(int)(cs.boxcar/2),0.1f);
    }
    pos=1;
  } else {
    NewScanAveragePlusDeNoise(sp,NULL,(int)(cs.boxcar/2),0.1f);
  }

  if(sp.getScanNumber()==0) return false;
  if(!cs.centroid) FirstDerivativePeaks(sp,1);
  return true;

}

bool CNoiseReduction::DeNoiseD(Spectrum& sp){

  Spectrum tmpSpec;  
  vector<int> v;

  sp.clear();

  if(pos==0){
    if(cs.scan.iLower>0) {
      if(!NewScanAverage(sp,cs.inFile,(int)(cs.boxcar/2),0.1f,cs.scan.iLower)) return false;
    } else {
      NewScanAverage(sp,cs.inFile,(int)(cs.boxcar/2),0.1f);
    }
    pos=1;
  } else {
    NewScanAverage(sp,NULL,(int)(cs.boxcar/2),0.1f);
  }

  if(sp.getScanNumber()==0) return false;
  if (!cs.centroid) FirstDerivativePeaks(sp, 1);
  return true;

}

double CNoiseReduction::CParam(Spectrum& sp, int tot){
  int i;
  int j=0;
  double d=0.0;

  if(sp.size()<10) i=1;
  else i=sp.size()/2;
  
  while(i<sp.size()-1){
    if(sp.at(i).intensity > sp.at(i-1).intensity && sp.at(i).intensity > sp.at(i+1).intensity) {
      d+= ((1/sp.at(i).mz) - (1/sp.at(i+1).mz));
      j++;
      i++;
      if(j==tot) break;
    } else {
      i++;
    }
  }

  if(j==0) return 0.0;
  else return d/j;
}

bool CNoiseReduction::NewScanAveragePlusDeNoise(Spectrum& sp, std::string file, int width, float cutoff, int scanNum){
  
  Spectrum ts;

  vector<int> vPos;
 
  int i;
  int j;
  int k;
  int m;
  int widthCount=0;
  int numScans=1;
  int match;
  double dif;
  double prec=0;
  double dt;
  double c=0;
  double ppm;

  bool bLeft=true;
  int posLeft;
  int posRight;
  int index;
  char cFilter1[256];
  char cFilter2[256];

  sp.clear();

  Spectrum* specs;
  specs = new Spectrum[width*2+1];

  //if file is not null, create new buffer
  if(file.length()){
    lastFile = file;
    bs.clear();
    if(scanNum>0) r->readFile(file.c_str(),ts,scanNum);
    else r->readFile(file.c_str(),ts);
    if(ts.getScanNumber()==0) {
      delete [] specs;
      return false;
    }
    bs.push_back(ts);
    specs[0]=bs[0];
    if (!cs.centroid) c = CParam(specs[0], 3);
    posA=0;
  } else {
    posA++;
    if(posA>=(int)bs.size()) { //end of buffer, no more data
      delete [] specs;
      return false; 
    }
    specs[0]=bs[posA];
    if (!cs.centroid) c = CParam(specs[0], 3);
  }

  //set our pivot spectrum
  specs[0].getRawFilter(cFilter1,256);

  posLeft=posA;
  posRight=posA;
  while(widthCount<(width*2)){

    index=-1;

    //Alternate looking left and right
    if(bLeft){
      bLeft=false;
      widthCount++;
      
      while(true){
        posLeft--;
        if(posLeft<0) { //buffer is too short on left, add spectra
          i=bs[0].getScanNumber();
          while(true){
            i--;
            if(i==0) break;
            r->readFile(lastFile.c_str(),ts,i);
            if(ts.getScanNumber()==0) continue;
            else break;
          }
          if(i==0) break;
          bs.push_front(ts);
          posA++;
          posRight++;
          posLeft=0;
          ts.getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posLeft;
            break;
          }
        } else {
          bs[posLeft].getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posLeft;
            break;
          }
        }
      }

    } else {
      bLeft=true;
      widthCount++;

      while(true){
        posRight++;
        if(posRight>=(int)bs.size()) { //buffer is too short on right, add spectra
          r->readFile(lastFile.c_str(),ts,bs[bs.size()-1].getScanNumber());
          r->readFile(NULL,ts);
          if(ts.getScanNumber()==0) {
            posRight--;
            break;
          }
          bs.push_back(ts);
          ts.getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posRight;
            break;
          }
        } else {
          bs[posRight].getRawFilter(cFilter2,256);
          if(strcmp(cFilter1,cFilter2)==0) {
            index=posRight;
            break;
          }
        }
      }
    }

    if(index==-1)  continue;
   
    //ts=bs[index];
    specs[numScans++]=bs[index];

    //cout << "NumScans: " << numScans << endl;

  }

  //Match peaks between pivot scan (0) and neighbors (the rest)
  //for(m=0;m<cs.ppMatch && m<numScans;m++){
  for(m=0;m<1;m++){

    //cout << "m " << m << " = " << specs[m].getScanNumber() << endl;
    
    vPos.clear();
    for(i=0;i<numScans;i++) vPos.push_back(0);
    //cout << "Checking " << m << " of " << numScans << " points remaining: " << specs[m].size() << endl;

    for(i=0;i<specs[m].size();i++){ //iterate all points
      if(specs[m].at(i).intensity<0.1) continue;
      if (!cs.centroid) prec = c * specs[m].at(i).mz * specs[m].at(i).mz / 2;
      match=1;

      for(k=m+1;k<numScans;k++){ //iterate all neighbors
        dif=100000.0;

        for(j=vPos[k];j<specs[k].size();j++){ //check if point is a match
          //cout << "Checking " << j << " of " << specs[k].size() << endl;
          if(specs[k].at(j).intensity<0.1) continue; //skip meaningless datapoints to speed along
          dt=fabs(specs[m].at(i).mz-specs[k].at(j).mz);
          //cout << dt << "\t" << c << "\t" << prec << endl;
          //dt=specs[m].at(i).mz-specs[k].at(j).mz;
          //if(dt<0.0)dt=-dt;
          if(dt<=dif) {
            if(cs.centroid){
              ppm = dt / specs[k].at(j).mz*1e6;
              if(ppm<cs.ppm){
                specs[m].at(i).intensity += specs[k].at(j).intensity;
                vPos[k] = j + 1;
                specs[k].at(j).intensity = -1.0;
                match++;
                break;
              }
            } else if(dt<prec) {
              specs[m].at(i).intensity+=specs[k].at(j).intensity;
              vPos[k]=j+1;
              specs[k].at(j).intensity=-1.0;
              match++;
              break;
            }
            dif=dt;
          } else {
            vPos[k]=j-1;
            break;
          }
        }

      }//for k

      //if data point was not visible across enough scans, set it to 0
			if(match<cs.boxcarFilter && match<numScans) {
        //ignore point
        //cout << "BAH! " << specs[m].at(i).mz << " has " << match << " matches." << endl;
      } else {
        //add to temp spectrum
        //cout << specs[m].at(i).mz << " has " << match << " matches." << endl;
        //sp.add(specs[m].at(i).mz,specs[m].at(i).intensity/numScans);
        sp.add(specs[m].at(i).mz,specs[m].at(i).intensity/match);
      }

    } //next i

  } //next m

  //sort
  //cout << "Done " << sp.size() << endl;
  if(sp.size()>0) sp.sortMZ();
  sp.setScanNumber(specs[0].getScanNumber());
  sp.setScanNumber(specs[0].getScanNumber(true),true);
  sp.setRTime(specs[0].getRTime());
  sp.setRawFilter(cFilter1);

  //clear unused buffer
  if(posLeft>0){
    while(posLeft>0){
      bs.pop_front();
      posLeft--;
      posA--;
    }
  }

  delete [] specs;

  return true;
}

