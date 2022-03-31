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

#pragma once

#include "BuildParser.h"
#include "MaxQuantModReader.h"
#include <algorithm>
#include <boost/tokenizer.hpp>
#include <iterator>
#include <sstream>
#include <stdexcept>

using boost::tokenizer;
using boost::escaped_list_separator;


namespace BiblioSpec {

/**
 * Extends the standard PSM with additional fields for the spectrum.
 */
struct MaxQuantPSM : PSM
{
    double mz;
    double retentionTime;
    vector<double> mzs;
    vector<double> intensities;

    MaxQuantPSM() : PSM(), mz(0), retentionTime(0) {}

    void clear()
    {
        PSM::clear();
        mz = 0;
        retentionTime = 0;
        vector<double>().swap(mzs);
        vector<double>().swap(intensities);
    }
};

/**
 * Holds all the values from one line of the MaxQuant msms.txt file.
 */
class MaxQuantLine
{
public:
    string rawFile;
    int scanNumber;
    string sequence;
    double mz;
    int charge;
    string modifications;
    string modifiedSequence;
    double retentionTime;
    double pep;
    double score;
    int labelingState;
    int evidenceID; // index into evidence.txt file for ion mobility info
    string masses;
    string intensities;

    MaxQuantLine() : scanNumber(0), mz(0), charge(0), retentionTime(0), score(0), labelingState(-1), evidenceID(-1) {}

    static void insertRawFile(MaxQuantLine& le, const string& value)
    {
        le.rawFile = value;
    }
    static void insertScanNumber(MaxQuantLine& le, const string& value)
    {
        le.scanNumber = (value.empty()) ? 0 : lexical_cast<int>(value);
    }
    static void insertSequence(MaxQuantLine& le, const string& value)
    {
        le.sequence = value;
    }
    static void insertMz(MaxQuantLine& le, const string& value)
    {
        le.mz = (value.empty()) ? 0 : lexical_cast<double>(value);
    }
    static void insertCharge(MaxQuantLine& le, const string& value)
    {
        le.charge = (value.empty()) ? 0 : lexical_cast<int>(value);
    }
    static void insertModifications(MaxQuantLine& le, const string& value)
    {
        le.modifications = value;
    }
    static void insertModifiedSequence(MaxQuantLine& le, const string& value)
    {
        le.modifiedSequence = value;
    }
    static void insertRetentionTime(MaxQuantLine& le, const string& value)
    {
        le.retentionTime = (value.empty()) ? 0 : lexical_cast<double>(value);
    }
    static void insertPep(MaxQuantLine& le, const string& value)
    {
        le.pep = (value.empty()) ? 0 : lexical_cast<double>(value);
    }
    static void insertScore(MaxQuantLine& le, const string& value)
    {
        le.score = (value.empty()) ? 0 : lexical_cast<double>(value);
    }
    static void insertLabelingState(MaxQuantLine& le, const string& value)
    {
        le.labelingState = (value.empty()) ? -1 : lexical_cast<int>(value);
    }
    static void insertEvidenceID(MaxQuantLine& le, const string& value)
    {
        le.evidenceID = (value.empty()) ? -1 : lexical_cast<int>(value);
    }
    static void insertMasses(MaxQuantLine& le, const string& value)
    {
        le.masses = value;
    }
    static void insertIntensities(MaxQuantLine& le, const string& value)
    {
        le.intensities = value;
    }
};
/**
 * Holds information about how the columns appear in the MaxQuant
 * msms.txt file. The column name is how it appears in the first row of
 * the file. The column position is the zero-based index of the
 * column in the file. The inserter is a function pointer that can be
 * called to insert this column's value into a MaxQuantLine object. This
 * allows constant-time translation between the column position and
 * the specific information in that column.
 */
class MaxQuantColumnTranslator
{
public:
    string name_;
    int position_;
    void (*inserter)(MaxQuantLine& le, const string& value); 

