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
 * 
 * There may also be an evidence.txt file present, from which ion mobility
 * data may be parsed.
 */

#include "MaxQuantReader.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/range/algorithm_ext/insert.hpp"
#include <boost/lexical_cast.hpp>

namespace filesystem = bfs;

namespace BiblioSpec {

MaxQuantReader::MaxQuantReader(BlibBuilder& maker,
                               const char* tsvName,
                               const ProgressIndicator* parentProgress)
  : BuildParser(maker, tsvName, parentProgress), 
    tsvName_(tsvName), scoreThreshold_(getScoreThreshold(MAXQUANT)), lineNum_(1), 
    curMaxQuantPSM_(NULL), separator_('\\', '\t', '\"')
{
    Verbosity::debug("Creating MaxQuantReader.");

    // MaxQuant defaults to requiring external spectra
    preferEmbeddedSpectra_ = maker.preferEmbeddedSpectra().get_value_or(false);

    // read ion mobility info from evidence.txt if it exists
    initEvidence();

    // get mods path (will be empty string if not set)
    modsPath_ = maker.getMaxQuantModsPath();

    // get params path (will be empty string if not set)
    paramsPath_ = maker.getMaxQuantParamsPath();

    // define which columns are pulled from the file
    initTargetColumns();

    // Initialize mods
    initModifications();
}
    
MaxQuantReader::~MaxQuantReader()
{
    if (specReader_ == this)
        specReader_ = NULL; // so parent class doesn't try to delete itself
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
    targetColumns_.push_back(MaxQuantColumnTranslator("Labeling State", -1,
                                                      MaxQuantLine::insertLabelingState));
    targetColumns_.push_back(MaxQuantColumnTranslator("Evidence ID", -1,
                                                      MaxQuantLine::insertEvidenceID)); // Lookup into evidence.txt file for ion mobility info

    // columns that can are useful but not required
    optionalColumns_.insert("Labeling State");
    optionalColumns_.insert("Evidence ID");
}

string checkForModificationsFile(filesystem::path parentPath, const char *filename)
{
	string modFile = (parentPath / filename).string();
	Verbosity::comment(V_DETAIL, "Checking for modification file %s",
		modFile.c_str());
	if (!filesystem::exists(modFile) || !filesystem::is_regular_file(modFile))
	{
		return string();
	}
	return modFile;
}

bool parseModificationsFile(const char* modFile, map<string, MaxQuantModification>& modBank)
{
	Verbosity::comment(V_DETAIL, "Parsing modification file %s",
		modFile);
	MaxQuantModReader modReader(modFile, &modBank);
	int initialSize = modBank.size();
	try
	{
		modReader.parse();
		Verbosity::comment(V_DETAIL, "Done parsing %s, %d modifications found",
			modFile, modBank.size() - initialSize);
		return true;
	}
	catch (BlibException& e)
	{
		Verbosity::error("Error parsing modifications file: %s", e.what());
		return false;
	}
	catch (...)
	{
		Verbosity::error("Unknown error while parsing modifications file");
		return false;
	}
}

/**
 * Read in the modifications file.
 */
void MaxQuantReader::initModifications()
{
	filesystem::path parentPath = filesystem::path(tsvName_).parent_path();
	string modFile = modsPath_;
	string localModFile;
	if (modFile.rfind(".local.xml") != string::npos)
	{
		localModFile = modFile;
		modFile.clear();
	}
	// Look for the main modifications file. If it has not already been specified in "modsPath", then look
	// for it in the same directory as the tsvFile, or use the one that comes with the exe.
	if (modFile.empty() ||
        !filesystem::exists(modFile) || !filesystem::is_regular_file(modFile))
    {
		modFile = checkForModificationsFile(parentPath, "modifications.xml");
		if (modFile.empty())
		{
			modFile = checkForModificationsFile(parentPath, "modification.xml");
		}
		if (modFile.empty())
        {
            // Not there, use default
            Verbosity::comment(V_DETAIL, "Loading default modifications");
            modFile = getExeDirectory() + "modifications.xml";
        }
    }

	if (!parseModificationsFile(modFile.c_str(), modBank_))
	{
		modBank_.clear();
		return;
	}

	// Check for the existence of an optional "modifications.local.xml"
	if (localModFile.empty())
		localModFile = checkForModificationsFile(parentPath, "modifications.local.xml");
	if (!localModFile.empty())
	{
		if (!parseModificationsFile(localModFile.c_str(), modBank_))
		{
			modBank_.clear();
			return;
		}
	}

    initFixedModifications();
}

/**
 * Read in the mqpar file.
 */
void MaxQuantReader::initFixedModifications()
{
    filesystem::path tsvDir = filesystem::path(tsvName_).parent_path();
    
    string mqparFile = paramsPath_;

    if (mqparFile.empty())
    {
        // Check same folder
        filesystem::path tryPath = tsvDir / "mqpar.xml"; 
        Verbosity::comment(V_DETAIL, "Checking for mqpar file two folders up from msms.txt file.");
        if (!filesystem::exists(tryPath) || !filesystem::is_regular_file(tryPath))
        {
            // Not there, check two folders up from tsv file
            tryPath = tsvDir / ".." / ".." / "mqpar.xml";
            Verbosity::comment(V_DETAIL, "Checking for mqpar file in same folder as msms.txt file.");
            if (!filesystem::exists(tryPath) || !filesystem::is_regular_file(tryPath))
            {
                // Not there, check parent folder
                tryPath = tsvDir / ".." / "mqpar.xml";
                Verbosity::comment(V_DETAIL, "Checking for mqpar file in parent folder of msms.txt file.");
                if (!filesystem::exists(tryPath) || !filesystem::is_regular_file(tryPath))
                {
                    // Not there, error
                    Verbosity::error("mqpar.xml file not found. Please move it to the directory %s "
                        "with the msms.txt file.", filesystem::canonical(tsvDir).string().c_str());
                }
            }
        }
        mqparFile = tryPath.string();
    }
    else if (!filesystem::exists(mqparFile) || !filesystem::is_regular_file(mqparFile))
        Verbosity::error("specfied MaxQuant params file not found (%s)", mqparFile.c_str());

    Verbosity::comment(V_DETAIL, "Parsing mqpar file %s",
                       mqparFile.c_str());
    set<string> fixedMods;
    MaxQuantModReader modReader(mqparFile.c_str(), &fixedMods, &labelBank_);
    try
    {
        modReader.parse();
        Verbosity::comment(V_DETAIL, "Done parsing %s, %d fixed modifications found",
                           mqparFile.c_str(), fixedMods.size());
    }
    catch (BlibException& e)
    {
        Verbosity::error("Error parsing mqpar file: %s", e.what());
        return;
    }
    catch (...)
    {
        Verbosity::error("Unknown error while parsing mqpar file");
        return;
    }

    string mqparXml = pwiz::util::read_file_header(mqparFile, bfs::file_size(mqparFile));

    // force embedded spectra for TIMS-DDA because there's no way to map those msms.txt scan numbers to external spectra
    if (mqparXml.find("<lcmsRunType>TIMS-DDA</lcmsRunType>") != string::npos)
        preferEmbeddedSpectra_ = true;

    if (preferEmbeddedSpectra_) // user wants deconv or has no access to sources
    {
        setSpecFileName(tsvName_, // this is for BuildParser
            false);  // don't look for the file

        // point to self as spec reader
        delete specReader_;
        specReader_ = this;
    }
    else
    {
        // HACK: if mqpar analyzed WIFF file, use index lookup, else use scan number
        if (mqparXml.find(".wiff</string>") != string::npos || mqparXml.find(".wiff2</string>") != string::npos)
            lookUpBy_ = INDEX_ID;
        else
            lookUpBy_ = SCAN_NUM_ID;
    }

    // initialize fixed mod vectors for supported positions
    fixedModBank_[MaxQuantModification::ANYWHERE].clear();
    fixedModBank_[MaxQuantModification::ANY_N_TERM].clear();
    fixedModBank_[MaxQuantModification::ANY_C_TERM].clear();
    fixedModBank_[MaxQuantModification::NOT_N_TERM].clear();
    fixedModBank_[MaxQuantModification::NOT_C_TERM].clear();

    // add all fixed mods to fixedModBank_
    for (set<string>::iterator iter = fixedMods.begin();
         iter != fixedMods.end();
         ++iter)
    {
        // lookup in modbank
        const MaxQuantModification* lookup = MaxQuantModification::find(modBank_, *iter);
        if (lookup == NULL)
        {
            Verbosity::error("Unknown modification %s in mqpar file. Add a modifications.xml "
                             "file to the same directory as msms.txt which contains this "
                             "modification.", iter->c_str());
            return;
        }
        /*if (lookup->position != MaxQuantModification::ANYWHERE)
        {
            Verbosity::warn("Fixed mod '%s' will not be used (position is `not 'anywhere').",
                            iter->c_str());
        }*/

        Verbosity::debug("Adding fixed mod '%s' (position %d).", iter->c_str(), lookup->position);

        fixedModBank_[lookup->position].push_back(lookup);
    }

    // add all labels to labelBank_
    // iterate over raw files
    for (vector<MaxQuantLabels>::iterator iter = labelBank_.begin();
         iter != labelBank_.end();
         ++iter)
    {
        // iterate over labeling states
        for (vector<MaxQuantLabelingState>::iterator stateIter = iter->labelingStates.begin();
             stateIter != iter->labelingStates.end();
             ++stateIter)
        {
            vector<const MaxQuantModification*> mods;
            // iterate over individual mods
            for (vector<string>::const_iterator labelIter = stateIter->modsStrings.begin();
                 labelIter != stateIter->modsStrings.end();
                 ++labelIter)
            {
                if (!labelIter->empty())
                {
                    const MaxQuantModification* lookup = MaxQuantModification::find(modBank_, *labelIter);
                    if (lookup == NULL)
                    {
                        Verbosity::error("Unknown label %s in mqpar file. Add a modifications.xml "
                                         "file to the same directory as msms.txt which contains this "
                                         "modification.", labelIter->c_str());
                        return;
                    }
                    mods.push_back(lookup);
                }
            }
            iter->addMods(stateIter, mods);
        }
    }
}

/**
 * Read in the evidence file, if it exists.
 */
void MaxQuantReader::initEvidence()
{

    // Check same folder
    filesystem::path tryPath = tsvName_.substr(0, tsvName_.length() - 8) + "evidence.txt"; // e.g. c:\blah\testing.msms.txt => c:\blah\testing.evidence.txt
    Verbosity::comment(V_DETAIL, "Checking for ion mobility information in evidence.txt file in same folder as msms.txt file.");
    if (!filesystem::exists(tryPath) || !filesystem::is_regular_file(tryPath))
    {
        // Not there
        Verbosity::comment(V_DETAIL, "Did not find evidence.txt file in same folder as msms.txt file. No ion mobility values.");
        return;
    }
    string evidenceFile = tryPath.string();

    Verbosity::comment(V_DETAIL, "Parsing evidence file %s", evidenceFile.c_str());
    try
    {
        ifstream evidence(evidenceFile.c_str());
        string line;
        vector<string> columns;
        getline(evidence, line);
        boost::split(columns, line, boost::is_any_of("\t"));
        int col = 0;
        int colInvK0 = -1;
        int colCCS = -1;
        for (vector<string>::iterator it = columns.begin(); it != columns.end(); ++it)
        {
            if ((*it == "K0") || (*it == "1/K0")) // Column name changed to the more correct "1/K0" sometime between July and October 2019
            {
                colInvK0 = col;
            }
            else if (*it == "CCS")
            {
                colCCS = col;
            }
            col++;
        }
        if (colInvK0 < 0 && colCCS < 0)
        {
            Verbosity::comment(V_DETAIL, "Did not find any ion mobility data in evidence.txt file.");
            return; // This file doesn't have what we're interested in
        }

        while (evidence.good())
        {
            getline(evidence, line);
            if (line.size() == 0)
                break;
            boost::split(columns, line, boost::is_any_of("\t"));
            if (colInvK0 >= 0)
                inverseK0_.push_back(boost::lexical_cast<double>(columns[colInvK0]));
            /* Some versions of MaxQuant are known to emit incorrect CCS values. Until we figure out how to tell them apart, best to just ignore. (bspratt July 2019)
            if (colCCS >= 0)
                CCS_.push_back(boost::lexical_cast<double>(columns[colCCS]));
            */
        }
        Verbosity::comment(V_DETAIL, "Done parsing %s", evidenceFile.c_str());
    }
    catch (std::exception& e)
    {
        Verbosity::error("Error parsing evidence.txt file: %s", e.what());
        return;
    }
    catch (...)
    {
        Verbosity::error("Unknown error while parsing evidence.txt file");
        return;
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
    getFilenamesAndLineCount();
    
    vector<string> dirs, extensions;
    // look in parent and grandparent dirs in addition to cwd
    dirs.push_back("../");   
    dirs.push_back("../../"); 
    dirs.push_back("../../../"); 
    dirs.push_back("../../../../");
    
    // look in common open and vendor formats
    extensions.push_back(".mz5");
    extensions.push_back(".mzML");
#ifdef VENDOR_READERS
    extensions.push_back(".raw"); // Waters/Thermo
    extensions.push_back(".wiff"); // Sciex
    extensions.push_back(".d"); // Bruker/Agilent
    extensions.push_back(".lcd"); // Shimadzu
#endif
    extensions.push_back(".mzXML");
    extensions.push_back(".cms2");
    extensions.push_back(".ms2");
    extensions.push_back(".mgf");

    // check that files exist before starting the full PSM parsing
    vector<string> missingFiles;
    if (!preferEmbeddedSpectra_)
        for (const auto& filePsmListPair : fileMap_)
        {
            try
            {
                setSpecFileName(filePsmListPair.first.c_str(), extensions, dirs);
            }
            catch (BlibException& e)
            {
                if (!bal::contains(e.what(), "searching for spectrum file"))
                    throw;
                missingFiles.emplace_back(filePsmListPair.first);
            }
        }

    if (!missingFiles.empty())
        throw BlibException(false, "%s\n\nRun with the -E flag to allow MaxQuant to use deisotoped/deconvoluted embedded spectra", filesNotFoundMessage(missingFiles, extensions, dirs).c_str());

    Verbosity::debug("Collecting PSMs.");
    collectPsms();

    Verbosity::debug("Building tables.");
    // add psms by filename
    initSpecFileProgress(fileMap_.size());
    for (const auto& filePsmListPair : fileMap_)
    {
        psms_.assign(filePsmListPair.second.begin(), filePsmListPair.second.end());

        string specFileName = filePsmListPair.first;
        if (preferEmbeddedSpectra_) //use deconv
            setSpecFileName(filePsmListPair.first.c_str(), false);
        else
        {
            setSpecFileName(filePsmListPair.first.c_str(), extensions, dirs);
            specFileName = bfs::path(getSpecFileName()).filename().string();
        }

        buildTables(MAXQUANT_SCORE, specFileName, false);
    }
    
    return true;
}

vector<PSM_SCORE_TYPE> MaxQuantReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, MAXQUANT_SCORE);
}

