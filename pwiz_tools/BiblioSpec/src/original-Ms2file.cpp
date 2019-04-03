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

//definition of Ms2file class

#include "pwiz/utility/misc/Std.hpp"
#include "original-Ms2file.h"

//Consructor
Ms2file::Ms2file() {
    init(DEFAULT_VERBOSITY);
}


//Consructor
Ms2file::Ms2file(int v) {
    init(v);
}

//Constructor that opens a file for either reading or writing
Ms2file::Ms2file(const char* openfilename, FILE_TYPE opentype, int v) {
    //could just call open
    init(v);
    verbosity = v;
    switch (opentype) {
    case READ:
        openRead(openfilename);
        break;
    case WRITE:
        openWrite(openfilename);
        break;
    }
}

//Destructor
Ms2file::~Ms2file() {
    if( file.is_open() )
        file.close();
}

//initializes objects, called by all constructors
void Ms2file::init(int v) {
    filename = NULL; 
    isOpen = false;
    type = READ;
    at_eof = false;
    noSpecLeft = false;
    verbosity = v;
    readHere = 0; 
    fileStart = 0;
    peaks.reserve(1000);
}

void Ms2file::open(const char* filename, FILE_TYPE type, int v) {
    init(v);
    verbosity = v;
    switch (type) {
    case READ:
        openRead(filename);
        break;
    case WRITE:
        openWrite(filename);
        break;
    }
}

/*opens a file to read
  if successful, sets isOpen to true, initializes by reading in header info, 
  reads first spectrum and stores it in currentSpec
*/
void  Ms2file::openRead(const char* openfilename) {
    //check that there is not already a file open or that this object is set to write
    //this might be redundant, isOpen might be a sufficient check
    //  if( type == WRITE || isOpen ) { return; }  //and complain

    file.close(); 
    file.clear();
    at_eof = false;
    noSpecLeft = false;
  
    type = READ;          
    filename = openfilename;    

    file.open(filename, ios::in );
    if( file.is_open() ) { 
        isOpen = true;
    } else {
        cout << "Could not open " << filename << endl;      
        throw exception();
    } 
    fileStart = file.tellg();       

    readHeader();
    //which sets the read pointer

}

/*opens a file for writing to
  won't overwrite an existing file
  if successful, sets isOpen to true, 
*/
void  Ms2file::openWrite(const char* openfilename)
{
    //check that there is not already a file open or that this object is set to write
    //this might be redundant, isOpen might be a sufficient check
    //  if( type == WRITE || isOpen ) { return; }  //and complain

    if( isOpen )
        file.close();

    type = WRITE;         //or maybe move this until after the open is successful
    filename = openfilename;           

    file.open(filename, ios::out );
    if( file.is_open() )
        { 
            isOpen = true;
        } else {
        cout << "Could not open " << filename << endl;      
        //return; 
        //exit(1);  
        throw exception();
    } 
}

void Ms2file::close() {
    if( file.is_open()) {
        file.close();
        isOpen = false;
    }
}

string Ms2file::readHeader() {
    file.seekg( fileStart );  //move back to the beginning
    string header;
    while( file.peek() != 'S' ) {
        string buffer;
        getline(file, buffer);
        if( buffer[0] == 'H' ) {
            parseHeader(buffer);  //store header info
            header += buffer;
            header += "\n";
        } else {
            //now at the first scan number, stop here
            break;
        }
    }
    readHere = file.tellg();
    return header;
}

//Copies the header from copyfile and writes it to this
void Ms2file::copyHeader(const char* copyfilename) {
    Ms2file infile(copyfilename, READ, verbosity);
    string header = infile.readHeader();
    file << header;
    //infile.close();
}


//eventually, this will store any important header info
void Ms2file::parseHeader(string headerline) {}


