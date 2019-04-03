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

#include "pwiz/utility/misc/Std.hpp"
#include <cstdlib>
#include <ctime>
#include "original-Spectrum.h"


spectrum::spectrum() {
    scanNumber = 0;
    type = SPEC_UNDEF;
    charge = 0;
    numPeaks = 0;
    peaks = NULL;
}

//Constructor
Spectrum::Spectrum()
{
    /*
      data.scanNumber = 0;
      data.mz = 0;
      data.type = SPEC_UNDEF;
      data.charge = 0;
      data.numPeaks = 0;
      data.peaks = NULL;  
    */
    //srand(static_cast<unsigned>(time(0)));
}

//Constructor that sets the mz value

Spectrum::Spectrum(float mz)
{
    //Spectrum();  apparently, does not act on this, creates and destroys a spectrum
    data.mz = mz;
    //  data.numPeaks = 0;
    //data.peaks = NULL;
    srand(static_cast<unsigned>(time(0)));
}

Spectrum::Spectrum(const Spectrum& s)
{
    data = s.data;
    data.peaks = new PEAK_T[data.numPeaks];
    //memcopy?
    for(int i=0; i<data.numPeaks; i++)
        data.peaks[i] = s.data.peaks[i];
    srand(static_cast<unsigned>(time(0)));
}

//Destructor
Spectrum::~Spectrum()
{
    //cout<<"this is Spectrum destructor!"<<endl;
    if( data.peaks ) { 
        //cout<<"deleting peaks in base class"<<endl;
        delete data.peaks; 
        data.peaks = NULL;
    }
}

void Spectrum::clear() {
    data.scanNumber = 0;
    data.mz = 0;
    data.type = SPEC_UNDEF;
    data.charge = 0;
    data.numPeaks = 0;
    data.peaks = NULL;  
    if( data.peaks ) { delete[] data.peaks; }   
}

//Assignment operator
Spectrum& Spectrum::operator= (const Spectrum& right) 
{
    if(this == &right) return *this;  //check for self assignment

    delete[] data.peaks;        
    data = right.data;
    data.peaks = new PEAK_T[data.numPeaks];  
    for(int i=0; i<data.numPeaks; i++)
        data.peaks[i] = right.data.peaks[i];
   
    return *this;
}

    bool Spectrum::operator< (Spectrum otherSpec) {
        return data.mz < otherSpec.getMz();
    }

//getters 
int Spectrum::getScanNum() const
{
    return data.scanNumber;
}
  
float Spectrum::getMz() const
{
    return data.mz;
}

int Spectrum::getNumPeaks() const
{ 
    return data.numPeaks;
}

int Spectrum::getCharge() const
{
    return data.charge;
}

//copies the peaks in this spec into the passed array which had better be of maxNum size
int Spectrum::getPeaks(PEAK_T* parray, int maxNumPeaks) const
{
    if(data.numPeaks < maxNumPeaks)
        return 0;

    for(int i=0; i<data.numPeaks; i++)
        parray[i] = data.peaks[i];

    return 1;
}

/*
  PEAK_T* Spectrum::getPeaks()
  {
  return data.peaks;
  }
*/

vector<PEAK_T> Spectrum::getPeaks()
{
    vector<PEAK_T> returnMe;
    for(int i=0; i<data.numPeaks; i++)
        returnMe.push_back(data.peaks[i]);

    return returnMe;
}

void Spectrum::putPeaksHere( vector<PEAK_T>* peakVectorp ) {
    //if the pointer is null, throw exception
    peakVectorp->clear();
    for(int i=0; i<data.numPeaks; i++)
        peakVectorp->push_back(data.peaks[i]);
}

SPECTRUM_T Spectrum::getSpecData() {
    return data;
}

//written for WritePpMs2 so that pre-processed peaks can be associated with
//a spectrum
void Spectrum::setPeaks(vector<PEAK_T>* peakv) {
    if( data.peaks )
        delete[] data.peaks;

    data.numPeaks = peakv->size();
    data.peaks = new PEAK_T[data.numPeaks];
    for(int i=0; i < data.numPeaks; i++)
        data.peaks[i] = peakv->at(i);
}

int Spectrum::mysize()
{
    return sizeof(*this);
}

void Spectrum::setScanNum(int newNum) {
    data.scanNumber = newNum;
}

void Spectrum::setCharge(int newz) {
    data.charge = newz;
}

void Spectrum::writeToFile(ofstream& file) 
{
    //file.binary(); ??  //check that it is binary
    file.write( (char*)this, sizeof(*this) );  //write self to stream
    file.write( (char*)data.peaks, data.numPeaks*sizeof(PEAK_T) );//write array to stream
}

