//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_TitleMaker.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include <boost/range/algorithm/find_if.hpp>


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_TitleMaker::SpectrumList_TitleMaker(const msdata::MSData& msd, const string& format)
:   SpectrumListWrapper(msd.run.spectrumListPtr), msd_(msd), format_(format)
{
    nativeIdFormat_ = id::getDefaultNativeIDFormat(msd);

    // title adding isn't really worth a processingMethod, is it?
}


namespace {

template <typename value_type>
void replaceCvParam(ParamContainer& pc, CVID cvid, const value_type& value)
{
    vector<CVParam>::iterator itr;
    
    itr = std::find(pc.cvParams.begin(), pc.cvParams.end(), cvid);
    if (itr == pc.cvParams.end())
        pc.set(cvid, value);
    else
        itr->value = lexical_cast<string>(value);
}

// TODO: Make this a public function? It's copied and modified from Serializer_mzXML.cpp;
//       instead of returning basename, it returns the full file or directory name.
string translate_SourceFileTypeToRunID(const SourceFile& sf, CVID sourceFileType)
{
    string nameExtension = bal::to_lower_copy(bfs::extension(sf.name));
    string locationExtension = bal::to_lower_copy(bfs::extension(sf.location));

    switch (sourceFileType)
    {
        // location="file://path/to" name="source.RAW"
        case MS_Thermo_RAW_format:
            if (nameExtension == ".raw")
                return sf.name;
            return "";

        // sane: location="file://path/to/source.raw" name="_FUNC001.DAT"
        // insane: location="file://path/to" name="source.raw"
        case MS_Waters_raw_format:
            if (nameExtension == ".dat" && locationExtension == ".raw")
                return bfs::path(sf.location).filename().string();
            else if (nameExtension == ".raw")
                return sf.name;
            return "";

        // location="file://path/to/source.d" name="Analysis.yep"
        case MS_Bruker_Agilent_YEP_format:
            if (nameExtension == ".yep" && locationExtension == ".d")
                return bfs::path(sf.location).filename().string();
            return "";
            
        // location="file://path/to/source.d" name="Analysis.baf"
        case MS_Bruker_BAF_format:
            if (nameExtension == ".baf" && locationExtension == ".d")
                return bfs::path(sf.location).filename().string();
            return "";
            
        // location="file://path/to/source.d" name="analysis.tdf"
        case MS_Bruker_TDF_format:
            if (nameExtension == ".tdf" && locationExtension == ".d")
                return bfs::path(sf.location).filename().string();
            return "";

        // location="file://path/to/source.d/AcqData" name="msprofile.bin"
        case MS_Agilent_MassHunter_format:
            if (nameExtension == ".bin" && bfs::path(sf.location).filename() == "AcqData")
                return bfs::path(sf.location).parent_path().filename().string();
            return "";

        // location="file://path/to" name="source.mzXML"
        // location="file://path/to" name="source.mz.xml"
        // location="file://path/to" name="source.d" (ambiguous)
        case MS_ISB_mzXML_format:
            if (nameExtension == ".mzxml" || nameExtension == ".d")
                return sf.name;
            else if (bal::iends_with(sf.name, ".mz.xml"))
                return sf.name.substr(0, sf.name.length()-7);
            return "";

        // location="file://path/to" name="source.mzData"
        // location="file://path/to" name="source.mz.data" ???
        case MS_PSI_mzData_format:
            if (nameExtension == ".mzdata")
                return sf.name;
            return "";

        // location="file://path/to" name="source.mgf"
        case MS_Mascot_MGF_format:
            if (nameExtension == ".mgf")
                return sf.name;
            return "";

        // location="file://path/to" name="source.wiff"
        case MS_ABI_WIFF_format:
            if (nameExtension == ".wiff")
                return sf.name;
            return "";

        // location="file://path/to/source/maldi-spot/1/1SRef" name="fid"
        // location="file://path/to/source/1/1SRef" name="fid"
        case MS_Bruker_FID_format:
            return (bfs::path(sf.location) / sf.name).string().substr(7);

        // location="file://path/to/source" name="spectrum-id.t2d"
        // location="file://path/to/source/MS" name="spectrum-id.t2d"
        // location="file://path/to/source/MSMS" name="spectrum-id.t2d"
        case MS_SCIEX_TOF_TOF_T2D_format:
            return (bfs::path(sf.location) / sf.name).string().substr(7);

        default:
            // TODO: log unsupported mass spectrometer file format
            return "";
    }
}

struct SourceFilePtrIdEquals
{
    SourceFilePtrIdEquals(const string& id) : id(id) {}
    bool operator() (const SourceFilePtr& sf) const {return sf->id == id;}

    private: const string& id;
};

SourceFilePtr spectrumSourceFilePtr(const Spectrum& s, const MSData& msd)
{
    if (s.sourceFilePtr.get())
        return s.sourceFilePtr;

    if (bal::starts_with(s.id, "file="))
    {
        string sourceFileId = s.id.substr(5);
        pwiz::minimxml::decode_xml_id(sourceFileId);
        vector<SourceFilePtr>::const_iterator itr = boost::find_if(msd.fileDescription.sourceFilePtrs, SourceFilePtrIdEquals(sourceFileId));
        if (itr == msd.fileDescription.sourceFilePtrs.end())
            throw runtime_error("id \"" + s.id + "\" does not match the id of a source file");
        return *itr;
    }

    return msd.run.defaultSourceFilePtr;
}

} // namespace