bool MaxQuantReader::openFile()
{
    Verbosity::debug("Opening TSV file.");
    tsvFile_.open(tsvName_.c_str(), ios::binary);
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
            if (bal::iequals(*token, targetColumns_[i].name_))
            {
                targetColumns_[i].position_ = colNumber;
                break;
            }
        }
        colNumber++;
    }
    
    // check that all required columns were in the file
    for (int i = targetColumns_.size() - 1; i >= 0; i--)
    {
        if (targetColumns_[i].position_ < 0)
        {
            // check if it was optional
            set<string>::iterator j = optionalColumns_.find(targetColumns_[i].name_);
            if (j != optionalColumns_.end())
            {
                optionalColumns_.erase(j);
                targetColumns_.erase(targetColumns_.begin() + i);
            }
            else
            {
                throw BlibException(false, "Did not find required column '%s'.",
                                    targetColumns_[i].name_.c_str());
            }
        }
    }
    
    // sort by column number so they can be fetched in order
    sort(targetColumns_.begin(), targetColumns_.end());
}

/// Get filenames from all lines of msms.txt (for checking that they can be found before parsing PSMs) and get line count as well
void MaxQuantReader::getFilenamesAndLineCount()
{
    string line;
    bool parseSuccess = true;
    string errorMsg;

    streampos originalPos = tsvFile_.tellg();
    lineCount_ = 1;
    //ProgressIndicator progress(lineCount);
    vector<MaxQuantPSM*> dummyPsmList;
    string lastFilename;

    try
    {
        while (getline(tsvFile_, line))
        {
            ++lineCount_;
            auto lineBegin = line.begin();
            if (targetColumns_[0].position_ > 0)
            {
                auto rawFileItrRange = bal::find_nth(line, "\t", targetColumns_[0].position_+1);
                if (rawFileItrRange.empty())
                    throw BlibException(false, ("unable to find raw file column in getFilenamesAndLineCount for line:\n" + line).c_str());
                lineBegin = rawFileItrRange.begin() + 1;
            }
            LineParser lineParser(lineBegin, line.end(), separator_);
            string filename = *lineParser.begin();
            if (lastFilename.empty() || lastFilename != filename)
            {
                lastFilename = filename;
                fileMap_.insert(make_pair(filename, dummyPsmList));
            }
        }
        tsvFile_.clear();
        tsvFile_.seekg(originalPos);
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
    catch (...)
    {
        parseSuccess = false;
        errorMsg = "Unknown exception";
    }

    if (!parseSuccess)
    {
        throw BlibException(false, "%s caught at line %d",
            errorMsg.c_str(), lineCount_);
    }
}

/**
 * Read the tsv file and parse all psms.
 */
void MaxQuantReader::collectPsms()
{
    string line;
    bool parseSuccess = true;
    string errorMsg;

    ProgressIndicator progress(lineCount_);

    // read file
    while (!tsvFile_.eof())
    {
        getline(tsvFile_, line);
        ++lineNum_;

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
                if (lineColNumber++ == targetColumns_[colListIdx].position_)
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
        
        progress.increment();
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
    else if (entry.masses.empty() || entry.intensities.empty())
    {
        Verbosity::warn("Not saving PSM %d with no spectrum (line %d)",
                        entry.scanNumber, lineNum_);
        return;
    }

    // Set up new PSM
    curMaxQuantPSM_ = new MaxQuantPSM();

    curMaxQuantPSM_->specKey = entry.scanNumber;
    curMaxQuantPSM_->specIndex = entry.scanNumber; // for WIFF files, "scan number" is actually 0-based index when all spectra are enumerated in cycle-major order
    curMaxQuantPSM_->unmodSeq = entry.sequence;
    curMaxQuantPSM_->mz = entry.mz;
    curMaxQuantPSM_->charge = entry.charge;
    if (entry.evidenceID >= 0) // look for ion mobility info from evidence.txt file
    {
        if (inverseK0_.size() > entry.evidenceID)
        {
            curMaxQuantPSM_->ionMobility = inverseK0_[entry.evidenceID];
            if (curMaxQuantPSM_->ionMobility != 0)
            {
                curMaxQuantPSM_->ionMobilityType = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
            }
        }
        if (CCS_.size() > entry.evidenceID)
        {
            curMaxQuantPSM_->ccs = CCS_[entry.evidenceID];
        }
    }

    try
    {
        addModsToVector(curMaxQuantPSM_->mods, entry.modifications, entry.modifiedSequence, entry.sequence);
    }
    catch (const MaxQuantWrongSequenceException& e)
    {
        Verbosity::error(e.what());
        delete curMaxQuantPSM_;
        return;
    }
    addLabelModsToVector(curMaxQuantPSM_->mods, entry.rawFile, entry.sequence, entry.labelingState);
    curMaxQuantPSM_->retentionTime = entry.retentionTime;
    curMaxQuantPSM_->score = entry.pep;
    addDoublesToVector(curMaxQuantPSM_->mzs, entry.masses);
    addDoublesToVector(curMaxQuantPSM_->intensities, entry.intensities);

    // Save PSM
        fileMap_[entry.rawFile].push_back(curMaxQuantPSM_);
    }

/**
 * Take a string of semicolon separated doubles and add them to the vector.
 */
void MaxQuantReader::addDoublesToVector(vector<double>& v, const string& valueList)
{
    vector<string> doubles;
    bal::split(doubles, valueList, bal::is_any_of(";"));

    try
    {
        for (const string& value : doubles)
            v.emplace_back(lexical_cast<double>(value));
    }
    catch (bad_lexical_cast&)
    {
        Verbosity::error("Could not cast \"%s\" to doubles", valueList.c_str());
    }
}

void MaxQuantReader::addFixedMods(vector<SeqMod>& v, const string& sequence, const map< MaxQuantModification::MAXQUANT_MOD_POSITION, vector<const MaxQuantModification*> >& modsByPosition)
{

    // get fixed modifications by position
    const vector<const MaxQuantModification*>& modsAnywhere = modsByPosition.find(MaxQuantModification::ANYWHERE)->second;
    const vector<const MaxQuantModification*>& modsAnyNTerm = modsByPosition.find(MaxQuantModification::ANY_N_TERM)->second;
    const vector<const MaxQuantModification*>& modsAnyCTerm = modsByPosition.find(MaxQuantModification::ANY_C_TERM)->second;
    const vector<const MaxQuantModification*>& modsNotNTerm = modsByPosition.find(MaxQuantModification::NOT_N_TERM)->second;
    const vector<const MaxQuantModification*>& modsNotCTerm = modsByPosition.find(MaxQuantModification::NOT_C_TERM)->second;

    /* Do not use since we don't know where the peptide is in relation to the Protein N-term/C-term
    vector<const MaxQuantModification*> modsProteinNTerm;
    vector<const MaxQuantModification*> modsProteinCTerm;
    */

    for (const auto& mod : modsAnyNTerm) { if (mod->sites.empty()) v.insert(v.begin(), SeqMod(1, mod->massDelta)); }

    // iterate over sequence
    for (int i = 0; i < (int)sequence.length(); i++)
    {
        boost::range::insert(v, v.end(), getFixedMods(sequence[i], i + 1, modsAnywhere));
        if (i == 0)
            boost::range::insert(v, v.end(), getFixedMods(sequence[i], i + 1, modsAnyNTerm));
        else if (i + 1 == sequence.length())
            boost::range::insert(v, v.end(), getFixedMods(sequence[i], i + 1, modsAnyCTerm));

        if (i > 0)
            boost::range::insert(v, v.end(), getFixedMods(sequence[i], i + 1, modsNotNTerm));
        if (i + 1 < sequence.length())
            boost::range::insert(v, v.end(), getFixedMods(sequence[i], i + 1, modsNotCTerm));
    }

    for (const auto& mod : modsAnyCTerm) { if (mod->sites.empty()) v.emplace_back(sequence.length(), mod->massDelta); }
}

/**
 * Adds a SeqMod for each modification in the given modified sequence string of the form
 * "_I(ab)AMASEQ_". The modifications string contains the (comma separated) full names of
 * the modifications; the string "Unmodified" can mean no variable modifications are present.
 */
void MaxQuantReader::addModsToVector(vector<SeqMod>& v, const string& modifications, string modSequence, const string& sequence)
{
    bal::replace_all(modSequence, "pS", "S(ph)");
    bal::replace_all(modSequence, "pT", "T(ph)");
    bal::replace_all(modSequence, "pY", "Y(ph)");

    // split modifications whole names
    vector<string> modNames;
    if (!bal::iequals(modifications, "Unmodified"))
    {
        bal::split(modNames, modifications, bal::is_any_of(","));
    }

    // remove underscore from beginning and end if they exist
    if (modSequence[0] == '_')
    {
        modSequence = modSequence.substr(1);
    }
    int sequenceLength = modSequence.length();
    if (modSequence[sequenceLength - 1] == '_')
    {
        modSequence = modSequence.substr(0, sequenceLength - 1);
        --sequenceLength;
    }
    // or before the final modification definition, which MaxQuant uses to destinguish
    // between C-terminal modifications and modifications on the C-terminal amino acid
    if (modSequence[sequenceLength - 1] == ')')
    {
        size_t openPos = modSequence.find_last_of('(');
        if (openPos != string::npos && openPos > 0 && modSequence[openPos - 1] == '_')
        {
            modSequence = modSequence.erase(openPos - 1, 1);
            --sequenceLength;
        }
    }

    const vector<const MaxQuantModification*>& modsAnyCTerm = fixedModBank_.find(MaxQuantModification::ANY_C_TERM)->second;

    // iterate over sequence
    int modsFound = 0;
    int modsTotalLength = 0;
    SeqMod seqMod;
    for (int i = 0; i < sequenceLength; i++)
    {
        switch (modSequence[i])
        {
        case '(':
            {
                ++modsFound;
                int posOpenParen = i;
                // which mod is it?
                seqMod = searchForMod(modNames, modSequence, i);
                modsTotalLength += i + 1 - posOpenParen;
                // add the mod unless it's in the modsAnyCTerm list
                if (find_if(modsAnyCTerm.begin(), modsAnyCTerm.end(), [&](const MaxQuantModification* maxQuantMod) { return maxQuantMod->massDelta == seqMod.deltaMass; }) == modsAnyCTerm.end())
                    v.push_back(seqMod);
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

    addFixedMods(v, sequence, fixedModBank_);

    if (modsFound < (int)modNames.size())
    {
        Verbosity::warn("Found %d modifications but expected at least %d in sequence %s (%d)",
                        modsFound, modNames.size(), modSequence.c_str(), lineNum_);
    }
}

/**
 * Adds a SeqMod for each labeled AA in the given unmodified sequence string.
 */
void MaxQuantReader::addLabelModsToVector(vector<SeqMod>& v, const string& rawFile,
                                          const string& sequence, int labelingState)
{
    const MaxQuantLabels* labels = MaxQuantLabels::findLabels(labelBank_, rawFile);
    if (labelingState < 0)
    {
        if (labels == NULL || labels->labelingStates.size() != 1 ||
            optionalColumns_.find("Labeling State") != optionalColumns_.end())
        {
            return;
        }
        labelingState = 0; // if labeling state column is missing and there is only 1, assume it is correct
    }

    // get fixed modifications by position
    if (labels == NULL)
    {
        throw BlibException(false, "Required raw file '%s' was not found in mqpar file.", rawFile.c_str());
    }
    else if ((int)labels->labelingStates.size() <= labelingState)
    {
        throw BlibException(false, "Labeling state was %d but mqpar file only had %d labeling states for "
                                   "raw file '%s'.",
                                   labelingState, labels->labelingStates.size(), rawFile.c_str());
    }

    addFixedMods(v, sequence, labels->labelingStates[labelingState].modsByPosition);
}

/**
 * Given a vector of modification names, a modified sequence, and the position of
 * the opening parentheses for the modification in the sequence, attempt to
 * look up which modification it is and return a SeqMod.
 */
SeqMod MaxQuantReader::searchForMod(vector<string>& modNames, const string& modSequence, int& posOpenParen) {
    // get mod abbreviation
    int nestDepth = 1;
    size_t posFirstParen = posOpenParen;
    size_t posNextParen = posOpenParen;
    while (nestDepth > 0)
    {
        posNextParen = modSequence.find_first_of("()", posNextParen + 1);
        if (posNextParen == string::npos) {
            throw BlibException(false, "Closing parentheses expected but not found in sequence %s "
                "(line %d)", modSequence.c_str(), lineNum_);
        }
        nestDepth += modSequence[posNextParen] == ')' ? -1 : 1;
    }
    size_t modStart = posOpenParen + 1;
    string modAbbreviation = modSequence.substr(modStart, posNextParen - modStart);

    posOpenParen = posNextParen; // advance index to the closing parenthesis

    // search list of mod names using abbreviation
    const MaxQuantModification* lookup = NULL;
    for (vector<string>::const_iterator i = modNames.begin(); i != modNames.end(); ++i) {
        if (bal::iequals(modAbbreviation, i->substr(0, modAbbreviation.length()))) {
            lookup = MaxQuantModification::find(modBank_, *i);
            if (lookup != NULL)
                return SeqMod(getModPosition(modSequence, posFirstParen), lookup->massDelta);
        }
    }

    /* Retry loop but try stripping numbers off the beginning of modification names -
     * Modification string might be "2 Oxidation (M) to indicate
     * that there are 2 "Oxidation (M)", not that the name of the
     * mod is "2 Oxidation (M)" */
    for (vector<string>::const_iterator i = modNames.begin(); i != modNames.end(); ++i) {
        // loop until we find a non numeric character or the end of the string
        unsigned int newStart = 0;
        while (newStart < i->length() && isdigit((*i)[newStart])) {
            ++newStart;
        }
        // Make sure we found a space and that there is at least 1 char after it
        if (newStart + 1 < i->length() && (*i)[newStart] == ' ' &&
            bal::iequals(modAbbreviation, i->substr(++newStart, modAbbreviation.length())) &&
            (lookup = MaxQuantModification::find(modBank_, i->substr(newStart))) != NULL) {
            return SeqMod(getModPosition(modSequence, posFirstParen), lookup->massDelta);
        }
    }

    // This may occur due to a bug in MaxQuant where the wrong sequence
    // (2nd best) is reported instead of the best one
    throw MaxQuantWrongSequenceException(modAbbreviation, modSequence, lineNum_);
}

int MaxQuantReader::getModPosition(const string& modSeq, int posOpenParen) {
    int modPosition = 0;
    int inMod = 0;
    for (int i = 0; i < posOpenParen; i++) {
        if (modSeq[i] == '(') {
            ++inMod;
        } else if (modSeq[i] == ')') {
            --inMod;
        } else if (inMod == 0) {
            modPosition++;
        }
    }
    return max(1, modPosition);
}

/**
 * Given an amino acid, return a vector of all SeqMods that should apply to it.
 */
vector<SeqMod> MaxQuantReader::getFixedMods(char aa, int aaPosition,
                                            const vector<const MaxQuantModification*>& mods)
{
    vector<SeqMod> modsApplied;

    for (vector<const MaxQuantModification*>::const_iterator iter = mods.begin();
         iter != mods.end();
         ++iter)
    {
        if ((*iter)->sites.find(aa) != (*iter)->sites.end())
        {
            SeqMod newMod(aaPosition, (*iter)->massDelta);
            modsApplied.push_back(newMod);
        }
    }

    return modsApplied;
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
    returnData.ionMobility = ((MaxQuantPSM*)psm)->ionMobility;
    returnData.ionMobilityType = ((MaxQuantPSM*)psm)->ionMobilityType;
    returnData.ccs = ((MaxQuantPSM*)psm)->ccs;

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