void Spectrum::readFromFile(ifstream &file)
{
    //check that it is binary
    delete []data.peaks;
    file.read( (char*)this, sizeof(*this) );  //read into self
    data.peaks = new  PEAK_T[data.numPeaks]; //allocate memory for array
    file.read( (char*)data.peaks, data.numPeaks*sizeof(PEAK_T) );  //read in array
}


float addIntensityToF(float n, PEAK_T p)
{ return n + p.intensity; }


//FOR DEBUGGING
//takes in a spectrum struct and copies its data to itself
void Spectrum::updateData(SPECTRUM_T specdat)
{
    data = specdat; 
}

//print peaks also
void Spectrum::printMe()
{
    cerr << "Scan Number: " << data.scanNumber << endl
         << "Mass to charge: " << data.mz << endl
         << "Number of Peaks: " << data.numPeaks << endl
         << "S Charge: " << data.charge << endl;

    for(int i=0; i<data.numPeaks; i++) {
        cerr << (data.peaks[i]).mass<< "  "<<(data.peaks[i]).intensity <<endl;
    }
}

/*
//re-assigns intensities to each mz
void Spectrum::shuffle()
{
cout << "shuffling peaks for " << data.scanNumber << endl;
for(int i=0; i<data.numPeaks - 1; i++) {
//int j = i + rand() / (RAND_MAX / (data.numPeaks-1) +1 );
int j =  rand() / (RAND_MAX / (data.numPeaks-1) +1 );
float tempInt = data.peaks[j].intensity;
data.peaks[j].intensity = data.peaks[i].intensity;
data.peaks[i].intensity = tempInt;
//cout << data.peaks[i].mass << "\t" << data.peaks[i].intensity << endl;
}

}
*/ 

 /* .
 //chooses random new mzs for each intensity
 void Spectrum::shuffle()
 {
 srand(static_cast<unsigned>(time(0)));
 //get min and max m/z
 float minmz = data.peaks[0].mass;
 float maxmz = data.peaks[data.numPeaks-1].mass;
 float span = maxmz - minmz;
 //  cout << "shuffling peaks for " << data.scanNumber << endl;
 for(int i=0; i<data.numPeaks - 1; i++) {
 float newmz = minmz + (span * rand()/(RAND_MAX+1.0));
 data.peaks[i].mass = newmz;
 }

 sort(data.peaks, data.peaks+data.numPeaks-1, compPeakMz());
 }

 */

 /*
 //chooses random new mzs, sorts them, and assigns them to the intensities in order
 void Spectrum::shuffle()
 {
 srand(static_cast<unsigned>(time(0)));
 //get min and max m/z
 float minmz = data.peaks[0].mass;
 float maxmz = data.peaks[data.numPeaks-1].mass;
 float span = maxmz - minmz;

 //create a temp array for the new mzs
 float* newmz = new float[data.numPeaks];
 //  cout << "shuffling peaks for " << data.scanNumber << endl;
 for(int i=0; i<data.numPeaks - 1; i++)
 newmz[i] = minmz + (span * rand()/(RAND_MAX+1.0));

 //sort the new mz values
 sort(newmz, newmz+data.numPeaks-1);

 //put back in the spec, in order
 for(int i=0; i<data.numPeaks - 1; i++)
 data.peaks[i].mass = newmz[i];
 }
 */


 //shifts mz by a fixed amount
void Spectrum::shift(int howmuch)
{
    cout << "shifting ";
    //after shift, the rightmost peak becomes the leftmost peak
    float smallMass=data.peaks[0].mass;
  
    for(int i=0; i<data.numPeaks - 1; i++) {
        data.peaks[i].mass += howmuch;
    }
  
    data.peaks[data.numPeaks-1].mass = smallMass;

    //sort the peaks
    sort(data.peaks, data.peaks+data.numPeaks, compPeakMz());
}


/*
//shifts mz by a fixed amount
void Spectrum::shift(int howmuch)
{
//cout << "shifting\t";  
for(int i=0; i<data.numPeaks - 1; i++) {
if(rand() < RAND_MAX/2)
data.peaks[i].mass -= howmuch;
else
data.peaks[i].mass += howmuch;
}
}
*/

 /*
   void Spectrum::fromMerge(vector<PEAK_T> newPeaks) {
   data.numPeaks = newPeaks.size();
   delete[] data.peaks; 
   data.peaks = new PEAK_T[data.numPeaks];

   for(int i=0; i<data.numPeaks; i++)
   data.peaks[i] = newPeaks.at(i);

   }
 */

 /*
  * Local Variables:
  * mode: c
  * c-basic-offset: 4
  * End:
 */
