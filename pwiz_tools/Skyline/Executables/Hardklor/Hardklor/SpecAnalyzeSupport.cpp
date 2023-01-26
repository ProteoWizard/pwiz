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
#include "SpecAnalyzeSupport.h"

using namespace std;
using namespace MSToolkit;

/* ******************************************************* */
/*									CPeakPrediction												 */
/* ******************************************************* */

//Constructors & Destructors
CPeakPrediction::CPeakPrediction() { 
	mz=0;
	intensity=0;
	charges = new vector<int>; 
}
CPeakPrediction::CPeakPrediction(const CPeakPrediction& p) { 
	mz = p.mz;
	intensity = p.intensity;
	charges = new vector<int>;
	for(unsigned int i=0;i<p.charges->size();i++) charges->push_back(p.charges->at(i));
}
CPeakPrediction::~CPeakPrediction() { 
	delete charges; 
}

//Overloaded operators
CPeakPrediction& CPeakPrediction::operator=(const CPeakPrediction& p){
	if(this!=&p){
		mz = p.mz;
		intensity = p.intensity;
		delete charges;
		charges = new vector<int>;
		for(unsigned int i=0;i<p.charges->size();i++) charges->push_back(p.charges->at(i));
	}
	return *this;
}

//Functions
void CPeakPrediction::AddCharge(int charge){ charges->push_back(charge); }
void CPeakPrediction::Clear(){ charges->clear(); }
void CPeakPrediction::EraseCharge(int index){ charges->erase(charges->begin()+index); }
int CPeakPrediction::GetCharge(int index){ return charges->at(index); }
float CPeakPrediction::GetIntensity(){ return intensity; }
double CPeakPrediction::GetMZ(){ return mz; }
void CPeakPrediction::SetIntensity(float newIntens){ intensity=newIntens; }
void CPeakPrediction::SetMZ(double newMZ){ mz=newMZ; }
int CPeakPrediction::Size(){ return (int)charges->size(); }




/* ******************************************************* */
/*                  CPeptideVariant                        */
/* ******************************************************* */

//Constructors & Destructors
CPeptideVariant::CPeptideVariant(){
  match = new Peak_T[1];
  matchSize=1;
  mismatch = new Peak_T[1];
  mismatchSize=1;
  extra = new vector<Peak_T>;
  charge=1;
  monoMass=0;
  distArea=0;
}

CPeptideVariant::CPeptideVariant(const CPeptideVariant& p){
  int i;

  matchSize = p.matchSize;
  mismatchSize = p.mismatchSize;
  charge = p.charge;
  monoMass = p.monoMass;
  variant = p.variant;
  distArea = p.distArea;
  strcpy(formula,p.formula);

  match = new Peak_T[matchSize];
  mismatch = new Peak_T[mismatchSize];
  extra = new vector<Peak_T>;

  for(i=0;i<p.matchSize;i++) match[i]=p.match[i];
  for(i=0;i<p.mismatchSize;i++) mismatch[i]=p.mismatch[i];
  for(i=0;i<(int)p.extra->size();i++) extra->push_back(p.extra->at(i));
}

CPeptideVariant::~CPeptideVariant(){
  delete [] match;
  delete [] mismatch;
  delete extra;
}

//Overloaded Operators
CPeptideVariant& CPeptideVariant::operator=(const CPeptideVariant& p) {
  int i;

  if(this!=&p){
    matchSize = p.matchSize;
    mismatchSize = p.mismatchSize;
    charge = p.charge;
    monoMass = p.monoMass;
    variant = p.variant;
    distArea = p.distArea;
    strcpy(formula,p.formula);

    delete [] match;
    delete [] mismatch;
    delete extra;

    match = new Peak_T[matchSize];
    mismatch = new Peak_T[mismatchSize];
    extra = new vector<Peak_T>;

    for(i=0;i<p.matchSize;i++) match[i]=p.match[i];
    for(i=0;i<p.mismatchSize;i++) mismatch[i]=p.mismatch[i];
    for(i=0;i<(int)p.extra->size();i++) extra->push_back(p.extra->at(i));
  }
  return *this;
}

