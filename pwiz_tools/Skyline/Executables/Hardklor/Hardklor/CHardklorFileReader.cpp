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
#include "CHardklorFileReader.h"

CHardklorFileReader::CHardklorFileReader(){
  posNative=0;
  posNR=0;
}

CHardklorFileReader::CHardklorFileReader(CHardklorSetting& hs){
  cs=hs;
  posNative=0;
  posNR=0;
}

CHardklorFileReader::~CHardklorFileReader(){
}

/*
bool CHardklorFileReader::initBuffers(){

  int i,j,k;
  Spectrum tmpSpec;
  char cFilter1[256];
  char cFilter2[256];
  cpar c;

  //Check if user requested starting from specific spectrum
  if((cs.scan.iLower>0)) {
    k=cs.scan.iLower; 
    r.readFile(&cs.inFile[0],tmpSpec,cs.scan.iLower);
  } else {
    r.readFile(&cs.inFile[0],tmpSpec);
    k=tmpSpec.getScanNumber();
  }
  if(tmpSpec.getScanNumber()==0) return false;
  tmpSpec.getRawFilter(cFilter1,256);
      
  //Gather left side of scan
  i=1;
  j=0;
  while( k-i > 0){
    r.readFile(&cs.inFile[0],tmpSpec,cs.scan.iLower-i);
    if(tmpSpec.getScanNumber()==0) {
      i++;
      continue;
    }
    tmpSpec.getRawFilter(cFilter2,256);

    c.scan=tmpSpec.getScanNumber();
    c.c=nr.CParam(tmpSpec,1);
    
    //Assume High resolution data at all times
    if(!cs.centroid) {
      nr.FirstDerivativePeaks(tmpSpec,1);
      tmpSpec.setRawFilter(cFilter2);
    }

    sNative.push_front(tmpSpec);
    dc.push_front(c);
    if(strcmp(cFilter1,cFilter2)==0){
      j++;
      if(j==cs.noiseWindow+cs.rawAvgWidth) break;
    }
    i++;
  }

  //Get our position again
  r.readFile(&cs.inFile[0],tmpSpec,k);

  c.scan=tmpSpec.getScanNumber();
  c.c=nr.CParam(tmpSpec,1);

  //Assume High resolution data at all times
  if(!cs.centroid) {
    nr.FirstDerivativePeaks(tmpSpec,1);
    tmpSpec.setRawFilter(cFilter1);
  }
    
  //Add first target scan
  sNative.push_back(tmpSpec);
  posNative=sNative.size()-1; 

  dc.push_back(c);

  //Add right side of scan
  i=1;
  j=0;
  while(true){
    r.readFile(NULL,tmpSpec);
    if(tmpSpec.getScanNumber()==0) break;
    tmpSpec.getRawFilter(cFilter2,256);

    c.scan=tmpSpec.getScanNumber();
    c.c=nr.CParam(tmpSpec,1);

    //Assume High resolution data at all times
    if(!cs.centroid) {
      nr.FirstDerivativePeaks(tmpSpec,1);
      tmpSpec.setRawFilter(cFilter2);
    }

    dc.push_back(c);

    sNative.push_back(tmpSpec);
    if(strcmp(cFilter1,cFilter2)==0){  
      j++;
      if(j==cs.noiseWindow+cs.rawAvgWidth) break;
    }
    i++;
  }




  //Initialize NR data
  //Find first noise reduced scan to buffer
  posNR=0;
  j=0;

  while(posNR<=posNative){
    sNative[posNR].getRawFilter(cFilter1,256);

    //Get left side spectrum
    vector<Spectrum> vNR;
    int pivot=0;
    i=1;
    j=0;
    while(j<cs.rawAvgWidth && posNR-i>=0){
      sNative[posNR-i].getRawFilter(cFilter2,256);
      if(strcmp(cFilter1,cFilter2)==0) {
        j++;
        vNR.push_back(sNative[posNR-i]);
      }
      i--;
    }

    //Add center (desired) spectrum
    vNR.push_back(sNative[posNR]);
    pivot=vNR.size()-1;

    //Add right side of spectrum
    i=1;
    j=0;
    while(j<cs.rawAvgWidth){
      sNative[posNR+i].getRawFilter(cFilter2,256);
      if(strcmp(cFilter1,cFilter2)==0) {
        j++;
        vNR.push_back(sNative[posNR+i]);
      }
      i++;
    }

    //Get noise reduced spectrum
    //for(i=0;i<vNR.size();i++) cout << vNR[i].getScanNumber() << " ";
    //cout << " - " << pivot << endl;
    nr.DeNoise(tmpSpec,vNR,pivot);
    tmpSpec.setRawFilter(cFilter1);
    sNR.push_back(tmpSpec);

    posNR++;

  }

  posNR--;
  //posNative=sNative.size()-1;

  cout << "Init scans: " << sNative[posNative].getScanNumber() << " " << sNR[posNR].getScanNumber() << endl;

}
*/

