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

/**
 *  The Proteowizard implementation of the SpecFileRader interface
 *  Does not yet support consecutive file reading (getNextSpec).
 */

#include "PwizReader.h"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"

using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace boost;

PwizReader::PwizReader() : curPositionInIndexMzPairs_(0)  {
    BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, 
                                   "Creating PwizReader.");
    fileReader_ = NULL;
    idType_ = BiblioSpec::SCAN_NUM_ID;
}

PwizReader::~PwizReader(){
    delete fileReader_;
}

void PwizReader::setIdType(BiblioSpec::SPEC_ID_TYPE specIdType) {
    idType_ = specIdType;
}
    
/** 
 * Open the filename given and prepare to find spectra.
 * Throw error if file not found, can't be opened, wrong type.
 */ 
void PwizReader::openFile(const char* filename, bool mzSort){
    BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, 
                                   "PwizReader preparing file '%s'.",
                                   filename);
    try {
        fileName_ = filename;
        delete fileReader_;
        #ifdef VENDOR_READERS
        fileReader_ = new MSDataFile(fileName_, &allReaders_);
        if (SpectrumList_PeakPicker::supportsVendorPeakPicking(fileName_))
            SpectrumListFactory::wrap(*fileReader_, "peakPicking true 1-");
        #else
        fileReader_ = new MSDataFile(fileName_);
        #endif
        allSpectra_ = fileReader_->run.spectrumListPtr;

        if( allSpectra_->size() == 0 ){
            BiblioSpec::Verbosity::error("No spectra found in %s.",
                filename);
        }
        BiblioSpec::Verbosity::debug("Found %d spectra in %s.",
                                     allSpectra_->size(), filename);
        nativeIdFormat_ = id::getDefaultNativeIDFormat(*fileReader_);
        if (nativeIdFormat_ == MS_no_nativeID_format)   // This never works
            nativeIdFormat_ = MS_scan_number_only_nativeID_format;
        
        const auto& nativeIdFormatInfo = cvTermInfo(nativeIdFormat_);
        BiblioSpec::Verbosity::debug("PwizReader lookup method is %s, nativeIdFormat is %s", specIdTypeToString(idType_), nativeIdFormatInfo.shortName().c_str());

        // HACK!  Without this block, I get an index out of bounds
        // error in getSpectrum(1, data, INDEX_ID)
        // With this block, I get errors for getSpectrum(n,data,SCAN_NUM_ID)
        // TODO: find out why look-up by index breaks when
        // non-sequential and remove this
        if( idType_ == BiblioSpec::INDEX_ID ){ 
            for(size_t i=0; i < allSpectra_->size(); i++){
                SpectrumPtr spec = allSpectra_->spectrum(i, false);
                if( spec == NULL ){
                    BiblioSpec::Verbosity::error(
                                 "Couldn't fetch spectrum index %d after "
                                 "opening file %s for lookup by index.",
                                 i, fileName_.c_str());
                } 
                if(lexical_cast<int>(spec->cvParam(MS_ms_level).value) != 2){
                    continue;
                }
                double mz = spec->precursors[0].selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>();
                pair<int,double> scan_mz(i,mz);
                indexMzPairs_.push_back(scan_mz);
                BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, 
                                               "Indexed scan %d, mz %f",
                                               i, mz);
            }
            // in case there were no MS2 spectra found
            if(indexMzPairs_.empty()){
                BiblioSpec::Verbosity::error("No MS/MS spectra found in %s.",
                                            fileName_.c_str());
            }
        
        } // end index chananagins

        
        if( mzSort ){
            sort(indexMzPairs_.begin(), indexMzPairs_.end(), 
                 compareSecond<int,double>);
        }
        
    } catch (std::exception& e){
        std::cerr << "ERROR: " << e.what() << "." << std::endl;
        // why does this cause a segfault??
        //BiblioSpec::throwError("PwizReader could not parse %s",fileName_.c_str()); 
        BiblioSpec::Verbosity::error("PwizReader could not parse %s", 
                                     fileName_.c_str());
    } catch (...){
        BiblioSpec::Verbosity::error("PwizReader could not parse %s", 
                                     fileName_.c_str());
    }
    
}