    MaxQuantColumnTranslator(const char* name, 
                             int pos, 
                             void (*fun)(MaxQuantLine&, const string&))
        : name_(name), position_(pos), inserter(fun) { };

    friend bool operator< (const MaxQuantColumnTranslator& left, 
                           const MaxQuantColumnTranslator& right)
    {
        return (left.position_ < right.position_);
    }
};

/**
 * Class for parsing msms.txt files from MaxQuant results.
 */
class MaxQuantReader : public BuildParser, public SpecFileReader
{
    typedef tokenizer< escaped_list_separator<char> > LineParser;

public:
    MaxQuantReader(BlibBuilder& maker,
                    const char* sslname,
                    const ProgressIndicator* parentProgress);
    ~MaxQuantReader();
    
    bool parseFile();
    vector<PSM_SCORE_TYPE> getScoreTypes();
    // these inherited from SpecFileReader
    virtual void openFile(const char*, bool);
    virtual void setIdType(SPEC_ID_TYPE);
    virtual bool getSpectrum(PSM* psm,
                             SPEC_ID_TYPE findBy,
                             SpecData& returnData,
                             bool getPeaks = true);    
    virtual bool getSpectrum(int, SpecData&, SPEC_ID_TYPE, bool);
    virtual bool getSpectrum(std::string, SpecData&, bool);
    virtual bool getNextSpectrum(SpecData&, bool);
    
private:
    string tsvName_;
    ifstream tsvFile_;
    string modsPath_;
    string paramsPath_;
    double scoreThreshold_;
    int lineNum_;
    int lineCount_;
    map< string, vector<MaxQuantPSM*> > fileMap_; // store psms by filename
    MaxQuantPSM* curMaxQuantPSM_; // use this instead of curPSM_
    vector<MaxQuantColumnTranslator> targetColumns_; // columns to extract
    set<string> optionalColumns_; // columns that are optional
    map<string, MaxQuantModification> modBank_;   // full mod name -> delta mass
    map< MaxQuantModification::MAXQUANT_MOD_POSITION, vector<const MaxQuantModification*> > fixedModBank_;
    vector<MaxQuantLabels> labelBank_;
    vector<double> inverseK0_; // optionally parsed from evidence.txt
    vector<double> CCS_; // optionally parsed from evidence.txt

    void initTargetColumns();
    void initModifications();
    void initFixedModifications();
    bool openFile();
    void parseHeader(std::string& line);
    void getFilenamesAndLineCount();
    void collectPsms();
    void storeLine(MaxQuantLine& entry);
    void addDoublesToVector(vector<double>& v, const string& valueList);
    void addModsToVector(vector<SeqMod>& v, const string& modifications, string modSequence, const string& unmodSequence);
    void addLabelModsToVector(vector<SeqMod>& v, const string& rawFile, const string& sequence, int labelingState);
    SeqMod searchForMod(vector<string>& modNames, const string& modSequence, int& posOpenParen);
    static int getModPosition(const string& modSeq, int posOpenParen);
    void addFixedMods(vector<SeqMod>& v, const string& seq, const map< MaxQuantModification::MAXQUANT_MOD_POSITION, vector<const MaxQuantModification*> >& modsByPosition);
    vector<SeqMod> getFixedMods(char aa, int aaPosition, const vector<const MaxQuantModification*>& mods);
    void initEvidence();  // optionally parse ion mobility info from evidence.txt

    const escaped_list_separator<char> separator_;
};

class MaxQuantWrongSequenceException : public std::exception {
public:
    MaxQuantWrongSequenceException(const std::string& mod, const std::string& seq, int line) {
        std::stringstream ss;
        ss << "No matching mod for " << mod << " in sequence " << seq << " (line " << line << "). Make sure you have provided the correct modifications[.local].xml file.";
        message_ = ss.str();
    }
    virtual ~MaxQuantWrongSequenceException() throw () {}
    virtual const char* what() const throw () {
        return message_.c_str();
    }
private:
    std::string message_;
};

} // namespace
