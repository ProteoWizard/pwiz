#define PWIZ_SOURCE

#include "data/msdata/CVTranslator.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/algorithm/string.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/format.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include "ChromatogramList_Thermo.hpp"
#include <iostream>
#include <stdexcept>

using boost::format;

namespace pwiz {
namespace msdata {
namespace detail {


string scanNumberToSpectrumID(long scanNumber)
{
    return "S" + lexical_cast<string>(scanNumber); 
}


SpectrumList_Thermo::SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile),
    size_(rawfile->value(NumSpectra)),
    spectrumCache_(size_),
    chromatograms_(new ChromatogramList_Thermo()),
    index_(size_),
    centroidSpectra_(false)
{
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Thermo::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Thermo::find(const string& id) const
{
    try
    {
        size_t scanNumber = lexical_cast<size_t>(id);
        if (scanNumber>=1 && scanNumber<=size()) 
            return scanNumber-1;
    }
    catch (bad_lexical_cast&) {}

    return size();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::findNative(const string& nativeID) const
{
    return find(nativeID);
}


// first: the pointer to the new or existing chromatogram
// second: true if the chromatogram is newly inserted
PWIZ_API_DECL
pair<ChromatogramPtr, bool> addPointToNewOrExistingChromatogram(ChromatogramList_Thermo& cl,
                                                                ScanInfo& scanInfo,
                                                                const string& id,
                                                                double time,
                                                                double intensity)
{
    size_t index = cl.find(id);
    ChromatogramPtr c;
    if (index == cl.size())
    {
        c = ChromatogramPtr(new Chromatogram);
        c->id = c->nativeID = id;
        c->index = index;

        vector<TimeIntensityPair> firstPair;
        firstPair.push_back(TimeIntensityPair(time, intensity));
        c->setTimeIntensityPairs(firstPair);

        cl.index_.push_back(c);
        cl.idMap_[id] = index;
        return pair<ChromatogramPtr, bool>(c, true);
    } else
    {
        c = cl.index_[index];
        // if most recently added time is the same, add to its intensity
        if (c->binaryDataArrayPtrs[0]->data.back() == time)
        {
            c->binaryDataArrayPtrs[1]->data.back() += intensity;
        } else
        {
            ++ c->defaultArrayLength;
            c->binaryDataArrayPtrs[0]->data.push_back(time);
            c->binaryDataArrayPtrs[1]->data.push_back(intensity);
        }
        return pair<ChromatogramPtr, bool>(c, false);
    }
}

PWIZ_API_DECL void SpectrumList_Thermo::addSpectrumToChromatogramList(ScanInfo& scanInfo) const
{
    //if (!msd.run.chromatogramListPtr.get())
    //    return;
    ChromatogramList_Thermo& cl = reinterpret_cast<ChromatogramList_Thermo&>(*chromatograms_);

    pair<ChromatogramPtr, bool> result = addPointToNewOrExistingChromatogram(cl, scanInfo,
        string("TIC"), scanInfo.startTime(), scanInfo.totalIonCurrent());
    if (result.second)
    {
        result.first->cvParams.push_back(CVParam(MS_total_ion_current_chromatogram));
    }

    if (scanInfo.scanType() == ScanType_SRM)
    {
        pair<ChromatogramPtr, bool> result2 = addPointToNewOrExistingChromatogram(cl, scanInfo,
            (format("SRM TIC %1.4f") % scanInfo.parentMass(0)).str(),
            scanInfo.startTime(), scanInfo.totalIonCurrent());
        if (result2.second)
        {
            result2.first->cvParams.push_back(CVParam(MS_total_ion_current_chromatogram));

            // TODO: change to CVParam when CV is updated

            result2.first->userParams.push_back(UserParam("MS_precursor_m_z", 
                                                lexical_cast<string>(scanInfo.parentMass(0))));

            //result2.first->cvParams.push_back(CVParam(MS_precursor_m_z, scanInfo.parentMass(0)));
        }

        auto_ptr<raw::MassList> massList = 
            rawfile_->getMassList(scanInfo.scanNumber(), "", raw::Cutoff_None, 0, 0, true);
        for (int i=0; i < massList->size(); ++i)
        {
            double productMz = massList->data()[i].mass;
            pair<ChromatogramPtr, bool> result3 = addPointToNewOrExistingChromatogram(cl, scanInfo,
                (format("SRM SIC %1.4f->%1.4f") % scanInfo.parentMass(0) % productMz).str(),
                scanInfo.startTime(), productMz);
            if (result3.second)
            {
                // TODO: change to CVParam when CV is updated

                result3.first->userParams.push_back(UserParam("MS_selected_ion_chromatogram"));

                result3.first->userParams.push_back(UserParam("MS_precursor_m_z", 
                                                    lexical_cast<string>(scanInfo.parentMass(0))));

                result3.first->userParams.push_back(UserParam("MS_product_m_z", 
                                                    lexical_cast<string>(productMz)));

                /*
                result3.first->cvParams.push_back(CVParam(MS_selected_ion_chromatogram));
                result3.first->cvParams.push_back(CVParam(MS_precursor_m_z, scanInfo.parentMass(0)));
                result3.first->cvParams.push_back(CVParam(MS_product_m_z, productMz));
                */
            }
        }
    }
}

PWIZ_API_DECL ChromatogramListPtr SpectrumList_Thermo::Chromatograms() const
{
    return chromatograms_;
}


InstrumentConfigurationPtr findInstrumentConfiguration(const MSData& msd, CVID massAnalyzerType)
{
    if (msd.instrumentConfigurationPtrs.empty())
        throw runtime_error("[SpectrumList_Thermo::findInstrumentConfiguration()] No instruments defined.");

    for (vector<InstrumentConfigurationPtr>::const_iterator it=msd.instrumentConfigurationPtrs.begin(),
         end=msd.instrumentConfigurationPtrs.end(); it!=end; ++it)
        if ((*it)->componentList.analyzer(0).hasCVParam(massAnalyzerType))
            return *it;

    throw runtime_error("[SpectrumList_Thermo::findInstrumentConfiguration()] "\
                        "Instrument configuration not found for mass analyzer type: " +
                        cvinfo(massAnalyzerType).name);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData) const 
{ 
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // returned cached Spectrum if possible

    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate a new Spectrum

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Allocation error.");

    // get rawfile::ScanInfo and translate

    long scanNumber = static_cast<int>(index) + 1;
    auto_ptr<ScanInfo> scanInfo = rawfile_->getScanInfo(scanNumber);
    if (!scanInfo.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo.");

    addSpectrumToChromatogramList(*scanInfo);

    result->index = index;
    result->id = scanNumberToSpectrumID(scanNumber);
    result->nativeID = lexical_cast<string>(scanNumber);

    SpectrumDescription& sd = result->spectrumDescription;
    Scan& scan = sd.scan;

    scan.instrumentConfigurationPtr = 
        findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

    string filterString = scanInfo->filter();

    scan.cvParams.push_back(CVParam(MS_filter_string, filterString));

    string scanEvent = scanInfo->trailerExtraValue("Scan Event:");
    scan.cvParams.push_back(CVParam(MS_preset_scan_configuration, scanEvent));

    result->set(MS_ms_level, scanInfo->msLevel());

    ScanType scanType = scanInfo->scanType();
    if (scanType!=ScanType_Unknown)
    {
        result->cvParams.push_back(translateAsSpectrumType(scanType));
        scan.cvParams.push_back(translateAsScanningMethod(scanType));
    }

    PolarityType polarityType = scanInfo->polarityType();
    if (polarityType!=PolarityType_Unknown) scan.cvParams.push_back(translate(polarityType));

    if (scanInfo->isProfileScan() && !centroidSpectra_) sd.cvParams.push_back(MS_profile_mass_spectrum); 
    else if (scanInfo->isCentroidScan() || centroidSpectra_) sd.cvParams.push_back(MS_centroid_mass_spectrum); 

    scan.cvParams.push_back(CVParam(MS_scan_time, scanInfo->startTime(), MS_minute));
    sd.cvParams.push_back(CVParam(MS_base_peak_m_z, scanInfo->basePeakMass()));
    sd.cvParams.push_back(CVParam(MS_base_peak_intensity, scanInfo->basePeakIntensity()));
    sd.cvParams.push_back(CVParam(MS_total_ion_current, scanInfo->totalIonCurrent()));

    scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass()));

    for (long i=0, precursorCount=scanInfo->parentCount(); i<precursorCount; i++)
    {
        // Note: we report what RawFile gives us, which comes from the filter string;
        // we can look in the trailer extra values for better (but still unreliable) 
        // info.  Precursor recalculation should be done outside the Reader.

        Precursor precursor;
        SelectedIon selectedIon;

        // TODO: better test here for data dependent modes
        if ((scanType==ScanType_Full || scanType==ScanType_Zoom ) && scanInfo->msLevel() > 1)
            precursor.spectrumID = findPrecursorID(scanInfo->msLevel()-1, index);

        selectedIon.cvParams.push_back(CVParam(MS_m_z, scanInfo->parentMass(i)));
        // TODO: determine precursor intensity? (parentEnergy is not precursor intensity!)

        ActivationType activationType = scanInfo->activationType();
        if (activationType == ActivationType_Unknown)
            activationType = ActivationType_CID; // assume CID
        precursor.activation.cvParams.push_back(CVParam(translate(activationType)));
        if (activationType == ActivationType_CID || activationType == ActivationType_HCD)
            precursor.activation.cvParams.push_back(CVParam(MS_collision_energy, scanInfo->parentEnergy(i)));

        precursor.selectedIons.push_back(selectedIon);
        sd.precursors.push_back(precursor);
    }

    if (getBinaryData)
    {
        auto_ptr<raw::MassList> massList = 
            rawfile_->getMassList(scanNumber, "", raw::Cutoff_None, 0, 0, centroidSpectra_);

        sd.cvParams.push_back(CVParam(MS_lowest_m_z_value, massList->data()[0].mass));
        sd.cvParams.push_back(CVParam(MS_highest_m_z_value, massList->data()[massList->size()-1].mass));

        result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(massList->data()), 
                                    massList->size());
    }

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


PWIZ_API_DECL void SpectrumList_Thermo::createIndex()
{
    for (size_t i=0; i<size_; i++)
    {
        SpectrumIdentity& si = index_[i];
        si.index = i;
        long scanNumber = (long)i+1;
        si.id = scanNumberToSpectrumID(scanNumber); 
        si.nativeID = lexical_cast<string>(scanNumber);
    }
}


PWIZ_API_DECL string SpectrumList_Thermo::findPrecursorID(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index>0)
    {
	    --index;
	    SpectrumPtr candidate = spectrum(index, false);
	    if (candidate->cvParam(MS_ms_level).valueAs<int>() == precursorMsLevel)
		    return candidate->id;
    }

    return "";
}

} // detail
} // msdata
} // pwiz
