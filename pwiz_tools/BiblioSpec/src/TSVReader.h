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
    vector<double> mzs;
    vector<double> intensities;

    TSVPSM() : PSM(), rt(0), mz(0), leftWidth(0), rightWidth(0) {}

    void clear() {
        PSM::clear();
        rt = 0;
        mz = 0;
        leftWidth = 0;
        rightWidth = 0;
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
    std::string peakArea;
    std::string fragmentAnnotation;
    double score;

    TSVLine() : rt(0), charge(0), mz(0), decoy(false), leftWidth(0), rightWidth(0), score(0) {}

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
    static void insertPeakArea(TSVLine& line, const std::string& value) {
        line.peakArea = value;
    }
    static void insertFragmentAnnotation(TSVLine& line, const std::string& value) {
        line.fragmentAnnotation = value;
    }
    static void insertScore(TSVLine& line, const std::string& value) {
        line.score = value.empty() ? 0 : lexical_cast<double>(value);
    }
};

class TSVColumnTranslator {
public:
    std::string name_;
    int position_;
    void (*inserter_)(TSVLine&, const std::string&); 

    TSVColumnTranslator(const char* name, void (*inserter)(TSVLine&, const std::string&))
        : name_(name), position_(-1), inserter_(inserter) {};

    friend bool operator<(const TSVColumnTranslator& left, const TSVColumnTranslator& right) {
        return left.position_ < right.position_;
    }
};

// Class for parsing .tsv files
class TSVReader : public BuildParser, public SpecFileReader {

    typedef boost::tokenizer< boost::escaped_list_separator<char> > LineParser;

public:
    TSVReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress);
    ~TSVReader();
    
    bool parseFile();
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
    
private:
    std::string tsvName_;
    ifstream tsvFile_;
    UnimodParser unimod_;
    double* masses_;
    double scoreThreshold_;
    int lineNum_;
    map< std::string, vector<TSVPSM*> > fileMap_; // store psms by filename
    vector<TSVColumnTranslator> targetColumns_; // columns to extract
    vector<TSVColumnTranslator> optionalColumns_; // columns to extract

    void parseHeader();
    std::vector<TSVColumnTranslator>::iterator findColumn(
        const std::string& column,
        std::vector<TSVColumnTranslator>& v);
    void collectPsms(std::map<std::string, Protein>& proteins);
    void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins);
    bool parsePeaks(
        const std::string& peakArea,
        const std::string& fragmentAnnotation,
        std::vector<double>* mz,
        std::vector<double>* intensity);
    bool calcIonMz(
        const std::string& seq,
        const std::vector<SeqMod>& mods,
        char ionType,
        int ionNum,
        int ionCharge,
        double* ionMz);

    const escaped_list_separator<char> separator_;
};

} // namespace

