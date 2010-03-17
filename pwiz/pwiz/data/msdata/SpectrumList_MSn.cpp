//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#define PWIZ_SOURCE

#include "SpectrumList_MSn.hpp"
#include <istream>
#include "boost/shared_ptr.hpp"
#include "References.hpp"
#include <limits>
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "zlib.h"


namespace pwiz {
namespace msdata {


using boost::shared_ptr;
using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;
using namespace std;


namespace {

// these are the fixed sizes used to write .bms2 and .cms2 files
// use these to read from files rather than sizeof(<type>)
const int sizeIntMSn       = 4;
const int sizeFloatMSn     = 4;   
const int sizeDoubleMSn    = 8; 
const int sizeChargeMSn    = 12; // struct Charge{ int z; double mass; }
const int sizePeakMSn      = 12; // struct Peak{ double mz; float intensity; }

struct MSnScanInfo
{
    int     scanNumber;
    double  mz;
    float   rTime;
    float   basePeakIntensity;
    double  basePeakMz;
    double  conversionFactorA;
    double  conversionFactorB;
    double  TIC;
    float   ionInjectionTime;
    int     numPeaks;
    int     numChargeStates;

    MSnScanInfo(): scanNumber(-1),
                   mz(-1),
                   rTime(-1),
                   basePeakIntensity(-1),
                   basePeakMz(-1),
                   conversionFactorA(-1),
                   conversionFactorB(-1),
                   TIC(-1),
                   ionInjectionTime(-1),
                   numPeaks(-1),
                   numChargeStates(-1) 
    {}

    void readSpectrumHeader(boost::shared_ptr<istream> is, int version) 
    {
        (*is).read(reinterpret_cast<char *>(&scanNumber), sizeIntMSn);
        (*is).read(reinterpret_cast<char *>(&scanNumber), sizeIntMSn); // yes, there are two
        (*is).read(reinterpret_cast<char *>(&mz), sizeDoubleMSn);
        (*is).read(reinterpret_cast<char *>(&rTime), sizeFloatMSn);

        if( version == 2 )
        {
            (*is).read(reinterpret_cast<char *>(&basePeakIntensity), sizeFloatMSn);
            (*is).read(reinterpret_cast<char *>(&basePeakMz), sizeDoubleMSn);
            (*is).read(reinterpret_cast<char *>(&conversionFactorA), sizeDoubleMSn); 
            (*is).read(reinterpret_cast<char *>(&conversionFactorB), sizeDoubleMSn); 
            (*is).read(reinterpret_cast<char *>(&TIC), sizeDoubleMSn);
            (*is).read(reinterpret_cast<char *>(&ionInjectionTime), sizeFloatMSn);   
        }

        (*is).read(reinterpret_cast<char *>(&numChargeStates), sizeIntMSn);
        (*is).read(reinterpret_cast<char *>(&numPeaks), sizeIntMSn);

    };
};


class SpectrumList_MSnImpl : public SpectrumList_MSn
{
  public:
  
  SpectrumList_MSnImpl(shared_ptr<std::istream> is, const MSData& msd, MSn_Type filetype)
    :   is_(is), msd_(msd), version_(0), filetype_(filetype)
  {
    switch( filetype_){
    case MSn_Type_MS2:
      createIndexText();
      break;
    case MSn_Type_CMS2:
      createIndexBinary();
      break;
    case MSn_Type_BMS2:
      createIndexBinary();
      break;
    case MSn_Type_UNKNOWN:
      throw runtime_error("[SpectrumList_MSn::constructor] Cannot create index for unknown MSn file type.");

    }
  }

  size_t size() const {return index_.size();}
  
  const SpectrumIdentity& spectrumIdentity(size_t index) const
  {
    return index_[index];
  }
  
  size_t find(const string& id) const
  {
    map<string, size_t>::const_iterator it = idToIndex_.find(id);
    return it != idToIndex_.end() ? it->second : size();
  }
  
  size_t findNative(const string& nativeID) const
  {
    size_t index;
    try
    {
      index = lexical_cast<size_t>(nativeID);
    }
    catch (boost::bad_lexical_cast&)
    {
      throw runtime_error("[SpectrumList_MSn::findNative] invalid nativeID format (expected a positive integer)");
    }
    
    if (index < size())
      return index;
    else
      return size();
  }
  
