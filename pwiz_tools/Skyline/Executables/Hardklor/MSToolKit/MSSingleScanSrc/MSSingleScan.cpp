/*
Copyright 2011, Michael R. Hoopmann, Institute for Systems Biology
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

#include <iostream>
#include <iomanip>
#include <cmath>
#include "MSToolkitTypes.h"
#include "MSReader.h"
#include "MSObject.h"
#include "Spectrum.h"

using namespace std;
using namespace MSToolkit;

int main(int argc, char *argv[]){

	//Here are all the variable we are going to need
	MSReader r;
	Spectrum s;
  int j;

  if(argc==1){
    printf("DESCRIPTION: Reads an MS/MS spectrum from any MSToolkit supported file type and outputs to screen in MS2 format.\n\n");
    printf("USAGE: MSSingleScan [scan number] [file]\n");
    exit(0);
  }

  r.addFilter(MS1);
  r.addFilter(MS2);
  r.addFilter(MS3);
	r.addFilter(MSX);
  r.addFilter(SRM);

  char nativeID[256];
	r.readFile(argv[2],s,atoi(argv[1]));
  if(s.getScanNumber()==0) exit(-1);

  cout << s.getMsLevel() << endl;
  cout << s.sizeMZ() << "\t" << s.getMZ(0) << endl;
  exit(1);


  char szNativeID[128];
  if (s.getNativeID(szNativeID, 128))
      printf("success:  scan %d  nativeID: %s\n", s.getScanNumber(), szNativeID);
  else
      printf("failure:  scan %d\n", s.getScanNumber());

  printf("size: %d\n", s.sizeMZ());

  s.getNativeID(nativeID, 256);
  printf("%s\n",nativeID);
  printf("S\t%d\t%d",s.getScanNumber(),s.getScanNumber());
	for(j=0;j<s.sizeMZ();j++){
		printf("\t%.*lf",4,s.getMZ(j));
	}
	printf("\n");
	if(s.getRTime()>0) printf("I\tRTime\t%.*f\n",4,s.getRTime());
	//printf("I\tConvA\t%.6lf\n",s.getConversionA());
	//printf("I\tConvB\t%.6lf\n",s.getConversionB());
	//printf("I\tConvC\t%.6lf\n",s.getConversionC());
	//printf("I\tConvD\t%.6lf\n",s.getConversionD());
	//printf("I\tConvE\t%.6lf\n",s.getConversionE());
	//printf("I\tConvI\t%.6lf\n",s.getConversionI());
	for(j=0;j<s.sizeZ();j++){
		printf("Z\t%d\t%.*f\n",s.atZ(j).z,6,s.atZ(j).mh);
	}

	for(j=0;j<s.size();j++){
		printf("%.4f %.4f\n",s.at(j).mz,s.at(j).intensity); 
	}

  return 0;

}
  

