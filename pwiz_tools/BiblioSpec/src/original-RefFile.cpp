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

//class def for RefFile class--parses whatever will be the intermediate file type for
//associateing sequence, mods, and charge with an ms2spec.  
//file will be tab-delimited columns of file name, scanNum, charge, seq, and mods

#include "pwiz/utility/misc/Std.hpp"
#include "original-RefFile.h"

refData::refData() {
    scanNum = 0;
    charge = 0;
    annot = -1;
}

//RefFile::RefFile() {}

RefFile::~RefFile() {} //close file if open

/*  opens an ssl 
    reads in every ref and puts in a vector
    sorts by ms2 file and by scan #
*/
//void RefFile::open(const char* name, bool printLog)
void RefFile::open(const char* name)
{
    input.open(name);
    if( !input.is_open() ) {
        cout << "Could not open " << name << endl;
        exit(1);
        //or throw exception
    }
    //save path name
    path = name;
    size_t lastSlash = path.find_last_of('/');

    if(lastSlash != string::npos) {  //found it, remove filename
        path.erase(lastSlash+1);      //keep the slash
    } else {                          //erase filename from path
        path.clear();
    }

    checkHeader();
    //read in whole file
    while( readNextRef() ) {
        refs.push_back(curRef);
    }
    input.close();

    if( refs.size() > 0) {
        moreRef = true;
    } else {
        moreRef = false;
    }

    //  ofstream logfile("log");
    //  for(int i=0; i<refs.size(); i++) {
    //    RefFile::printRefToFile(refs.at(i), logfile);
    //  }

    //  if( !sorted )
    sort(refs.begin(), refs.end(), compRefData());

    //  logfile << "SORTED" << endl;
    //  for(int i=0; i<refs.size(); i++) {
    //    RefFile::printRefToFile(refs.at(i), logfile);
    //  }
}

int RefFile::getNumRef() {
    return refs.size();
}

void RefFile::checkHeader() {
    string fileheader;
    getline(input, fileheader);
    if( fileheader != requiredheader ) {
        cout << "SSL file header is incorrect.  It should read '" << requiredheader << endl;
        throw exception();
    }

    //If only header line in file, not eof here, but peek returns -1
    if( input.peek() > 0 ) {
        moreRef = true;
    } else {
        moreRef = false;
    }
}

bool RefFile::hasRef()
{
    return moreRef;
}

/* used internally for reading in from file
 */
bool RefFile::readNextRef()
{
    bool readSuccessful = false;

    if( moreRef == false ) {
        return false;
    }

    if( input.good() && !input.eof()) {
        input >> curRef.file >> curRef.scanNum >> curRef.charge 
              >> curRef.seq >> curRef.mods >> curRef.annot;

        //if curRef.file starts with /, don't add path to it
        if( curRef.file.at(0) != '/' ) {
            curRef.file = path + curRef.file;
        }
        readSuccessful = true;

    }

    return readSuccessful;

}

/* public for returning from sorted vector
 */
refData RefFile::getNextRef()
{
    if( curRefIndex  == (int)refs.size() ) {
        moreRef = false;
        refData r;
        return r;
    }

    return refs.at(curRefIndex++);
}


void RefFile::printRef(refData ref) {
    cout << "file: " << ref.file << endl
         << "scanNum: " << ref.scanNum << endl
         << "charge: " << ref.charge << endl
         << "seq: " << ref.seq << endl
         << "mods: " << ref.mods << endl;

}

void RefFile::printRefToFile(refData& ref, ofstream& file) {
    file << ref.file << "\t"
         << ref.scanNum << "\t"
         << ref.charge << "\t"
         << ref.seq << "\t"
         << ref.mods << "\t"
         << ref.annot << endl;

}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