/*
bool CHardklorFileReader::getProcessedSpectrum(Spectrum& sp){

  sp.clear();

  int i,j,k;
  vector<Spectrum> vs;
  Spectrum tmpSpec;
  int pivot;
  char cFilter1[256];
  char cFilter2[256];
  double cParam;

  //Get current filter
  sNR[posNR].getRawFilter(cFilter1,256);

  //get Cparam
  j=sNR[posNR].getScanNumber();
  for(i=0;i<dc.size();i++){
    if(dc[i].scan==j){
      cParam=dc[i].c;
      break;
    }
  }

  cout << "Looking for: " << sNR[posNR].getScanNumber() << "  " << posNR << " : " << cFilter1 << endl;

  //Get left of noise reduced
  i=0;
  j=0;
  k=0;
  while(posNR-i>0){
    i++;
    sNR[posNR-i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      cout << sNR[posNR-i].getScanNumber() << " matches " << sNR[posNR].getScanNumber() << " with " << cFilter1 << " and " << cFilter2 << endl;
      vs.push_back(sNR[posNR-i]);
      j++;
      if(j==cs.rawAvgWidth) break;
    }
  }
  if(posNR-i>0) k=posNR-i;

  //for(i=0;i<posNR;i++){
  //  sNR[i].getRawFilter(cFilter2,256);
  //  if(strcmp(cFilter1,cFilter2)==0) vs.push_back(sNR[i]);
  //}

  //Add target spectrum
  vs.push_back(sNR[posNR]);
  pivot=vs.size()-1;

  //Add right side, expanding if needed
  i=0;
  j=0;
  while(j<cs.rawAvgWidth){
    i++;
    while(i+posNR>=sNR.size()) {
      if(getNRSpectrum(tmpSpec,i+posNR)){
        sNR.push_back(tmpSpec);
      } else {
        cout << "FALSE!!!" << endl;
        //dunno
      }
    }
    sNR[i+posNR].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      vs.push_back(sNR[i+posNR]);
      j++;
    }   
  }

  nr.ScanAverage(sp,vs,pivot,(float)cs.rawAvgCutoff,cParam);
  cout << "Average for: " << sp.getScanNumber() << " made of ";
  for(i=0;i<vs.size();i++) cout << vs[i].getScanNumber() << " ";
  cout << endl;

  FILE *f=fopen("scaninfo.txt","at");
  fprintf(f,"Averaged Scan: %d\n",sp.getScanNumber());
  for(i=0;i<sp.size();i++) fprintf(f,"%lf %f\n",sp.at(i).mz,sp.at(i).intensity);
  fclose(f);

  //posNative++;
  posNR++;

  cout << "Avg ok" << endl;
  cout << "A: " << sNR.size() << " " << posNative << " " << posNR << endl;

  //Pop front
  if(k>0){
    for(i=0;i<k;i++){
      sNR.pop_front();
      posNR--;
      //posNative--;
    }
  }

  cout << "B: " << sNR.size() << " " << posNative << " " << posNR << endl;

  //cout << "After getting avg: " << sNative[posNative].getScanNumber() << " " << sNR[posNR].getScanNumber() << endl;

  return true;
  
}
*/