/**
 * Read file to find the spectrum corresponding to identifier.
 * Fill in mz and numPeaks in returnData
 * Allocate arrays for mzs and intensities and fill with peak
 * values. 
 * Return true if spectrum found and successfully parsed, false if
 * spec not in file.
 */
bool PwizReader::getSpectrum(int identifier, 
                             BiblioSpec::SpecData& returnData, 
                             BiblioSpec::SPEC_ID_TYPE findBy,
                             bool getPeaks)
{
    BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, "PwizReader looking for %s %d.", specIdTypeToString(findBy), identifier);
    
    size_t foundIndex = getSpecIndex(identifier, findBy);
    
    if( foundIndex >= allSpectra_->size()){ // already warned
        return false;
    }
    
    // read the spectrum from the file
    SpectrumPtr foundSpec = allSpectra_->spectrum(foundIndex, getPeaks);
    
    if( foundSpec == NULL ){
        return false;
    }
    unique_ptr<SpectrumInfo> specInfo(new SpectrumInfo());
    specInfo->SpectrumInfo::update(*foundSpec, getPeaks);
    
    // confirm that it's an ms/ms spectrum
    if( specInfo->msLevel != 2 ){
        BiblioSpec::Verbosity::warn("Spectrum %d is level %d, not 2.",
                                    identifier, specInfo->msLevel);
        return false;
    }
    
    transferSpec(returnData, specInfo);
    
    return true;
}

/**
 * Find an index for this spectrum identifier, either as a native id
 * string or as a TITLE= field.
 * \returns The found index or -1 if not found.
 */
int PwizReader::getSpecIndex(string identifier){
    static bool lookUpByNative = true; // remember this value for next time

    int timesLooked = 0;
    int foundIndex = -1;
    while( timesLooked < 2 && foundIndex == -1 ){
        switch(lookUpByNative){
        case(1):
            if (identifier.find('=') == string::npos){
                foundIndex = -1;
                lookUpByNative = !lookUpByNative; // try other method
            } else {
                foundIndex = allSpectra_->find(identifier);
                if (foundIndex == (int)allSpectra_->size()) {// not found
                    foundIndex = -1;
                    lookUpByNative = !lookUpByNative; // try other method
                }
            }
            timesLooked++;
            break;
            
        case(0):
            IndexList list = allSpectra_->findSpotID(identifier);
            if( list.size() == 1 ){        // found
                foundIndex = list[0];
            } else if( list.size() > 1 ){  // error
                BiblioSpec::Verbosity::error("Multiple spectra found with "
                                             "TITLE='%s'.", identifier.c_str());
            } else { // list is empty      // not found
                foundIndex = -1;
                lookUpByNative = !lookUpByNative; // try other method
            }
            timesLooked++;
            break;
        }
    }

    return foundIndex;
}

/**
 * Retrieve a spectrum from file with the given id string.  May be
 * either native id or, for .mgf, the TITLE= field.  
 * \returns True if spectrum is found, else false.
 */
bool PwizReader::getSpectrum(string identifier, 
                             BiblioSpec::SpecData& returnData, 
                             bool getPeaks){
    int foundIndex = getSpecIndex(identifier);
    BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, "PwizReader looking for id %s.", identifier.c_str());
    return getSpectrum(foundIndex, returnData, BiblioSpec::INDEX_ID, getPeaks);
}

/**
 * Get the next spectrum in the file according to the index.
 * Returns false if no spectra are left in the file.
 *
 * Calls to getNextSpectrum() can be interspersed with calls to
 * getSpectrum(scanNum) without changing the order of spectra
 * returned by getNextSpectrum().
 * 
 */
