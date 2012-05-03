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
        LibraryBabelFish::LibraryBabelFish(const string& name)
            : libraryName (name), 
              libraryIndexName(name + ".index"),
              libraryIndex(libraryIndexName),
              error(false)
        {}

        LibraryBabelFish::~LibraryBabelFish()
        {
            libraryIndex.disconnect();
            if (error)
                bfs::remove(libraryIndexName);
        }

        void LibraryBabelFish::initializeDatabase()
        {
            try
            {
                // optimized for bulk insertion
                libraryIndex.execute("PRAGMA journal_mode=OFF;"
                    "PRAGMA synchronous=OFF;"
                    "PRAGMA automatic_indexing=OFF;"
                    "PRAGMA page_size=32768;"
                    "PRAGMA cache_size=5000;"
                    );
                // Create tables
                sqlite::transaction transaction(libraryIndex);
                libraryIndex.execute("CREATE TABLE LibMetaData (Id INTEGER PRIMARY KEY, LibraryId TEXT, Peptide TEXT, LibraryMass REAL, MonoMass REAL, AvgMass REAL, Charge INTEGER);"
                                     "CREATE TABLE LibSpectrumData (Id INTEGER PRIMARY KEY, NumPeaks INTEGER, SpectrumData BLOB);"
                                    );
                transaction.commit();
            }
            catch(...)
            {
                error = true;
                throw;
            }
        }

        inline bool checkForHomology(const string& firstPeptide, const string& secondPeptide, double threshold)
        {
            string::size_type m = firstPeptide.length();
            string::size_type n = secondPeptide.length();

            vector<string> aa1(m + 1);
            vector<string> aa2(n + 1);

            string::size_type shorter = m;
            if (m > n) shorter = n;

            // if the shorter sequence is a perfect sub-sequence of the longer one, its score is (shorter * 2 - 1).
            // so we are calculating our threshold identity score by multiplying the specified threshold by this "perfect" score
            double minIdentity = threshold * (shorter * 2 - 1);

            int** alignedLength = new int*[m + 1];
            int** identical = new int*[m + 1];

            for (size_t i = 0; i <= m; ++i) 
            {
                alignedLength[i] = new int[n + 1];
                identical[i] = new int[n + 1];

                alignedLength[i][0] = 0;
                if (i > 0)
                    aa1[i] = firstPeptide[i - 1];

                for (size_t j = 0; j <= n; ++j) 
                    identical[i][j] = 0;
            }  

            for (size_t j = 0; j <= n; ++j) 
            {
                alignedLength[0][j] = 0;
                if (j > 0) 
                    aa2[j] = firstPeptide[j - 1];
            }

            for (size_t i = 1; i <= m; ++i) 
            {
                for (size_t j = 1; j <= n; ++j) 
                {
                    if (aa1[i] == aa2[j] ||
                        (aa1[i] == "L" && aa2[j] == "I") || // we consider I and L the same, K and Q the same
                        (aa1[i] == "I" && aa2[j] == "L") ||
                        (aa1[i] == "K" && aa2[j] == "Q") ||
                        (aa1[i] == "Q" && aa2[j] == "K")) 
                    {
                        alignedLength[i][j] = alignedLength[i - 1][j - 1] + 1 + identical[i - 1][j - 1];
                        identical[i][j] = 1;
                    } 
                    else 
                    {
                        if (alignedLength[i - 1][j] > alignedLength[i][j - 1]) 
                            alignedLength[i][j] = alignedLength[i - 1][j];
                        else 
                            alignedLength[i][j] = alignedLength[i][j - 1];
                    }
                }
            }

            //identity = alignedLength[m][n];
            bool result = ((double)(alignedLength[m][n]) >= minIdentity);

            for (unsigned int r = 0; r <= m; r++) 
            {
                delete[] alignedLength[r];
                delete[] identical[r];
            }
            delete[] alignedLength;
            delete[] identical;

            return (result);
        }

        inline string shufflePeptideSequence(const DigestedPeptide& peptide)
        {
            // Amino acids containing modifications and also proline are not movable.
            int peptideLength = (int) peptide.sequence().length();
            ModificationMap& mods = const_cast<ModificationMap&>(peptide.modifications());
            int firstMovableAAIndex = 200;
            int lastMovableAAIndex = -1;
            vector<bool> immutable(peptideLength,false);
            vector<char> movableAAs;
            for(int position = 0; position < peptideLength -1; ++position)
            {
                if(peptide.sequence()[position] == 'P' || mods.find(position) != mods.end())
                    immutable[position] = true;
                else
                {
                    movableAAs.push_back(peptide.sequence()[position]);
                    firstMovableAAIndex = firstMovableAAIndex > position ? position : firstMovableAAIndex;
                    lastMovableAAIndex = lastMovableAAIndex < position ? position : lastMovableAAIndex;
                }
            }

            size_t numAttempts = 0;
            bool   foundDecoy = false;
            string shuffledSequence;
            while(numAttempts < 10 && !foundDecoy)
            {
                stringstream shuffledPeptide;    
                std::random_shuffle(movableAAs.begin(),movableAAs.end());
                size_t randomAAIndex = 0;
                for(int position = 0; position < peptideLength-1; ++position)
                {
                    if(!immutable[position])
                    {
                        shuffledPeptide << movableAAs[randomAAIndex];
                        ++randomAAIndex;
                    } else
                        shuffledPeptide << peptide.sequence()[position];
                }
                bool firstAAIsSame = shuffledPeptide.str()[firstMovableAAIndex] == peptide.sequence()[firstMovableAAIndex] ? true : false;
                bool lastAAIsSame = shuffledPeptide.str()[lastMovableAAIndex] == peptide.sequence()[lastMovableAAIndex] ? true : false;
                bool isHomolog = checkForHomology(peptide.sequence(), shuffledPeptide.str(), 0.6);
                foundDecoy = (!firstAAIsSame && !lastAAIsSame && !isHomolog);
                if(foundDecoy)
                    shuffledSequence = shuffledPeptide.str();
                ++numAttempts;
            }
            if(shuffledSequence.length() > 0)
                return shuffledSequence;
            return string();
        }

        void LibraryBabelFish::decoyLibrary()
        {
            library.loadLibrary(libraryName);
            size_t totalSpectra = library.size();
            cout << "Generating decoys for " << totalSpectra << " found in the library." << endl;
            set<string> decoys;
            for(size_t index = 0; index < totalSpectra; ++ index)
            {
                if(!(index % 1000)) 
                    cout << totalSpectra << ": " << index << '\r' << flush;
                library[index]->readSpectrumForIndexing();
                string decoy = shufflePeptideSequence(*library[index]->matchedPeptide);
                if(decoys.find(decoy) != decoys.end())
                {
                    DigestedPeptide decoyedPeptide(decoy.begin(),decoy.end(), 0, 
                                                    library[index]->matchedPeptide->missedCleavages() , 
                                                    library[index]->matchedPeptide->NTerminusIsSpecific(), 
                                                    library[index]->matchedPeptide->CTerminusIsSpecific(), 
                                                    library[index]->matchedPeptide->NTerminusPrefix(), 
                                                    library[index]->matchedPeptide->CTerminusSuffix());
                }

                library[index]->clearSpectrum();
            }
        }

        void LibraryBabelFish::indexLibrary()
        {
            try
            {
                // Get insertion commands
                sqlite::command insertMetaData(libraryIndex, "INSERT INTO LibMetaData (Id, LibraryId, Peptide, LibraryMass, MonoMass, AvgMass, Charge) VALUES (?,?,?,?,?,?,?)");
                sqlite::command insertSpectrumData(libraryIndex, "INSERT INTO LibSpectrumData (Id, NumPeaks, SpectrumData) VALUES (?,?,?)");
                shared_ptr<sqlite::transaction> transactionPtr;
                transactionPtr.reset(new sqlite::transaction(libraryIndex));

                library.loadLibrary(libraryName);
                NativeFileReader nativeReader(libraryName);

                size_t totalSpectra = library.size();
                cout << "Indexing " << totalSpectra << " spectra found in the library." << endl;
                for(sqlite3_int64 index = 0; index < totalSpectra; ++index)
                {
                    if(!(index % 1000)) 
                        cout << totalSpectra << ": " << index << '\r' << flush;
                    // Read the library spectrum metadata and insert it into the DB
                    library[index]->readHeader(nativeReader);
                    library[index]->averageMass = library[index]->matchedPeptide->molecularWeight();
                    library[index]->monoisotopicMass = library[index]->matchedPeptide->monoisotopicMass();
                    library[index]->readPeaks(nativeReader);

                    insertMetaData.binder() << (index+1)
                                            << ("scan="+boost::lexical_cast<string>(index))
                                            << library[index]->matchedPeptide->sequence()
                                            << library[index]->libraryMass
                                            << library[index]->monoisotopicMass
                                            << library[index]->averageMass
                                            << library[index]->id.charge;
                    //cout << index << "," << library[index]->matchedPeptide->sequence() << "," << library[index]->libraryMass
                    //     << "," << library[index]->monoisotopicMass << "," << library[index]->averageMass << "," << library[index]->id.charge << endl;
                    insertMetaData.execute();
                    insertMetaData.reset();
                    // Archive the peptide, peaks, and proteins data.
                    stringstream packStream(ios::binary|ios::out);
                    // This library offer binary portability. boost::binary_oarchive is not protable
                    eos::portable_oarchive packArchive( packStream );
                    packArchive & *library[index];
                    sqlite3_int64 numPeaks = library[index]->peakPreData.size();
                    // Insert the spectrum data into library
                    string tmpStr = packStream.str();
                    insertSpectrumData.bind(1, index+1);
                    insertSpectrumData.bind(2, numPeaks);
                    insertSpectrumData.bind(3, static_cast<const void*>(tmpStr.data()), tmpStr.length());
                    insertSpectrumData.execute();
                    insertSpectrumData.reset();
                    library[index]->clearSpectrum();
                    // Commit the transcation and create a fresh one.
                    if(!(index % 10000)) 
                    {
                        transactionPtr->commit();
                        transactionPtr.reset(new sqlite::transaction(libraryIndex));
                    }
                }
                transactionPtr->commit();

                libraryIndex.execute("CREATE INDEX LibMetaData_LibraryMass ON LibMetaData (LibraryMass);"
                                     "VACUUM");
            }
            catch(...)
            {
                error = true;
                throw;
            }
        }
    }
}

#ifdef LIBRARYBABELFISH_EXE
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

    return 0;
}
#endif
