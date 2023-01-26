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
#ifndef _CMODELLIBRARY_H
#define _CMODELLIBRARY_H

#include "HardklorTypes.h"
#include "CAveragine.h"
#include "CMercury8.h"
#include "CHardklorVariant.h"
#include <vector>

class CModelLibrary {
public:

	//Constructors & Destructors
	CModelLibrary(CAveragine* avg, CMercury8* mer);
	~CModelLibrary();

	//User functions
  bool buildLibrary(int lowCharge, int highCharge, std::vector<CHardklorVariant>& pepVariants);
	void eraseLibrary();
	mercuryModel* getModel(int charge, int var, double mz);
  void setSize(double mz);

protected:

private:

	//Data Members
	int chargeMin;
	int chargeCount;
	int varCount;
	int merCount;

	CAveragine* averagine;
	CMercury8* mercury;
	mercuryModel*** libModel;

};

#endif