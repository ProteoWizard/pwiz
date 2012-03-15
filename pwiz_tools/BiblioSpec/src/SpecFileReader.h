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

/**
 *  The SpecFileRader is an interface for the Proteowizard,
 *  ms_parser and any other package used for reading files
 *  containing spectra.  It is used by BuildParser and BlibSearch.
 */

#include "Verbosity.h"
#include "PSM.h"

namespace BiblioSpec {

struct SpecData{
    int id;
    double retentionTime;
    double mz;
    int numPeaks;
    double* mzs;
    float* intensities;
    
    SpecData():
    id(0), retentionTime(0), mz(0), numPeaks(-1){
        mzs = NULL;
        intensities = NULL;
    };

    ~SpecData(){
        delete [] mzs;
        delete [] intensities;
    };

    SpecData& operator=(SpecData& rhs){
        id = rhs.id;
        retentionTime = rhs.retentionTime;
        mz = rhs.mz;
        numPeaks = rhs.numPeaks;

        // clear any existing peaks
        delete [] mzs;
        delete [] intensities;
        mzs = NULL;
        intensities = NULL;

        if( numPeaks){
            mzs = new double[numPeaks];
            intensities = new float[numPeaks];
            for(int i=0; i<numPeaks; i++){
                mzs[i] = rhs.mzs[i];
                intensities[i] = rhs.intensities[i];
            }   
        }
        return *this;
    }
};

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
                             bool getPeaks){    
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
