/*  The Ms2file will read in a spectrum BEFORE a request is made, so a request for
    specSize will return the size of the next spec in the file
*/
/*
  read in scan number and mz into currentSpec
  read peaks into a vector
  create array of peaks for currentSpec
  copy peaks from vector into array
  clear vector
*/
/*
  void Ms2file::readSpec()
  {
  file.seekg( readHere );   //if last operation was a read, won't move
                            //if last operation was nextScan, will jump back
                            string tmp;
                            currentSpec.data.charge = 0;  //this is dumb, there should probably be somethign more general to wipe clean currentSpec
                            file.get();   //get the 'S'
                            file >> currentSpec.data.scanNumber >> tmp >> currentSpec.data.mz;  
                            getline(file, tmp);   //remaining whitespace and eol so peek works

                            char nextChar = file.peek();      //what is the next character
                            //discard I, Z, and D lines
                            while( nextChar=='I' || nextChar=='Z' || nextChar=='D' )
                            {

                            //get charge state as either 1 or 2or3, 
                            //This way stores all multiple charges as 3, not ideal
                            if( nextChar=='Z' && currentSpec.data.charge==2 )
                            currentSpec.data.charge=23;
                            else if( nextChar=='Z' )
                            {
                            char c;
                            file >> c >> currentSpec.data.charge;
                            }
                            getline(file, tmp);
                            nextChar = file.peek();             //and check the next character
                            }//now the next line should be the start of the list of peaks

                            //shove all of the peak values into the vector
                            PEAK_T curPeak;
                            while( !file.eof() && file.peek() != 'S' )
                            {
                            file >> curPeak.mass;
                            file >> curPeak.intensity;
                            getline(file, tmp); //gets rest of line so peek works
                            peaks.push_back(curPeak);
                            }

                            //move peaks from vector into array in currentSpec
                            delete []currentSpec.data.peaks;                //delete what was there
                            currentSpec.data.numPeaks = peaks.size();       //update size of array
                            currentSpec.data.peaks = new PEAK_T [currentSpec.data.numPeaks];
                            for(int i=0; i<currentSpec.data.numPeaks; i++)
                            currentSpec.data.peaks[i] = peaks.at(i);


                            peaks.clear(); //empty vector to be ready for next

                            readHere = file.tellg();  //might not need this, but it can't hurt
                            if(file.eof())    //can read() be called again?
                            at_eof = true;

                            }
*/
void Ms2file::readSpec(Spectrum* spec) {
    if( at_eof ) {
        spec->clear();
        return;
    }
    file.seekg( readHere );
    string tmp;
    char c;
    file >> c >> spec->data.scanNumber >> tmp >> spec->data.mz;
    getline(file,tmp); //remaining whitespace and eol so peek works
    // OR file.ignore(1000, '\n');

    char nextChar = file.peek();
    //get charge, discard I and D lines
    while( nextChar=='I' || nextChar=='Z' || nextChar=='D' || nextChar=='Q') {
        if( nextChar=='Z' && spec->data.charge==2 )
            spec->data.charge = 23;
        else if( nextChar=='Z' )
            file >> c >> spec->data.charge;
    
        getline(file,tmp);
        //or? file.ignore(1000, '\n');
        nextChar = file.peek();
    }//should be at the start of peak list

    PEAK_T curPeak;
    //peaks.clear();
    while( !file.eof() && file.peek() != 'S' ) {
        file >> curPeak.mass >> curPeak.intensity;
        getline(file,tmp);
        peaks.push_back(curPeak);
    }

    //move peaks to the spec
    int peakCount = peaks.size();
    spec->data.numPeaks = peakCount;
    spec->data.peaks = new PEAK_T[peakCount];
    for(int i=0; i<peakCount; i++)
        spec->data.peaks[i] = peaks.at(i);

    peaks.clear();
    readHere = file.tellg();
    if( file.eof() ) {
        at_eof = true;
        noSpecLeft = true;
    }
}

//reads the next scan number in the file and returns it
//file pointer remains in the same place after calling
int Ms2file::nextScanNum() {
    //find the next scan line
    file.ignore(10000000, 'S');  //what would be a good number for this?
    if( file.eof() ) {
        at_eof = true;
        return -1;
    }

    file.putback('S');

    //mark where this spec starts
    readHere = file.tellg();
    //discard the S and read the scan number
    char s;
    int thisNum;
    file >> s >> thisNum;
    return thisNum; 
}


//returns the currentSpectrum and reads in the next from file
void Ms2file::nextSpec(Spectrum* spec)
{
    //Ms2Spectrum returnMe = currentSpec;

    if( at_eof )  //we just returned the last spec in the file, don't try to read
        noSpecLeft = true;
    else
        readSpec(spec);
}
 


void Ms2file::find(int scNum, Spectrum* spec) {
    int nextNum  = 0;
    while( (scNum > nextNum) && (nextNum >= 0 ) ) {
        nextNum = nextScanNum();
    } //loop
    readSpec(spec);
    if( spec->data.scanNumber != scNum) {
        spec->clear();
    }
}


bool Ms2file::hasSpec()
{
    return !noSpecLeft;
}

