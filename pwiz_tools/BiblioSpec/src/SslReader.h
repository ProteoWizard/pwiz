/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
#pragma once

#include "BuildParser.h"
#include "DelimitedFileReader.h"

namespace BiblioSpec {

// classes and functions to use with the DelimitedFileReader
class sslPSM : public PSM {
  public:
    std::string filename; 
    PSM_SCORE_TYPE scoreType;

    sslPSM() : PSM(), scoreType(UNKNOWN_SCORE_TYPE){};

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
                psm.specKey = boost::lexical_cast<int>(value);
            } catch (bad_lexical_cast) {
                psm.specName = value;
            }
        }
    }
    static void setCharge(sslPSM& psm, const std::string& value){
        if( value.empty() ){
            psm.charge = 0;
        } else {
            try{
                psm.charge =  boost::lexical_cast<int>(value);
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
};

/**
 * The SslReader class to parse .ssl files.  Uses a DelimitedFileReader.
 */
class SslReader : public BuildParser, DelimitedFileConsumer<sslPSM> {
  public:
    SslReader(BlibBuilder& maker,
              const char* sslname,
              const ProgressIndicator* parent_progress);
    ~SslReader();

    virtual bool parseFile();  // inherited from BuildParser
    virtual void addDataLine(sslPSM& data); // from DelimitedFileConsumer 


  private:
    string sslName_;
    string sslDir_;   // look for spectrum files in the same dir as the ssl
    map<string, vector<PSM*> > fileMap_; // vector of PSMs for each spec file
    map<string, PSM_SCORE_TYPE> fileScoreTypes_; // score type for each file

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
