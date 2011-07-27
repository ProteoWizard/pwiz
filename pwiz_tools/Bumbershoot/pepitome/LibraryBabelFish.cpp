//
// $Id$
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
// The Original Code is the Pepitome search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#include "LibraryBabelFish.h"

namespace freicore
{
namespace pepitome
{

void LibraryBabelFish::initializeDatabase()
{
    // optimized for bulk insertion
    libraryIndex.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA automatic_indexing=OFF;"
                      "PRAGMA default_cache_size=500000;"
                      "PRAGMA temp_store=MEMORY"
                     );
    // Create tables
    sqlite::transaction transaction(libraryIndex);
    libraryIndex.execute("CREATE TABLE LibMetaData ( Id TEXT PRIMARY KEY, Peptide TEXT, LibraryMass REAL, MonoMass REAL, AvgMass REAL, Charge INTEGER);"
                         "CREATE TABLE LibSpectrumData (Id TEXT PRIMARY KEY, NumPeaks INTEGER, SpectrumData BLOB, FOREIGN KEY (Id) REFERENCES LibMetaData(Id));"
                        );
    transaction.commit();
}

void LibraryBabelFish::indexLibrary()
{
    // Get insertion commands
    sqlite::command insertMetaData(libraryIndex, "INSERT INTO LibMetaData ( Id, Peptide, LibraryMass, MonoMass, AvgMass, Charge) VALUES (?,?,?,?,?,?)");
    sqlite::command insertSpectrumData(libraryIndex, "INSERT INTO LibSpectrumData ( Id, NumPeaks, SpectrumData) VALUES (?,?,?)");
    sqlite::transaction transaction(libraryIndex);

    library.loadLibrary(libraryName);
    size_t totalSpectra = library.size();
    cout << "Indexing  " << totalSpectra << " found in the library." << endl;
    for(sqlite3_int64 index = 0; index < totalSpectra; ++index)
    {
        if(!(index % 1000)) 
            cout << totalSpectra << ": " << index << '\r' << flush;
        // Read the library spectrum metadata and insert it into the DB
        library[index]->readSpectrumForIndexing();
        insertMetaData.binder() << ("scan="+boost::lexical_cast<string>(index)) 
                                << library[index]->matchedPeptide->sequence()
                                << library[index]->libraryMass
                                << library[index]->monoisotopicMass
                                << library[index]->averageMass
                                << library[index]->id.charge;
        insertMetaData.execute();
        insertMetaData.reset();

        // Archive the peptide, peaks, and proteins data.
        stringstream packStream(stringstream::binary|stringstream::in|stringstream::out);
        // This library offer binary portability. boost::binary_oarchive is not protable
        eos::portable_oarchive packArchive( packStream );
        packArchive  & *library[index];
        sqlite3_int64 numPeaks = library[index]->peakPreData.size();
        // Insert the spectrum data into library
        string tmpStr = packStream.str();
        insertSpectrumData.bind(1, ("scan="+boost::lexical_cast<string>(index)) );
        insertSpectrumData.bind(2, numPeaks);
        insertSpectrumData.bind(3, static_cast<const void*>(tmpStr.data()), tmpStr.length());
        insertSpectrumData.execute();
        insertSpectrumData.reset();
        /*if(index == 3)
        {
            ofstream tmp("tmp.bin.in");
            cout << "\n" << packStream.str().length() << endl;
            tmp.write(packStream.str().data(), packStream.str().length());
            tmp.close();
            //transaction.commit(); 
            //exit(1);
        }*/
        library[index]->clearSpectrum();
    }
    transaction.commit();
    libraryIndex.execute("VACUUM");   
}
}
}

int main( int argc, char* argv[] )
{
    // Get the command line arguments and process them
    vector< string > args;
    for( int i=0; i < argc; ++i )
        args.push_back( argv[i] );
    if(args.size() < 2)
    {
        cout << "LibraryBabelFish <library file path>" << endl;
        exit(1);
    }

    freicore::pepitome::LibraryBabelFish converter(args[1]);
    converter.initializeDatabase();
    converter.indexLibrary();
}