PWIZ_API_DECL SpectrumPtr SpectrumList_TitleMaker::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s = inner_->spectrum(index, getBinaryData);

    /// <RunId> - Run::id
    /// <SourcePath> - "Data.d" from "C:/Agilent/Data.d/AcqData/mspeak.bin"
    /// <Index> - SpectrumIdentity::index
    /// <Id> - SpectrumIdentity::id (nativeID)
    /// <ScanNumber> - if the nativeID can be represented as a single number, that number, else index
    /// <ActivationType> - for the first precursor, Activation::cvParamChild("dissociation method")
    /// <IsolationMz> - for the first precursor, IsolationWindow::cvParam("isolation target m/z")
    /// <SelectedIonMz> - for the first selected ion of the first precursor, SelectedIon::cvParam("selected ion m/z")
    /// <ChargeState> - for the first selected ion of the first precursor, SelectedIon::cvParam("charge state")
    /// <PrecursorSpectrumId> - for the first precursor, Precursor::spectrumID or Precursor::externalSpectrumID
    /// <SpectrumType> - Spectrum::cvParamChild("spectrum type")
    /// <MsLevel> - Spectrum::cvParam("ms level")
    /// <ScanStartTime> - for the first scan, Scan::cvParam("scan start time")
    /// <BasePeakMz> - Spectrum::cvParam("base peak m/z")
    /// <BasePeakIntensity> - Spectrum::cvParam("base peak intensity")
    /// <TotalIonCurrent> - Spectrum::cvParam("total ion current")
    /// <IonMobility> - Scan::cvParam("ion mobility drift time") or Scan::cvParam("inverse reduced ion mobility")

    string title = format_;

    bal::replace_all(title, "<RunId>", msd_.run.id);
    bal::replace_all(title, "<Index>", lexical_cast<string>(s->index));
    bal::replace_all(title, "<Id>", s->id);

    if (bal::contains(title, "<SourcePath>"))
    {
        string nativeSourcePath;
        SourceFilePtr sfp = spectrumSourceFilePtr(*s, msd_);
        if (sfp.get())
        {
            const SourceFile& sf = *sfp;
            CVID nativeFileFormat = sf.cvParamChild(MS_mass_spectrometer_file_format).cvid;
            nativeSourcePath = translate_SourceFileTypeToRunID(sf, nativeFileFormat);
        }
        bal::replace_all(title, "<SourcePath>", nativeSourcePath);
    }

    string scanNumberStr = id::translateNativeIDToScanNumber(nativeIdFormat_, s->id);
    if (scanNumberStr.empty())
        scanNumberStr = lexical_cast<string>(s->index+1); // scanNumber is a 1-based index for some nativeID formats
    bal::replace_all(title, "<ScanNumber>", scanNumberStr);

    if (!s->precursors.empty())
    {
        const Precursor& p = s->precursors[0];

        vector<string> activationTypes;
        BOOST_FOREACH(const CVParam& cvParam, p.activation.cvParamChildren(MS_dissociation_method))
            activationTypes.push_back(cv::cvTermInfo(cvParam.cvid).shortName());

        bal::replace_all(title, "<ActivationType>", bal::join(activationTypes, "/"));
        bal::replace_all(title, "<IsolationMz>", p.isolationWindow.cvParam(MS_isolation_window_target_m_z).value);

        if (!p.selectedIons.empty())
        {
            bal::replace_all(title, "<SelectedIonMz>", p.selectedIons[0].cvParam(MS_selected_ion_m_z).value);
            bal::replace_all(title, "<ChargeState>", p.selectedIons[0].cvParam(MS_charge_state).value);
        }
        else
        {
            bal::replace_all(title, "<SelectedIonMz>", "");
            bal::replace_all(title, "<ChargeState>", "");
        }

        if (!p.spectrumID.empty())
            bal::replace_all(title, "<PrecursorSpectrumId>", p.spectrumID);
        else if (!p.externalSpectrumID.empty())
            bal::replace_all(title, "<PrecursorSpectrumId>", p.externalSpectrumID);
    }
    else
    {
        bal::replace_all(title, "<ActivationType>", "");
        bal::replace_all(title, "<IsolationMz>", "");
        bal::replace_all(title, "<SelectedIonMz>", "");
        bal::replace_all(title, "<ChargeState>", "");
        bal::replace_all(title, "<PrecursorSpectrumId>", "");
    }

    if (!s->scanList.scans.empty())
    {
        Scan& firstScan = s->scanList.scans[0];

        double scanStartTimeInSeconds = firstScan.cvParam(MS_scan_start_time).timeInSeconds();
        bal::replace_all(title, "<ScanStartTimeInSeconds>", lexical_cast<string>(scanStartTimeInSeconds));
        bal::replace_all(title, "<ScanStartTimeInMinutes>", lexical_cast<string>(scanStartTimeInSeconds / 60));

        CVParam driftTime = firstScan.cvParam(MS_ion_mobility_drift_time);
        bal::replace_all(title, "<IonMobility>", driftTime.empty() ? firstScan.cvParam(MS_inverse_reduced_ion_mobility).value : driftTime.value);
    }

    bal::replace_all(title, "<SpectrumType>", s->cvParamChild(MS_spectrum_type).name());
    bal::replace_all(title, "<MsLevel>", s->cvParam(MS_ms_level).value);
    bal::replace_all(title, "<BasePeakMz>", s->cvParam(MS_base_peak_m_z).value);
    bal::replace_all(title, "<BasePeakIntensity>", s->cvParam(MS_base_peak_intensity).value);
    bal::replace_all(title, "<TotalIonCurrent>", s->cvParam(MS_TIC).value);


    replaceCvParam(*s, MS_spectrum_title, title);

    return s;
}


} // namespace analysis 
} // namespace pwiz
