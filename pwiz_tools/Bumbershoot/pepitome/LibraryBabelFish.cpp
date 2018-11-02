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
#include <boost/xpressive/xpressive.hpp>
using freicore::AhoCorasickTrie;
typedef boost::shared_ptr<std::string> shared_string;
namespace bxp = boost::xpressive;

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
        
        bool _hcdMode = false;
        bool _iTraqMode = false;

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
        
        inline map<string,double> findIonWeights(string sequence)
        {
            bxp::sregex aminoAcidRegex = bxp::sregex::compile("[a-zA-Z]\\[\\d+\\]|[a-zA-Z]", bxp::regex_constants::icase );
            vector<string> aminoAcidList;
            map<string,double> ionMap;
            bxp::sregex_token_iterator aminoAcidLetter(sequence.begin(), sequence.end(), aminoAcidRegex, 0);
            bxp::sregex_token_iterator end;

            double b = 1;
            double y = 19;
            double nTermMod = 0;
            bool diasableiTraq = false;
            
            if (_iTraqMode)
            {
                b += 144.1021;
                nTermMod += 144.1021;
            }
                        
            for( ; aminoAcidLetter != end; ++aminoAcidLetter )
            {
                string tempString = *aminoAcidLetter;
                if (tempString[0] == 'n') //consider n-term mods
                {
                    tempString = tempString.substr(2,tempString.length()-3);
                    b += lexical_cast<double>(tempString);
                    nTermMod += lexical_cast<double>(tempString);
                    if (_iTraqMode)
                    {
                        b -= 144.1021;
                        nTermMod -= 144.1021;
                        diasableiTraq = true;
                    }
                }
                else if (tempString[0] == 'c') //consider c-term mods
                {
                    tempString = tempString.substr(2,tempString.length()-3);
                    y += lexical_cast<double>(tempString);
                }
                else
                    aminoAcidList.push_back(tempString);
            }
            
            for (int x=0; x < aminoAcidList.size(); x++)
            {
                //calculate b
                double mass = AminoAcid::Info::record(aminoAcidList[x][0]).residueFormula.monoisotopicMass();
                if (_iTraqMode && !diasableiTraq && aminoAcidList[x][0] == 'K')
                    mass += 144.1021;
                if (aminoAcidList[x].length() > 1)
                {
                    string tempString = aminoAcidList[x].substr(2,aminoAcidList[x].length()-3);
                    mass += lexical_cast<double>(tempString);
                }
                b += mass;
                ionMap.insert(pair<string,double>("b"+lexical_cast<string>(x+1),b));
                ionMap.insert(pair<string,double>("a"+lexical_cast<string>(x+1),b-28));

                //calculate y
                mass = AminoAcid::Info::record(aminoAcidList[aminoAcidList.size()-1-x][0]).residueFormula.monoisotopicMass();
                if (_iTraqMode && !diasableiTraq && aminoAcidList[aminoAcidList.size()-1-x][0] == 'K')
                    mass += 144.1021;
                if (aminoAcidList[aminoAcidList.size()-1-x].length() > 1)
                {
                    string tempString = aminoAcidList[aminoAcidList.size()-1-x].substr(2,aminoAcidList[aminoAcidList.size()-1-x].length()-3);
                    mass += lexical_cast<double>(tempString);
                }
                y += mass;
                ionMap.insert(pair<string,double>("y"+lexical_cast<string>(x+1),y));
            }
            ionMap.insert(pair<string,double>("p",y+nTermMod));
            
            return ionMap;
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
        
        inline string reversePeptideSequence(const vector<string>& peptide)
        {
            stringstream peptideSequence; 
            int peptideLength = (int) peptide.size();
            int firstMovableAAIndex = 200;
            int lastMovableAAIndex = -1;
            vector<bool> immutable(peptideLength,false);
            vector<string> movableAAs;
            for(int position = 0; position < peptideLength; ++position)
            {
                if(position == peptideLength-1 || peptide[position].find("n") != string::npos)
                    immutable[position] = true;
                else
                {
                    movableAAs.push_back(peptide[position]);
                    firstMovableAAIndex = firstMovableAAIndex > position ? position : firstMovableAAIndex;
                    lastMovableAAIndex = lastMovableAAIndex < position ? position : lastMovableAAIndex;
                }
                peptideSequence << peptide[position][0];
            }
            string reversedSequence;
            stringstream reversedPeptide;
            vector<string> reversedAAs;
            for (int x = (int)movableAAs.size()-1; x >=0; x--)
                reversedAAs.push_back(movableAAs[x]);
            size_t randomAAIndex = 0;
            size_t repeat = 0;
            size_t nonRepeat = 0;
            for(int position = 0; position < peptideLength; ++position)
            {
                if(!immutable[position])
                {
                    reversedPeptide << reversedAAs[randomAAIndex];
                    if (peptide[position] == reversedAAs[randomAAIndex] ||
                        ((peptide[position] == "L" || peptide[position] == "I") &&
                        (reversedAAs[randomAAIndex] == "L" || reversedAAs[randomAAIndex] == "I")) || // we consider I and L the same, K and Q the same
                        ((peptide[position] == "K" || peptide[position] == "Q") &&
                        (reversedAAs[randomAAIndex] == "K" || reversedAAs[randomAAIndex] == "Q"))
                    )
                        ++repeat;
                    else
                        ++nonRepeat;
                    ++randomAAIndex;
                }
                else
                {
                    reversedPeptide << peptide[position];
                    ++repeat;
                }

            }
            double currentOverlap = repeat / (nonRepeat + repeat);
            reversedSequence = reversedPeptide.str();
            currentOverlap = repeat / (nonRepeat + repeat);

            return reversedSequence;
        }

        //old-style decoy creation
        inline string shufflePeptideSequence(const vector<string>& peptide, map<string,double> theoreticalIons, vector<string> ionDescriptions, bool calledOnce = false)
        {
        
            // Amino acids containing modifications and also proline are not movable.
            stringstream peptideSequence; 
            int peptideLength = (int) peptide.size();
            int firstMovableAAIndex = 200;
            int lastMovableAAIndex = -1;
            double overlap = 1;
            vector<bool> immutable(peptideLength,false);
            vector<string> movableAAs;
            for(int position = 0; position < peptideLength; ++position)
            {
                if(peptide[position].find("[") != string::npos || peptide[position].find("K") != string::npos
                    || peptide[position].find("R") != string::npos || peptide[position].find("P") != string::npos)
                {
                    immutable[position] = true;
                    if(peptide[position].find("n") != string::npos)
                        immutable[position+1] = true;
                }
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
                std::random_shuffle(movableAAs.begin(),movableAAs.end());
                size_t repeat = 0;
                size_t nonRepeat = 0;
                size_t randomAAIndex = 0;
                
                for(int position = 0; position < peptideLength; ++position)
                {
                    if(!immutable[position])                    
                    {
                        shuffledPeptide << movableAAs[randomAAIndex];
                        randomAAIndex++;
                    }
                    else                    
                        shuffledPeptide << peptide[position];        
                }
                
                map<string,double> shuffledIons;
                {
                    map<string,double> tempMap = findIonWeights(shuffledPeptide.str());
                    shuffledIons.insert(tempMap.begin(), tempMap.end());
                }
                BOOST_FOREACH(const string ion, ionDescriptions)
                {
                    if (theoreticalIons[ion] == shuffledIons[ion])
                        repeat++;
                    else
                        nonRepeat++;
                }
                
                double currentOverlap = repeat / (nonRepeat + repeat);
                //bool isHomolog = checkForHomology(peptideSequence.str(), shuffledPeptideNoMods.str(), 0.6);
                if(currentOverlap < overlap)
                {
                    shuffledSequence = shuffledPeptide.str();
                    overlap = repeat / (nonRepeat + repeat);
                }
                ++numAttempts;
            }
            
            if(overlap <= 0.7 || calledOnce)
                return shuffledSequence;
            else
            {
                vector<string> aminoAcids = boost::assign::list_of("A")("R")("N")("D")("C")("E")("Q")("G")("H")("I")("L")("K")("M")("F")("P")("S")("T")("U")("W")("Y")("V");
                //{"A","R","N","D","E","Q","G","H","I","L","M","F","P","S","T","U","W","Y","V"};
                std::random_shuffle(aminoAcids.begin(),aminoAcids.end());
                vector<string> saltedPeptide;
                saltedPeptide.push_back(peptide[0]);
                saltedPeptide.push_back(aminoAcids[0]);
                saltedPeptide.push_back(aminoAcids[1]);
                for (int x = 1; x < peptideLength ; x++)
                    saltedPeptide.push_back(peptide[x]);
                string saltedDecoy = shufflePeptideSequence(saltedPeptide,theoreticalIons,ionDescriptions, true);
                return saltedDecoy;
            }
        }
        
        inline string InsertItraqMods(const string& originalModString, map<int,string> addedMap)
        {
            //Mods=2/10,M,Oxidation/4,N,Deamidation
            int modCount = addedMap.size();
            vector<string> strs;
            boost::split(strs,originalModString,boost::is_any_of("/"));
            modCount += (strs.size()-1);
            for (int x = 1; x < strs.size(); x++)
            {
                vector<string> elements;
                boost::split(elements,strs[x],boost::is_any_of(","));
                int index = lexical_cast<int>(elements[0]);
                addedMap.insert ( std::pair<int, string>(index, strs[x]));
            }
            string modString = "Mods=" + lexical_cast<string>(modCount);
            for (std::map<int, string>::iterator it=addedMap.begin(); it!=addedMap.end(); ++it)
            {
                modString += ("/" + it->second);
            }
            //cout << modString << endl;
            return modString;
        }

        std::string LibraryBabelFish::mergeDatabaseWithContam(const std::string& database, const std::string& contam)
        {
            //Get local contam database
            string contamPath;
            {
                if (contam == "")
                    return database;
                else
                {
                    ifstream testStream(contam.c_str());
                    if (testStream)
                    {
                        testStream.close();
                        contamPath = contam;
                    }
                    else
                        return database;
                }
            }
            cout << "Appending contaminants to database..." << endl;
            
            ifstream originalData(database.c_str());
            ifstream contamData(contamPath.c_str());
            
            //Set up output for new database
            path originalDatabase(database);
            path newDatabase(originalDatabase.stem().string() + "_with_contams.fasta");
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

        std::string LibraryBabelFish::mergeLibraryWithContam(const std::string& library, const std::string& contam)
        {
            bool appendContams = true;

            //get location of contam library
            string contamPath;
            {
                if (contam == "")
                    appendContams = false;
                else
                {
                    ifstream testStream(contam.c_str());
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
            {
                filenameSuffix = "_decoyed_with_contams.sptxt";
                cout << "Appending contaminants to spectral library..." << endl;
            }
            else
            {
                if (_iTraqMode)
                    filenameSuffix = "_decoyed_itraq4plex.sptxt";
                else if (_hcdMode)
                    filenameSuffix = "_decoyed_hcd.sptxt";
                else
                    filenameSuffix = "_decoyed.sptxt";
            }
            path originalLibraryPath(library);
            path newLibraryFile(originalLibraryPath.stem().string() + filenameSuffix);
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

        void LibraryBabelFish::refreshLibrary(const std::string& libraryPath, proteinStore proteins, bool hcdMode, bool iTraqMode, const std::string& decoy)
        {
            cout << "Synchronizing spectral library with database..." << endl;
            _hcdMode = hcdMode;
            _iTraqMode = iTraqMode;

            PeptideTrie peptideTrie;
            SpectraStore tempLibrary;

            bxp::sregex removeModRegex = bxp::sregex::compile("n?\\[\\d+\\]", bxp::regex_constants::icase );

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

                    tempLibrary[x]->readHeader(nativeReader);
                    tempLibrary[x]->readPeaks(nativeReader);

                    string rawSequence = tempLibrary[x]->matchedPeptide->sequence();
                    const string& sequence = bxp::regex_replace(rawSequence, removeModRegex, "");
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
            bxp::sregex findPeptideRegex = bxp::sregex::compile("^Name: ([^/]+)/(\\d+)", bxp::regex_constants::icase );
            bxp::sregex findMwRegex = bxp::sregex::compile("MW: ([\\d\\.]+)", bxp::regex_constants::icase );
            bxp::sregex findPrecursorMzRegex = bxp::sregex::compile("PrecursorMZ: ([\\d\\.]+)", bxp::regex_constants::icase );
            bxp::sregex findCommentRegex = bxp::sregex::compile("(Comment: .*)(Mods=[^ ]+)(.*)(Protein=\\d/)([^ ]*)(.*Spec=)([^ ]*)(.*)", bxp::regex_constants::icase );
            bxp::sregex aminoAcidRegex = bxp::sregex::compile("[a-zA-Z]\\[\\d+\\]|[a-zA-Z]", bxp::regex_constants::icase );
            bxp::sregex spectraRegex = bxp::sregex::compile("([\\d.]+)(\\t[\\d.]+)\\t\\[?([?a-zA-Z]\\d*)(i|[+-]\\d+i?)?\\^?(\\d+i?)?\\/?([^\\],\\t]+)?[^\\t]*(.+)", bxp::regex_constants::icase );
            bxp::sregex fullNameRegex = bxp::sregex::compile("(FullName: ([\\w\\-]\\.)?)[\\w\\[\\]]+(\\.?[\\w\\-]?\\/\\d)", bxp::regex_constants::icase );

            ofstream outFile((libraryPath + ".tmp").c_str());
            ifstream inFile(libraryPath.c_str());
            int charge = -1;
            string activeProtein = "";
            string decoyPeptide;
            string peptideShortName;
            string peptideFullName;
            stringstream outputStream;
            stringstream decoyStream;
            bool isDecoy = false;
            string newLine;
            vector<string> ionDescriptions;
            map<string,double> unlabeledIons;
            map<string,double> theoreticalIons;
            map<string,double> decoyIons;
            map<double,string> outputSpectra;
            map<double,string> decoySpectra;
            map<int,string> iTraqMods;
            bool report = false;

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
                else if (bxp::regex_search (newLine, findPeptideRegex, bxp::regex_constants::format_perl))
                {
                    string sequence;
                    bxp::match_results<std::string::const_iterator> regex_matches;
                    if (bxp::regex_search (newLine, regex_matches, findPeptideRegex, bxp::regex_constants::format_perl))
                    {
                        sequence = regex_matches[1];
                        charge = lexical_cast<int>(regex_matches[2]);
                    }
                    
                    string noModSequence = bxp::regex_replace(sequence, removeModRegex, "");
                    map<string,string>::iterator it = peptideMap.find(noModSequence);
                    if (it != peptideMap.end())
                    {
                        //calculate theoretical ions
                        vector<string> fullAminoAcidList;
                        bxp::sregex_token_iterator aminoAcidLetter(sequence.begin(), sequence.end(), aminoAcidRegex, 0);
                        bxp::sregex_token_iterator end;
                        stringstream tempSS;
                        string firstAA = (*aminoAcidLetter);
                        bool ignoreiTraq = !_iTraqMode || firstAA[0] == 'n';
                        if (!ignoreiTraq)
                        {
                            fullAminoAcidList.push_back("n[144]");
                            tempSS << "n[144]";
                            //string modString = "0," + lexical_cast<string>(firstAA[0]) + ",iTRAQ4plex";
                            string modString = "-1,(,iTRAQ4plex";
                            iTraqMods.insert ( std::pair<int, string>(0,modString) );
                        }
                        
                        int index = 0;
                        for( ; aminoAcidLetter != end; ++aminoAcidLetter )
                        {
                            string currentAA = *aminoAcidLetter;
                            if (currentAA[0] == 'K' && !ignoreiTraq)
                            {
                                currentAA = "K[272]";
                                string modString = lexical_cast<string>(index) + ",K,iTRAQ4plex";
                                iTraqMods.insert ( std::pair<int, string>(index, modString) );
                            }
                            fullAminoAcidList.push_back(currentAA);
                            tempSS << currentAA;
                            index++;
                        }
                        
                        peptideShortName = tempSS.str();
                        
                        /*for (size_t x=0; x < fullAminoAcidList.size(); x++)
                        {
                            ionDescriptions.push_back("b"+lexical_cast<string>(x+1));
                            ionDescriptions.push_back("a"+lexical_cast<string>(x+1));
                            ionDescriptions.push_back("y"+lexical_cast<string>(x+1));
                        }
                        ionDescriptions.push_back("p");    */
                        {
                            map<string,double> tempMap = findIonWeights(sequence);
                            theoreticalIons.insert(tempMap.begin(), tempMap.end());
                        }
                        if (_iTraqMode)
                        {
                            _iTraqMode = false;
                            map<string,double> tempMap = findIonWeights(sequence);
                            unlabeledIons.insert(tempMap.begin(), tempMap.end());
                            _iTraqMode = true;
                        }
                    
                        //create decoy peptide
                        decoyPeptide = reversePeptideSequence(fullAminoAcidList);
                        if (decoyPeptide.length()<sequence.length())
                            decoyPeptide = "";
                        {
                            map<string,double> tempMap = findIonWeights(decoyPeptide);
                            decoyIons.insert(tempMap.begin(), tempMap.end());
                        }
                        
                        decoyStream << "Name: " << decoyPeptide << "/" << newLine[newLine.length()-1] << endl;
                    
                        activeProtein = it->second;
                        outputStream << "Name: " << peptideShortName << "/" << newLine[newLine.length()-1] << endl;
                        
                    }
                    else
                        activeProtein = "";
                }
                //only continue if there is a protein associated
                else if (activeProtein != "")
                {
                    //MW Line
                    if ((_hcdMode || _iTraqMode) && bxp::regex_search (newLine, findMwRegex, bxp::regex_constants::format_perl))
                    {
                        outputStream << "MW: " << std::fixed << std::setprecision(4) << theoreticalIons["p"] << endl;
                        decoyStream << "MW: " << std::fixed << std::setprecision(4) << theoreticalIons["p"] << endl;
                    }        
                    //PrecursorMZ Line
                    else if ((_hcdMode || _iTraqMode) && charge > 0 && bxp::regex_search (newLine, findPrecursorMzRegex, bxp::regex_constants::format_perl))
                    {
                        outputStream << "PrecursorMZ: " << std::fixed << std::setprecision(4) << theoreticalIons["p"]/charge << endl;
                        decoyStream << "PrecursorMZ: " << std::fixed << std::setprecision(4) << theoreticalIons["p"]/charge << endl;
                    }                    
                    //Comment line
                    else if (bxp::regex_search (newLine, findCommentRegex, bxp::regex_constants::format_perl))
                    {
                        bxp::match_results<std::string::const_iterator> regex_matches;
                        if (bxp::regex_search (newLine, regex_matches, findCommentRegex, bxp::regex_constants::format_perl))
                        {
                            //check for decoy indicator
                            if (regex_matches[5].str().length() >= decoy.length() && regex_matches[5].str().substr(0,decoy.length()) == decoy)
                                isDecoy = true;
                            
                            string newModString = regex_matches[2];
                            if (_iTraqMode)
                                newModString = InsertItraqMods(regex_matches[2], iTraqMods);
                            //create decoy comment
                            decoyStream << regex_matches[1] << newModString << regex_matches[3] << "OrigPeptide=" << peptideFullName << " " << regex_matches[4] << decoy << activeProtein << " Remark=DECOY" << regex_matches[6] << "Decoy" << regex_matches[8] << endl;
                            outputStream << regex_matches[1] << newModString << regex_matches[3] << regex_matches[4] << activeProtein << regex_matches[6] << regex_matches[7] << regex_matches[8] << endl;                        
                        }
                    }
                    //Fullname Line
                    else if (bxp::regex_search (newLine, fullNameRegex, bxp::regex_constants::format_perl))
                    {
                        bxp::match_results<std::string::const_iterator> regex_matches;
                        if (bxp::regex_search (newLine, regex_matches, fullNameRegex, bxp::regex_constants::format_perl))
                            decoyStream << regex_matches[1] << decoyPeptide << regex_matches[3] << endl;
                        peptideFullName = regex_matches[2] + peptideShortName + regex_matches[3];
                        outputStream << regex_matches[1] << peptideShortName << regex_matches[3] << endl;
                    }
                    //Fullname Line
                    else if (newLine.length()>5 && newLine.substr(0,5) == "LibID")
                    {
                        outputStream << "LibID: " << writeIndex << endl;
                        decoyStream << "LibID: " << writeIndex+1 << endl;
                    }
                    //spectra line
                    else if (!isDecoy && bxp::regex_search (newLine, spectraRegex, bxp::regex_constants::format_perl))
                    {
                        bxp::match_results<std::string::const_iterator> regex_matches;
                        if (bxp::regex_search (newLine, regex_matches, spectraRegex, bxp::regex_constants::format_perl))
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
                                //calculate theoretical mass 
                                double theoreticalMass = theoreticalIons[regex_matches[3]];
                                if (regex_matches[4].length() > 0)                            
                                {
                                    int offset = 0;
                                    if (regex_matches[4].str().find("i") != string::npos)
                                    {
                                        theoreticalMass += 1;
                                        offset = 1;
                                    }
                                    if (regex_matches[4].length() > offset)
                                        theoreticalMass += lexical_cast<double>(regex_matches[4].str().substr(0,regex_matches[4].str().length()-offset));
                                }
                                if (regex_matches[5].length() > 0)
                                {
                                    int offset = 0;
                                    if (regex_matches[5].str().find("i") != string::npos)
                                    {
                                        theoreticalMass += 1;
                                        offset = 1;
                                    }
                                    if (regex_matches[5].length() > offset)
                                        theoreticalMass /= lexical_cast<double>(regex_matches[5].str().substr(0,regex_matches[5].str().length()-offset));
                                }
                                
                                //calculate unlabeled mass if iTRAQ mode
                                double unlabeledMass = theoreticalMass;
                                if (_iTraqMode)
                                {
                                    unlabeledMass = unlabeledIons[regex_matches[3]];
                                    if (regex_matches[4].length() > 0)                            
                                    {
                                        int offset = 0;
                                        if (regex_matches[4].str().find("i") != string::npos)
                                        {
                                            unlabeledMass += 1;
                                            offset = 1;
                                        }
                                        if (regex_matches[4].length() > offset)
                                            unlabeledMass += lexical_cast<double>(regex_matches[4].str().substr(0,regex_matches[4].str().length()-offset));
                                    }
                                    if (regex_matches[5].length() > 0)
                                    {
                                        int offset = 0;
                                        if (regex_matches[5].str().find("i") != string::npos)
                                        {
                                            unlabeledMass += 1;
                                            offset = 1;
                                        }
                                        if (regex_matches[5].length() > offset)
                                            unlabeledMass /= lexical_cast<double>(regex_matches[5].str().substr(0,regex_matches[5].str().length()-offset));
                                    }
                                }
                                
                                //calculate decoy mass 
                                double mass = decoyIons[regex_matches[3]];
                                if (regex_matches[4].length() > 0)                            
                                {
                                    int offset = 0;
                                    if (regex_matches[4].str().find("i") != string::npos)
                                    {
                                        mass += 1;
                                        offset = 1;
                                    }
                                    if (regex_matches[4].length() > offset)
                                        mass += lexical_cast<double>(regex_matches[4].str().substr(0,regex_matches[4].str().length()-offset));
                                }
                                if (regex_matches[5].length() > 0)                            
                                {
                                    int offset = 0;
                                    if (regex_matches[5].str().find("i") != string::npos)
                                    {
                                        mass += 1;
                                        offset = 1;
                                    }
                                    if (regex_matches[5].length() > offset)
                                        mass /= lexical_cast<double>(regex_matches[5].str().substr(0,regex_matches[5].str().length()-offset));
                                }
                                double massCorrection = (lexical_cast<double>(regex_matches[1]) - unlabeledMass );
                                /*if (massCorrection > 5)
                                {
                                    if (report)
                                        cout << newLine << endl << "| " << regex_matches[1] << " | " << theoreticalMass << " | " << unlabeledMass << " | " << massCorrection << " |" << endl;
                                    else
                                    {
                                        cout << peptideFullName << endl;
                                        cout << newLine << endl << "| " << regex_matches[1] << " | " << theoreticalMass << " | " << massCorrection << " |" << endl;
                                        report = true;
                                    }
                                }*/
                                //cout << std::fixed << std::setprecision(4) << theoreticalMass << "\t" << mass << "\t" << massCorrection << endl << endl;
                                if (!_hcdMode)
                                    mass += massCorrection;
                                stringstream temp;
                                temp << std::fixed << std::setprecision(4) << mass;
                                decoySpectra[mass] = temp.str() + contents;
                                //decoySpectra.insert(pair<double, string>(mass, temp.str() + contents));
                                
                                if (_hcdMode || _iTraqMode)
                                {
                                    stringstream temp;
                                    if (_hcdMode)
                                        temp << std::fixed << std::setprecision(4) << theoreticalMass;
                                    else
                                        temp << std::fixed << std::setprecision(4) << theoreticalMass + massCorrection;
                                    //outputSpectra.insert(pair<double, string>(lexical_cast<double>(regex_matches[1]), temp.str() + contents));
                                    outputSpectra[lexical_cast<double>(regex_matches[1])] = temp.str() + contents;
                                }
                                else
                                    outputSpectra[lexical_cast<double>(regex_matches[1])] = regex_matches[1] + contents;
                                    //outputSpectra.insert(pair<double, string>(lexical_cast<double>(regex_matches[1]), regex_matches[1] + contents));
                            }
                            else
                            {
                                outputSpectra[lexical_cast<double>(regex_matches[1])] = regex_matches[1] + contents;
                                //outputSpectra.insert(pair<double, string>(lexical_cast<double>(regex_matches[1]), regex_matches[1] + contents));
                                decoySpectra[lexical_cast<double>(regex_matches[1])] = regex_matches[1] + contents;
                                //decoySpectra.insert(pair<double, string>(lexical_cast<double>(regex_matches[1]), regex_matches[1] + contents));
                            }
                            
                        }
                        
                    }
                    //end of spectra
                    else if (newLine.length() == 0)
                    {
                        //consolidate output spectra
                        map<double, string>::iterator it;
                        for (it = outputSpectra.begin(); it != outputSpectra.end(); ++it)
                            outputStream << it->second << endl;
                    
                        //consolidate decoy spectra
                        for (it = decoySpectra.begin(); it != decoySpectra.end(); ++it)
                            decoyStream << it->second << endl;

                        //write
                        if (!isDecoy && decoyPeptide.length() > 0)
                        {
                            outFile << outputStream.rdbuf() << endl;
                            outFile << decoyStream.rdbuf() << endl;
                            writeIndex+=2;
                            if (writeIndex%1000 == 0)
                                cout << "Refreshing spectra: " << writeIndex/2 << '\r' << flush;
                        }
                            
                        decoyPeptide = "";
                        outputSpectra.clear();
                        decoySpectra.clear();
                        iTraqMods.clear();
                        outputStream.str("");
                        decoyStream.str("");
                        decoyIons.clear();
                        unlabeledIons.clear();
                        theoreticalIons.clear();
                        ionDescriptions.clear();
                        isDecoy = false;
                        
                        if (report)
                        {
                            cout << endl;
                            report = false;
                        }
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