//Functions
void CPeptideVariant::AddExtra(double& mz, float& intensity) {
	Peak_T p;
	p.mz=mz;
	p.intensity=intensity;
	extra->push_back(p);
}
void CPeptideVariant::AddMatch(int& index, double& mz, float& intensity) {
	Peak_T p;
	p.mz=mz;
	p.intensity=intensity;
	match[index]=p;
}
void CPeptideVariant::AddMismatch(int& index, double& mz, float& intensity) {
	Peak_T p;
	p.mz=mz;
	p.intensity=intensity;
	mismatch[index]=p;
}
void CPeptideVariant::Clear(){
	int i;
	CHardklorVariant v;
	delete [] match;
	delete [] mismatch;
	delete extra;
	match = new Peak_T[matchSize];
	for(i=0;i<matchSize;i++) {
		match[i].mz=0;
		match[i].intensity=0;
	}
	mismatch = new Peak_T[mismatchSize];
	for(i=0;i<mismatchSize;i++) {
		mismatch[i].mz=0;
		mismatch[i].intensity=0;
	}
	extra = new vector<Peak_T>;
	charge=0;
	monoMass=0;
	variant=v;
	distArea=0;
	strcpy(formula,"");
}
void CPeptideVariant::DeleteExtra(){
	delete extra;
	extra = new vector<Peak_T>;
}
double& CPeptideVariant::GetArea() { return distArea; }
int& CPeptideVariant::GetCharge() { return charge; }
Peak_T& CPeptideVariant::GetExtra(int& index) { return extra->at(index); }
char* CPeptideVariant::GetFormula() { return formula; }
CHardklorVariant& CPeptideVariant::GetHKVariant() { return variant; }
Peak_T& CPeptideVariant::GetMatch(int& index) { return match[index]; }
Peak_T& CPeptideVariant::GetMismatch(int& index) { return mismatch[index]; }
double& CPeptideVariant::GetMonoMass() { return monoMass; }
void CPeptideVariant::SetArea(double& d) { distArea=d; }
void CPeptideVariant::SetCharge(int ch) { charge=ch; }
void CPeptideVariant::SetFormula(char form[64]) { strcpy(formula,form); }
void CPeptideVariant::SetMatchSize(int& num) {
	delete [] match;
	match = new Peak_T[num];
	for(int i=0;i<num;i++) {
		match[i].mz=0;
		match[i].intensity=0;
	}
	matchSize = num;
}
void CPeptideVariant::SetMismatchSize(int& num) {
  delete [] mismatch;
  mismatch = new Peak_T[num];
  for(int i=0;i<num;i++) {
    mismatch[i].mz=0;
    mismatch[i].intensity=0;
  }
  mismatchSize = num;
}
void CPeptideVariant::SetMonoMass(double mass) { monoMass=mass; }
void CPeptideVariant::SetVariant(CHardklorVariant &v) { variant=v; }
int CPeptideVariant::SizeExtra() { return (int)extra->size(); }






/* ******************************************************* */
/*                CPeptidePrediction                       */
/* ******************************************************* */

//Constructors & Destructors
CPeptidePrediction::CPeptidePrediction(){
	variantList = new vector<CPeptideVariant>;
	mz=0;
	intensity=0;
	bestVar=0;
	maxPeakIndex=0;
}
CPeptidePrediction::CPeptidePrediction(const CPeptidePrediction& p){
	mz=p.mz;
	intensity=p.intensity;
	bestVar=p.bestVar;
	maxPeakIndex=p.maxPeakIndex;
	variantList = new vector<CPeptideVariant>;
	for(unsigned int i=0;i<p.variantList->size();i++){
		variantList->push_back(p.variantList->at(i));
	}
}
CPeptidePrediction::~CPeptidePrediction(){
	delete variantList;
}

//Overloaded operators
CPeptidePrediction& CPeptidePrediction::operator=(const CPeptidePrediction& p){
	if(this!=&p){
		mz=p.mz;
		intensity=p.intensity;
		bestVar=p.bestVar;
		maxPeakIndex=p.maxPeakIndex;
		delete variantList;
		variantList = new vector<CPeptideVariant>;
		for(unsigned int i=0;i<p.variantList->size();i++){
			variantList->push_back(p.variantList->at(i));
		}
	}
	return *this;
}

//Functions
void CPeptidePrediction::AddVariant(CPeptideVariant& var) { variantList->push_back(var); }
void CPeptidePrediction::Clear() {
	delete variantList;
	variantList = new vector<CPeptideVariant>;
	mz=0;
	intensity=0;
	bestVar=0;
}
float CPeptidePrediction::GetIntensity() { return intensity; }
int CPeptidePrediction::GetMaxPeakIndex() { return maxPeakIndex; }
double CPeptidePrediction::GetMZ() { return mz; }
CPeptideVariant& CPeptidePrediction::GetVariant(int& index) { return variantList->at(index); }
void CPeptidePrediction::SetIntensity(float intens) { intensity = intens; }
void CPeptidePrediction::SetMaxPeakIndex(int index) { maxPeakIndex = index; }
void CPeptidePrediction::SetMZ(double mass) { mz = mass; }
int CPeptidePrediction::VariantListSize() { return (int)variantList->size(); }

float& CPeptidePrediction::GetIntensityAt(int& variant, int& mz) {
	return variantList->at(variant).GetMatch(mz).intensity;
}

