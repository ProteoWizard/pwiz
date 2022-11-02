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

#pragma once

#include "BuildParser.h"
#include "DelimitedFileReader.h"

namespace BiblioSpec {

// classes and functions to use with the DelimitedFileReader
class sslPSM : public PSM {
  public:
    std::string filename; 
    PSM_SCORE_TYPE scoreType;
    double retentionTime;

    sslPSM() : PSM(), scoreType(UNKNOWN_SCORE_TYPE), retentionTime(-1) {};

    static void setFile(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            throw BlibException(false, "Missing filename.");
        } else {
            psm.filename =  value;
        }
    }
    static void setScanNumber(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            throw BlibException(false, "Missing scan number.");
        } else {
            try{// might be a scan number or a string identifier
                psm.specKey = boost::lexical_cast<int>(trimLeadingZeros(value));
                psm.specIndex = -1;
            } catch (bad_lexical_cast) {
                if (bal::istarts_with(value, "index="))
                    psm.specIndex = boost::lexical_cast<int>(value.substr(6));
                else
                    psm.specName = value;
            }
        }
    }
    static void setCharge(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            psm.charge = 0;
        } else {
            try{
                psm.charge =  boost::lexical_cast<int>(trimLeadingZeros(value));
            } catch (bad_lexical_cast) {
                throw BlibException(false, "Non-numeric charge value: %s.",
                                    value.c_str());
            }
        }
    }
    /**
     * Save the modified sequence in the unmodSeq slot and parse it later.
     */
    static void setModifiedSequence(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            throw BlibException(false, "Missing peptide sequence.");
        } else {
            psm.unmodSeq = value;
        }
    }
    static void setScoreType(sslPSM& psm, const std::string& value){
        psm.scoreType = stringToScoreType(value);
        //        Verbosity::status("Got score type %d from string %s.", psm.scoreType, value.c_str() );
    }
    static void setScore(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            psm.score = 0;
        } else {
            try {
                psm.score = boost::lexical_cast<double>(value);
            } catch (bad_lexical_cast) {
                throw BlibException(false, "Non-numeric score: %s",
                                    value.c_str());
            }
        }
    }
    static void setRetentionTime(sslPSM& psm, const std::string& value) {
        if (!value.empty()) {
            try {
                psm.retentionTime = boost::lexical_cast<double>(value);
            } catch (bad_lexical_cast) {
                throw BlibException(false, "Non-numeric retention time: %s",
                                    value.c_str());
            }
        }
    }
    static void setPrecursorAdduct(sslPSM& psm, const std::string& value) {
        if (value.empty()){
            throw BlibException(false, "Missing precursor adduct.");
        }
        else {
            psm.smallMolMetadata.precursorAdduct = value;
        }
    }
    static void setChemicalFormula(sslPSM& psm, const std::string& value) {
        if (value.empty()){
            throw BlibException(false, "Missing chemical formula.");
        }
        else {
            psm.smallMolMetadata.chemicalFormula = value;
        }
    }
    static void setInchiKey(sslPSM& psm, const std::string& value) {
        if (value.empty()){
            throw BlibException(false, "Missing InChiKey.");
        }
        else {
            psm.smallMolMetadata.inchiKey = value;
        }
    }
    static void setMoleculeName(sslPSM& psm, const std::string& value) {
        if (value.empty()){
            throw BlibException(false, "Missing molecule name.");
        }
        else {
            psm.smallMolMetadata.moleculeName = value;
        }
    }
    static void setotherKeys(sslPSM& psm, const std::string& value) {
        if (value.empty()){
            throw BlibException(false, "Missing otherKeys.");
        }
        else {
            psm.smallMolMetadata.otherKeys = value;
        }
    }

  private:
     static std::string trimLeadingZeros(std::string s) {
         if (s.empty()) {
            return s;
         }
         size_t nonZero = s.find_first_not_of('0');
         return nonZero != string::npos
            ? s.substr(nonZero)
            : "0"; // just return a single zero if the string consists of only zeros
     }

};

/**
 * The SslReader class to parse .ssl files.  Uses a DelimitedFileReader.
 */
class SslReader : public BuildParser, DelimitedFileConsumer<sslPSM>, public PwizReader {
  public:
    SslReader(BlibBuilder& maker,
              const char* sslname,
              const ProgressIndicator* parent_progress);
    ~SslReader();

    void parse();
    virtual bool parseFile();  // inherited from BuildParser
    vector<PSM_SCORE_TYPE> getScoreTypes(); // inherited from BuildParser
    virtual void addDataLine(sslPSM& data); // from DelimitedFileConsumer

    virtual bool getSpectrum(int identifier,
                             SpecData& returnData,
                             SPEC_ID_TYPE type,
                             bool getPeaks);

    virtual bool getSpectrum(std::string identifier,
                             SpecData& returnData,
                             bool getPeaks);

  private:
    string sslName_;
    string sslDir_;   // look for spectrum files in the same dir as the ssl
    map<string, vector<PSM*> > fileMap_; // vector of PSMs for each spec file
    map<string, PSM_SCORE_TYPE> fileScoreTypes_; // score type for each file

    map<int, double> overrideRt_; // forced retention times (key is scan number)

    void parseModSeq(vector<SeqMod>& mods, string& modSeq);
    void unmodifySequence(string& seq);

  };

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
