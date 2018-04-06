//
// $Id$
//
//
// Original author: Barbara Frewen <ferwen@u.washington.edu>
//
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

#include "Serializer_MSn.hpp"
#include "SpectrumList_MSn.hpp"
#include "SpectrumInfo.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "zlib.h"
#include <time.h>
#include "SpectrumWorkerThreads.hpp"


namespace pwiz {
namespace msdata {

// these are the fixed sizes used to read/write .bms2 and .cms2 files
// use these to write to files rather than sizeof(<type>)
const int sizeIntMSn       = 4;
const int sizeFloatMSn     = 4;   
const int sizeDoubleMSn    = 8; 
const int sizeChargeMSn    = 12; // struct Charge{ int z; double mass; }
const int sizePeakMSn      = 12; // struct Peak{ double mz; float intensity; }

using namespace std;
using boost::shared_ptr;
using boost::iostreams::stream_offset;
using namespace pwiz::util;
using namespace pwiz::chemistry;


class Serializer_MSn::Impl
{
    public:
        Impl(MSn_Type filetype) : _filetype(filetype) {}
        
        void write(ostream& os, const MSData& msd, 
                   const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;
        
        void read(shared_ptr<istream> is, MSData& msd) const;

    private: 
        MSn_Type _filetype; // .ms1, .cms1, .bms1, .ms2, .cms2, .bms2
};

namespace 
{
    void writeBinaryFileHeader(MSn_Type filetype, int version, const MSData& msd, ostream& os)
    {
        time_t rawtime;
        time (&rawtime);

        os.write(reinterpret_cast<char *>(&filetype), sizeIntMSn);
        os.write(reinterpret_cast<char *>(&version), sizeIntMSn);

        MSnHeader header;
        sprintf(header.header[0], "CreationDate %s", ctime(&rawtime));
        sprintf(header.header[1], "Extractor\tProteoWizard\n");
        sprintf(header.header[2], "Extractor version\t%s\n", (msd.softwarePtrs.empty() ? "unknown" : msd.softwarePtrs.at(0)->id.c_str()));
        sprintf(header.header[3], "Source file\t%s\n", (msd.fileDescription.sourceFilePtrs.empty() ? "unknown" : msd.fileDescription.sourceFilePtrs.at(0)->name.c_str()));
        os.write(reinterpret_cast<char *>(&header), sizeof(MSnHeader));
    }
   
    void writeTextFileHeader(const MSData& msd, ostream& os)
    {
      time_t rawtime;
      time (&rawtime);
      os << "H\tCreationDate " << ctime(&rawtime) << flush;
      os << "H\tExtractor\tProteoWizard" << endl;
      os << "H\tExtractor version\t" << 
        ( msd.softwarePtrs.empty() ? "unknown" : msd.softwarePtrs.at(0)->id ) << endl;
      os << "H\tSource file\t" << 
        ( (msd.fileDescription.sourceFilePtrs.empty()) ? "unknown" : 
          msd.fileDescription.sourceFilePtrs.at(0)->name ) << endl;
    }

    // Could be one charge state, with or without accurate mass
    // or could be multiple possible charge states without accurate mass
    // if no accurate mass, compute it from mz and charge
    double calculateMass(double mz, int charge)
    {
        return (mz * charge) - ((charge - 1) * Proton);
    }

    size_t getChargeStates(const SelectedIon& si, vector<int>& charges, vector<double>& masses)
    {
        int startingChargesCount = charges.size();
        CVParam chargeParam = si.cvParam(MS_charge_state);
        CVParam massParam = si.cvParam(MS_accurate_mass_OBSOLETE);
        double mz = si.cvParam(MS_selected_ion_m_z).valueAs<double>();
        if (!chargeParam.empty())
        {
            charges.push_back(chargeParam.valueAs<int>());
            if (!massParam.empty())
            {
                masses.push_back(massParam.valueAs<double>());
            }
            else
            {
              masses.push_back(calculateMass(mz, charges.back()));
            }
        }
        else
        {
            BOOST_FOREACH(const CVParam& param, si.cvParams)
            {
                if (param.cvid == MS_possible_charge_state)
                {
                    charges.push_back(param.valueAs<int>());
                    masses.push_back(calculateMass(mz, charges.back()));
                }
            }
        }

        return (int)(charges.size() - startingChargesCount);
    }
    
    int getScanNumber(SpectrumPtr s)
    {
        string scanNumber = id::translateNativeIDToScanNumber(MS_scan_number_only_nativeID_format, s->id);
        int scanNum = 0;
        if (!scanNumber.empty())
        {
            scanNum = lexical_cast<int>(scanNumber);
        }

        return scanNum;
    }

