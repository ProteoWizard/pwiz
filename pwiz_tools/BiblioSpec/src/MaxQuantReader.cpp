//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
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

/**
 * The MaxQuantReader parses the PSMs from the msms.txt tab delimited file
 * and stores each record. Records are grouped by file. Spectra are then
 * retrieved from the spectrum files. 
 */

#include "MaxQuantReader.h"

namespace BiblioSpec {

MaxQuantReader::MaxQuantReader(BlibBuilder& maker,
                               const char* tsvName,
                               const ProgressIndicator* parentProgress)
  : BuildParser(maker, tsvName, parentProgress), 
    tsvName_(tsvName), scoreThreshold_(getScoreThreshold(MAXQUANT)), lineNum_(1), 
    curMaxQuantPSM_(NULL), numColumns_(0), separator_('\\', '\t', '\"')
{
    Verbosity::debug("Creating MaxQuantReader.");
    
    setSpecFileName(tsvName, // this is for BuildParser
                    false);  // don't look for the file
    
    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    // define which columns are pulled from the file
    initTargetColumns();

    // Initialize mods
    initModifications();
}
    
MaxQuantReader::~MaxQuantReader()
{
    specReader_ = NULL; // so parent class doesn't try to delete itself
    if (tsvFile_.is_open())
    {
        tsvFile_.close();
    }
}

/**
 * Define the columns we are looking for in the file.
 */
void MaxQuantReader::initTargetColumns()
{
    targetColumns_.push_back(MaxQuantColumnTranslator("Raw File", -1, 
                                                      MaxQuantLine::insertRawFile));
    targetColumns_.push_back(MaxQuantColumnTranslator("Scan Number", -1, 
                                                      MaxQuantLine::insertScanNumber));
    targetColumns_.push_back(MaxQuantColumnTranslator("Sequence", -1,
                                                      MaxQuantLine::insertSequence));
    targetColumns_.push_back(MaxQuantColumnTranslator("m/z", -1,
                                                      MaxQuantLine::insertMz));
    targetColumns_.push_back(MaxQuantColumnTranslator("Charge", -1,
                                                      MaxQuantLine::insertCharge));
    targetColumns_.push_back(MaxQuantColumnTranslator("Modifications", -1,
                                                      MaxQuantLine::insertModifications));
    targetColumns_.push_back(MaxQuantColumnTranslator("Modified Sequence", -1,
                                                      MaxQuantLine::insertModifiedSequence));
    targetColumns_.push_back(MaxQuantColumnTranslator("Retention Time", -1,
                                                      MaxQuantLine::insertRetentionTime));
    targetColumns_.push_back(MaxQuantColumnTranslator("PEP", -1,
                                                      MaxQuantLine::insertPep));
    targetColumns_.push_back(MaxQuantColumnTranslator("Score", -1,
                                                      MaxQuantLine::insertScore));
    targetColumns_.push_back(MaxQuantColumnTranslator("Masses", -1,
                                                      MaxQuantLine::insertMasses));
    targetColumns_.push_back(MaxQuantColumnTranslator("Intensities", -1,
                                                      MaxQuantLine::insertIntensities));
    numColumns_ = targetColumns_.size();
}

/**
 * Read in the modifications file.
 */
void MaxQuantReader::initModifications()
{
    // Check for modifications.xml in same folder as tsv file
    string modFile = (filesystem::path(tsvName_).parent_path() / "modifications.xml").string();

    Verbosity::comment(V_DETAIL, "Checking for modification file %s",
                       modFile.c_str());
    if (!filesystem::exists(modFile) || !filesystem::is_regular_file(modFile))
    {
        // Not there, use default
        Verbosity::comment(V_DETAIL, "Loading default modifications");
        modFile = getExeDirectory() + "modifications.xml";
    }

    Verbosity::comment(V_DETAIL, "Parsing modification file %s",
                       modFile.c_str());
    MaxQuantModReader modReader(modFile.c_str(), &modBank_);
    try
    {
        bool success = modReader.parse();
        Verbosity::comment(V_DETAIL, "Done parsing %s, %d modifications found",
                           modFile.c_str(), modBank_.size());
        if (success)
        {
            return;
        }
    }
    catch (BlibException& e)
    {
        Verbosity::error("Error parsing modifications file: %s", e.what());
        modBank_.clear();
    }
    catch (...)
    {
        Verbosity::error("Unknown error while parsing modifications file");
        modBank_.clear();
    }
}

/**
 * Open the file, read header, read remaining file, build tables.
 */
bool MaxQuantReader::parseFile()
{
    Verbosity::debug("Parsing file.");
    if (!openFile())
    {
        return false;
    }

    // read header in first line
    string line;
    getline(tsvFile_, line);
    parseHeader(line);
    
    Verbosity::debug("Collecting PSMs.");
    collectPsms();

    Verbosity::debug("Building tables.");
    // add psms by filename
    initSpecFileProgress(fileMap_.size());
    for (map< string, vector<MaxQuantPSM*> >::iterator iter = fileMap_.begin();
         iter != fileMap_.end();
         ++iter)
    {
        psms_.assign(iter->second.begin(), iter->second.end());
        setSpecFileName(iter->first.c_str(), false);
        buildTables(MAXQUANT_SCORE, iter->first, false);
    }
    
    return true;
}

bool MaxQuantReader::openFile()
{
    Verbosity::debug("Opening TSV file.");
    tsvFile_.open(tsvName_.c_str());
    if(!tsvFile_.is_open())
    {
        throw BlibException(true, "Could not open tsv file '%s'.", 
                            tsvName_.c_str());
    }
    
    return true;
}

/**
 * Read the given line (the first in the tsv file) and locate the
 * position of each of the targeted columns.  Sort the target columns
 * by position.
 */
void MaxQuantReader::parseHeader(string& line)
{
    LineParser lineParser(line, separator_);
    int colNumber = 0;
    size_t numColumns = targetColumns_.size();
    
    // for each token in the line
    for (LineParser::iterator token = lineParser.begin();
         token != lineParser.end();
         ++token)
    {
        // check each column for a match
        for (size_t i = 0; i < numColumns; i++)
        {
            if (*token == targetColumns_[i].name_)
            {
                targetColumns_[i].position_ = colNumber;
            }
        }
        colNumber++;
    }
    
    // check that all required columns were in the file
    for (size_t i = 0; i < targetColumns_.size(); i++)
    {
        if (targetColumns_[i].position_ < 0)
        {
            throw BlibException(false, "Did not find required column '%s'.",
                                targetColumns_[i].name_.c_str());
        }
    }
    
    // sort by column number so they can be fetched in order
    sort(targetColumns_.begin(), targetColumns_.end());
}

/**
 * Read the tsv file and parse all psms.
 */
void MaxQuantReader::collectPsms()
{
    string line;
    bool parseSuccess = true;
    string errorMsg;

    // get file size and set progress
    streampos originalPos = tsvFile_.tellg();
    int lineCount = count(istreambuf_iterator<char>(tsvFile_),
                          istreambuf_iterator<char>(), '\n') + 1;
    tsvFile_.seekg(originalPos);
    ProgressIndicator progress(lineCount);

    // read file
    while (!tsvFile_.eof())
    {
        getline(tsvFile_, line);
        lineNum_++;
        
        // progress increment every 100 lines
        progress.increment();
        //streampos curPos = tsvFile_.tellg()/1024;
        //progress_->add(curPos - lastPos_);
        //lastPos_ = curPos;

        size_t colListIdx = 0;  // go through all target columns
        int lineColNumber = 0;  // compare to all file columns
        
        MaxQuantLine entry; // store each line
        try
        {
            LineParser lineParser(line, separator_); // create object for parsing line
            for (LineParser::iterator token = lineParser.begin();
                 token != lineParser.end();
                 ++token)
            {
                if (lineColNumber++ == targetColumns_[colListIdx].position_ )
                {
                    // insert the value in the proper field
                    targetColumns_[colListIdx++].inserter(entry, *token);
                    if (colListIdx == targetColumns_.size())
                    {
                        break;
                    }
                }
            }
            if (colListIdx != targetColumns_.size())
            {
                Verbosity::warn("Skipping invalid line %d", lineNum_);
                continue;
            }
        }
        catch (BlibException& e)
        {
            parseSuccess = false;
            errorMsg = e.what();
        }
        catch (std::exception& e)
        {
            parseSuccess = false;
            errorMsg = e.what();
        }
        catch (string& s)
        {
            parseSuccess = false;
            errorMsg = s;
        }
        catch (bad_lexical_cast e)
        {
            parseSuccess = false;
            errorMsg = e.what();
        }
        catch (...)
        {
            parseSuccess = false;
            errorMsg = "Unknown exception";
        }
        
        if (!parseSuccess)
        {
            throw BlibException(false, "%s caught at line %d, column %d", 
                                errorMsg.c_str(), lineNum_, lineColNumber + 1);
        }
        // store this line's information in the curPSM
        storeLine(entry);
    }
}
 
/**
 * Evaluate each line and add it to the map of files/PSMs.
 */
void MaxQuantReader::storeLine(MaxQuantLine& entry)
{
    if (entry.pep > scoreThreshold_)
    {
        Verbosity::comment(V_DETAIL, "Not saving PSM %d with PEP %f (line %d)",
                           entry.scanNumber, entry.pep, lineNum_);
        return;
    }

    // Set up new PSM
    curMaxQuantPSM_ = new MaxQuantPSM();

    curMaxQuantPSM_->specKey = entry.scanNumber;
    curMaxQuantPSM_->unmodSeq = entry.sequence;
    curMaxQuantPSM_->mz = entry.mz;
    curMaxQuantPSM_->charge = entry.charge;
    addModsToVector(curMaxQuantPSM_->mods, entry.modifications, entry.modifiedSequence);
    curMaxQuantPSM_->retentionTime = entry.retentionTime;
    curMaxQuantPSM_->score = entry.score;
    addDoublesToVector(curMaxQuantPSM_->mzs, entry.masses);
    addDoublesToVector(curMaxQuantPSM_->intensities, entry.intensities);

    // Save PSM
    map< string, vector<MaxQuantPSM*> >::iterator mapAccess
        = fileMap_.find(entry.rawFile);
    // file not in map yet, add it
    if (mapAccess == fileMap_.end())
    {
        vector<MaxQuantPSM*> tmpPsms;
        tmpPsms.push_back(curMaxQuantPSM_);
        fileMap_[entry.rawFile] = tmpPsms;
        mapAccess = fileMap_.find(entry.rawFile);
    }
    else
    {
        fileMap_[entry.rawFile].push_back(curMaxQuantPSM_);
    }
}

/**
 * Take a string of semicolon separated doubles and add them to the vector.
 */
void MaxQuantReader::addDoublesToVector(vector<double>& v, string valueList)
{
    vector<string> doubles;
    split(doubles, valueList, is_any_of(";"));

    try
    {
        transform(doubles.begin(), doubles.end(),
                  back_inserter(v), lexical_cast<double, string>);
    }
    catch (bad_lexical_cast e)
    {
        Verbosity::error("Could not cast \"%s\" to doubles", valueList.c_str());
    }
}

/**
 * Adds a SeqMod for each modification in the given modified sequence string of the form
 * "_I(ab)AMASEQ_". The modifications string contains the (comma separated) full names of
 * the modifications; the string "Unmodified" can mean no modifications are present.
 */
void MaxQuantReader::addModsToVector(vector<SeqMod>& v, string modifications, string modSequence)
{
    if (modifications == "Unmodified")
    {
        return;
    }

    // split modifications whole names
    vector<string> modNames;
    split(modNames, modifications, is_any_of(","));

    // check first and last characters
    int lastIndex = modSequence.length() - 1;
    if (modSequence[0] != '_' || modSequence[lastIndex] != '_')
    {
        throw BlibException(false, "Modified sequence %s must start and end with '_' (line %d)", 
                            modSequence.c_str(), lineNum_);
    }

    // iterate over sequence
    bool matchedMod;
    string modAbbreviation;
    unsigned int modsFound = 0;
    for (int i = 1; i < lastIndex; i++)
    {
        switch (modSequence[i])
        {
        case '(':
            modAbbreviation.clear();
            // found mod, get next 2 characters
            // check that there are at least 3 more characters (2 + closing paren)
            if (i > lastIndex - 3)
            {
                throw BlibException(false, "Opening parentheses found in sequence %s but not enough "
                                    "characters following (line %d)", modSequence.c_str(), lineNum_);
            }
            for (int j = 0; j < 2; j++)
            {
                ++i;
                if (modSequence[i] < 'a' || modSequence[i] > 'z')
                {
                    throw BlibException(false, "Illegal character %c found in sequence %s (line %d)", 
                                        modSequence[i], modSequence.c_str(), lineNum_);
                }
                modAbbreviation += modSequence[i];
            }
            // check for closing parentheses
            if (modSequence[++i] != ')')
            {
                throw BlibException(false, "Closing parentheses expected but %c found in sequence %s (line %d)",
                                           modSequence[i], modSequence.c_str(), lineNum_);
            }
            // which mod is it?
            matchedMod = false;
            for (vector<string>::iterator iter = modNames.begin(); iter != modNames.end(); ++iter)
            {
                string modCheck = "";
                modCheck += tolower((*iter)[0]);
                modCheck += tolower((*iter)[1]);
                if (modAbbreviation == modCheck)
                {
                    map<string, double>::iterator lookup = modBank_.find(*iter);
                    if (lookup == modBank_.end())
                    {
                        throw BlibException(false, "Unknown modification %s in sequence %s (line %d)",
                                            modAbbreviation.c_str(), modSequence.c_str(), lineNum_);
                    }
                    // subtract 4 from position because "(xx" is 3 + 1 since it comes after the AA modified
                    // and 4 from position for each previous mod found (two parentheses and abbreviation)
                    SeqMod curMod(max((unsigned int)1, i - (4 + 4*modsFound)), lookup->second);
                    v.push_back(curMod);

                    matchedMod = true;
                    ++modsFound;
                    break;
                }
                // Are we testing against the last modification name in the vector?
                if (iter == modNames.end() - 1)
                {
                    /* Try stripping numbers off the beginning -
                     * Modification string might be "2 Oxidation (M) to indicate
                     * that there are 2 "Oxidation (M)", not that the name of the
                     * mod is "2 Oxidation (M)" */
                    for (vector<string>::iterator retryIter = modNames.begin();
                         retryIter != modNames.end();
                         ++retryIter)
                    {
                        // loop until we find a non numeric character or the end of the string
                        unsigned int newStart;
                        for (newStart = 0; newStart < retryIter->length(); ++newStart)
                        {
                            if (!isdigit((*retryIter)[newStart])) break;
                        }
                        // Make sure we found a space and that there are at least 2 chars after it
                        if ((*retryIter)[newStart] == ' ' && newStart + 2 < retryIter->length())
                        {
                            string modRecheck = "";
                            modRecheck += tolower((*retryIter)[++newStart]);
                            modRecheck += tolower((*retryIter)[newStart + 1]);
                            if (modAbbreviation == modRecheck)
                            {
                                // Change name in vector
                                *retryIter = retryIter->substr(newStart);
                                // Move iterator
                                iter = retryIter - 1;
                            }
                        }
                    }
                }
            }

            if (!matchedMod)
            {
                throw BlibException(false, "No matching mod for %s in sequence %s (line %d)",
                                    modAbbreviation.c_str(), modSequence.c_str(), lineNum_);
            }
            break;
        case ')':
            throw BlibException(false, "Unexpected closing parentheses found in sequence %s (line %d)",
                                modSequence.c_str(), lineNum_);
            break;
        default:
            if (modSequence[i] < 'A' || modSequence[i] > 'Z')
            {
                throw BlibException(false, "Illegal character %c found in sequence %s (line %d)", 
                                    modSequence[i], modSequence.c_str(), lineNum_);
            }
            break;
        }
    }

    if (modsFound < modNames.size())
    {
        Verbosity::warn("Found %d exceptions but expected at least %d in sequence %s (%d)",
                        modsFound, modNames.size(), modSequence.c_str(), lineNum_);
    }
}

void MaxQuantReader::openFile(const char*, bool) {}
void MaxQuantReader::setIdType(SPEC_ID_TYPE) {}

/**
 * Overrides the SpecFileReader implementation.  Assumes the PSM is a
 * MaxQuantPSM and copies the additional data from it to the SpecData.
 */
bool MaxQuantReader::getSpectrum(PSM* psm,
                                 SPEC_ID_TYPE findBy,
                                 SpecData& returnData,
                                 bool getPeaks)
{
    returnData.id = ((MaxQuantPSM*)psm)->specKey;
    returnData.retentionTime = ((MaxQuantPSM*)psm)->retentionTime;
    returnData.mz = ((MaxQuantPSM*)psm)->mz;
    returnData.numPeaks = ((MaxQuantPSM*)psm)->mzs.size();

    if (getPeaks)
    {
        returnData.mzs = new double[returnData.numPeaks];
        returnData.intensities = new float[returnData.numPeaks];
        for (int i = 0; i < returnData.numPeaks; i++)
        {
            returnData.mzs[i] = ((MaxQuantPSM*)psm)->mzs[i];
            returnData.intensities[i] = (float)((MaxQuantPSM*)psm)->intensities[i];  
        }
    }
    else
    {
        returnData.mzs = NULL;
        returnData.intensities = NULL;
    }
    return true;
}

bool MaxQuantReader::getSpectrum(int, SpecData&, SPEC_ID_TYPE, bool) { return false; }
bool MaxQuantReader::getSpectrum(string, SpecData&, bool) { return false; }
bool MaxQuantReader::getNextSpectrum(SpecData&, bool) { return false; }

} // namespace
