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
    MascotSpecReader()
        : ms_file_(NULL), ms_results_(NULL), disableRtConversion_(false), needsRtConversion_(false) {
    }
    MascotSpecReader(const char* filename,
                     ms_mascotresfile* ms_file, 
                     ms_mascotresults* ms_results = NULL,
                     const std::vector<std::string>& rawfiles = std::vector<std::string>(),
                     boost::shared_ptr<TempFileDeleter> tmpFileDeleter = boost::shared_ptr<TempFileDeleter>())
        : disableRtConversion_(false), needsRtConversion_(false), tmpFileDeleter_(tmpFileDeleter) {
        setFile(ms_file, ms_results);
        filename_ = filename;
        numRawFiles_ = rawfiles.size();
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
        double rt = -1;
        try {
            rt = boost::lexical_cast<double>(spec.getRetentionTimes());
        } catch (...) {
            rt = -1;
        }

        if (rt < 0) {
            for (size_t i = 0; i < numRawFiles_; i++) {
                try {
                    rt = boost::lexical_cast<double>(spec.getRetentionTimes(i));
                    break;
                } catch (...) {
                    rt = -1;
                }
            }
        }

        if (rt >= 0) {
            returnData.retentionTime = rt / 60;
            disableRtConversion_ = true;
            needsRtConversion_ = false;
            // seconds to minutes
        } else {
            // if it wasn't there, try the title string
            returnData.retentionTime = getRetentionTimeFromTitle(spec.getStringTitle(true));
        }
        getIonMobilityFromTitle(returnData, spec.getStringTitle(true)); // For Bruker TIMS
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

    bool needsRtConversion() const {
        return needsRtConversion_;
    }
    
 private:
    string filename_;   // keep around for reporting purposes
    ms_mascotresfile* ms_file_;    // get spec from here
    ms_mascotresults* ms_results_; // get pep from here (for m/z)
    size_t numRawFiles_;
    bool disableRtConversion_; // rt units known
    bool needsRtConversion_; // rt units unknown and we've seen a retention time >750
    boost::shared_ptr<TempFileDeleter> tmpFileDeleter_; // deletes temporary file when needed for Unicode filepath

    // TODO: This code is now duplicated in SpectrumList_MGF.cpp
    /**
     * Parse the spectrum title to look for retention times.  If there are
     * two times, return the center of the range.  Possible formats to look
     * for are "Elution:<time> min", "RT:<time>min" and "rt=<time>,".
     */
    double getRetentionTimeFromTitle(const string& title)
    {
        // text to search for preceeding and following time
        const char* startTags[4] = { "Elution:", "Elution from: ", "RT:", "rt=" };
        const char* secondStartTags[4] = { "to ", " to ", NULL, NULL };
        const char* endTags[4] = { "min", " ", "min", "," };

        double time = 0;
        double secondTime = 0;
        for (int format_idx = 0; format_idx < 4; format_idx++)
        {
            size_t position = 0;
            if ((time = getTime(title, startTags[format_idx], endTags[format_idx], &position)) == 0)
                continue;

            if (secondStartTags[format_idx] != NULL)
                secondTime = getTime(title, secondStartTags[format_idx], endTags[format_idx], &position);

            if (time != 0) {
                if (!disableRtConversion_) {
                    bool unitsKnownMin = format_idx == 0 || format_idx == 2;
                    if (unitsKnownMin) {
                        Verbosity::debug("RT conversion to minutes disabled");
                        disableRtConversion_ = true;
                        needsRtConversion_ = false;
                    } else if (time > 750 && !needsRtConversion_) {
                        Verbosity::debug("RT conversion to minutes enabled");
                        needsRtConversion_ = true;
                    }
                }
                break;
            }
        } // try another format

        if (time != 0 && secondTime != 0)
        {
            time = (time + secondTime) / 2 ;
        }

        return time;
    }

    /**
     * Get ion mobility as encoded for Bruker TIMS
     */
    void getIonMobilityFromTitle(SpecData& returnData, const string& title) // For Bruker TIMS
    {
        // Pick out the Mobility value from something like
        // title= Cmpd X, +MSn(383.4828600), 61.20 min MS: 151978/ |MSMS:151979/|count(pasefframemsmsinfo.precursor)=10|Id=2813793|AverageMz=383.672206171834|MonoisotopicMz=383.482886705023|Charge=3|Intensity=1217|ScanNumber=829.680327868853|Mobility=0.701142751474809
        const char* tag = "Mobility=";
        const char* delimiter = "|";
        size_t start = title.find(tag, 0);
        if (start == string::npos)
        {
            // Try the "TITLE=Cmpd 1, +MS2(948.5056), 63.0eV, 52.60-52.61min, 1/K0=1.409 #26317-26323" variant        
            tag = "1/K0=";
            delimiter = " ";
            start = title.find(tag, 0);
        }
        size_t end;
        if (start != string::npos)
        {
            int tagLen = strlen(tag);
            start += tagLen;
            end = title.find(delimiter, start);
            if (end == string::npos)
                end = title.length();
        }
        else // Try the "TITLE=1 Features, +MS2(479.538163, 2+), 0.8123 1/k0," variant
        {
            end = title.find(" 1/k0", 0);
            if (end != string::npos)
            {
                start = title.find_last_of(' ',end - 1);
                if (start != string::npos)
                {
                    start++;
                }
            }
        }
        if (start != string::npos)
        {
            string imStr = title.substr(start, end - start);
            try
            {
                returnData.ionMobility = boost::lexical_cast<double>(imStr);
                returnData.ionMobilityType = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
                return;
            }
            catch (...)
            {
            }
            returnData.ionMobility = 0;
            returnData.ionMobilityType = IONMOBILITY_NONE;
            throw BlibException(false, "Failure reading TIMS ion mobility value \"%s\"", imStr.c_str());
        }
    }

    /**
     * Helper function to parse a double from the given string
     * found between the two tags.  Search for number after position
     * Update position to the end of the parsed double.
     */
    double getTime(const string& title, const char* startTag,
                   const char* endTag, size_t* position) const
    {
        size_t start = title.find(startTag, *position);
        if( start == string::npos )
            return 0; // not found

        start += strlen(startTag);
        size_t end = title.find(endTag, start);
        string timeStr = title.substr(start, end - start);
        try
        {
            double time = boost::lexical_cast<double>(timeStr);
            *position = start;
            return time;
        }
        catch(...)
        {
            return 0;
        }
    }
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