//take a filename and a vector of spectra to write to file
//where to get the header information?
//let's make it a Spectrum rather than Ms2 spectrum so it can take any kind
void Ms2file::write(const char* filename, vector<Spectrum> specs)
{
    //try to open file
    ofstream outfile(filename, ios::trunc );

    if( !outfile.is_open() )
        {
            cout << "Could not open " << filename << endl;
            throw exception();
        }
    //write header
    time_t t=time(NULL);
    char* date=ctime(&t);

    outfile << "H\tCreationDate\t" << date 
            << "H\tExtractor\tMs2file\n" 
            << "H\tExtractorVersion\t0.1\n" 
            << "H\tExtractorOptions\t\n" << endl;

    //sort spectra by scanNumber
    sort(specs.begin(), specs.end(), compSpecScanNum());

    //print each spec
    for(int i=0; i<(int)specs.size(); i++)
        {
            //print S line with scan number(twice) and precursor mass
            outfile << "S\t" << specs.at(i).getScanNum() 
                    << "\t"  << specs.at(i).getScanNum() 
                    << "\t"  << specs.at(i).getMz() << endl;

            //print a Z line and m/z times charge (mass)
            //charge is 2 or 3
            if(specs.at(i).getCharge() == 23) {
                outfile << "Z\t2" << "\t" << 2 * specs.at(i).getMz() << endl;
                outfile << "Z\t3" << "\t" << 3 * specs.at(i).getMz() << endl;
            }
            else
                outfile << "Z\t" << specs.at(i).getCharge()  
                        << "\t" << specs.at(i).getCharge() * specs.at(i).getMz() << endl;


     
            //for all peaks, print peaks
            vector<PEAK_T> peaks = specs.at(i).getPeaks();
            for(int j=0; j<(int)peaks.size(); j++)
                outfile << peaks.at(j).mass << "\t" << fixed << peaks.at(j).intensity << endl;

        }
    outfile.close();
}


void Ms2file::write(const char* filename, vector<Spectrum*> specs, bool sortSpec)
{
    //try to open file
    ofstream outfile(filename);

    if( !outfile.is_open() )
        {
            cout << "Could not open " << filename << endl;
            throw exception();
        }

  
    //write header
    time_t t=time(NULL);
    char* date=ctime(&t);

    outfile << "H\tCreationDate\t" << date 
            << "H\tExtractor\tMs2file\n" 
            << "H\tExtractorVersion\t0.1\n" 
            << "H\tExtractorOptions\t" << endl;

    //sort spectra by scanNumber
    if( sortSpec )
        sort(specs.begin(), specs.end(), compSpecPtrScanNum());

    //print each spec
    for(int i=0; i<(int)specs.size(); i++)
        {
            //print S line with scan number(twice) and precursor mass
            outfile << "S\t" << specs.at(i)->getScanNum() 
                    << "\t"  << specs.at(i)->getScanNum() 
                    << "\t"  << specs.at(i)->getMz() << endl;

            //print a Z line and m/z times charge (mass)
            if(specs.at(i)->getCharge() == 1)
                outfile << "Z\t" << specs.at(i)->getCharge() 
                        << "\t" << specs.at(i)->getMz() << endl;
            else {//2 or 3 charge, technically could be 0 or other
                outfile << "Z\t2" << "\t" << 2 * specs.at(i)->getMz() << endl;
                outfile << "Z\t3" << "\t" << 3 * specs.at(i)->getMz() << endl;
            }

            //for all peaks, print peaks
            vector<PEAK_T> peaks = specs.at(i)->getPeaks();
            for(int j=0; j<(int)peaks.size(); j++)
                outfile << peaks.at(j).mass << "\t" << fixed 
                        << peaks.at(j).intensity << endl;
        }
 
    outfile.close();
}

void Ms2file::write(string line) {
    if(isOpen && (type==WRITE)) {  //if not, throw exception
        file << line;
    }
}

void Ms2file::write(Spectrum* spec) {
    if(isOpen && (type==WRITE)) {  //if not, throw exception
        //print S line with scan number(twice) and precursor mass
        file << "S\t" << spec->getScanNum() 
             << "\t"  << spec->getScanNum() 
             << "\t"  << spec->getMz() << endl;

        //print a Z line and m/z times charge (mass)
        if(spec->getCharge() == 23) {  //ambiguous multiple charge
            file << "Z\t2" << "\t" <<  (2 * spec->getMz())-1 << endl;
            file << "Z\t3" << "\t" << (3 * spec->getMz())-2 << endl;
        } else {
            file << "Z\t" << spec->getCharge() 
                 << "\t" << spec->getCharge() * spec->getMz() << endl;
        }

        //for all peaks, print peaks
        vector<PEAK_T> peaks = spec->getPeaks();
        for(int j=0; j<(int)peaks.size(); j++)
            file << peaks.at(j).mass << "\t" //<< fixed 
                 << peaks.at(j).intensity << endl;
    } else {
        cerr << "MS2 file is not open for writing\n";
        throw exception();
    }
}