  SpectrumPtr spectrum(size_t index, bool getBinaryData) const
  {
    if (index > index_.size())
      throw runtime_error("[SpectrumList_MSn::spectrum] Index out of bounds");
    
    // allocate Spectrum object and read it in
    SpectrumPtr result(new Spectrum);
    if (!result.get())
      throw runtime_error("[SpectrumList_MSn::spectrum] Out of memory");
    
    result->index = index;
    result->sourceFilePosition = index_[index].sourceFilePosition;
    
    is_->seekg(bio::offset_to_position(result->sourceFilePosition));
    if (!*is_)
      throw runtime_error("[SpectrumList_MSn::spectrum] Error seeking to spectrum index " + 
                          lexical_cast<string>(index));
    
    if( filetype_ == MSn_Type_MS2 ){
      parseSpectrumText(*result, getBinaryData);
    }else{
      parseSpectrumBinary(*result, getBinaryData);
    }
    // resolve any references into the MSData object
    References::resolve(*result, msd_);
    
    return result;
  }
  
  private:
  shared_ptr<istream> is_;
  const MSData& msd_;
  vector<SpectrumIdentity> index_;
  map<string, size_t> idToIndex_;
  int version_; // read from fileheader for bms2 and cms2 filetypes
  MSn_Type filetype_;

