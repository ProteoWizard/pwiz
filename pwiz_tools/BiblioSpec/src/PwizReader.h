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
 *  The Proteowizard implementation of the SpecFileRader interface
 *  Does not yet support consecutive file reading (getNextSpec).
 */

#include <exception>
#include "Verbosity.h"
#include "BlibUtils.h"
#include "SpecFileReader.h"
#include "Spectrum.h"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"

// NOTE:  This adds about 600 KB to the size of the binary, which should only be done
//        if it is of benefit.  It did not benefit BlibBuild, and so was made conditional.
#ifdef VENDOR_READERS
#ifdef _MSC_VER
#include "pwiz_tools/common/FullReaderList.hpp"
#endif
#endif
using namespace pwiz::msdata;
using namespace boost;

class PwizReader : public BiblioSpec::SpecFileReader {
 public:
    PwizReader();  
    virtual ~PwizReader();
    virtual void setIdType(BiblioSpec::SPEC_ID_TYPE specIdType);
    
    /** 
     * Open the filename given and prepare to find spectra.
     * Throw error if file not found, can't be opened, wrong type.
     */ 
    virtual void openFile(const char* filename, bool mzSort = false);
    
    /**
     * Read file to find the spectrum corresponding to identifier.
     * Fill in mz and numPeaks in returnData
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(int identifier, 
                             BiblioSpec::SpecData& returnData, 
                             BiblioSpec::SPEC_ID_TYPE findBy,
                             bool getPeaks);

    // TODO: have this assume the identifier is the native id string
    // and look up the spectrum based on that.
    virtual bool getSpectrum(string identifier, 
                             BiblioSpec::SpecData& returnData, 
                             bool getPeaks = true);
    /**
     * Get the next spectrum in the file according to the index.
     * Returns false if no spectra are left in the file.
     *
     * Calls to getNextSpectrum() can be interspersed with calls to
     * getSpectrum(scanNum) without changing the order of spectra
     * returned by getNextSpectrum().
     * 
     */
    virtual bool getNextSpectrum(BiblioSpec::SpecData& returnData, 
                                 bool getPeaks);

    /**
     * Read file to find the spectrum corresponding to identifier.
     * Fill in mz and numPeaks in spectrum.
     * Allocate arrays for mzs and intensities and fill with peak
     * values. 
     * Return true if spectrum found and successfully parsed, false if
     * spec not in file.
    */
    virtual bool getSpectrum(int identifier, 
                             BiblioSpec::Spectrum& returnSpectrum, 
                             BiblioSpec::SPEC_ID_TYPE findBy,
                             bool getPeaks);

    bool getNextSpectrum(BiblioSpec::Spectrum& spectrum);

 private:
    string fileName_;
    #ifdef _MSC_VER
#ifdef VENDOR_READERS
    FullReaderList allReaders_;
#endif
    #endif
    MSDataFile* fileReader_;
    CVID nativeIdFormat_;
    SpectrumListPtr allSpectra_;
    size_t curPositionInIndexMzPairs_;
    vector< pair<int,double> > indexMzPairs_; // scan/pre-mz pairs, may besorted byeither
    BiblioSpec::SPEC_ID_TYPE idType_;


    /**
     * Return the index of the next spectrum (as indexed in the file)
     * to fetch and update the current position in the list of
     * index-mz pairs. 
     */
    int getNextSpecIndex();

    /**
     * Find the index of the spectrum with the given identifier in the
     * current file.
     */
    size_t getSpecIndex(int identifier, BiblioSpec::SPEC_ID_TYPE findBy);

    /**
     * Find the index of the spectrum with the given string
     * identifier.  Try the string as a native id and as a TITLE=
     * field for .mgf.
     */
    int getSpecIndex(string identifier);

    /**
     * Add any charge states from the pwiz spectrum to the BiblioSpec
     * spectrum.  Look in all precursors, all SelectedIons, and all
     * CVParams for both charge_state and possible_charge_state.
     */
    void addCharges(BiblioSpec::Spectrum& returnSpectrum, 
                    SpectrumPtr foundSpec);

    /**
     * Copy the information from the Pwiz spectrum to the BiblioSpec
     * SpecData.
     */
    void transferSpec(BiblioSpec::SpecData& returnData, 
                      auto_ptr<SpectrumInfo>& specInfo);

    /**
     * Copy the information from the Pwiz spectrum to the BiblioSpec spectrum.
     */
    void transferSpectrum(BiblioSpec::Spectrum& returnSpectrum, 
                          auto_ptr<SpectrumInfo>& specInfo, 
                          SpectrumPtr foundSpec);

};


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















