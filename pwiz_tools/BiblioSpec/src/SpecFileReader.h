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
 *  The SpecFileRader is an interface for the Proteowizard,
 *  ms_parser and any other package used for reading files
 *  containing spectra.  It is used by BuildParser and BlibSearch.
 */

#include "Verbosity.h"
#include "PSM.h"
#include "SpecData.h"

namespace BiblioSpec {

class SpecFileReader {
 public:
    /** 
     * Open the filename given and prepare to find spectra.
     * Throw error if file not found, can't be opened, wrong type.
     */ 
    virtual void openFile(const char*, bool mzSort = false) = 0;
    
    /**
     * Needed for a hack to make accessing spectra by index using pwiz
     * work.  Eventually get rid of this and only use spec_id_type
     * with getSpectrum.
     */
    virtual void setIdType(SPEC_ID_TYPE type) = 0;

    /**
     * Read file to find the spectrum corresponding to the given PSM.
     * Use the appropriate identifier in the PSM (specKey, specName)
     * based on the SPEC_ID_TYPE. Fill in mz and numPeaks in returnData
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(PSM* psm,
                             SPEC_ID_TYPE findBy,
                             SpecData& returnData,
                             bool getPeaks)
    {
        bool isMS1 = psm->isPrecursorOnly();
        if (isMS1)
        {
            getPeaks = false;
            if (psm->specKey == -1 && findBy == SPEC_ID_TYPE::SCAN_NUM_ID)
            {
                findBy = NAME_ID; // Look up by constructed ID since there's no actual spectrum associated
            }
        }
        switch(findBy){
        case NAME_ID:
            return getSpectrum(psm->specName, returnData, getPeaks);
        case SCAN_NUM_ID:
            return getSpectrum(psm->specKey, returnData, findBy, getPeaks);
        case INDEX_ID:
            return getSpectrum(psm->specIndex, returnData, findBy, getPeaks);
        }
        return false;
    };



    /**
     * Read file to find the spectrum corresponding to identifier.
     * Fill in mz and numPeaks in returnData
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(int identifier, 
                             SpecData& returnData, 
                             SPEC_ID_TYPE findBy,
                             bool getPeaks = true) = 0;

    /**
     * Read file to find the spectrum corresponding to identifier.
     * Fill in mz and numPeaks in returnData
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(string identifier, 
                             SpecData& returnData, 
                             bool getPeaks = true) = 0;

    /**
     *  Return the next spectrum in the file via the SpecData
     *  parameter.  If getPeaks is false, do not include the mz and
     *  intensity arrays.  Spectra are either returned in the order in which
     *  they appear in the file or sorted by precursor m/z if set in
     *  the constructor.  Returns false if there are no more spectra
     *  in the file. 
     */
    virtual bool getNextSpectrum(SpecData& returnData, bool getPeaks = true) = 0;

    /**
     * Virtual destructor to silence compiler warnings.
     */    
    virtual ~SpecFileReader(){};
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















