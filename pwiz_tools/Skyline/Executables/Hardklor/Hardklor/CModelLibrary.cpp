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
#include "CModelLibrary.h"

using namespace std;
using namespace MSToolkit;

CModelLibrary::CModelLibrary(CAveragine* avg, CMercury8* mer){
	averagine=avg;
	mercury=mer;
  libModel=NULL;

	chargeMin=0;
	chargeCount=0;
	varCount=0;
	merCount=0;
}

CModelLibrary::~CModelLibrary(){
	averagine=NULL;
	mercury=NULL;
	if(libModel!=NULL) {
		eraseLibrary();
		libModel=NULL;
	}
}

bool CModelLibrary::buildLibrary(int lowCharge, int highCharge, vector<CHardklorVariant>& pepVariants){

	int i,j,k;
	unsigned int n;

	vector<Peak_T> vMR;
	Peak_T p;
	float da;
	double mass;
	char av[AV_FORMULA_BUFFER_LENGTH];

	if(libModel!=NULL) {
		cout << "library memory already in use." << endl;
		return false;
	}

	//Fill in boundaries; Note Nov11_2019: This needs dynamic updating, perhaps drawn from a new parameter.
	chargeMin=lowCharge;
	chargeCount=highCharge+1;
	varCount=(int)pepVariants.size();
	if(merCount==0) merCount=1000;

	libModel = new mercuryModel**[chargeCount];
	for(i=chargeMin;i<chargeCount;i++){

		libModel[i] = new mercuryModel*[varCount];
		for(j=0;j<varCount;j++){

			libModel[i][j] = new mercuryModel[merCount];
			libModel[i][j][0].area=0.0f;
			libModel[i][j][0].size=0;
			libModel[i][j][0].zeroMass=0.0;
			libModel[i][j][0].peaks=NULL;
			for(k=1;k<merCount;k++){

				mass=k*5*i-(1.007276466*i);
				averagine->clear();
				averagine->calcAveragine(mass,pepVariants[j]);
				averagine->getAveragine(&av[0]);
        //cout << mass << "\t" << pepVariants[j].sizeAtom() << "\t" << pepVariants[j].sizeEnrich() << "\t" << av << endl;
        for(n=0;n<(unsigned int)pepVariants[j].sizeEnrich();n++){
          mercury->Enrich(pepVariants[j].atEnrich(n).atomNum,pepVariants[j].atEnrich(n).isotope,pepVariants[j].atEnrich(n).ape);
        }
				mercury->GoMercury(&av[0],i);
				memmove(libModel[i][j][k].averagine, av, AV_FORMULA_BUFFER_LENGTH);

				vMR.clear();
				da=0.0f;
				for(n=0; n<mercury->FixedData.size(); n++) {
					if(mercury->FixedData[n].data<1.0) continue;
					p.intensity=(float)mercury->FixedData[n].data;
					p.mz=mercury->FixedData[n].mass;
					da+=p.intensity;
					vMR.push_back(p);
				}
				da/=100.0f;

				libModel[i][j][k].area = da;
				libModel[i][j][k].size = (int)vMR.size();
				libModel[i][j][k].peaks = new Peak_T[vMR.size()];
				libModel[i][j][k].zeroMass = mercury->getZeroMass();

				for(n=0;n<vMR.size();n++) libModel[i][j][k].peaks[n]=vMR[n];
			}
		}
	}

	return true;

}

void CModelLibrary::eraseLibrary(){

	int i,j,k;

	if(libModel==NULL) return;

	for(i=chargeMin;i<chargeCount;i++){
		for(j=0;j<varCount;j++){
			for(k=0;k<merCount;k++){
				delete [] libModel[i][j][k].peaks;
			}
			delete [] libModel[i][j];
		}
		delete [] libModel[i];
	}
	delete [] libModel;

	libModel=NULL;
  merCount=0;
	
}

mercuryModel* CModelLibrary::getModel(int charge, int var, double mz){

	int intMZ=(int)(mz/5);

  //punting this for now. A better solution needs to be made, but will wait until necessary.
  if(intMZ>=merCount) {
    cout << "Spectrum feature beyond molecule_max_mz. Hard boundary encountered. Please increase molecule_max_mz." << endl;
    exit(-47);
  }
	return &libModel[charge][var][intMZ];

}

void CModelLibrary::setSize(double mz){
  merCount=(int)(mz/5)+1;
}