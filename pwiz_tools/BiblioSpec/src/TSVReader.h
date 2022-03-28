//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2018 University of Washington - Seattle, WA 98195
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
#include "UnimodParser.h"
#include "AminoAcidMasses.h"
#include <boost/tokenizer.hpp>
using boost::tokenizer;
using boost::escaped_list_separator;

namespace BiblioSpec {

// Extends the standard PSM with additional fields for the spectrum
struct TSVPSM : PSM {
    double rt;
    double mz;
    double leftWidth;
    double rightWidth;
    double ce;
    vector<double> mzs;
    vector<double> intensities;

    TSVPSM() : PSM(), rt(0), mz(0), leftWidth(0), rightWidth(0), ce(0) {}

    void clear() {
        PSM::clear();
        rt = 0;
        mz = 0;
        leftWidth = 0;
        rightWidth = 0;
        ce = 0;
        vector<double>().swap(mzs);
        vector<double>().swap(intensities);
    }
};

// Holds values from one line of the TSV file
class TSVLine {
public:
    std::string filename;
    double rt;
    std::string sequence;
    int charge;
    double mz;
    std::string proteinName;
    bool decoy;
    double leftWidth;
    double rightWidth;
    double ce;
    double ionMobility;
    std::string peakArea;
    std::string fragmentAnnotation;
    int fragmentSeriesNumber;
    double score;

    TSVLine() : rt(0), charge(0), mz(0), decoy(false), leftWidth(0), rightWidth(0), ce(0), ionMobility(0), score(0) {}

    static void insertFilename(TSVLine& line, const std::string& value) {
        line.filename = value;
    }
    static void insertRt(TSVLine& line, const std::string& value) {
        line.rt = value.empty() ? 0 : lexical_cast<double>(value) / 60;
    }
    static void insertSequence(TSVLine& line, const std::string& value) {
        line.sequence = value;
    }
    static void insertCharge(TSVLine& line, const std::string& value) {
        line.charge = value.empty() ? 0 : lexical_cast<int>(value);
    }
    static void insertMz(TSVLine& line, const std::string& value) {
        line.mz = value.empty() ? 0 : lexical_cast<double>(value);
    }
    static void insertProteinName(TSVLine& line, const std::string& value) {
        line.proteinName = value;
    }
    static void insertDecoy(TSVLine& line, const std::string& value) {
        line.decoy = value == "1" ? true : false;
    }
    static void insertLeftWidth(TSVLine& line, const std::string& value) {
        line.leftWidth = value.empty() ? 0 : lexical_cast<double>(value) / 60;
    }
    static void insertRightWidth(TSVLine& line, const std::string& value) {
        line.rightWidth = value.empty() ? 0 : lexical_cast<double>(value) / 60;
    }
    static void insertProductMz(TSVLine& line, const std::string& value) {
        line.leftWidth = value.empty() ? 0 : lexical_cast<double>(value);
    }
    static void insertPeakArea(TSVLine& line, const std::string& value) {
        line.peakArea = value;
    }
    static void insertFragmentAnnotation(TSVLine& line, const std::string& value) {
        line.fragmentAnnotation = value;
    }
    static void insertFragmentSeriesNumber(TSVLine& line, const std::string& value) {
        line.fragmentSeriesNumber = lexical_cast<int>(value);
    }
    static void insertScore(TSVLine& line, const std::string& value) {
        line.score = value.empty() ? 0 : lexical_cast<double>(value);
    }
    static void insertCE(TSVLine& line, const std::string& value) {
        line.ce = lexical_cast<double>(value);
    }
    static void insertIonMobility(TSVLine& line, const std::string& value) {
        line.ionMobility = lexical_cast<double>(value);
    }
};

struct TSVColumnTranslator {
    const char* name_;
    int position_;
    void (*inserter_)(TSVLine&, const std::string&); 
};

// Class for parsing .tsv files
class TSVReader : public BuildParser, public SpecFileReader {

public:
    TSVReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress);
    ~TSVReader();

    /// factory function for creating correct implementaiton of TSVReader based on column names
    static std::shared_ptr<TSVReader> create(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress);

    virtual bool parseFile() = 0;
    virtual vector<PSM_SCORE_TYPE> getScoreTypes() = 0;

    // these inherited from SpecFileReader
    virtual void openFile(const char*, bool) {}
    virtual void setIdType(SPEC_ID_TYPE) {}
    virtual bool getSpectrum(PSM* psm, SPEC_ID_TYPE findBy, SpecData& returnData, bool getPeaks = true);    
    virtual bool getSpectrum(int, SpecData&, SPEC_ID_TYPE, bool);
    virtual bool getSpectrum(std::string, SpecData&, bool);
    virtual bool getNextSpectrum(SpecData&, bool);

    static bool parseSequence(const UnimodParser& unimod, 
                              const std::string& seq,
                              std::string* outSeq,
                              std::vector<SeqMod>* outMods,
                              int* line = NULL);
    
protected:
    std::string tsvName_;
    ifstream tsvFile_;
    UnimodParser unimod_;
    double* masses_;
    double scoreThreshold_;
    int lineNum_;
    map< std::string, vector<TSVPSM*> > fileMap_; // store psms by filename
    vector<TSVColumnTranslator> targetColumns_; // columns to extract
    vector<TSVColumnTranslator> optionalColumns_; // columns to extract

    typedef boost::tokenizer< boost::escaped_list_separator<char> > LineParser;
    static const escaped_list_separator<char> separator_;

    static void parseHeader(LineParser& headerLine, vector<TSVColumnTranslator>& targetColumns, vector<TSVColumnTranslator>& optionalColumns);

    //std::vector<TSVColumnTranslator>::iterator findColumn(const std::string& column, std::vector<TSVColumnTranslator>& v);
    void collectPsms(std::map<std::string, Protein>& proteins);
    virtual void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins) = 0;
};

} // namespace