/*
bool CHardklorFileReader::getNRSpectrum(Spectrum& sp, int pos){

  int i,j,k;
  char cFilter1[256];
  char cFilter2[256];
  cpar c;

  Spectrum tmpSpec;
  vector<int> vNR;

  sp.clear();

  vNR.clear();
  if(sNR.size()==0) {
    pos=0;
  } else {
    int target=sNR[sNR.size()-1].getScanNumber();
    for(i=0;i<sNative.size();i++){
      if(sNative[i].getScanNumber()==target) break;
    }
    pos=i+1;
  }
  sNative[pos].getRawFilter(cFilter1,256);
  cout << "Adding " << pos << " sc: " << sNative[pos].getScanNumber() << endl;
  vNR.push_back(pos);

  //look left
  for(i=pos-1;i>=0;i--){
    sNative[i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      vNR.push_back(i);
      if(vNR.size()==cs.noiseWindow) break;
    }
  }

  //look right
  j=0;
  for(i=pos+1;i<sNative.size();i++){
    sNative[i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      vNR.push_back(i);
      j++;
      if(j==cs.noiseWindow) break;
    }
  }

  //if we need to add right, we delete on left
  while(j<cs.noiseWindow){
    r.readFile(NULL,tmpSpec);
    if(tmpSpec.getScanNumber()==0) break;    
    tmpSpec.getRawFilter(cFilter2,256);

    c.scan=tmpSpec.getScanNumber();
    c.c=nr.CParam(tmpSpec,1);

    //Assume High resolution data at all times
    if(!cs.centroid) {
      nr.FirstDerivativePeaks(tmpSpec,1);
      tmpSpec.setRawFilter(cFilter2);
    }

    dc.pop_front();

    sNative.pop_front();
    //change indexes too
    i--;
    for(j=0;j<vNR.size();j++) vNR[j]--;
    pos--;

    dc.push_back(c);

    sNative.push_back(tmpSpec);
    if(strcmp(cFilter1,cFilter2)==0) {
      vNR.push_back(sNative.size()-1);
      j++;
    }
  }

  //if there is nothing to compare to, exit now
  if(vNR.size()==0) return false;

  vector<Spectrum> vs;
  for(i=0;i<vNR.size();i++) vs.push_back(sNative[vNR[i]]);

  nr.DeNoise(sp,vs,0);

  FILE *f=fopen("scaninfo.txt","at");
  fprintf(f,"Denoise Scan: %d\n",sp.getScanNumber());
  for(i=0;i<sp.size();i++) fprintf(f,"%lf %f\n",sp.at(i).mz,sp.at(i).intensity);
  fclose(f);

  cout << "Noise reduced for: " << sp.getScanNumber() << " made of ";
  for(i=0;i<vNR.size();i++) cout << sNative[vNR[i]].getScanNumber() << " ";
  cout << endl;
  sp.setRawFilter(cFilter1);
  return true;

}
*/

/*
bool CHardklorFileReader::getProcessedSpectrumB(Spectrum& sp){
  int i,j,k;
  MSReader r2;
  char cFilter1[256];
  char cFilter2[256];
  sp.clear();

  Spectrum tmpSpec;
  r2.readFile(&cs.inFile[0],tmpSpec,vScans[posNR]);
  tmpSpec.getRawFilter(cFilter,256);

  //Identify spectra to be averaged on left side
  i=1;
  j=0;
  while(true){
    r2.readFile(&cs.inFile[0],tmpSpec,

  
  vector<Spectrum> vs;
  Spectrum tmpSpec;
  int pivot;
  

  //Get current filter
  sNR[posNR].getRawFilter(cFilter1,256);

  cout << "Looking for: " << sNR[posNR].getScanNumber() << "  " << posNR << " : " << cFilter1 << endl;

  //Get left of noise reduced
  i=0;
  j=0;
  k=0;
  while(posNR-i>0){
    i++;
    sNR[i].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      vs.push_back(sNR[posNR-i]);
      j++;
      if(j==cs.rawAvgWidth) break;
    }
  }
  if(posNR-i>0) k=posNR-i;

  //for(i=0;i<posNR;i++){
  //  sNR[i].getRawFilter(cFilter2,256);
  //  if(strcmp(cFilter1,cFilter2)==0) vs.push_back(sNR[i]);
  //}

  //Add target spectrum
  vs.push_back(sNR[posNR]);
  pivot=vs.size()-1;

  //Add right side, expanding if needed
  i=0;
  j=0;
  while(j<cs.rawAvgWidth){
    i++;
    while(i+posNR>=sNR.size()) {
      if(getNRSpectrum(tmpSpec,i+posNR)){
        sNR.push_back(tmpSpec);
      } else {
        cout << "FALSE!!!" << endl;
        //dunno
      }
    }
    sNR[i+posNR].getRawFilter(cFilter2,256);
    if(strcmp(cFilter1,cFilter2)==0) {
      vs.push_back(sNR[i+posNR]);
      j++;
    }   
  }

  nr.ScanAverage(sp,vs,pivot,(float)cs.rawAvgCutoff);
  cout << "Average for: " << sp.getScanNumber() << " made of ";
  for(i=0;i<vs.size();i++) cout << vs[i].getScanNumber() << " ";
  cout << endl;

  FILE *f=fopen("scaninfo.txt","at");
  fprintf(f,"Averaged Scan: %d\n",sp.getScanNumber());
  for(i=0;i<sp.size();i++) fprintf(f,"%lf %f\n",sp.at(i).mz,sp.at(i).intensity);
  fclose(f);

  //posNative++;
  posNR++;

  cout << "Avg ok" << endl;
  cout << "A: " << sNR.size() << " " << posNative << " " << posNR << endl;

  //Pop front
  if(k>0){
    for(i=0;i<k;i++){
      sNR.pop_front();
      posNR--;
      //posNative--;
    }
  }

  cout << "B: " << sNR.size() << " " << posNative << " " << posNR << endl;

  cout << "After getting avg: " << sNative[posNative].getScanNumber() << " " << sNR[posNR].getScanNumber() << endl;

  return true;
  
}
*/