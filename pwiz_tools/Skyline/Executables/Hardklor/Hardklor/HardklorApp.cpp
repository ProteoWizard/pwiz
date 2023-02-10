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
#include <fstream>
#include <string>

#include "CHardklor.h"
#include "CHardklor2.h"
#include "CModelLibrary.h"
#include "CHardklorParser.h"
#include "CHardklorSetting.h"
#include "CHardklorVariant.h"

using namespace std;

int main(int argc, char* argv[]) {
  int i;
	unsigned int j;
  char tstr[512]="\0";
  fstream fptr;

	CAveragine *averagine;
	CMercury8 *mercury;
	CModelLibrary *models;

	cout << "Hardklor v2.3.3, October 5 2020" << endl;
	cout << "Mike Hoopmann, Mike MacCoss\nCopyright 2007-2020\nUniversity of Washington" << endl;
	if(argc < 2 || (argc>2 && strcmp(argv[1],"-cmd")!=0) ){
		cout << "Usage:\t\thardklor <config file>\n";
    cout << "\t\thardklor -cmd [options] <input file> <output file>\n" << endl;
		cout << "See documentation for instructions to modify and use config files." << endl;
		exit(1);
	}
	cout << "\nAdaptations for Skyline integration Copyright 2023 University of Washington\n" << endl;
	cout << "Note: generated averagine formulas are for isotope envelope approximation only and do not represent actual molecule identifications.\n" << endl;

  CHardklorParser hp;
  if(strcmp(argv[1],"-cmd")==0){
    if(!hp.parseCMD(argc, argv)){
      exit(-3);
    }
  } else if(!hp.parseConfig(argv[1])){
    cout << "\nThere was an error parsing your configuration file." << endl;  
    exit(-2);
  }

  //Create all the output files that will be used
  for(i=0;i<hp.size();i++){
    fptr.clear();
    fptr.open(&hp.queue(i).outFile[0],ios::out);
    fptr.close();
  }

	averagine = new CAveragine(hp.queue(0).MercuryFile.c_str(),hp.queue(0).HardklorFile.c_str());
	mercury = new CMercury8(hp.queue(0).MercuryFile.c_str());
	models = new CModelLibrary(averagine,mercury);

  CHardklor h(averagine,mercury);
	CHardklor2 h2(averagine,mercury,models);
	vector<CHardklorVariant> pepVariants;
	CHardklorVariant hkv;

  for(i=0;i<hp.size();i++) {
		if(hp.queue(i).algorithm==Version2){
			pepVariants.clear();
			if(!hp.queue(i).noBase) pepVariants.push_back(hkv);
			for(j=0;j<hp.queue(i).variant->size();j++)  pepVariants.push_back(hp.queue(i).variant->at(j));

			models->eraseLibrary();
      models->setSize(hp.queue(i).maxMolMZ);
			models->buildLibrary(hp.queue(i).minCharge,hp.queue(i).maxCharge,pepVariants);
			h2.GoHardklor(hp.queue(i));
		} else {
			h.GoHardklor(hp.queue(i));
		}
  }

	delete models;
	delete averagine;
	delete mercury;
  
  return 0;
  
}
