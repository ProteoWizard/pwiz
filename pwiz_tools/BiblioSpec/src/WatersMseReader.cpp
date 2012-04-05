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

/**
 * The WatersMseReader parses the scan number, charge, sequence and spectrum
 * file name from the csv file and stores each record.  Records are
 * sorted and grouped by file.  Spectra are then retrieved from the
 * spectrum files. 
 */

#include "WatersMseReader.h"
#include "boost/algorithm/string.hpp"

using namespace std;

namespace BiblioSpec {

const char* WatersMseReader::modNames_[] = {
    "Acetyl",
    "Amidation",
    "Biotin",
    "Carbamidomethyl",
    "Carbamyl",
    "Carboxymethyl",
    "Deamidation",
    "Dehydration",
    "Farnesyl",
    "Flavin-adenine",
    "Formyl",
    "Gamma-carboxyglutamic",
    "Geranyl-geranyl",
    "Glycation",
    "Hydroxyl",
    "Lipoyl",
    "Methyl",
    "Myristoyl",
    "NIPCAM",
    "O-GlcNAc",
    "Oxidation",
    "Palmitoyl",
    "Phosphopantetheine",
    "Phosphoryl",
    "Propionamide",
    "Pyridoxal",
    "Pyrrolidone",
    "S-pyridylethyl",
    "SMA",
    "1H",
    "2H",
    "12C",
    "13C",
    "Isobaric 114",
    "Isobaric 115",
    "Isobaric 116",
    "Isobaric 117",
    "O18",
    "13C",
    "13C N15",
    "N-Glycosylation",
    "O-Glycosylation"
};
// placeholder value for glycosylation
const double GLYCOL_MASS = numeric_limits<double>::max();
double WatersMseReader::modMasses_[] = {
    42.010565,
    -0.984016,
    226.077598,
    57.021464,
    43.005814,
    58.005479,
    0.984016,
    -18.010565,
    204.187801,
    783.141486,
    27.994915,
    43.98980,
    272.250401 ,
    162.052824,
    15.994915,
    188.032956,
    14.015650,
    210.198366,
    99.068414,
    203.0794,
    15.994915,
    238.229666,
    340.085794,
    79.966331,
    71.037114,
    229.014009,
    -17.0265,
    105.057849,
    127.063329,
    442.225,
    450.2752,
    227.1270,
    236.1572,
    144.105863,
    144.059563,
    144.102063,
    144.102063,
    4.008491,
    6.020129,
    10.008269,
    GLYCOL_MASS,
    GLYCOL_MASS
};
int WatersMseReader::numModNames_ = sizeof(modNames_) / sizeof(char*);


WatersMseReader::WatersMseReader(BlibBuilder& maker,
                                 const char* csvname,
                                 const ProgressIndicator* parentProgress)
  : BuildParser(maker, csvname, parentProgress), 
    csvName_(csvname), scoreThreshold_(getScoreThreshold(MSE)), lineNum_(1), 
    curMsePSM_(NULL), numColumns_(0)
{
    Verbosity::debug("Creating WatersMseReader.");
    
    setSpecFileName(csvname, // this is for BuildParser
                    false);  // don't look for the file
    
    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    // define which columns are pulled from the file
    initTargetColumns();
}
    
WatersMseReader::~WatersMseReader()
{
    specReader_ = NULL; // so parent class doesn't try to delete itself
    if( csvFile_.is_open() ){
        csvFile_.close();
    }
}

/**
 * Define the columns we are looking for in the file.
 */
void WatersMseReader::initTargetColumns(){
  targetColumns_.push_back(wColumnTranslator("peptide.seq", -1, 
                                            LineEntry::insertSequence));
  targetColumns_.push_back(wColumnTranslator("peptide.modification", -1,
                                            LineEntry::insertModification));
  targetColumns_.push_back(wColumnTranslator("peptide.score", -1,
                                            LineEntry::insertScore));
  targetColumns_.push_back(wColumnTranslator("precursor.retT", -1,
                                            LineEntry::insertRetentionTime));
  targetColumns_.push_back(wColumnTranslator("precursor.z", -1,
                                            LineEntry::insertPrecursorZ));
  targetColumns_.push_back(wColumnTranslator("precursor.mz", -1,
                                            LineEntry::insertPrecursorMz));
  targetColumns_.push_back(wColumnTranslator("product.m_z", -1,
                                            LineEntry::insertFragmentMz));
  targetColumns_.push_back(wColumnTranslator("product.inten", -1,
                                         LineEntry::insertFragmentIntensity));
  targetColumns_.push_back(wColumnTranslator("peptide.Pass", -1,
                                            LineEntry::insertPass));

  optionalColumns_.push_back(wColumnTranslator("precursor.mhp", -1,
                                              LineEntry::insertPrecursorMass));
  optionalColumns_.push_back(wColumnTranslator("minMass", -1,
                                              LineEntry::insertMinMass));


  numColumns_ = targetColumns_.size();

}

/**
 * Open the file, read header, read remaining file, build tables.
 */
bool WatersMseReader::parseFile(){
    Verbosity::debug("Parsing File.");
    if( ! openFile() ){
        return false;
    }
    // read header in first line
    string line;
    getline(csvFile_, line);
    parseHeader(line);
    
    Verbosity::debug("Collecting Psms.");
    collectPsms();
  
    // copy the unique psms to the BuildParser vector
    psms_.assign(uniquePSMs_.begin(), uniquePSMs_.end());

    buildTables(WATERS_MSE_PEPTIDE_SCORE);
    
    return true;
}

/**
 * Try to open .csv file and read the header line.  Return false if
 * the file cannot be opened or if the header is incorrect.
 */
bool WatersMseReader::openFile(){

    Verbosity::debug("Opening csv File.");
    csvFile_.open(csvName_.c_str());
    if( !csvFile_.is_open() ){
        throw BlibException(true, "Could not open csv file '%s'.", 
                            csvName_.c_str());
    }
    
    return true;
}

/**
 * Read the given line (the first in the csv file) and locate the
 * position of each of the targeted columns.  Sort the target columns
 * by position.
 */
void WatersMseReader::parseHeader(string& line){
    CsvTokenizer lineParser(line);
    CsvTokenIterator token = lineParser.begin();
    int colNumber = 0;
    size_t numColumns = targetColumns_.size();
    
    // for each token in the line
    for(; token != lineParser.end(); ++token){
        // check each column for a match
        for(size_t i = 0; i < numColumns; i++){
            if( *token == targetColumns_[i].name_ ){
                targetColumns_[i].position_ = colNumber;
            }
        }
        // check each optional column
        for(size_t i = 0; i < optionalColumns_.size(); i++){
            if( *token == optionalColumns_[i].name_ ){
                optionalColumns_[i].position_ = colNumber;
            }
        }
        colNumber++;
    }
    
    // check that all required columns were in the file
    for(size_t i = 0; i < targetColumns_.size(); i++){
        if( targetColumns_[i].position_ == -1 ){
            throw BlibException(false, "Did not find required column '%s'.", 
                                targetColumns_[i].name_.c_str());
        }
    }

    // if both optional columns were found, add them to the targets
    if( optionalColumns_[0].position_ != -1 &&
        optionalColumns_[1].position_ != -1 ){
        targetColumns_.insert(targetColumns_.end()-1, 
                              optionalColumns_.begin(), optionalColumns_.end());
    }
    
    // sort by column number so they can be fetched in order
    sort(targetColumns_.begin(), targetColumns_.end());
}

/**
 * Read the csv file and parse all psms.  Store them both in a std::set
 * so that duplicates are not saved.  Use line number as specKey.
 */
void WatersMseReader::collectPsms(){
    
    // read first non-header line
    string line;
    getline(csvFile_, line);
    lineNum_++;
    bool parseSuccess = true;
    string errorMsg;

    // read remainder of file
    while( ! csvFile_.eof() ){

        size_t colListIdx = 0;  // go through all target columns
        int lineColNumber = 0;  // compare to all file columns
        
        LineEntry entry; // store each line
        try{
            CsvTokenizer lineParser(line); // create object for parsing line
            CsvTokenIterator token = lineParser.begin();// iterator for parser
            for(; token != lineParser.end(); ++token){
                if( lineColNumber == targetColumns_[colListIdx].position_ ){
                    
                    // insert the value in the proper field
                    targetColumns_[colListIdx].inserter(entry, (*token));
                    colListIdx++; // next target column
                    if( colListIdx == targetColumns_.size() )
                        break;
                }
                lineColNumber++;  // next token in the line
            }
        } catch (BlibException& e) {
            parseSuccess = false;
            errorMsg = e.what();
        } catch (std::exception& e) {
            parseSuccess = false;
            errorMsg = e.what();
        } catch (string& s) {
            parseSuccess = false;
            errorMsg = s;
        } catch (...) {
            errorMsg = "Unknown exception";
        }
        
        if(!parseSuccess){
            BlibException e(false, "%s caught at line %d, column %d", 
                            errorMsg.c_str(), lineNum_, lineColNumber + 1);
            throw e;
        }
        // store this line's information in the curPSM
        storeLine(entry);
        
        getline(csvFile_, line);
        lineNum_++;
    } // next line

    // store the last one
    insertCurPSM();
}
 
/**
 * Evaluate each line.  If the peak has 0 m/z and 0 intensity, throw
 * out the line.  If it is part of the same PSM as the previous
 * line, add the peaks.  If it is a new PSM, store the current and
 * start a new PSM.
 */
void WatersMseReader::storeLine(LineEntry& entry){
    if( curMsePSM_ == NULL ){
        curMsePSM_ = new MsePSM();
        curMsePSM_->specKey = lineNum_;
    }

    // does this line have a valid peak?
    if( entry.fragmentMz == 0 || entry.fragmentIntensity == 0 ){
        Verbosity::comment(V_DETAIL, "Throwing out line %d with no peak", 
                           lineNum_);
        return;
    }

    // is this entry the same as the curPSM?
    if( curMsePSM_->unmodSeq == entry.sequence &&
        curMsePSM_->charge == entry.precursorZ &&
        curMsePSM_->mz == entry.precursorMz ){
        // yes, same PSM: just add peaks
        curMsePSM_->mzs.push_back(entry.fragmentMz);
        curMsePSM_->intensities.push_back(entry.fragmentIntensity);
        return;
    }

    // else, entry is a new PSM. Save current
    insertCurPSM();

    // init the new PSM with all of the current entry values
    // must be a Pass1 or Pass2 peptide
    if( entry.pass == "Pass1" || entry.pass == "Pass2" ){

        curMsePSM_->charge = entry.precursorZ;
        curMsePSM_->unmodSeq = entry.sequence;
        curMsePSM_->mz = entry.precursorMz;
        curMsePSM_->score = entry.score;
        curMsePSM_->retentionTime = entry.retentionTime;
        parseModString(entry, curMsePSM_);
        curMsePSM_->mzs.push_back(entry.fragmentMz);
        curMsePSM_->intensities.push_back(entry.fragmentIntensity);

    } else {
        curMsePSM_->valid = false;
    }
}

/**
 * Evaluate the currently held PSM and insert it into the set of PSMs
 * to add to the library.  Clear the current PSM for the next entry.
 */
void WatersMseReader::insertCurPSM(){
    if( !curMsePSM_->unmodSeq.empty() && curMsePSM_->valid 
        && curMsePSM_->score > scoreThreshold_ ){
        pair<set<MsePSM*,compMsePsm>::iterator, bool> inserted = 
            uniquePSMs_.insert(curMsePSM_);  
        if( ! inserted.second ){ // same psm already there
            curMsePSM_->clear();
        } else {
            curMsePSM_ = new MsePSM();
        }
    } else {
        if( !curMsePSM_->valid ){
            Verbosity::comment(V_DETAIL, "Not inserting invalid psm %d.",
                               curMsePSM_->specKey);
        } else if( curMsePSM_->score <= scoreThreshold_ ){
            Verbosity::comment(V_DETAIL, "Not inserting psm %d with score %f",
                               curMsePSM_->specKey, curMsePSM_->score);
        }
        curMsePSM_->clear();
    }
    curMsePSM_->specKey = lineNum_;
}


/**
 * Create a SeqMod for each modification in the given modification
 * string of the form <name>(<pos>)[;<name>(<pos>)]. Add each to the
 * given PSM if there are no errors.  The string "None" can mean no
 * modifications are present.
 * \return False if any of the mods are not valid (e.g. have no position).
 */
void WatersMseReader::parseModString(LineEntry& entry, 
                                     MsePSM* psm){
    string& modStr = entry.modification;
    if( modStr.empty() || boost::iequals(modStr, "None")){
        return;
    }

    Verbosity::debug("Parsing mod string '%s'.", modStr.c_str());
    
    size_t startParsing = 0;
    
    while(startParsing != string::npos){
        SeqMod mod;
        // search for name and get corresponding mass
        for(int i=0; i < numModNames_; i++){
            const char* name = modNames_[i];
            if( modStr.compare(startParsing, strlen(name), name) == 0 ){
                mod.deltaMass = modMasses_[i];
                break;
            }
        }
        if( mod.deltaMass == 0 ){
            throw BlibException(false, "The modification '%s' on line %d "
                                "is not recognized.", modStr.c_str(), lineNum_);
        }
        
        if( mod.deltaMass == GLYCOL_MASS ){
            mod.deltaMass = entry.precursorMass - entry.minMass;
        }
        
        // find the position in the sequence
        size_t openBrace = modStr.find("(", startParsing);
        mod.position = atoi(modStr.c_str() + openBrace +1);
        
        // check that there was a valid position
        if( mod.position == 0 ){
            psm->valid = false;
            psm->mods.clear();
            return;
        } else {
            psm->mods.push_back(mod);
        }
        
        startParsing = modStr.find(';', startParsing);
        // move past the ;
        if( startParsing != string::npos ){
            startParsing++;
        }
    } // next mod in string
    return;
}

void WatersMseReader::openFile(const char*, bool){}
void WatersMseReader::setIdType(SPEC_ID_TYPE){}

/**
 * Overrides the SpecFileReader implementation.  Assumes the PSM is a
 * MsePSM and copies the additional data from it to the SpecData.
 */
bool WatersMseReader::getSpectrum(PSM* psm,
                                  SPEC_ID_TYPE findBy,
                                  SpecData& returnData,
                                  bool getPeaks){    

    returnData.id = ((MsePSM*)psm)->specKey;
    returnData.retentionTime = ((MsePSM*)psm)->retentionTime;
    returnData.mz = ((MsePSM*)psm)->mz;
    returnData.numPeaks = ((MsePSM*)psm)->mzs.size();

    if( getPeaks ){
        returnData.mzs = new double[returnData.numPeaks];
        returnData.intensities = new float[returnData.numPeaks];
        for(int i=0; i < returnData.numPeaks; i++){
            returnData.mzs[i] = ((MsePSM*)psm)->mzs[i]; 
            returnData.intensities[i] = (float)((MsePSM*)psm)->intensities[i];  
        }
    } else {
        returnData.mzs = NULL;
        returnData.intensities = NULL;
    }
    return true;
}

  bool WatersMseReader::getSpectrum(int, SpecData&, SPEC_ID_TYPE, bool){ return false; }
  bool WatersMseReader::getSpectrum(std::string, SpecData&, bool){ return false; }
  bool WatersMseReader::getNextSpectrum(SpecData&, bool){ return false; }
} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