void Ms2file::writeHeader(const char* comment) {
    if(isOpen && (type==WRITE)) {  //if not, throw exception
        time_t t=time(NULL);
        char* date=ctime(&t);
    
        file << "H\tCreationDate\t" << date 
             << "H\tExtractor\tMs2file\n" 
             << "H\tExtractorVersion\t0.1\n" 
             << "H\tExtractorOptions\t\n" 
             << "H\tComment\t"<< comment << endl;
    }
}

/* write a single spectrum to an already open ms2
   add an I comment line and use the given scan number */
void Ms2file::write_I(Ms2Spectrum* spec, int scanNum, string comment) {
    if( isOpen ) {   //if not, throw exception
        //print S line
        file << "S\t" << scanNum 
             << "\t"  << scanNum
             << "\t"  << spec->getMz() << endl;

        //print I line
        file << "I\t" << comment << endl;

        //print one or two Z lines
        if(spec->getCharge() == 23) {
            file << "Z\t2" << "\t" << 2 * spec->getMz() << endl;
            file << "Z\t3" << "\t" << 3 * spec->getMz() << endl;
        } else {
            file << "Z\t" << spec->getCharge() << "\t" 
                 << spec->getCharge() * spec->getMz() << endl;
        }

        //print peaks
        vector<PEAK_T> peaks = spec->getPeaks();
        for(int i=0; i<(int)peaks.size(); i++) {
            file << peaks.at(i).mass << "\t" << peaks.at(i).intensity << endl;
        }

    }
}




//For LibToMs2

//take a filename and a vector of spectra to write to file
//where to get the header information?
//let's make it a Spectrum rather than Ms2 spectrum so it can take any kind
vector<SEQ_T> Ms2file::writeLib(const char* filename, 
                                vector<RefSpectrum*>& specs,
                                SEQ_TYPE type)
{

    //try to open file
    ofstream outfile(filename);


    if( !outfile.is_open() ) {
        cout << "Could not open " << filename << endl;
        throw exception();
    }

    //write header
    time_t t=time(NULL);
    char* date=ctime(&t);

    outfile << "H\tCreationDate\t" << date 
            << "H\tExtractor\tMs2file\n" 
            << "H\tExtractorVersion\t1.0\n" 
            << "H\tExtractorOptions\t\n" 
            << "H\tComment Libraryfile" << endl;

    //the vector of scan#, charge, seq to return
    vector<SEQ_T> seqs;

    //print each spec
    for(int i=0; i<(int)specs.size(); i++)
        {
            //print S line with scan number(twice) and precursor mass
            outfile << "S\t" << specs.at(i)->getID() 
                    << "\t"  << specs.at(i)->getScanNum() 
                    << "\t"  << specs.at(i)->getMz() << endl;


            //print a Z line and m/z times charge (mass)
            //charge is 2 or 3
            if(specs.at(i)->getCharge() == 23) {
                outfile << "Z\t2" << "\t" << 2 * specs.at(i)->getMz() << endl;
                outfile << "Z\t3" << "\t" << 3 * specs.at(i)->getMz() << endl;
            }
            else
                outfile << "Z\t" << specs.at(i)->getCharge()  
                        << "\t" << specs.at(i)->getCharge() * specs.at(i)->getMz() << endl;

            //print a seq line to be used for index
            if( type == SEQ )
                outfile << "D\tseq\t" << specs.at(i)->getSeq() << endl;
      
            //add the seq to the list
            SEQ_T s;
            s.id = specs.at(i)->getID() ;
            s.charge = specs.at(i)->getCharge();
            s.annot = specs.at(i)->getAnnot();
            s.seq = specs.at(i)->getSeq();
            s.mods = specs.at(i)->getMods();
            seqs.push_back(s);

            //for all peaks, print peaks
            vector<PEAK_T> peaks = specs.at(i)->getPeaks();
            for(int j=0; j<(int)peaks.size(); j++)
                outfile << peaks.at(j).mass << "\t" << peaks.at(j).intensity << endl;

        }
    outfile.close();

    return seqs;
}

/*
//returns the spectrum of the given scan number, or an empty spec with scanNum=0 if not found
//assumes sequential searching, maintains its current place in the file
Ms2Spectrum Ms2file::find(int scNum) {
//check to see if the current spec is the one we are looking for
if( currentSpec.data.scanNumber == scNum ) {
return currentSpec;
}
//else
//find the next spectrum
while( scNum > nextScanNum()) {}//loop
readSpec();
if( currentSpec.data.scanNumber == scNum) {
return currentSpec;
}
//else 
Ms2Spectrum emptySpec;
return emptySpec;
}
*/

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
