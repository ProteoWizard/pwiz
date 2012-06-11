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
    double retentionTime;
    std::vector<double> mzs;
    std::vector<double> intensities;
    bool valid; // if false, don't add to library

    MsePSM() : PSM(), mz(0), retentionTime(0), valid(true) {
    }

    void clear(){
        PSM::clear();
        mz = 0;
        retentionTime = 0;
        mzs.clear();
        intensities.clear();
        valid = true;
    }
};

/**
 * For set::insert, compare seq, charge, mz, RT. If all equal, return
 * false.
 */
struct compMsePsm{
  bool operator() (const MsePSM* left, const MsePSM* right) const
  {
    if( left->unmodSeq == right->unmodSeq ){
        if( left->charge == right->charge ){
            if( left->mz == right->mz ){
                if( left->retentionTime == right->retentionTime ){
                    return false;
                } else {
                    return (left->retentionTime < right->retentionTime);
                }
                
            } else {
                return (left->mz < right->mz);
            }
        } else {
            return (left->charge < right->charge);
        }
    } else {
        return (left->unmodSeq < right->unmodSeq);
    }
  }
};

/**
 * Holds all the values from one line of the Waters MSE .csv file.
 */
class LineEntry{
public:
  double precursorMz;
  int precursorZ;
  double score;
  double retentionTime;
  string sequence;
  string modification;
  double fragmentMz;
  double fragmentIntensity;
  double precursorMass;
  double minMass;
  string pass;

  LineEntry() : precursorMz(0), precursorZ(0), retentionTime(0),
      fragmentMz(0), fragmentIntensity(0), precursorMass(0), minMass(0){};

  static void insertPrecursorMz(LineEntry& le, const string& value){
      if( value.empty() ){
          le.precursorMz = 0;
      } else {
          le.precursorMz = boost::lexical_cast<double>(value);
      }
  }
  static void insertPrecursorZ(LineEntry& le, const string& value){
      if( value.empty() ){
          le.precursorZ = 0;
      } else {
          le.precursorZ = boost::lexical_cast<int>(value);
      }
  }
  static void insertScore(LineEntry& le, const string& value){
      if( value.empty() ){
          le.score = 0;
      } else {
          le.score = boost::lexical_cast<double>(value);
      }
  }
  static void insertRetentionTime(LineEntry& le, const string& value){
      if( value.empty() ){
          le.retentionTime = 0;
      } else {
          le.retentionTime = boost::lexical_cast<double>(value);
      }
  }
  static void insertSequence(LineEntry& le, const string& value){
    le.sequence = value;
  }
  static void insertModification(LineEntry& le, const string& value){
    le.modification = value;
  }
  static void insertFragmentMz(LineEntry& le, const string& value){
      if( value.empty() ){
          le.fragmentMz = 0;
      } else {
          le.fragmentMz = boost::lexical_cast<double>(value);
      }
  }
  static void insertFragmentIntensity(LineEntry& le, const string& value){
      if( value.empty() ){
          le.fragmentIntensity = 0;
      } else {
          le.fragmentIntensity = boost::lexical_cast<double>(value);
      }
  }
  static void insertPrecursorMass(LineEntry& le, const string& value){
      if( value.empty() ){
          le.precursorMass = 0;
      } else {
          le.precursorMass = boost::lexical_cast<double>(value);
      }
  }
  static void insertMinMass(LineEntry& le, const string& value){
      if( value.empty() ){
          le.minMass = 0;
      } else {
          le.minMass = boost::lexical_cast<double>(value);
      }
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

        void initTargetColumns();
        bool openFile();
        void parseHeader(std::string& line);
        void collectPsms();
        void storeLine(LineEntry& entry);
        void parseModString(LineEntry& entry, MsePSM* psm);
        void insertCurPSM();

        static const char* modNames_[];
        static double modMasses_[];
        static int numModNames_;
        
  };

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
