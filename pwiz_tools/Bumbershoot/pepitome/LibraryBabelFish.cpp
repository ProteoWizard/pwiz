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
#include "../freicore/AhoCorasickTrie.hpp"
using freicore::AhoCorasickTrie;
typedef boost::shared_ptr<std::string> shared_string;

namespace freicore
{
	struct AminoAcidTranslator
	{
		static int size() {return 26;}
		static int translate(char aa) {return aa - 'A';};
		static char translate(int index) {return static_cast<char>(index) + 'A';}
	};

	typedef AhoCorasickTrie<AminoAcidTranslator> PeptideTrie;

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
			double temp = (double)(alignedLength[m][n]);
            bool result = (temp >= minIdentity);

            for (unsigned int r = 0; r <= m; r++) 
            {
                delete[] alignedLength[r];
                delete[] identical[r];
            }
            delete[] alignedLength;
            delete[] identical;

            return (result);
        }

        inline string shufflePeptideSequence(const vector<string>& peptide)
        {
            // Amino acids containing modifications and also proline are not movable.
			stringstream peptideSequence; 
            int peptideLength = (int) peptide.size();
            int firstMovableAAIndex = 200;
            int lastMovableAAIndex = -1;
            vector<bool> immutable(peptideLength,false);
            vector<string> movableAAs;
            for(int position = 0; position < peptideLength; ++position)
            {
                if(position == peptideLength-1 || peptide[position].find("[") != string::npos)
					immutable[position] = true;
                else
                {
                    movableAAs.push_back(peptide[position]);
                    firstMovableAAIndex = firstMovableAAIndex > position ? position : firstMovableAAIndex;
                    lastMovableAAIndex = lastMovableAAIndex < position ? position : lastMovableAAIndex;
                }
				peptideSequence << peptide[position][0];
            }

            size_t numAttempts = 0;
            bool   foundDecoy = false;
            string shuffledSequence;
			string bestTry;
			size_t bestTryNumber = 0;
            while(numAttempts < 100 && !foundDecoy)
            {
                stringstream shuffledPeptide;    
				stringstream shuffledPeptideNoMods;
                std::random_shuffle(movableAAs.begin(),movableAAs.end());
                size_t randomAAIndex = 0;
				size_t repeat = 0;
				size_t nonRepeat = 0;
                for(int position = 0; position < peptideLength; ++position)
                {
                    if(!immutable[position])
                    {
                        shuffledPeptide << movableAAs[randomAAIndex];
						shuffledPeptideNoMods << movableAAs[randomAAIndex][0];
						if (peptide[position] == movableAAs[randomAAIndex] ||
							((peptide[position] == "L" || peptide[position] == "I") &&
							(movableAAs[randomAAIndex] == "L" || movableAAs[randomAAIndex] == "I")) || // we consider I and L the same, K and Q the same
							((peptide[position] == "K" || peptide[position] == "Q") &&
							(movableAAs[randomAAIndex] == "K" || movableAAs[randomAAIndex] == "Q"))
						)
							++repeat;
						else
							++nonRepeat;
						++randomAAIndex;
                    }
					else
					{
						shuffledPeptide << peptide[position];
						shuffledPeptideNoMods << peptide[position][0];
						++repeat;
					}

                }
                bool wellScrambled = nonRepeat >= 1.5*repeat ? true : false;
                //bool isHomolog = checkForHomology(peptideSequence.str(), shuffledPeptideNoMods.str(), 0.6);
                foundDecoy = wellScrambled;
                if(foundDecoy)
                    shuffledSequence = shuffledPeptide.str();
				else if (nonRepeat > bestTryNumber)
				{
					bestTry = shuffledPeptide.str();
					bestTryNumber = nonRepeat;
				}
                ++numAttempts;
            }
            if(shuffledSequence.length() > 0)
                return shuffledSequence;
			else if(bestTry.length() > 0)
                return bestTry;
            return string();
        }

		std::string LibraryBabelFish::mergeDatabaseWithContam(std::string database, std::string contam)
		{
			cout << "Appending contaminants to database..." << endl;

			//Get local contam database
			string contamPath;
			{
				if (contam == "default")
				{
					char buffer[MAX_PATH];
					GetModuleFileName(NULL,buffer,sizeof(buffer));
					string exeDir(buffer);
					exeDir = exeDir.substr(0, exeDir.find_last_of("/\\"));
					path dataDir (exeDir);
					path dataFile ("contams.fasta");
					path data_full_path = dataDir / dataFile;
					contamPath = data_full_path.string();
				}
				else if (contam == "")
					return database;
				else
				{
					ifstream testStream(contam);
					if (testStream)
					{
						testStream.close();
						contamPath = contam;
					}
					else
						return database;
				}
			}
			
			ifstream originalData(database.c_str());
			ifstream contamData(contamPath.c_str());
			
			//Set up output for new database
			path originalDatabase(database);
			path newDatabase(originalDatabase.stem() + "_with_contams.fasta");
			path data_out_full_path = current_path() / newDatabase;
			ofstream mergedDatabase(data_out_full_path.string().c_str());

			//Merge databases together and close files
			cout << contamPath + " & " + database << endl;
			mergedDatabase << contamData.rdbuf();
			mergedDatabase << originalData.rdbuf();
			mergedDatabase.flush();
			mergedDatabase.close();
			contamData.close();
			originalData.close();

			return data_out_full_path.string();
		}

		std::string LibraryBabelFish::mergeLibraryWithContam(std::string library, std::string contam)
		{
			cout << "Appending contaminants to spectral library..." << endl;
			bool appendContams = true;

			//get location of contam library
			string contamPath;
			{
				if (contam == "default")
				{
					char buffer[MAX_PATH];
					GetModuleFileName(NULL,buffer,sizeof(buffer));
					string exeDir(buffer);
					exeDir = exeDir.substr(0, exeDir.find_last_of("/\\"));			
					path contamDirectory (exeDir);
					path contamFile ("contams.sptxt");
					path contamFullPath = contamDirectory / contamFile;
					contamPath = contamFullPath.string();
				}
				else if (contam == "")
					appendContams = false;
				else
				{
					ifstream testStream(contam);
					if (testStream)
					{
						testStream.close();
						contamPath = contam;
					}
					else
						appendContams = false;
				}
			}
			
			//Set up output for merged library
			string filenameSuffix;
			if (appendContams)
				filenameSuffix = "_with_contams.sptxt";
			else
				filenameSuffix = "_with_decoys.sptxt";
			path originalLibraryPath(library);
			path newLibraryFile(originalLibraryPath.stem() + filenameSuffix);
			path newLibraryPath = current_path() / newLibraryFile;
			ofstream mergedLibrary(newLibraryPath.string().c_str());

			//open files to read
			ifstream originalLib(originalLibraryPath.string().c_str());

			//copy original library to merged file, except for last line
			string nextLine;
			getline(originalLib, nextLine);
			while (!originalLib.eof())
			{
				/*if (nextLine != "")
					lastEmpty = true;
				else if (lastEmpty)
				{
					getline(originalLib, nextLine);
					continue;
				}*/
				mergedLibrary << nextLine << "\n";
				getline(originalLib, nextLine);
			}
			originalLib.close();

			//copy entire contam library to merged file 
			if (appendContams)
			{
				ifstream contamLib(contamPath.c_str());
				mergedLibrary << contamLib.rdbuf();
				contamLib.close();
				mergedLibrary.flush();
				mergedLibrary.close();
			}

			return newLibraryPath.string();
		}

		void LibraryBabelFish::refreshLibrary(string libraryPath, proteinStore proteins, string decoy = "rev_")
		{
			cout << "Synchronizing spectral library with database..." << endl;

			PeptideTrie peptideTrie;
			SpectraStore tempLibrary;

			boost::regex removeModRegex ("\\[\\d+\\]",boost::regex_constants::icase|boost::regex_constants::perl);

			//get all peptides from library and construct trie
			{
				vector<shared_string> peptides;
				tempLibrary.loadLibrary(libraryPath);
				NativeFileReader nativeReader(libraryPath);

				cout << "Library Loaded- " << libraryPath << endl;
				size_t totalSpectra = tempLibrary.size();
                for(sqlite3_int64 x = 0; x < totalSpectra; ++x)
				{
					if (x%1000 == 0)
						cout << "Gathering sequences: " << x << " of " << lexical_cast<string>(totalSpectra) << '\r' << flush;
					//if (x > 50460)
					//	cout << "Gathering sequences: " << x << " of " << lexical_cast<string>(totalSpectra) << '\r' << flush;

					tempLibrary[x]->readHeader(nativeReader);
                    tempLibrary[x]->readPeaks(nativeReader);

					string rawSequence = tempLibrary[x]->matchedPeptide->sequence();
					const string& sequence = boost::regex_replace(rawSequence, removeModRegex, "");
					shared_string newSequence(new string(sequence));
					peptides.push_back(newSequence);
				}
				cout << "Sequences Gathered...                    " << endl;
				peptideTrie.insert(peptides.begin(), peptides.end());
				cout << "Trie constructed..." << endl;
			}

			//search through each protein and save the peptides that are associated with it
			map<string,string> peptideMap;
			for (int x = 0; x < (int)proteins.size();x++)
			{
				if (x%1000 == 0)
					cout << "Mapping peptides: " << x << " of " << lexical_cast<string>(proteins.size()) << '\r' << flush;
				if(proteins[x].isDecoy())
					continue;
				proteinData currentProtein = proteins[x];
				vector<PeptideTrie::SearchResult> peptideInstances = peptideTrie.find_all(currentProtein.getSequence());
				if (peptideInstances.empty())
                        continue;
				BOOST_FOREACH(const PeptideTrie::SearchResult& instance, peptideInstances)
                {
					peptideMap[*instance.keyword().get()] = currentProtein.getName();
				}
			}

			cout << "Peptides mapped...                    " << endl;

			//copy library into new file with modified proteins
			boost::regex findPeptideRegex ("^Name: ([^/]+)/",boost::regex_constants::icase|boost::regex_constants::perl);
			boost::regex findCommentRegex ("(Comment: .*)(Protein=\\d/)([^ ]*)(.*Spec=)([^ ]*)(.*)",boost::regex_constants::icase|boost::regex_constants::perl);
			boost::regex aminoAcidRegex ("[a-zA-Z]\\[\\d+\\]|[a-zA-Z]",boost::regex_constants::icase|boost::regex_constants::perl);
			boost::regex spectraRegex ("([\\d.]+)(\\t[\\d.]+)\\t([?a-zA-Z]\\d*)([+-]\\d+)?\\^?(\\d+)?i?\\/?([^,\\t]+)?[^\\t]*(.+)",boost::regex_constants::icase|boost::regex_constants::perl);
			boost::regex fullNameRegex ("(FullName: (?:\\w\\.)?)[\\w\\[\\]]+(\\.?\\w?\\/\\d)",boost::regex_constants::icase|boost::regex_constants::perl);

			ofstream outFile((libraryPath + ".tmp").c_str());
			ifstream inFile(libraryPath.c_str());
			string activeProtein = "";
			string decoyPeptide;
			string peptideFullName;
			stringstream outputStream;
			stringstream decoyStream;
			bool isDecoy = false;
			string newLine;
			map<string,double> decoyIons;
			map<double,string> decoySpectra;

			getline(inFile,newLine);
			int writeIndex = 0;

			//go through file
			while (!inFile.eof())
			{
				if (newLine.length() > 0 && newLine[0] == '#')
				{
					outputStream << newLine << endl;
				}
				//Initial name line
				else if (boost::regex_search (newLine, findPeptideRegex, boost::regex_constants::format_perl))
				{
					string sequence = newLine.substr(6,(newLine.length()-8));
					boost::match_results<std::string::const_iterator> regex_matches;
					
					string noModSequence = boost::regex_replace(sequence, removeModRegex, "");
					map<string,string>::iterator it = peptideMap.find(noModSequence);
					if (it != peptideMap.end())
					{
						//create decoy peptide					
						boost::sregex_token_iterator originalAminoAcidLetter(sequence.begin(), sequence.end(), aminoAcidRegex, 0);
					    boost::sregex_token_iterator end;

						vector<string> originalAminoAcidList;
						vector<string> aminoAcidList;
						for( ; originalAminoAcidLetter != end; ++originalAminoAcidLetter )
							originalAminoAcidList.push_back(*originalAminoAcidLetter);
						decoyPeptide = shufflePeptideSequence(originalAminoAcidList);
						if (decoyPeptide.length()<sequence.length())
							decoyPeptide = "";
						boost::sregex_token_iterator aminoAcidLetter(decoyPeptide.begin(), decoyPeptide.end(), aminoAcidRegex, 0);
						for( ; aminoAcidLetter != end; ++aminoAcidLetter )
							aminoAcidList.push_back(*aminoAcidLetter);
						

						//calculate decoy ions
						double b = 1;
						double y = 19;
						for (int x=0; x < aminoAcidList.size(); x++)
						{
							//calculate b
							double mass = AminoAcid::Info::record(aminoAcidList[x][0]).residueFormula.monoisotopicMass();
							if (aminoAcidList[x].length() > 1)
							{
								string tempString = aminoAcidList[x].substr(2,aminoAcidList[x].length()-3);
								mass += lexical_cast<double>(tempString);
							}
							b += mass;
							decoyIons.insert(pair<string,double>("b"+lexical_cast<string>(x+1),b));
							decoyIons.insert(pair<string,double>("a"+lexical_cast<string>(x+1),b-28));

							//calculate y
							mass = AminoAcid::Info::record(aminoAcidList[aminoAcidList.size()-1-x][0]).residueFormula.monoisotopicMass();
							if (aminoAcidList[aminoAcidList.size()-1-x].length() > 1)
							{
								string tempString = aminoAcidList[aminoAcidList.size()-1-x].substr(2,aminoAcidList[aminoAcidList.size()-1-x].length()-3);
								mass += lexical_cast<double>(tempString);
							}
							y += mass;
							decoyIons.insert(pair<string,double>("y"+lexical_cast<string>(x+1),y));
						}
						decoyIons.insert(pair<string,double>("p",y));
						decoyStream << "Name: " << decoyPeptide << "/" << newLine[newLine.length()-1] << endl;
					
						activeProtein = it->second;
						outputStream << newLine << endl;
					}
					else
						activeProtein = "";
				}
				//only continue if there is a protein associated
				else if (activeProtein != "")
				{
					//comment line
					if (boost::regex_search (newLine, findCommentRegex, boost::regex_constants::format_perl))
					{
						boost::match_results<std::string::const_iterator> regex_matches;
						if (boost::regex_search (newLine, regex_matches, findCommentRegex, boost::regex_constants::format_perl))
						{
							//check for decoy indicator
							if (regex_matches[3].str().length() >= decoy.length() && regex_matches[3].str().substr(0,decoy.length()) == decoy)
								isDecoy = true;

							//create decoy comment
							decoyStream << regex_matches[1] << "OrigPeptide=" << peptideFullName << " " << regex_matches[2] << decoy << activeProtein << " Remark=DECOY" << regex_matches[4] << "Decoy" << regex_matches[6] << endl;
							newLine = boost::regex_replace(newLine, findCommentRegex, regex_matches[1] + " " + regex_matches[2] + activeProtein + regex_matches[4] + regex_matches[5] + regex_matches[6]);							
						}

						outputStream << newLine << endl;
					}
					//Fullname Line
					else if (boost::regex_search (newLine, fullNameRegex, boost::regex_constants::format_perl))
					{
						boost::match_results<std::string::const_iterator> regex_matches;
						if (boost::regex_search (newLine, regex_matches, fullNameRegex, boost::regex_constants::format_perl))
							decoyStream << regex_matches[1] << decoyPeptide << regex_matches[2] << endl;
						peptideFullName = newLine.substr(10);
						outputStream << newLine << endl;
					}
					//Fullname Line
					else if (newLine.length()>5 && newLine.substr(0,5) == "LibID")
					{
						outputStream << "LibID: " << writeIndex << endl;
						decoyStream << "LibID: " << writeIndex+1 << endl;
					}
					//spectra line
					else if (!isDecoy && boost::regex_search (newLine, spectraRegex, boost::regex_constants::format_perl))
					{
						boost::match_results<std::string::const_iterator> regex_matches;
						if (boost::regex_search (newLine, regex_matches, spectraRegex, boost::regex_constants::format_perl))
						{
							string contents = regex_matches[2] + "\t" + regex_matches[3];
							if (regex_matches[4].length() > 0)							
								contents+= regex_matches[4];
							if (regex_matches[5].length() > 0)							
								contents+= "^" + regex_matches[5];
							if (regex_matches[6].length() > 0)
								contents+= "/" + regex_matches[6];
							contents+= "\t" + regex_matches[7];
							map<string,double>::iterator it = decoyIons.find(regex_matches[3]);
							if (it != decoyIons.end())
							{
								double mass = decoyIons[regex_matches[3]];
								if (regex_matches[4].length() > 0)							
									mass += lexical_cast<double>(regex_matches[4]);
								if (regex_matches[5].length() > 0)							
									mass /= lexical_cast<double>(regex_matches[5]);
								stringstream temp;
								temp << std::fixed << std::setprecision(4) << mass;
								decoySpectra.insert(pair<double, string>(mass, temp.str() + "\t" + contents));
							}
							else
								decoySpectra.insert(pair<double, string>(lexical_cast<double>(regex_matches[1]), regex_matches[1] + "\t" + contents));
							
						}
						outputStream << newLine << endl;
					}
					//end of spectra
					else if (newLine.length() == 0)
					{
						//consolidate decoy spectra
						map<double, string>::iterator it;
						for (it = decoySpectra.begin(); it != decoySpectra.end(); ++it)
							decoyStream << it->second << endl;

						//write
						if (!isDecoy && decoyPeptide.length() > 0)
						{
							outFile << outputStream.rdbuf() << endl;
							outFile << decoyStream.rdbuf() << endl;
							writeIndex+=2;
							if (writeIndex%1000 == 0)
								cout << "Refreshing spectra: " << writeIndex << '\r' << flush;
						}
						decoyPeptide = "";
						decoySpectra.clear();
						outputStream.str("");
						decoyStream.str("");
						decoyIons.clear();
						isDecoy = false;
					}
					//any other line
					else
					{
						outputStream << newLine << endl;
						decoyStream << newLine << endl;
					}
				}
				getline(inFile,newLine);
			}
			inFile.close();
			outFile.close();
			remove(libraryPath.c_str());
			rename((libraryPath + ".tmp").c_str(),libraryPath.c_str());
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