bool PwizReader::getNextSpectrum(BiblioSpec::SpecData& returnData, 
                                         bool getPeaks){
    
    bool success = false;
    size_t nextIndex = getNextSpecIndex();
    if( nextIndex < allSpectra_->size() ){
        if( getSpectrum(nextIndex, returnData, 
                        BiblioSpec::INDEX_ID, getPeaks) ){
            success = true;
        } else {
            BiblioSpec::Verbosity::warn("Could not fetch spectrum at "
                                        "index %d even though there should "
                                        "be %d spec in the file.", 
                                        nextIndex, allSpectra_->size());
            success = false;
        }
    }  // else already read last spec 
    
    return success;
}

/**
 * Read file to find the spectrum corresponding to identifier.
 * Fill in mz and numPeaks in spectrum.
 * Allocate arrays for mzs and intensities and fill with peak
 * values. 
 * Return true if spectrum found and successfully parsed, false if
 * spec not in file.
 */
bool PwizReader::getSpectrum(int identifier, 
                                     BiblioSpec::Spectrum& returnSpectrum, 
                                     BiblioSpec::SPEC_ID_TYPE findBy,
                                     bool getPeaks)
{
    BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL, "PwizReader looking for %s %d.", specIdTypeToString(findBy), identifier);
    
    size_t foundIndex = getSpecIndex(identifier, findBy);
    
    if( foundIndex >= allSpectra_->size()){ // already warned
        return false;
    }
    
    // read spectrum from file
    SpectrumPtr foundSpec = allSpectra_->spectrum(foundIndex, getPeaks);
    
    if( foundSpec == NULL ){
        return false;
    }
    unique_ptr<SpectrumInfo> specInfo(new SpectrumInfo());
    specInfo->SpectrumInfo::update(*foundSpec, getPeaks);
    
    // confirm that it's an ms/ms spectrum
    if( specInfo->msLevel != 2 ){
        BiblioSpec::Verbosity::warn("Spectrum %d is level %d, not 2.",
                                    identifier, specInfo->msLevel);
        returnSpectrum.clear();
        return false;
    }
    
    transferSpectrum(returnSpectrum, specInfo, foundSpec);

    // a hack for spectra without a scan number in file
    if( returnSpectrum.getScanNumber() == 0 ){
        returnSpectrum.setScanNumber(foundIndex);
    }
    
    return true;
}


bool PwizReader::getNextSpectrum(BiblioSpec::Spectrum& spectrum){
    bool success = false;
    size_t curIndex = getNextSpecIndex();
    if( curIndex < allSpectra_->size() ){
        if( getSpectrum(curIndex, spectrum, 
                        BiblioSpec::INDEX_ID, true) ){ // do get peaks
            success = true;
        } else {
            BiblioSpec::Verbosity::warn("Could not fetch spectrum at "
                                        "index %d even though there should "
                                        "be %d spec in the file.", 
                                        curIndex, allSpectra_->size());
            // look for the next one
            return getNextSpectrum(spectrum);
        }
    }  // else already read last spec 
    
    return success;
}

/**
 * Return the index of the next spectrum (as indexed in the file)
 * to fetch and update the current position in the list of
 * index-mz pairs. 
 */
int PwizReader::getNextSpecIndex(){
    if( curPositionInIndexMzPairs_ >= indexMzPairs_.size() ){ // no more to return
        return allSpectra_->size();
    }
    int indexToReturn = indexMzPairs_[curPositionInIndexMzPairs_].first;
    curPositionInIndexMzPairs_++;
    return indexToReturn;
}

/**
 * Find the index of the spectrum with the given identifier in the
 * current file.
 */