  void parseSpectrumText(Spectrum& spectrum, bool getBinaryData) const
  {
    // Every MS2 spectrum is assumed to be:
    // * MSn spectrum
    // * MS level 2
    // * a peak list (centroided)
    // * not deisotoped (even though it may actually be, there's no way to tell)
    
    spectrum.set(MS_MSn_spectrum);
    spectrum.set(MS_ms_level, 2);
    spectrum.set(MS_centroid_spectrum);
    
    spectrum.precursors.push_back(Precursor());
    Precursor& precursor = spectrum.precursors.back();
    precursor.selectedIons.push_back(SelectedIon());
    SelectedIon& selectedIon = precursor.selectedIons.back();

    string lineStr;
    bool inPeakList = false;
    double lowMZ = std::numeric_limits<double>::max();
    double highMZ = 0;
    double tic = 0;
    double basePeakMZ = 0;
    double basePeakIntensity = 0;
    spectrum.defaultArrayLength = 0;
    spectrum.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
    vector<double>& mzArray = spectrum.getMZArray()->data;
    vector<double>& intensityArray = spectrum.getIntensityArray()->data;
    double precursor_mz = 0;
    
    // start reading the file
    if( getline(*is_, lineStr) )	// not end of file
    {
        // confirm that the first line is an S line
        if (lineStr.find("S") != 0)
        {
            throw runtime_error(("[SpectrumList_MSn::parseSpectrum] S line found mixed "
                             "with other S/Z/I/D lines at offset " +
                             lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
        } 
      
        // read in the scan number
        size_t first_num_pos = lineStr.find_first_of("0123456789");
        size_t second_space_pos = lineStr.find_first_of(" \t", first_num_pos);
        int scanNum = lexical_cast<int>(lineStr.substr(first_num_pos, second_space_pos-first_num_pos+1));
        spectrum.id = "scan=" + lexical_cast<string>(scanNum);

        // read in the precursor mz
        size_t last_num_pos = lineStr.find_last_of("0123456789");
        size_t last_space_pos = lineStr.find_last_of(" \t", last_num_pos);
        precursor_mz = lexical_cast<double>(lineStr.substr(last_space_pos, last_num_pos-last_space_pos+1));
        selectedIon.set(MS_selected_ion_m_z, precursor_mz, MS_m_z);
    }
    else // eof, exit
    {
      // clean up?
      return;
    }
    
    // We may have multiple charges, so build a list
    vector<int> charges;

    // read in remainder of spectrum
    while (getline(*is_, lineStr))
    {
        if (lineStr.find("S") == 0) // we are at the next spectrum
        {
            if (!inPeakList)
            {
                // the spec had no peaks, clean up?
            }
            
            break; // stop reading file
        }
        else if (lineStr.find("Z") == 0)
        {
            if (inPeakList)
            {
                throw runtime_error(("[SpectrumList_MSn::parseSpectrum] Z line found without S line at offset " +
                               lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
            }
            
            // This is where we would get the charge state, but unless the file
            // has been processed for charge state determination (e.g. Hardklor), 
            // it's not really known.  Thus, we need to use "possible charges".
            
            size_t first_space_pos = lineStr.find_first_of(" \t");
            size_t first_num_pos = lineStr.find_first_of("0123456789", first_space_pos);
            charges.push_back(lexical_cast<int>(lineStr.substr(first_num_pos, 1)));  // assume one digit
        }
        else if (lineStr.find("I") == 0)
        {
            if (inPeakList)
            {
                throw runtime_error(("[SpectrumList_MSn::parseSpectrum] I line found without S line at offset " +
                               lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
            }
            
            // else
            if(lineStr.find("RTime") != string::npos)
            {
                // get the retention time
                size_t last_num_pos = lineStr.find_last_of("0123456789");
                size_t last_space_pos = lineStr.find_last_of(" \t", last_num_pos);
                size_t len = last_num_pos - last_space_pos;

                double rt = lexical_cast<double>(lineStr.substr(last_space_pos + 1, len));
                spectrum.scanList.scans.push_back(Scan());
                spectrum.scanList.scans.back().set(MS_scan_start_time, rt, UO_second);
            }
        }
        else if (lineStr.find("D") == 0)
        {
            // ignore D lines for now
            if (inPeakList)
            {
                throw runtime_error(("[SpectrumList_MSn::parseSpectrum] D line found without S line at offset " +
                               lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
            }
        }
        else
        {
            inPeakList = true;
        
            // always parse the peaks (intensity must be summed to build TIC)
            size_t delim = lineStr.find_first_of(" \t");
            if(delim == string::npos)
            {
                continue;
            }
        
            size_t delim2 = lineStr.find_first_not_of(" \t", delim+1);
            if(delim2 == string::npos)
            {
                continue;
            }

            size_t delim3 = lineStr.find_first_of(" \t\r\n", delim2);
            if(delim3 == string::npos)
            {
                delim3 = lineStr.length();
            }
        
            double mz = lexical_cast<double>(lineStr.substr(0, delim));
            double inten = lexical_cast<double>(lineStr.substr(delim2, delim3-delim2));
            tic += inten;
            if (inten > basePeakIntensity)
            {
                basePeakMZ = mz;
                basePeakIntensity = inten;
            }
            
            lowMZ = std::min(lowMZ, mz);
            highMZ = std::max(highMZ, mz);
        
            ++spectrum.defaultArrayLength;
            
            if (getBinaryData)
            {
                mzArray.push_back(mz);
                intensityArray.push_back(inten);
            }
        }// header vs peaks
    }// read next line
    
    // If we have only ONE charge state, we read it in as "MS_charge_state";
    // otherwise, the charge states are all read as "MS_possible_charge_state"
    size_t numCharges = charges.size();
    if (1 == numCharges)
    {
        selectedIon.set(MS_charge_state, charges[0]);
    }
    else
    {
        for (size_t i = 0; i < numCharges; i++)
        {
            precursor.selectedIons.back().cvParams.push_back(CVParam(MS_possible_charge_state, charges[i]));
        }
    }

    spectrum.set(MS_lowest_observed_m_z, lowMZ);
    spectrum.set(MS_highest_observed_m_z, highMZ);
    spectrum.set(MS_total_ion_current, tic);
    spectrum.set(MS_base_peak_m_z, basePeakMZ);
    spectrum.set(MS_base_peak_intensity, basePeakIntensity);

  }
  
  void parseSpectrumBinary(Spectrum& spectrum, bool getBinaryData) const
  {
    // Every MSn spectrum is assumed to be:
    // * MSn spectrum
    // * MS level 2
    // * a peak list (centroided)
    // * not deisotoped (even though it may actually be, there's no way to tell)
    
    spectrum.set(MS_MSn_spectrum);
    spectrum.set(MS_ms_level, 2);
    spectrum.set(MS_centroid_spectrum);

    spectrum.precursors.push_back(Precursor());
    Precursor& precursor = spectrum.precursors.back();
    precursor.selectedIons.push_back(SelectedIon());
    SelectedIon& selectedIon = precursor.selectedIons.back();
    
    MSnScanInfo scanInfo;
    scanInfo.readSpectrumHeader(is_, version_);

    spectrum.id = "scan=" + lexical_cast<string>(scanInfo.scanNumber);
    selectedIon.set(MS_selected_ion_m_z, scanInfo.mz, MS_m_z);

    // get charge states
    int charge = 0;
    double mass = 0;

    // If we have only ONE charge state, we read it in as "MS_charge_state";
    // otherwise, the charge states are all read as "MS_possible_charge_state"
    if (1 == scanInfo.numChargeStates)
    {
        (*is_).read(reinterpret_cast<char *>(&charge), sizeIntMSn);
        selectedIon.set(MS_charge_state, charge);

        (*is_).read(reinterpret_cast<char *>(&mass), sizeDoubleMSn);
    }
    else
    {
        for(int i=0; i<scanInfo.numChargeStates; i++)
        {
          (*is_).read(reinterpret_cast<char *>(&charge), sizeIntMSn);
          precursor.selectedIons.back().cvParams.push_back(CVParam(MS_possible_charge_state, charge));

          (*is_).read(reinterpret_cast<char *>(&mass), sizeDoubleMSn);

          // add another selected ion
        }
    }

    // get retention time
    spectrum.scanList.scans.push_back(Scan());
    spectrum.scanList.scans.back().set(MS_scan_start_time, scanInfo.rTime, UO_second);

    double* mzs = NULL;
    float* intensities = NULL;
    if( filetype_ == MSn_Type_CMS2 )// get compression info
    {
      int iTemp;
      
      // get length of compressed array of m/z
      (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
      uLong mzLen = (unsigned long)iTemp;
      
      // get length of compressed array of intensities
      (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
      uLong intensityLen = (unsigned long)iTemp;
      
      // allocate a buffer for storing the compressed data from file
      Byte* compressedData = new Byte[mzLen];
      
      // allocate a buffer for the uncompressed version 
      mzs = new double[scanInfo.numPeaks];
      uLong uncompressedLen = scanInfo.numPeaks * sizeDoubleMSn;
      
      (*is_).read(reinterpret_cast<char *>(compressedData), mzLen);
      
      int success = uncompress((Bytef*)mzs, &uncompressedLen, compressedData, mzLen);

      if( success != Z_OK )
        throw runtime_error("[SpectrumList_MSn::parseSpectrum] Error uncompressing peaks.");
      
      // repeat for intensities
      delete [] compressedData;
      compressedData = new Byte[intensityLen];
      intensities = new float[scanInfo.numPeaks];
      uncompressedLen = scanInfo.numPeaks * sizeFloatMSn;
      (*is_).read(reinterpret_cast<char *>(compressedData), intensityLen);
      
      success = uncompress((Bytef*)intensities, &uncompressedLen, compressedData, intensityLen);
      
      if( success != Z_OK )
        throw runtime_error("[SpectrumList_MSn::parseSpectrum] Error uncompressing peaks.");
      
      delete [] compressedData;
    }

    // always get the peaks to find lowMZ, highMZ
    double lowMZ = std::numeric_limits<double>::max();
    double highMZ = 0;
    double tic = 0;
    double basePeakMZ = 0;  // we may already have these but we have to read the peaks anyway...
    double basePeakIntensity = 0;
    spectrum.defaultArrayLength = 0;
    spectrum.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
    vector<double>& mzArray = spectrum.getMZArray()->data;
    vector<double>& intensityArray = spectrum.getIntensityArray()->data;
    double mz = 0;
    float intensity = 0;

    for(int i=0; i<scanInfo.numPeaks; i++){
      if( filetype_ == MSn_Type_CMS2 )
      {
        intensity = intensities[i];
        mz = mzs[i];
      }else //MSn_Type_BMS2
      {
        (*is_).read(reinterpret_cast<char *>(&mz), sizeDoubleMSn);
        (*is_).read(reinterpret_cast<char *>(&intensity), sizeFloatMSn);
      }

      tic += intensity;
      if( intensity > basePeakIntensity)
      {
        basePeakMZ = mz;
        basePeakIntensity = intensity;
      }
      
      lowMZ = std::min(lowMZ, mz);
      highMZ = std::max(highMZ, mz);
      
      ++spectrum.defaultArrayLength;
      if (getBinaryData)
      {
        mzArray.push_back(mz);
        intensityArray.push_back(intensity);
      }
      
    }// next peak
    
    // done reading the file
    
    delete [] intensities;
    delete [] mzs;

    spectrum.set(MS_lowest_observed_m_z, lowMZ);
    spectrum.set(MS_highest_observed_m_z, highMZ);
    spectrum.set(MS_total_ion_current, tic);
    spectrum.set(MS_base_peak_m_z, basePeakMZ);
    spectrum.set(MS_base_peak_intensity, basePeakIntensity);
    
  }
  
  void createIndexText()
  {
    string lineStr;
    size_t lineCount = 0;
    map<string, size_t>::iterator curIdToIndexItr;
    
    while (getline(*is_, lineStr))
    {
      ++lineCount;
      if (lineStr.find("S") == 0)
      {
        // beginning of spectrum, get the scan number
        // format: 'S <scanNum> <scanNum> <precursor mz>'
        int scanNum = 0;
        if( sscanf(lineStr.c_str(), "S %d", &scanNum) != 1 ){
          throw runtime_error(("[SpectrumList_MSn::createIndex] Did not find scan number at offset " +
                               lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + ": " 
                               + lineStr + "\n"));
          
        }
        
        // create a new SpectrumIdentity and put it on the list
        index_.push_back(SpectrumIdentity());
        // get a pointer to the current identity

        SpectrumIdentity& curIdentity = index_.back();
        curIdentity.index = index_.size()-1;
        curIdentity.id = "scan=" + lexical_cast<string>(scanNum);
        curIdentity.sourceFilePosition = size_t(is_->tellg())-lineStr.length()-1;
        curIdToIndexItr = idToIndex_.insert(pair<string, size_t>(curIdentity.id, index_.size()-1)).first;  
      }
    }// next line
    is_->clear();
    is_->seekg(0);
  }

  void createIndexBinary()
  {
    map<string, size_t>::iterator curIdToIndexItr;
    
    // header information
    int intFileType = 0;
    MSnHeader header;
    
    (*is_).read(reinterpret_cast<char *>(&intFileType), sizeIntMSn);
    (*is_).read(reinterpret_cast<char *>(&version_), sizeIntMSn);
    (*is_).read(reinterpret_cast<char *>(&header), sizeof(MSnHeader));

    // temp varabiles for each scan
    MSnScanInfo scanInfo;
    
    // until we get to the end of the file...
    while( true )
    {
      // keep track of where we are at the beginning of spectrum
      streampos specBegin = is_->tellg();
      
      scanInfo.readSpectrumHeader(is_, version_);
      
      if( !*is_ ){
        break;
      }

      // create a new SpectrumIdentity and put it on the list
      index_.push_back(SpectrumIdentity());
      // get a pointer to the current identity
      SpectrumIdentity& curIdentity = index_.back();
      curIdentity.index = index_.size()-1;
      curIdentity.id = "scan=" + lexical_cast<string>(scanInfo.scanNumber); 
      curIdentity.sourceFilePosition = size_t(specBegin); 
      curIdToIndexItr = idToIndex_.insert(pair<string, size_t>(curIdentity.id, index_.size()-1)).first;  
      
      // skip to next spec
      if( filetype_ == MSn_Type_CMS2 ){
        // skip the charge states
        (*is_).seekg(scanInfo.numChargeStates * sizeChargeMSn, std::ios_base::cur); 
        // skip the peaks, first find out how far
        int iTemp;
        unsigned long mzLen, intensityLen;
        (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
        mzLen = (unsigned long)iTemp;
        (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
        intensityLen = (unsigned long)iTemp;
        
        (*is_).seekg(mzLen + intensityLen, std::ios_base::cur); 
      }else if( filetype_ == MSn_Type_BMS2 ){
        // skip the charge states
        (*is_).seekg(scanInfo.numChargeStates * sizeChargeMSn, std::ios_base::cur); 
        // skip the peaks
        (*is_).seekg(scanInfo.numPeaks * sizePeakMSn, std::ios_base::cur);
        
      }

    }// next spectrum
      is_->clear();
      is_->seekg(0);
    }
};


} // namespace


SpectrumListPtr SpectrumList_MSn::create(boost::shared_ptr<std::istream> is,
                                         const MSData& msd,
                                         MSn_Type filetype)
{
    return SpectrumListPtr(new SpectrumList_MSnImpl(is, msd, filetype));
}


} // namespace msdata
} // namespace pwiz
