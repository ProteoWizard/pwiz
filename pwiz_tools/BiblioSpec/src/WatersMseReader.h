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
#include <set>
#include "boost/tokenizer.hpp"

namespace BiblioSpec {

typedef boost::tokenizer< boost::escaped_list_separator<char> > CsvTokenizer;
typedef boost::tokenizer< boost::escaped_list_separator<char> >::iterator 
                                                           CsvTokenIterator;
/**
 * Extends the standard PSM with additional fields for the spectrum.
 */
struct MsePSM : PSM {
    double mz;
    double precursorNonIntegerCharge;   // A non-integer value that reveals relative abundance of charge states.  Rounded, it's the same value as "precursorZ".
                          // For example, "2.74" means a mix of 2 and 3, with most being 3.  "3.2" would be a mix of 3 and 4, mostly 3. "1.5" is an even mix of 1 and 2.
                          // This information is useful in interpreting product drift times, which differ from precursor drift times due
                          // to the extra kinetic energy that is added post-drift-cell to induce fragmentation. (per telecon with Will Thompson 7/28/14) 
    double precursorIonMobility;
    double retentionTime;
    std::vector<double> mzs;
    std::vector<double> intensities;
    std::vector<double> productIonMobilities;
    bool valid; // if false, don't add to library

    MsePSM() : PSM(), mz(0), precursorNonIntegerCharge(0), precursorIonMobility(0), retentionTime(0), valid(true) {
    }

    MsePSM(const MsePSM& rhs)
    {
        *this = rhs;
    }

    MsePSM& operator= (const MsePSM& rhs)
    {
        clear();
        PSM::operator=(rhs); // base class copy
        mz = rhs.mz;
        precursorIonMobility = rhs.precursorIonMobility;
        precursorNonIntegerCharge = rhs.precursorNonIntegerCharge;
        retentionTime = rhs.retentionTime;
        mzs = rhs.mzs;
        intensities = rhs.intensities;
        productIonMobilities = rhs.productIonMobilities;
        valid = rhs.valid;
        return *this;
    }

    void clear(){
        PSM::clear();
        mz = 0;
        precursorIonMobility = 0;
        precursorNonIntegerCharge = 0;
        retentionTime = 0;
        mzs.clear();
        intensities.clear();
        productIonMobilities.clear();
        valid = true;
    }
};

/**
 * For set::insert, implements the Less<> predicate.
 */
struct compMsePsm{
    bool operator() (const MsePSM* left, const MsePSM* right) const
    {
        if (left->unmodSeq != right->unmodSeq) 
            return (left->unmodSeq < right->unmodSeq);
        if (left->charge != right->charge) 
            return (left->charge < right->charge);
        if (left->mz != right->mz) 
            return (left->mz < right->mz);
        if (left->retentionTime != right->retentionTime) 
            return (left->retentionTime < right->retentionTime);
        if (left->precursorIonMobility != right->precursorIonMobility) 
            return (left->precursorIonMobility < right->precursorIonMobility);
        if (left->smallMolMetadata.chemicalFormula != right->smallMolMetadata.chemicalFormula) 
            return (left->smallMolMetadata.chemicalFormula < right->smallMolMetadata.chemicalFormula);
        if (left->smallMolMetadata.inchiKey != right->smallMolMetadata.inchiKey) 
            return (left->smallMolMetadata.inchiKey < right->smallMolMetadata.inchiKey);
        if (left->smallMolMetadata.precursorAdduct != right->smallMolMetadata.precursorAdduct) 
            return (left->smallMolMetadata.precursorAdduct < right->smallMolMetadata.precursorAdduct);
        return false;
    }
};

/**
 * Holds all the values from one line of the Waters MSE .csv file.
 */
class LineEntry{
public:
  double precursorMz;
  double precursorNonIntegerCharge; // A non-integer value that reveals relative abundance of charge states.  Rounded, it's the same value as "precursorZ".
                          // For example, "2.74" means a mix of 2 and 3, with most being 3.  "3.2" would be a mix of 3 and 4, mostly 3. "1.5" is an even mix of 1 and 2.
                          // This information is useful in interpreting product drift times, which differ from precursor drift times due
                          // to the extra kinetic energy that is added post-drift-cell to induce fragmentation. (per telecon with Will Thompson 7/28/14)
  int precursorZ; // The most abundant charge.  Same as the rounded value of precursorCharge.
  double score;
  double retentionTime;
  string sequence;
  string modification;
  double fragmentMz;
  double fragmentIntensity;
  double precursorMass;
  double minMass;
  float precursorIonMobility;
  float productIonMobility;
  string pass;