    void writeSpectrumText(SpectrumPtr s, ostream& os)
    {
        os << std::setprecision(7); // 123.4567
        bool ms1File = s->cvParam(MS_ms_level).valueAs<int>() == 1;
        
        // Write the scan numbers 
        os << "S\t";
        int scanNum = getScanNumber(s);
        os << scanNum <<  "\t" << scanNum;

        if (!ms1File)
        {
            // Write the precursor mz
            Precursor& precur = s->precursors[0];
            double mz = precur.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            os << "\t" << mz;
        }
        os << "\n";
        
        // Write the scan time, if available
        if( !(s->scanList.empty()) && s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds() )
          os << "I\tRTime\t" << s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds()/60 << "\n";

        if (s->defaultArrayLength > 0)
        {
            // Write the base peak intensity and base peak m/z
            if (s->hasCVParam(MS_base_peak_intensity))
            {
                double bpi = s->cvParam(MS_base_peak_intensity).valueAs<double>();
                os << "I\tBPI\t" << bpi << "\n";
            }
            if (s->hasCVParam(MS_base_peak_m_z))
            {
                double bpm = s->cvParam(MS_base_peak_m_z).valueAs<double>();
                os << "I\tBPM\t" << bpm << "\n";
            }

            // Write the total ion current
            if (s->hasCVParam(MS_total_ion_current))
            {
                double tic = s->cvParam(MS_total_ion_current).valueAs<double>();
                os << "I\tTIC\t" << tic << "\n";
            }
        }

        //TODO 
        // Write ConvA/ConvB information
        // Write the ion injection time
        //os << "I\tIIT\t" << s->scanList.scans[0].cvParam(MS_ion_injection_time).timeInSeconds()/60 << "\n";

        if (!ms1File)
        {
            Precursor& precur = s->precursors[0];
            SelectedIon& si = precur.selectedIons[0];
            // Collect charge and mass info
            vector<int> charges;
            vector<double> masses;
            int numChargeStates = 0;
            // for each selected ion
            BOOST_FOREACH(const SelectedIon& curIon, precur.selectedIons){
              numChargeStates += getChargeStates(curIon, charges, masses);
            }

            // Write EZ lines if accurate masses are available
            CVParam massParam = si.cvParam(MS_accurate_mass_OBSOLETE);
            if( !massParam.empty() ){
              for(int i=0; i < numChargeStates; i++){
                os << "I\tEZ\t" << charges[i] << "\t" << masses[i] << "\t0\t0" << endl; // pad last two fields with 0
              }
            }

            // For each charge, write the charge and mass
            for(int i = 0; i < numChargeStates; i++)
            {
              os << "Z\t" << charges[i] << "\t" << masses[i] << "\n"; 
            }
        }

        // Write each mz, intensity pair
        const BinaryDataArray& mzArray = *s->getMZArray();
        const BinaryDataArray& intensityArray = *s->getIntensityArray();
        for (size_t p=0; p < s->defaultArrayLength; ++p)
        {
            os << mzArray.data[p] << " " << intensityArray.data[p] << "\n";
        }
    }

    void writeCompressedPeaks(SpectrumPtr s, ostream& os)
    {
        // Build arrays to hold peaks prior to compression
        int numPeaks = (int) s->defaultArrayLength;
        double *pD = new double[numPeaks];
        float *pF = new float[numPeaks];

        const BinaryDataArray& mzArray = *s->getMZArray();
        const BinaryDataArray& intensityArray = *s->getIntensityArray();
        for(int j = 0; j < numPeaks; j++)
        {
            pD[j] = mzArray.data[j];
            pF[j] = (float) intensityArray.data[j];
        }

        // compress mz
        uLong sizeM = (uLong) (numPeaks * sizeDoubleMSn);
        uLong comprLenM = compressBound(sizeM);
        Byte *comprM = (Byte*)calloc((uInt)comprLenM, 1);
        int retM = compress(comprM, &comprLenM, (const Bytef*)pD, sizeM);
        
        // compress intensity
        uLong sizeI = (uLong) (numPeaks * sizeFloatMSn);
        uLong comprLenI = compressBound(sizeI);
        Byte *comprI = (Byte*)calloc((uInt)comprLenI, 1);
        int retI = compress(comprI, &comprLenI, (const Bytef*)pF, sizeI);

        // Write the compressed peaks if all is well
        if ((Z_OK == retM) && (Z_OK == retI))
        {
            // write length of compressed array of m/z
            os.write(reinterpret_cast<char *>(&comprLenM), sizeIntMSn);

            // write length of compressed array of intensities
            os.write(reinterpret_cast<char *>(&comprLenI), sizeIntMSn);

            // write compressed array of m/z
            os.write(reinterpret_cast<char *>(comprM), comprLenM);

            // write compressed array of intensities
            os.write(reinterpret_cast<char *>(comprI), comprLenI);
        }

        // Clean up memory
        free(comprM);
        free(comprI);
        delete [] pD;
        delete [] pF;
        
        // In case of error, throw exception AFTER cleaning up memory
        if (Z_OK != retM || Z_OK != retI)
        {
            throw runtime_error("[Serializer_MSn::writeCompressedPeaks] Error compressing peaks.");
        }
    }

