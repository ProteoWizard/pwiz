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

//class definition for RefSpectrum

#include "pwiz/utility/misc/Std.hpp"
#include "original-RefSpectrum.h"

//constructors
RefSpectrum::RefSpectrum()
{ 
    data.type = REFERENCE; 
    length = 0;
    annot = -1;
    copies = 1;
    id = -1;
}

RefSpectrum::RefSpectrum(const RefSpectrum& rs)
{
    length = rs.length;
    seq = rs.seq;
    mods = rs.mods;
    data = rs.data;
    data.peaks = new PEAK_T[data.numPeaks];
    rs.getPeaks(data.peaks, data.numPeaks);
    annot = rs.annot;
    copies = rs.copies;
    id = rs.id;
}

RefSpectrum::RefSpectrum(const Spectrum& s)
{
    length = 0;
    annot = -1;
    copies = 1;
    id = -1;
    //or could do data = s.data and then change type and peaks
    data.type = REFERENCE;
    data.scanNumber = s.getScanNum();
    data.mz = s.getMz();
    data.numPeaks = s.getNumPeaks();
    if(data.numPeaks)
        {
            data.peaks = new PEAK_T[data.numPeaks];  //add if(data.numPeaks?)
            s.getPeaks(data.peaks, data.numPeaks);
        }
}

RefSpectrum::RefSpectrum(refData rd) {
    data.charge = rd.charge;
    seq = rd.seq; 
    length = seq.length();
    mods = rd.mods;
    if( mods.length() == 1 )  //assumes "0" which means no mods
        mods.assign(length, '0');

    annot = rd.annot;
    copies = 1;
    id = -1;
}

RefSpectrum::~RefSpectrum()
{
    //cout<<"this is RefSpectrum destructor!"<<endl;
    if( data.peaks ) {
        //delete []data.peaks;
        //cout<<"deleteing peaks"<<endl;
        delete data.peaks;
        data.peaks = NULL;
    }
  
}

RefSpectrum& RefSpectrum::operator=(const Spectrum& s)
{
    seq.clear();
    mods.clear();
    delete []data.peaks;

    data.type = REFERENCE;
    length = 0;
    annot = -1;
    copies = 1;
    id = -1;
 
    data.scanNumber = s.getScanNum();
    data.mz = s.getMz();
    data.numPeaks = s.getNumPeaks();
    if(data.numPeaks)
        {
            data.peaks = new PEAK_T[data.numPeaks];
            s.getPeaks(data.peaks, data.numPeaks);
        }
    return *this;
}

    RefSpectrum& RefSpectrum::operator=(const RefSpectrum& s)
{
    //clear existing
    seq.clear();
    mods.clear();
    delete []data.peaks;

    //add new data
    seq = s.getSeq();
    mods = s.getMods();
    data.type = REFERENCE;
    length = seq.length();
    annot = s.getAnnot();
    copies = 1;
    id = s.getID();
 
    data.scanNumber = s.getScanNum();
    data.mz = s.getMz();
    data.numPeaks = s.getNumPeaks();
    if(data.numPeaks) {
        data.peaks = new PEAK_T[data.numPeaks];
        s.getPeaks(data.peaks, data.numPeaks);
    }
    return *this;
}


    //overridden from Spectrum
        void RefSpectrum::writeToFile(ofstream& file) 
{

    file.write( (char*)this, mysize()-(2*sizeof(string))  );
    file.write( seq.c_str(), length );
    file << "\n";  //this way can use getline( file, string) for input
    file.write( mods.c_str(), length );
    file << "\n";

    file.write( (char*)data.peaks, data.numPeaks*sizeof(PEAK_T) );
}

void RefSpectrum::readFromFile(ifstream &file)
{
    seq.clear();
    mods.clear();
    delete []data.peaks;

    file.read( (char*)this, mysize()-(2*sizeof(string)) );  
    //set type of spectrum
    getline( file, seq );
    getline( file, mods );
    data.peaks = new  PEAK_T[data.numPeaks]; 
    file.read( (char*)data.peaks, data.numPeaks*sizeof(PEAK_T) );  

}

//setters

void RefSpectrum::addRefData(refData rd) {
    data.charge = rd.charge;
    seq = rd.seq;
    length = seq.length();
    mods = rd.mods;
    if( mods.length() == 1 )   //assumes mods=="0" which means no mods
        mods.assign(length, '0');

    annot = rd.annot;
    copies = 1;
    id = -1;
}
 
//eventually add in processing mods
void RefSpectrum::addSeq(string newSeq)
{
    length = newSeq.length();
    seq = newSeq;
    mods.assign(length, '0');
}

//eventually add in processing mods
void RefSpectrum::addMods(string newMods)
{
    //leagal mods can be "0", what if newMods.length != seq.length?
    if( newMods.length() > 1 )
        mods = newMods;
}

void RefSpectrum::addCharge(int c)
{
    data.charge = c;
}

void RefSpectrum::setMz(float newmz) {
    data.mz = newmz;
}

void RefSpectrum::setID(int newid)
{
    id = newid;
}

void RefSpectrum::setAnnot(int a) {
    annot = a;
}

void RefSpectrum::setCopies(int duplicates) {
    copies = duplicates;
}

//getters
string RefSpectrum::getSeq() const
{
    return seq;
}

string RefSpectrum::getMods() const
{
    return mods;
}


int RefSpectrum::getCharge() const
{
    return data.charge;
}

int RefSpectrum::getAnnot() const
{
    return annot;
}

int RefSpectrum::getID() const
{
    return id;
}

int RefSpectrum::getCopies() const
{
    return copies;
}

int RefSpectrum::mysize()
{
    return sizeof(*this);
}


void RefSpectrum::printMe()
{
    cerr << "---------------------------------------" << endl;
    Spectrum::printMe();
    cerr << "Seq: " << seq << endl
         << "Mods: " << mods << endl
         << "Length " << length << " annot " << annot
         << " copies " << copies << " id " << id << endl;

}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