size_t PwizReader::getSpecIndex(int identifier, 
                                BiblioSpec::SPEC_ID_TYPE findBy){
    
    if( findBy == BiblioSpec::INDEX_ID ){
        if( identifier >= (int)allSpectra_->size()) { 
            BiblioSpec::Verbosity::warn("Given index, %d, is out of range "
                                        " (%d spec in file).", identifier,
                                        allSpectra_->size());
        }
        return identifier;
    } // else, identifier is a scan number
    
    // turn the scan number into a nativeId string
    string idString = id::translateScanNumberToNativeID(nativeIdFormat_,
                                  boost::lexical_cast<string>(identifier));
    
    // find the index of the spectrum with this scan number
    size_t foundIndex = allSpectra_->find(idString);
    
    if( foundIndex == allSpectra_->size() ){
        BiblioSpec::Verbosity::comment(BiblioSpec::V_DETAIL,
                                       "Could not find scan number %d, "
                                       "native id '%s' in %s.", identifier,
                                       idString.c_str(), fileName_.c_str());
    }
    return foundIndex;
}

/**
 * Add any charge states from the pwiz spectrum to the BiblioSpec
 * spectrum.  Look in all precursors, all SelectedIons, and all
 * CVParams for both charge_state and possible_charge_state.
 */
void PwizReader::addCharges(BiblioSpec::Spectrum& returnSpectrum, 
                            SpectrumPtr foundSpec){
    
    // for each precursor
    for(size_t prec_i = 0; prec_i < foundSpec->precursors.size(); prec_i++){
        const Precursor& cur_prec = foundSpec->precursors[prec_i];
        
        // for each selected ion
        for(size_t ion_i=0; ion_i < cur_prec.selectedIons.size(); ion_i++){
            const SelectedIon& cur_ion = cur_prec.selectedIons[ion_i];
            //for each param
            for(size_t param_i=0; param_i < cur_ion.cvParams.size(); param_i++){
                const CVParam& param = cur_ion.cvParams[param_i];
                if( param.cvid == MS_possible_charge_state ){
                    returnSpectrum.addCharge(param.valueAs<int>());
                } else if( param.cvid == MS_charge_state ){
                    returnSpectrum.addCharge(param.valueAs<int>());
                }
            } // next param
        } // next selected ion
    } // next precursor
}

/**
 * Copy the information from the Pwiz spectrum to the BiblioSpec
 * SpecData.
 */
void PwizReader::transferSpec(BiblioSpec::SpecData& returnData, 
                              unique_ptr<SpectrumInfo>& specInfo){
    
    returnData.id = specInfo->scanNumber;
    returnData.retentionTime = specInfo->retentionTime/60;  // seconds to minutes
    returnData.mz = specInfo->precursors[0].mz;
    returnData.numPeaks = specInfo->data.size();
    
    if( returnData.numPeaks > 0 ){
        returnData.mzs = new double[returnData.numPeaks];
        returnData.intensities = new float[returnData.numPeaks];
        
        for(int i = 0; i < returnData.numPeaks; i++){
            returnData.mzs[i] = specInfo->data.at(i).mz;
            returnData.intensities[i] = (float)specInfo->data.at(i).intensity;
        }
    } else {
        returnData.mzs = NULL;
        returnData.intensities = NULL;
    }
}

/**
 * Copy the information from the Pwiz spectrum to the BiblioSpec spectrum.
 */
void PwizReader::transferSpectrum(BiblioSpec::Spectrum& returnSpectrum, 
                                  unique_ptr<SpectrumInfo>& specInfo, 
                                  SpectrumPtr foundSpec){
    
    returnSpectrum.setScanNumber(specInfo->scanNumber);
    returnSpectrum.setMz(specInfo->precursors[0].mz);
    addCharges(returnSpectrum, foundSpec);
    
    if( specInfo->data.size() > 0 ){
        vector<BiblioSpec::PEAK_T> peaks;
        for(size_t i = 0; i < specInfo->data.size(); i++){
            BiblioSpec::PEAK_T p;
            p.mz = specInfo->data.at(i).mz;
            p.intensity = (float)specInfo->data.at(i).intensity;
            if( p.intensity > 0 ){
                peaks.push_back(p);
            }
        }
        returnSpectrum.setRawPeaks(peaks);
    }
}


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