    void writeSpectrumBinary(SpectrumPtr s, int version, bool compress, ostream& os)
    {
        bool ms1File = s->cvParam(MS_ms_level).valueAs<int>() == 1;

        int scanNum = getScanNumber(s);
        os.write(reinterpret_cast<char *>(&scanNum), sizeIntMSn);
        os.write(reinterpret_cast<char *>(&scanNum), sizeIntMSn); // Yes, there are two

        Precursor precur;
        SelectedIon si;
        if (!ms1File)
        {
            precur = s->precursors[0];
            si = precur.selectedIons[0];
            double mz = precur.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            os.write(reinterpret_cast<char *>(&mz), sizeDoubleMSn);
        }
        else
        {
            double mz = 0;
            os.write(reinterpret_cast<char *>(&mz), sizeDoubleMSn);
        }

        float rt = 0.0;
        if( !(s->scanList.empty()) && s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds() )
        {
            rt = (float) s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds()/60;
        }

        os.write(reinterpret_cast<char *>(&rt), sizeFloatMSn);
        
        if (version >= 2)
        {
            float basePeakIntensity = s->cvParam(MS_base_peak_intensity).valueAs<float>();
            os.write(reinterpret_cast<char *>(&basePeakIntensity), sizeFloatMSn);

            double basePeakMZ = s->cvParam(MS_base_peak_m_z).valueAs<double>();
            os.write(reinterpret_cast<char *>(&basePeakMZ), sizeDoubleMSn);
            
            // We don't have this information, but we need to write something,
            // so pad with 0's. (version 2 specific data)
            double conversionFactorA = (double)0;
            os.write(reinterpret_cast<char *>(&conversionFactorA), sizeDoubleMSn); 
            double conversionFactorB = (double)0;
            os.write(reinterpret_cast<char *>(&conversionFactorB), sizeDoubleMSn); 

            double tic = s->cvParam(MS_total_ion_current).valueAs<double>();
            os.write(reinterpret_cast<char *>(&tic), sizeDoubleMSn);

            // TODO
            float ionInjectionTime = (float)0;
            os.write(reinterpret_cast<char *>(&ionInjectionTime), sizeFloatMSn);
        }
        
        vector<int> charges;
        vector<double> masses;
        int numChargeStates = 0;
        if (!ms1File)
        {
            BOOST_FOREACH(const SelectedIon& curIon, precur.selectedIons)
            {
                numChargeStates += getChargeStates(curIon, charges, masses);
            }
        }
        os.write(reinterpret_cast<char *>(&numChargeStates), sizeIntMSn);
        
        bool hasAccurateMass = false;
        if (version == 3)
        {
          int numEzStates = 0;
          CVParam massParam = si.cvParam(MS_accurate_mass_OBSOLETE);
          if (!massParam.empty())
          {
            numEzStates = numChargeStates;
            hasAccurateMass = true;
          }
          os.write(reinterpret_cast<char *>(&numEzStates), sizeIntMSn);
        }

        int numPeaks = (int) s->defaultArrayLength;
        os.write(reinterpret_cast<char *>(&numPeaks), sizeIntMSn);

        // end spectrum header info

        // Write out each charge state and corresponding mass
        for(int i = 0; i < numChargeStates; i++)
        {
            os.write(reinterpret_cast<char *>(&(charges[i])), sizeIntMSn);
            os.write(reinterpret_cast<char *>(&(masses[i])), sizeDoubleMSn);
        }
    
        // if there are accurate masses, write out EZ entries
        if( hasAccurateMass ){
          float blank = 0;  // we don't have rTime or area, pad with zeros
          for(int i=0; i < numChargeStates; i++){
            os.write(reinterpret_cast<char *>(&charges[i]), sizeIntMSn);
            os.write(reinterpret_cast<char *>(&masses[i]), sizeDoubleMSn);
            os.write(reinterpret_cast<char *>(&blank), sizeFloatMSn);
            os.write(reinterpret_cast<char *>(&blank), sizeFloatMSn);
          }
        }

        // Do we need to write compressed m/z, intensity arrays?
        if (compress)
        {
            writeCompressedPeaks(s, os);
        }
        else
        {
            // No need to compress, just write out the arrays
            const BinaryDataArray& mzArray = *s->getMZArray();
            const BinaryDataArray& intensityArray = *s->getIntensityArray();
            for(int i = 0; i < numPeaks; i++)
            {
                double mzPeak = mzArray.data[i];
                os.write(reinterpret_cast<char *>(&mzPeak), sizeDoubleMSn);
                
                float intensityPeak = (float) intensityArray.data[i];
                os.write(reinterpret_cast<char *>(&intensityPeak), sizeFloatMSn);
            }
        }
    }
    
} // namespace


void Serializer_MSn::Impl::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    // Write the header
    if ((MSn_Type_BMS1 == _filetype) ||
        (MSn_Type_CMS1 == _filetype) ||
        (MSn_Type_BMS2 == _filetype) ||
        (MSn_Type_CMS2 == _filetype))
    {
        writeBinaryFileHeader(_filetype, 3 /* version */, msd, os);
    } 
    else if ((MSn_Type_MS1 == _filetype) ||
             (MSn_Type_MS2 == _filetype))
    {
      writeTextFileHeader(msd, os);
    }