  LineEntry() : precursorMz(0), precursorNonIntegerCharge(0), precursorZ(0), retentionTime(0),
      fragmentMz(0), fragmentIntensity(0), precursorMass(0), minMass(0),
      precursorIonMobility(0), productIonMobility(0) {};

  static void insertPrecursorMz(LineEntry& le, const string& value){
      le.precursorMz = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertPrecursorNonIntegerCharge(LineEntry& le, const string& value){
      le.precursorNonIntegerCharge = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertPrecursorZ(LineEntry& le, const string& value){
      le.precursorZ = (value.empty()) ? 0 : boost::lexical_cast<int>(value);
  }
  static void insertScore(LineEntry& le, const string& value){
      le.score = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertRetentionTime(LineEntry& le, const string& value){
      le.retentionTime = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertSequence(LineEntry& le, const string& value){
      le.sequence = value;
  }
  static void insertModification(LineEntry& le, const string& value){
      le.modification = value;
  }
  static void insertFragmentMz(LineEntry& le, const string& value){
      le.fragmentMz = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertFragmentIntensity(LineEntry& le, const string& value){
      le.fragmentIntensity = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertPrecursorMass(LineEntry& le, const string& value){
      le.precursorMass = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertMinMass(LineEntry& le, const string& value){
      le.minMass = (value.empty()) ? 0 : boost::lexical_cast<double>(value);
  }
  static void insertPrecursorIonMobility(LineEntry& le, const string& value){
      le.precursorIonMobility = (value.empty()) ? 0 : boost::lexical_cast<float>(value);
  }
  static void insertProductIonMobility(LineEntry& le, const string& value){
      le.productIonMobility = (value.empty()) ? 0 : boost::lexical_cast<float>(value);
  }
  static void insertPass(LineEntry& le, const string& value){
      le.pass = value;
  }
};
/**
 * Holds information about how the columns appear in the Waters MSE
 * .csv file.  The column name is how it appears in the first row of
 * the file.  The column position is the zero-based index of the
 * column in the file.  The inserter is a function pointer that can be
 * called to insert this column's value into a LineEntry object.  This
 * allows constant-time translation between the column position and
 * the specific information in that column.
 */
class wColumnTranslator{
public:
  string name_;
  int position_;
  void (*inserter)(LineEntry& le, const string& value); 

  wColumnTranslator(const char* name, 
                   int pos, 
                   void (*fun)(LineEntry&, const string&))
    : name_(name), position_(pos), inserter(fun) { };

  friend bool operator< (const wColumnTranslator& left, 
                         const wColumnTranslator& right)
  {
    return (left.position_ < right.position_);
  }
};

/**
 * Class for parsing .csv files from Waters' MSE results.
 */
class WatersMseReader : public BuildParser, public SpecFileReader {
    public:
        WatersMseReader(BlibBuilder& maker,
                        const char* sslname,
                        const ProgressIndicator* parentProgress);
        ~WatersMseReader();
        
        bool parseFile();
        std::vector<PSM_SCORE_TYPE> getScoreTypes();
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
        std::string csvName_;
        ifstream csvFile_;
        double scoreThreshold_;
        int lineNum_;
        MsePSM* curMsePSM_; // use this instead of curPSM_
        std::set<MsePSM*, compMsePsm> uniquePSMs_; // select unique psms
        int numColumns_;   // size of targetColumns_;
        std::vector<wColumnTranslator>targetColumns_; // columns to extract 
        std::vector<wColumnTranslator>optionalColumns_; // not required

        double pusherInterval_;

        void initTargetColumns();
        bool openFile();
        void parseHeader(std::string& line);
        void collectPsms();
        void storeLine(LineEntry& entry);
        void parseModString(LineEntry& entry, MsePSM* psm);
        void insertCurPSM();

        std::map<std::string, double> mods_;
  };

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
