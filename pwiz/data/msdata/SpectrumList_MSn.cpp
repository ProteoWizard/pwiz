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
#include "References.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "zlib.h"
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {


using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;
using namespace pwiz::chemistry;
using namespace pwiz::util;


namespace {

// these are the fixed sizes used to write .bms1, .cms1, .bms2, and .cms2 files
// use these to read from files rather than sizeof(<type>)
const int sizeIntMSn       = 4;
const int sizeFloatMSn     = 4;   
const int sizeDoubleMSn    = 8; 
const int sizeChargeMSn    = 12; // struct Charge{ int z; double mass; }
const int sizeEzMSn        = 20; // struct EZState{ int z; double mass; float rTime; float area; }
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
    int     numEzStates;

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
                   numChargeStates(-1),
                   numEzStates(0) 
    {}

    void readSpectrumHeader(boost::shared_ptr<istream> is, int version) 
    {
        (*is).read(reinterpret_cast<char *>(&scanNumber), sizeIntMSn);
        (*is).read(reinterpret_cast<char *>(&scanNumber), sizeIntMSn); // yes, there are two
        (*is).read(reinterpret_cast<char *>(&mz), sizeDoubleMSn);
        (*is).read(reinterpret_cast<char *>(&rTime), sizeFloatMSn);

        if( version >= 2 )
        {
            (*is).read(reinterpret_cast<char *>(&basePeakIntensity), sizeFloatMSn);
            (*is).read(reinterpret_cast<char *>(&basePeakMz), sizeDoubleMSn);
            (*is).read(reinterpret_cast<char *>(&conversionFactorA), sizeDoubleMSn); 
            (*is).read(reinterpret_cast<char *>(&conversionFactorB), sizeDoubleMSn); 
            (*is).read(reinterpret_cast<char *>(&TIC), sizeDoubleMSn);
            (*is).read(reinterpret_cast<char *>(&ionInjectionTime), sizeFloatMSn);   
        }

        (*is).read(reinterpret_cast<char *>(&numChargeStates), sizeIntMSn);

        if( version == 3 )
        {
            (*is).read(reinterpret_cast<char *>(&numEzStates), sizeIntMSn);   
        }
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
    case MSn_Type_MS1:
    case MSn_Type_MS2:
      createIndexText();
      break;
    case MSn_Type_BMS1:
    case MSn_Type_CMS1:
    case MSn_Type_BMS2:
    case MSn_Type_CMS2:
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
    return it != idToIndex_.end() ? it->second : checkNativeIdFindResult(size(), id);
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
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
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
    
    if( filetype_ == MSn_Type_MS1 || filetype_ == MSn_Type_MS2 ){
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
  int version_; // read from fileheader for bms1, cms1, bms2, and cms2 filetypes
  MSn_Type filetype_;
  mutable boost::mutex readMutex;

  void parseSpectrumText(Spectrum& spectrum, bool getBinaryData) const
  {
    // Every MS1/MS2 spectrum is assumed to be:
    // * MSn spectrum
    // * MS level <n>
    // * a peak list (centroided)
    // * not deisotoped (even though it may actually be, there's no way to tell)

    bool ms1File = MSn_Type_MS1 == filetype_ || MSn_Type_BMS1 == filetype_ || MSn_Type_CMS1 == filetype_;
    
    spectrum.set(MS_MSn_spectrum);
    spectrum.set(MS_ms_level, (ms1File ? 1 : 2));
    spectrum.set(MS_centroid_spectrum);

    if (!ms1File)
    {
        spectrum.precursors.push_back(Precursor());
        Precursor& precursor = spectrum.precursors.back();
        precursor.selectedIons.push_back(SelectedIon());
    }

    string lineStr;
    bool inPeakList = false;
    double lowMZ = std::numeric_limits<double>::max();
    double highMZ = 0;
    double tic = 0;
    double basePeakMZ = 0;
    double basePeakIntensity = 0;
    spectrum.defaultArrayLength = 0;
    spectrum.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
    BinaryData<double>& mzArray = spectrum.getMZArray()->data;
    BinaryData<double>& intensityArray = spectrum.getIntensityArray()->data;
    double precursor_mz = 0;
    
    // start reading the file
    if(getlinePortable(*is_, lineStr) )	// not end of file
    {
        // confirm that the first line is an S line
        if (lineStr.find("S") != 0)
        {
            throw runtime_error(("[SpectrumList_MSn::parseSpectrum] S line found mixed "
                             "with other S/Z/I/D lines at offset " +
                             lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
        } 
      
        // read in the scan number
        size_t first_num_pos = lineStr.find_first_of("123456789");
        size_t second_space_pos = lineStr.find_first_of(" \t", first_num_pos);
        int scanNum = lexical_cast<int>(lineStr.substr(first_num_pos, second_space_pos-first_num_pos));
        spectrum.id = "scan=" + lexical_cast<string>(scanNum);

        // read in the precursor mz
        if (!ms1File)
        {
            size_t last_num_pos = lineStr.find_last_of("0123456789");
            size_t last_space_pos = lineStr.find_last_of(" \t", last_num_pos);
            precursor_mz = lexical_cast<double>(lineStr.substr(last_space_pos+1, last_num_pos-last_space_pos));
            // store precursor in the first selected ion if we do not have accurate mass data (below)
            Precursor& precursor = spectrum.precursors.back();
            precursor.isolationWindow.set(MS_isolation_window_target_m_z, precursor_mz, MS_m_z);
        }
    }
    else // eof, exit
    {
      // clean up?
      return;
    }
    
    // We may have multiple charges, so build a list
    vector<int> charges;
    // and we may have multiple charges with accurate mass
    vector< pair<int, double> > chargeMassPairs;

    // read in remainder of spectrum
    while (getlinePortable(*is_, lineStr))
    {
        if (lineStr.find("S") == 0) // we are at the next spectrum
        {
            // if (!inPeakList) // the spec had no peaks, clean up?
            break; // stop reading file
        }
        else if (lineStr.find("Z") == 0)
        {
            if (ms1File)
            {
                throw runtime_error(("[SpectrumList_MSn::parseSpectrum] Z line found in MS1 file at offset " +
                               lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
            }

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
            size_t next_space_pos = lineStr.find_first_of(" \t", first_num_pos);
            int charge = lexical_cast<int>(lineStr.substr(first_num_pos, next_space_pos-first_num_pos));
            charges.push_back(charge);

            size_t last_num_pos = lineStr.find_last_of("0123456789");
            size_t last_space_pos = lineStr.find_last_of(" \t", last_num_pos);
            double z_precursor_mz = calculateMassOverCharge(
                lexical_cast<double>(lineStr.substr(last_space_pos+1, last_num_pos-last_space_pos)), charge, 1);
            stringstream ss;
            ss << charge << ' ' << std::fixed << std::setprecision(4) << z_precursor_mz;
            // Store Z line information in UserParams, in the format "<charge> <m/z calculated from Z line mass>"
            spectrum.userParams.push_back(UserParam("ms2 file charge state", ss.str()));
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
                spectrum.scanList.scans.back().set(MS_scan_start_time, rt*60, UO_second);
            }
            else if (lineStr.find("EZ") != string::npos)
            {
              // get the charge and mass pair
              size_t num_start_pos = lineStr.find_first_of("123456789");
              size_t next_space_pos = lineStr.find_first_of(" \t", num_start_pos);
              size_t len = next_space_pos - num_start_pos;
              int charge = lexical_cast<int>(lineStr.substr(num_start_pos, len));

              num_start_pos = lineStr.find_first_of("0123456789", next_space_pos);
              next_space_pos = lineStr.find_first_of(" \t", num_start_pos);
              len = next_space_pos - num_start_pos;
              double mass = lexical_cast<double>(lineStr.substr(num_start_pos, len));
              chargeMassPairs.push_back(pair<int,double>(charge, mass));
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

    // if we got to the end of the file, clear the eof bit and return to beginning of file
    if( is_->eof() ){
       is_->clear();
       is_->seekg(0);
    }
    if (!ms1File)
    {
        Precursor& precursor = spectrum.precursors.back();
        // if no accurate masses, set charge as possible
        if (chargeMassPairs.empty())
        {
          size_t numCharges = charges.size();
          for (size_t i = 0; i < numCharges; i++)
          {
            precursor.selectedIons.back().cvParams.push_back(CVParam(MS_possible_charge_state, charges[i]));
          }
          precursor.selectedIons.back().set(MS_selected_ion_m_z, precursor_mz, MS_m_z);
        }
        else // create a new selected ion for each charge,mass pair
        {
          for(size_t i=0; i < chargeMassPairs.size(); i++)
          {
            const pair<int, double>& chargeMass = chargeMassPairs.at(i);
            precursor.selectedIons.back().cvParams.push_back(CVParam(MS_charge_state, chargeMass.first));
            precursor.selectedIons.back().userParams.emplace_back("accurate mass", toString(chargeMass.second), "xsd:double");
            precursor.selectedIons.back().set(MS_selected_ion_m_z, 
                                              calculateMassOverCharge(chargeMass.second, chargeMass.first,
                                                                      1), // this is a singly charged mass
                                              MS_m_z);
            precursor.selectedIons.push_back(SelectedIon());
          }
          // last ion added has no data
          precursor.selectedIons.pop_back();
        }
    }
    spectrum.set(MS_lowest_observed_m_z, lowMZ);
    spectrum.set(MS_highest_observed_m_z, highMZ);
    spectrum.set(MS_total_ion_current, tic);
    spectrum.set(MS_base_peak_m_z, basePeakMZ);
    spectrum.set(MS_base_peak_intensity, basePeakIntensity);

  }

  // Calcualte m/z given mass (neutral or charged) and charge
  double calculateMassOverCharge(double mass, int charge, int charges_on_mass /* = 0 for neutral mass */) const
  {
    double neutralMass = mass - (charges_on_mass * Proton);
    double mz = (neutralMass + (charge * Proton)) / charge;
    return mz;
  }

  void parseSpectrumBinary(Spectrum& spectrum, bool getBinaryData) const
  {
    // Every MSn spectrum is assumed to be:
    // * MSn spectrum
    // * MS level <n>
    // * a peak list (centroided)
    // * not deisotoped (even though it may actually be, there's no way to tell)

    bool ms1File = MSn_Type_MS1 == filetype_ || MSn_Type_BMS1 == filetype_ || MSn_Type_CMS1 == filetype_;
    
    spectrum.set(MS_MSn_spectrum);
    spectrum.set(MS_ms_level, (ms1File ? 1 : 2));
    spectrum.set(MS_centroid_spectrum);

    MSnScanInfo scanInfo;
    scanInfo.readSpectrumHeader(is_, version_);
    if (!ms1File)
    {
        spectrum.precursors.push_back(Precursor());
        Precursor& precursor = spectrum.precursors.back();
        precursor.selectedIons.push_back(SelectedIon());
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, scanInfo.mz, MS_m_z);
    }

    spectrum.id = "scan=" + lexical_cast<string>(scanInfo.scanNumber);

    // read in all the charge state information before adding it to the spectrum
    // get charge states from equivalent of Z lines
    int charge = 0;
    vector<int> charges;
    double mass = 0;

    if (!ms1File)
    {
        Precursor& precursor = spectrum.precursors.back();
        for(int i=0; i<scanInfo.numChargeStates; i++)
        {
            (*is_).read(reinterpret_cast<char *>(&charge), sizeIntMSn);
            charges.push_back(charge);
            (*is_).read(reinterpret_cast<char *>(&mass), sizeDoubleMSn);
        }

        // if there is no extended charge information, add the (possible) charges
        if( scanInfo.numEzStates == 0 )
        {
          for(int i=0; i<scanInfo.numChargeStates; i++)
          {
            precursor.selectedIons.back().cvParams.push_back(CVParam(MS_possible_charge_state, charges.at(i)));
          }
          precursor.selectedIons.back().set(MS_selected_ion_m_z, scanInfo.mz, MS_m_z);
        }
        else  // get extended charge informationfrom equivalent of EZ lines
        {
            for(int i=0; i<scanInfo.numEzStates; i++){
              int eCharge;
              double eMass;
              float pRTime;  // rTime of chromatogram peak from MS1 scans
              float pArea;   // area under chromatogram peak from MS1 scans
              (*is_).read(reinterpret_cast<char *>(&eCharge), sizeIntMSn);
              (*is_).read(reinterpret_cast<char *>(&eMass), sizeDoubleMSn);
              (*is_).read(reinterpret_cast<char *>(&pRTime), sizeFloatMSn);
              (*is_).read(reinterpret_cast<char *>(&pArea), sizeFloatMSn);
          
              // store each charge and accurate mass as a separate selected ion
              precursor.selectedIons.back().cvParams.push_back(CVParam(MS_charge_state, eCharge));
              precursor.selectedIons.back().userParams.emplace_back("accurate mass", toString(eMass), "xsd:double");
              precursor.selectedIons.back().set(MS_selected_ion_m_z, calculateMassOverCharge(eMass, eCharge, 1), MS_m_z);
              precursor.selectedIons.push_back(SelectedIon());
            }
            // last ion added was not populated
            precursor.selectedIons.pop_back();
        }
    }

    // get retention time
    spectrum.scanList.scans.push_back(Scan());
    spectrum.scanList.scans.back().set(MS_scan_start_time, scanInfo.rTime*60, UO_second);

    double* mzs = NULL;
    float* intensities = NULL;
    if( filetype_ == MSn_Type_CMS1 || filetype_ == MSn_Type_CMS2 )// get compression info
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
    spectrum.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
    BinaryData<double>& mzArray = spectrum.getMZArray()->data;
    BinaryData<double>& intensityArray = spectrum.getIntensityArray()->data;
    double mz = 0;
    float intensity = 0;

    for(int i=0; i<scanInfo.numPeaks; i++){
      if( filetype_ == MSn_Type_CMS1 || filetype_ == MSn_Type_CMS2 )
      {
        intensity = intensities[i];
        mz = mzs[i];
      }else //MSn_Type_BMS1, MSn_Type_BMS2
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
    
    while (std::getline(*is_, lineStr)) // need accurate line length, so do not use pwiz::util convenience wrapper
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
  
    if( version_ > 3 ){
        throw runtime_error(("[SpectrumList_MSn::createIndexBinary] The version of this file is " +
                               lexical_cast<string>(version_) + " but the latest version handled is 3"));
    }

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
      if( filetype_ == MSn_Type_CMS1 || filetype_ == MSn_Type_CMS2 ){
        // skip the charge states
        (*is_).seekg(scanInfo.numChargeStates * sizeChargeMSn, std::ios_base::cur); 
        // skip the EZ states
        (*is_).seekg(scanInfo.numEzStates * sizeEzMSn, std::ios_base::cur); 

        // skip the peaks, first find out how far
        int iTemp;
        unsigned long mzLen, intensityLen;
        (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
        mzLen = (unsigned long)iTemp;
        (*is_).read(reinterpret_cast<char *>(&iTemp), sizeIntMSn);
        intensityLen = (unsigned long)iTemp;
        
        (*is_).seekg(mzLen + intensityLen, std::ios_base::cur); 
      }else if( filetype_ == MSn_Type_BMS1 || filetype_ == MSn_Type_BMS2 ){
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
