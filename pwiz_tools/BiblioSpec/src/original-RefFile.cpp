/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
//class def for RefFile class--parses whatever will be the intermediate file type for
//associateing sequence, mods, and charge with an ms2spec.  
//file will be tab-delimited columns of file name, scanNum, charge, seq, and mods

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
