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

/**
 *  The MascotSpecReader uses the ms_parser library to read spectra
 *  from a .dat file.  MascotSpecReader can be instantiated with a
 *  filename or with a ms_mascotresfile object.
 */

#include "Verbosity.h"
#include "BlibUtils.h"
#include "SpecFileReader.h"
#include "msparser.hpp"

using namespace matrix_science;

namespace BiblioSpec {

class MascotSpecReader : public SpecFileReader {
 public:
    MascotSpecReader(){
        ms_file_ = NULL;
        ms_results_ = NULL;
    }
    MascotSpecReader(const char* filename,
                     ms_mascotresfile* ms_file, 
                     ms_mascotresults* ms_results = NULL){
        setFile(ms_file, ms_results);
        filename_ = filename;
    }
    virtual ~MascotSpecReader(){
        delete ms_results_;
        delete ms_file_;
    }


    /** 
     * This is the SpecFileReader function.  Can call with an empty 
     * filename if setFile(ms_mascotresfile) has already been called.
     * If filename is not empty, will replace any ms_mascotresfile it
     * already has.
     * Throw error if file not found, can't be opened, wrong type.
     */ 
    virtual void openFile(const char* filename, bool mzSort){
        if( ms_file_ != NULL ){
            Verbosity::comment(V_STATUS, "Importing spectra from %s.", 
                               filename_.c_str());
            return;
        } else { //we have a new file to open

        Verbosity::comment(V_STATUS, "Opening and importing spectra from %s.", 
                           filename);
            delete ms_file_;
            ms_file_ = new ms_mascotresfile(filename);
            if( !ms_file_->isValid() ) {
                throw BlibException(true,
                                    "'%s' is not a valid .dat file.", 
                                   filename);
            }
            if( !ms_file_->isMSMS() ){
                throw BlibException(true,
                                    "'%s' does not have spectra.",
                                    filename);

            }
        }
    }
    
    void setFile(ms_mascotresfile* ms_file, 
                 ms_mascotresults* ms_results = NULL){
        Verbosity::comment(V_DETAIL, "MascotSpecReader attaching open file.");
        //delete ms_file_;
        ms_file_ = ms_file;

        if( !ms_file_->isMSMS() ){
            throw BlibException(false,
                                "The given .dat file with search results does not also have spectra.");
            // in the future, we could then let the caller look for another input file type.
        }

        ms_results_ = ms_results;
    }
    
    /**
     * Read file to find the spectrum corresponding to identifier.
     * Fill in mz and numPeaks in returnData
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(int specId, 
                             SpecData& returnData, 
                             SPEC_ID_TYPE findBy,
                             bool getPeaks = true)
    {
        Verbosity::comment(V_DETAIL, "MascotSpecReader looking for spec %d.",
                           specId);


        // I can only find a way to get the precursor m/z from the
        // peptide
        ms_peptide* pep;
        ms_results_->getPeptide(specId, 1, pep); // rank 1
        returnData.mz = pep->getObserved();
        returnData.id = specId;

        ms_inputquery spec(*ms_file_, specId);
        // retention time is optional in .dat files
        string rtStr = spec.getRetentionTimes();
        try{
            returnData.retentionTime = boost::lexical_cast<double>(rtStr) / 60;
            // seconds to minutes
        } catch (...){
            // if it wasn't there, try the title string
            returnData.retentionTime = getRetentionTimeFromTitle(spec);
        }
        returnData.numPeaks = spec.getNumberOfPeaks(1);// first ion series

        if( getPeaks ){
            returnData.mzs = new double[returnData.numPeaks];
            returnData.intensities = new float[returnData.numPeaks];

            vector<pair<double,double> > peaks = spec.getPeakList(1);
            sort(peaks.begin(), peaks.end(), 
                 compare_first_pair_doubles_descending);
            for(size_t peak_i = 0; peak_i < peaks.size(); peak_i++) {
                returnData.mzs[peak_i] = peaks[peak_i].first;
                returnData.intensities[peak_i] = (float)peaks[peak_i].second;
            }
        }

        return true;
    }

    virtual void setIdType(SPEC_ID_TYPE type){}

    virtual bool getSpectrum(string identifier, 
                             SpecData& returnData, 
                             bool getPeaks)
    {
        Verbosity::warn("MascotSpecReader cannot fetch spectra by string" 
                        "identifier, only by scan number");
        return false;
    }

    /**
     * For now, only specific spectra can be accessed from the Mascot
     * files.
     */
    virtual bool getNextSpectrum(SpecData& returnData, bool getPeaks){
        Verbosity::warn("MascotSpecReader does not support sequential "
                        "file reading.");
        return false;
    }
    
 private:
    string filename_;   // keep around for reporting purposes
    ms_mascotresfile* ms_file_;    // get spec from here
    ms_mascotresults* ms_results_; // get pep from here (for m/z)

    /**
     * Helper function to parse a double from the given string
     * found between the two tags.  Search for number after position
     * Update position to the end of the parsed double.
     */
     double getTime(const string& title, const char* startTag,
                    const char* endTag, size_t&position)
     {
        size_t start = title.find(startTag, position);
        if( start == string::npos ){ // not found
            return 0;
        }
        start += strlen(startTag);
        size_t end = title.find(endTag, start);
        string timeStr = title.substr(start, end - start);
        try{
            double time = boost::lexical_cast<double>(timeStr);
            position = start;
            return time;
        } catch(...){
            return 0;
        }
     }


    /**
     * Parse the spectrum title to look for retention times.  If there are
     * two times, return the center of the range.  Possible formats to look
     * for are "Elution:<time> min" and "rt=<time>,".
     */
    double getRetentionTimeFromTitle(ms_inputquery& spec){
        // text to search for preceeding and following time
        const char* startTags[2] = { "Elution:", "rt=" };
        const char* secondStartTags[2] = { "to ", "rt=" };
        const char* endTags[2] = { "min", "," };

        double firstTime = 0;
        double secondTime = 0;
        string title = spec.getStringTitle(true);
        for(int format_idx = 0; format_idx < 2; format_idx++){

            size_t position = 0;
            firstTime = getTime(title, startTags[format_idx], 
                                endTags[format_idx], position);
            secondTime = getTime(title, secondStartTags[format_idx], 
                                 endTags[format_idx], position);

            if( firstTime > 0 ){
               break;
            }
        } // try another format

        double time = firstTime;
        if( secondTime != 0 ){
            time = (firstTime + secondTime) / 2 ;
        }

        return time; // shouldn't get to here
    }




};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















