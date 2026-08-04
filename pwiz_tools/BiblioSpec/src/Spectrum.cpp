//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

//Class definition for Spectrum

#include <iostream>
#include <cstdlib>
#include <ctime>
#include "Spectrum.h"
#include "pwiz/utility/misc/Std.hpp"


namespace BiblioSpec {

Spectrum::Spectrum() :
    scanNumber_(0), 
    type_(SPEC_UNDEF), 
    mz_(0),
    ionMobility_(0),
    collisionalCrossSection_(0),
    ionMobilityHighEnergyOffset_(0),
    ionMobilityType_(IONMOBILITY_NONE),
    retentionTime_(0),
    startTime_(0),
    endTime_(0),
    totalIonCurrentRaw_(-1),
    totalIonCurrentProcessed_(-1), 
    basePeakIntensityRaw_(-1), 
    basePeakIntensityProcessed_(-1)
{
}

Spectrum::Spectrum(const Spectrum& s) 
{
    scanNumber_ = s.scanNumber_;
    mz_ = s.mz_;
    ionMobility_ = s.ionMobility_;
    ionMobilityType_ = s.ionMobilityType_;
    collisionalCrossSection_ = s.collisionalCrossSection_;
    ionMobilityHighEnergyOffset_ = s.ionMobilityHighEnergyOffset_;
    retentionTime_ = s.retentionTime_;
    startTime_ = s.startTime_;
    endTime_ = s.endTime_;
    type_ = s.type_;
    totalIonCurrentRaw_ = s.totalIonCurrentRaw_;
    totalIonCurrentProcessed_ = s.totalIonCurrentProcessed_;
    basePeakIntensityRaw_ = s.basePeakIntensityRaw_;
    basePeakIntensityProcessed_ = s.basePeakIntensityProcessed_;
    possibleCharges_.assign(s.possibleCharges_.begin(), s.possibleCharges_.end());
    setRawPeaks(s.rawPeaks_);
    setProcessedPeaks(s.processedPeaks_);

    srand(static_cast<unsigned>(time(0)));
}

//Destructor
Spectrum::~Spectrum()
{
}

void Spectrum::clear() {
    scanNumber_ = 0;
    mz_ = 0;
    ionMobility_ = 0;
    ionMobilityType_ = IONMOBILITY_NONE;
    collisionalCrossSection_ = 0;
    ionMobilityHighEnergyOffset_ = 0;
    retentionTime_ = 0;
    startTime_ = 0;
    endTime_ = 0;
    type_ = SPEC_UNDEF;
    possibleCharges_.clear();
    rawPeaks_.clear();
    processedPeaks_.clear();
}

//Assignment operator
Spectrum& Spectrum::operator= (const Spectrum& right) 
{
    if(this == &right) return *this;  //check for self assignment

    scanNumber_ = right.scanNumber_;
    type_ = right.type_;
    mz_ = right.mz_;
    ionMobility_ = right.ionMobility_;
    ionMobilityType_ = right.ionMobilityType_;
    collisionalCrossSection_ = right.collisionalCrossSection_;
    ionMobilityHighEnergyOffset_ = right.ionMobilityHighEnergyOffset_;
    retentionTime_ = right.retentionTime_;
    startTime_ = right.startTime_;
    endTime_ = right.endTime_;
    possibleCharges_ = right.possibleCharges_;
    rawPeaks_ = right.rawPeaks_;
    processedPeaks_ = right.processedPeaks_;
   
    return *this;
}

bool Spectrum::operator< (Spectrum otherSpec) {
    return mz_ < otherSpec.getMz();
}

//getters 
int Spectrum::getScanNumber() const
{
    return scanNumber_;
}
  
double Spectrum::getMz() const
{
    return mz_;
}

double Spectrum::getIonMobility() const
{
    return ionMobility_;
}

IONMOBILITY_TYPE Spectrum::getIonMobilityType() const
{
    return ionMobilityType_;
}

double Spectrum::getCollisionalCrossSection() const
{
    return collisionalCrossSection_;
}

double Spectrum::getRetentionTime() const
{
    return retentionTime_;
}

double Spectrum::getStartTime() const
{
    return startTime_;
}

double Spectrum::getEndTime() const
{
    return endTime_;
}

int Spectrum::getNumRawPeaks() const
{ 
    return (int)rawPeaks_.size();
}

int Spectrum::getNumProcessedPeaks() const
{ 
    return (int)processedPeaks_.size();
}

double Spectrum::getTotalIonCurrentRaw() const
{
    if(totalIonCurrentRaw_ < 0){
        double sum = 0;
        for(size_t i = 0; i < rawPeaks_.size(); i++){
            sum += rawPeaks_[i].intensity;
        }
        return sum;
    }
    return totalIonCurrentRaw_;
}

// In Waters Mse IMS, product ions have kinetic energy added post-drift tube and fly the last part of path to detector slightly faster
double Spectrum::getIonMobilityHighEnergyOffset() const 
{
    if (ionMobilityHighEnergyOffset_ == 0)
    {
        double sum = 0;
        for(size_t i = 0; i < rawPeaks_.size(); i++){
            sum += rawPeaks_[i].driftTime;
        }
        if (sum > 0)
            return (sum/rawPeaks_.size()) - getIonMobility();
    }
    return ionMobilityHighEnergyOffset_;
}

double Spectrum::getTotalIonCurrentProcessed() const
{
    if(totalIonCurrentProcessed_ < 0){
        double sum = 0;
        for(size_t i = 0; i < processedPeaks_.size(); i++){
            sum += processedPeaks_[i].intensity;
        }
        return sum;
    }
    return totalIonCurrentProcessed_;
}

double Spectrum::getBasePeakIntensityRaw() const
{
    if( basePeakIntensityRaw_ < 0 ){
        return (*max_element(rawPeaks_.begin(), rawPeaks_.end(),
                             PeakIntLessThan())).intensity;
    }
    return basePeakIntensityRaw_;
}

double Spectrum::getBasePeakIntensityProcessed() const
{
    if( basePeakIntensityProcessed_ < 0 ){
        return (*max_element(processedPeaks_.begin(), 
                             processedPeaks_.end(),
                             PeakIntLessThan())).intensity;
    }
    return basePeakIntensityProcessed_;
}

double Spectrum::getBasePeakMzRaw() const
{
    return (*max_element(rawPeaks_.begin(), rawPeaks_.end(),
                         PeakIntLessThan())).mz;
}

double Spectrum::getBasePeakMzProcessed() const
{
    return (*max_element(processedPeaks_.begin(), processedPeaks_.end(),
                         PeakIntLessThan())).mz;
}

const vector<int>& Spectrum::getPossibleCharges() const
{
    return (const vector<int>&)possibleCharges_;
}

const vector<PEAK_T>& Spectrum::getRawPeaks() const
{
    return (const vector<PEAK_T>&)rawPeaks_;
}

const vector<PEAK_T>& Spectrum::getProcessedPeaks() const
{
    return (const vector<PEAK_T>&)processedPeaks_;
}

double Spectrum::getSignalToNoise() {
	size_t size = rawPeaks_.size();

	sort(rawPeaks_.begin(), rawPeaks_.end(), compPeakInt());

	double signal = 0.0;
	int signalPeaks = 0;
	for (size_t i = 1; i != size && i < 6; ++i) {
		signal += rawPeaks_[i].intensity;
		++signalPeaks;
	}
	signal /= signalPeaks;

	double noise = (size % 2 == 0) ? (rawPeaks_[size / 2 - 1].intensity + rawPeaks_[size / 2].intensity) / 2 : rawPeaks_[size/2].intensity;

	return signal / noise;
}

// deletes existing peaks
void Spectrum::setRawPeaks(const vector<PEAK_T>& newpeaks) {
    rawPeaks_.assign(newpeaks.begin(), newpeaks.end()); 
}

void Spectrum::setProcessedPeaks(const vector<PEAK_T>& newpeaks) {
    processedPeaks_.assign(newpeaks.begin(), newpeaks.end()); 
}

void Spectrum::setTotalIonCurrentRaw(double tic){
    totalIonCurrentRaw_ = tic;
}

void Spectrum::setTotalIonCurrentProcessed(double tic){
    totalIonCurrentProcessed_ = tic;
}

int Spectrum::mysize()
{
    return sizeof(*this);
}

void Spectrum::setScanNumber(int newNum) {
    scanNumber_ = newNum;
}

void Spectrum::setMz(double newmz){
    mz_ = newmz;
}

void Spectrum::setIonMobility(double im, IONMOBILITY_TYPE type) {
    ionMobility_ = im;
    ionMobilityType_ = type;
}

void Spectrum::setIonMobilityHighEnergyOffset(double offset) { // In Waters Mse IMS, ions are given extra kinetic energy post-drift to fragment them, and so product ions reach the detector a bit sooner
    ionMobilityHighEnergyOffset_ = offset;
}

void Spectrum::setCollisionalCrossSection(double ccs) {
    collisionalCrossSection_ = ccs;
}

void Spectrum::setRetentionTime(double rt){
    retentionTime_ = rt;
}

void Spectrum::setStartTime(double rt){
    startTime_ = rt;
}

void Spectrum::setEndTime(double rt){
    endTime_ = rt;
}

/*
 * Adds an additional possible charge state to the spectrum.
 */
void Spectrum::addCharge(int newz) {
    possibleCharges_.push_back(newz);
}

float addIntensityToF(float n, PEAK_T p)
{ return n + p.intensity; }

} // namespace

 /*
  * Local Variables:
  * mode: c
  * c-basic-offset: 4
  * End:
 */