    // Go through the spectrum list and write each spectrum
    bool ms1File = MSn_Type_MS1 == _filetype || MSn_Type_BMS1 == _filetype || MSn_Type_CMS1 == _filetype;
    SpectrumList& sl = *msd.run.spectrumListPtr;
    SpectrumWorkerThreads spectrumWorkers(sl);
    for (size_t i=0, end=sl.size(); i < end; ++i)
    {
        //SpectrumPtr s = sl.spectrum(i, true);
        SpectrumPtr s = spectrumWorkers.processBatch(i);
        int msLevel = s->cvParam(MS_ms_level).valueAs<int>();
        if ((ms1File && msLevel == 1) ||
            (!ms1File && msLevel == 2 && !s->precursors.empty() && !s->precursors[0].selectedIons.empty()))
        {
            switch (_filetype)
            {
            case MSn_Type_MS1:
                writeSpectrumText(s, os);
                break;
            case MSn_Type_CMS1:
                writeSpectrumBinary(s, 3 /* version */, true, os);
                break;
            case MSn_Type_BMS1:
                writeSpectrumBinary(s, 3 /* version */, false, os);
                break;
            case MSn_Type_MS2:
                writeSpectrumText(s, os);
                break;
            case MSn_Type_CMS2:
                writeSpectrumBinary(s, 3 /* version */, true, os);
                break;
            case MSn_Type_BMS2:
                writeSpectrumBinary(s, 3 /* version */, false, os);
                break;
            case MSn_Type_UNKNOWN:
                throw runtime_error("[SpectrumList_MSn::Impl::write] Cannot create unknown MSn file type.");
            }
        }

        // update any listeners and handle cancellation
        IterationListener::Status status = IterationListener::Status_Ok;

        if (iterationListenerRegistry)
        {
            status = iterationListenerRegistry->broadcastUpdateMessage(
                IterationListener::UpdateMessage(i, end));
        }

        if (status == IterationListener::Status_Cancel)
        {
            break;
        }
    }
}


void Serializer_MSn::Impl::read(shared_ptr<istream> is, MSData& msd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_MSn::read()] Bad istream.");

    is->seekg(0);

    msd.fileDescription.fileContent.set(MS_MSn_spectrum);
    msd.fileDescription.fileContent.set(MS_centroid_spectrum);
    msd.fileDescription.fileContent.set(MS_scan_number_only_nativeID_format);
    msd.run.spectrumListPtr = SpectrumList_MSn::create(is, msd, _filetype);
    msd.run.chromatogramListPtr.reset(new ChromatogramListSimple);

}


//
// Serializer_MSn
//

PWIZ_API_DECL Serializer_MSn::Serializer_MSn(MSn_Type filetype)
:   impl_(new Impl(filetype))
{}

PWIZ_API_DECL void Serializer_MSn::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
  
{
    return impl_->write(os, msd, iterationListenerRegistry);
}


PWIZ_API_DECL void Serializer_MSn::read(shared_ptr<istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


} // namespace msdata
} // namespace pwiz

