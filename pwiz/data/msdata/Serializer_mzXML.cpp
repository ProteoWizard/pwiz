//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "Serializer_mzXML.hpp"
#include "SpectrumList_mzXML.hpp"
#include "Diff.hpp"
#include "SHA1OutputObserver.hpp"
#include "LegacyAdapter.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "SpectrumWorkerThreads.hpp"

namespace pwiz {
namespace msdata {


using minimxml::XMLWriter;
using boost::iostreams::stream_offset;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::data;


class Serializer_mzXML::Impl
{
    public:

    Impl(const Config& config)
    :   config_(config)
    {}

    void write(ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;

    void read(shared_ptr<istream> is, MSData& msd) const;

    private:
    Config config_; 
    CVTranslator cvTranslator_;
};


//
// write() implementation
//


namespace {


void start_mzXML(XMLWriter& xmlWriter)
{
    XMLWriter::Attributes attributes; 
    attributes.add("xmlns", "http://sashimi.sourceforge.net/schema_revision/mzXML_3.2");
    attributes.add("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
    attributes.add("xsi:schemaLocation", "http://sashimi.sourceforge.net/schema_revision/mzXML_3.2 http://sashimi.sourceforge.net/schema_revision/mzXML_3.2/mzXML_idx_3.2.xsd");

    xmlWriter.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
    xmlWriter.startElement("mzXML", attributes);
    xmlWriter.popStyle();
}


string getRetentionTime(const Scan& scan)
{
    ostringstream oss;
    oss << "PT" << scan.cvParam(MS_scan_start_time).timeInSeconds() << "S";
    return oss.str();
}


void start_msRun(XMLWriter& xmlWriter, const MSData& msd)
{
    string scanCount, startTime, endTime;

    if (msd.run.spectrumListPtr.get())
    {
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        scanCount = lexical_cast<string>(sl.size());

        if (sl.size() > 0)
        {
            SpectrumPtr spectrum = sl.spectrum(0);
            if (!spectrum->scanList.scans.empty())
                startTime = getRetentionTime(spectrum->scanList.scans[0]);

            spectrum = sl.spectrum(sl.size()-1);
            if (!spectrum->scanList.scans.empty())
                endTime = getRetentionTime(spectrum->scanList.scans[0]);
        }
    }

    XMLWriter::Attributes attributes; 
    attributes.add("scanCount", scanCount);
    attributes.add("startTime", startTime);
    attributes.add("endTime", endTime);
    xmlWriter.startElement("msRun", attributes);
}


string translate_SourceFileTypeToRunID(const SourceFile& sf, CVID sourceFileType)
{
    string nameExtension = bal::to_lower_copy(bfs::extension(sf.name));
    string locationExtension = bal::to_lower_copy(bfs::extension(sf.location));

    switch (sourceFileType)
    {
        // location="file://path/to" name="source.RAW"
        case MS_Thermo_RAW_format:
            if (nameExtension == ".raw")
                return bfs::basename(sf.name);
            return "";

        // sane: location="file://path/to/source.raw" name="_FUNC001.DAT"
        // insane: location="file://path/to" name="source.raw"
        case MS_Waters_raw_format:
            if (nameExtension == ".dat" && locationExtension == ".raw")
                return bfs::basename(bfs::path(sf.location).leaf());
            else if (nameExtension == ".raw")
                return bfs::basename(sf.name);
            return "";

        // location="file://path/to/source.d" name="Analysis.yep"
        case MS_Bruker_Agilent_YEP_format:
            if (nameExtension == ".yep" && locationExtension == ".d")
                return bfs::basename(bfs::path(sf.location).leaf());
            return "";
            
        // location="file://path/to/source.d" name="Analysis.baf"
        case MS_Bruker_BAF_format:
            if (nameExtension == ".baf" && locationExtension == ".d")
                return bfs::basename(bfs::path(sf.location).leaf());
            return "";

        // location="file://path/to/source.d/AcqData" name="msprofile.bin"
        case MS_Agilent_MassHunter_format:
            if (bfs::path(sf.location).leaf() == "AcqData" &&
                (bal::iends_with(sf.name, "msprofile.bin") ||
                 bal::iends_with(sf.name, "mspeak.bin") ||
                 bal::iends_with(sf.name, "msscan.bin")))
                return bfs::basename(bfs::path(sf.location).parent_path().leaf());
            return "";

        // location="file://path/to" name="source.mzXML"
        // location="file://path/to" name="source.mz.xml"
        // location="file://path/to" name="source.d" (ambiguous)
        case MS_ISB_mzXML_format:
            if (nameExtension == ".mzxml" || nameExtension == ".d")
                return bfs::basename(sf.name);
            else if (bal::iends_with(sf.name, ".mz.xml"))
                return sf.name.substr(0, sf.name.length()-7);
            return "";

        // location="file://path/to" name="source.mzData"
        // location="file://path/to" name="source.mz.data" ???
        case MS_PSI_mzData_format:
            if (nameExtension == ".mzdata")
                return bfs::basename(sf.name);
            return "";

        // location="file://path/to" name="source.mgf"
        case MS_Mascot_MGF_format:
            if (nameExtension == ".mgf")
                return bfs::basename(sf.name);
            return "";

        // location="file://path/to" name="source.wiff"
        case MS_ABI_WIFF_format:
            if (nameExtension == ".wiff")
                return bfs::basename(sf.name);
            return "";

        // location="file://path/to/source/maldi-spot/1/1SRef" name="fid"
        // location="file://path/to/source/1/1SRef" name="fid"
        case MS_Bruker_FID_format:
            // need the full list of FIDs to create a run ID (from the common prefix)
            return bfs::path(sf.location).parent_path().parent_path().string();

        // location="file://path/to/source" name="spectrum-id.t2d"
        // location="file://path/to/source/MS" name="spectrum-id.t2d"
        // location="file://path/to/source/MSMS" name="spectrum-id.t2d"
        case MS_SCIEX_TOF_TOF_T2D_format:
            // need the full list of T2Ds to create a run ID (from the common prefix)
            return sf.location;

        default:
            return "";
    }
}


void write_parentFile(XMLWriter& xmlWriter, const MSData& msd)
{
    BOOST_FOREACH(const SourceFilePtr& sourceFilePtr, msd.fileDescription.sourceFilePtrs)
    {
        const SourceFile& sf = *sourceFilePtr;

        // skip files with unknown source file type
        CVID sourceFileType = sf.cvParamChild(MS_mass_spectrometer_file_format).cvid;
        if (sourceFileType == CVID_Unknown)
            continue;

        // skip files with no nativeID format (like acquisition settings)
        CVID nativeIdFormat = sf.cvParamChild(MS_nativeID_format).cvid;
        if (nativeIdFormat == MS_no_nativeID_format)
            continue;

        // if we can't translate the file to a run ID, skip it as a parentFile
        string runID = translate_SourceFileTypeToRunID(sf, sourceFileType);
        if (runID.empty())
            continue;

        string fileName, fileType, fileSha1;

        fileName = sf.location + "/" + sf.name;
        switch (nativeIdFormat)
        {
            // nativeID formats from processed data file types
            case MS_scan_number_only_nativeID_format:
            case MS_spectrum_identifier_nativeID_format:
            case MS_multiple_peak_list_nativeID_format:
            case MS_single_peak_list_nativeID_format:
                fileType = "processedData";
                break;

            // consider other formats to be raw
            default:
                fileType = "RAWData";
                break;
        }
        fileSha1 = sf.cvParam(MS_SHA_1).value;

        XMLWriter::Attributes attributes;
        attributes.add("fileName", fileName);
        attributes.add("fileType", fileType);
        attributes.add("fileSha1", fileSha1);
        xmlWriter.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
        xmlWriter.startElement("parentFile", attributes, XMLWriter::EmptyElement);
        xmlWriter.popStyle();
    }
}


void writeCategoryValue(XMLWriter& xmlWriter, const string& category, const string& value)
{
    XMLWriter::Attributes attributes; 
    attributes.add("category", category);
    attributes.add("value", value);
    xmlWriter.startElement(category, attributes, XMLWriter::EmptyElement);
}


void writeSoftware(XMLWriter& xmlWriter, SoftwarePtr software, 
                   const MSData& msd, const CVTranslator& cvTranslator,
                   const string& type = "")
{
    LegacyAdapter_Software adapter(software, const_cast<MSData&>(msd), cvTranslator);
    XMLWriter::Attributes attributes; 

    attributes.add("type", type.empty() ? adapter.type() : type);
    attributes.add("name", adapter.name());
    attributes.add("version", adapter.version());

    xmlWriter.startElement("software", attributes, XMLWriter::EmptyElement);
}


void write_msInstrument(XMLWriter& xmlWriter, const InstrumentConfigurationPtr& instrumentConfiguration, 
                        const MSData& msd, const CVTranslator& cvTranslator,
                        map<InstrumentConfigurationPtr, int>& instrumentIndexByPtr)
{
    const LegacyAdapter_Instrument adapter(
        const_cast<InstrumentConfiguration&>(*instrumentConfiguration), cvTranslator);
    
    int index = (int) instrumentIndexByPtr.size() + 1;
    instrumentIndexByPtr[instrumentConfiguration] = index;

    XMLWriter::Attributes attributes;
    attributes.add("msInstrumentID", index);
    xmlWriter.startElement("msInstrument", attributes);
    writeCategoryValue(xmlWriter, "msManufacturer", adapter.manufacturer());
    writeCategoryValue(xmlWriter, "msModel", adapter.model());
    try { writeCategoryValue(xmlWriter, "msIonisation", adapter.ionisation()); } catch (std::out_of_range&) {}
    try { writeCategoryValue(xmlWriter, "msMassAnalyzer", adapter.analyzer()); } catch (std::out_of_range&) {}
    try { writeCategoryValue(xmlWriter, "msDetector", adapter.detector()); } catch (std::out_of_range&) {}
    if (instrumentConfiguration->softwarePtr.get())
        writeSoftware(xmlWriter, instrumentConfiguration->softwarePtr,
                      msd, cvTranslator, "acquisition");
    xmlWriter.endElement(); // msInstrument
}


void write_msInstruments(XMLWriter& xmlWriter, const MSData& msd,
                        const CVTranslator& cvTranslator,
                        map<InstrumentConfigurationPtr, int>& instrumentIndexByPtr)
{
    BOOST_FOREACH(const InstrumentConfigurationPtr& icPtr, msd.instrumentConfigurationPtrs)
        if (icPtr.get()) write_msInstrument(xmlWriter, icPtr, msd, cvTranslator, instrumentIndexByPtr);
}


void write_processingOperation(XMLWriter& xmlWriter, const ProcessingMethod& pm, CVID action)
{
    vector<CVParam> actionParams = pm.cvParamChildren(action);
    for (auto & actionParam : actionParams)
    {
        XMLWriter::Attributes attributes;
        attributes.add("name", actionParam.name());
        xmlWriter.startElement("processingOperation", attributes, XMLWriter::EmptyElement);
    }
}


void write_dataProcessing(XMLWriter& xmlWriter, const MSData& msd, const CVTranslator& cvTranslator)
{
    BOOST_FOREACH(const DataProcessingPtr& dpPtr, msd.allDataProcessingPtrs())
    {
        if (!dpPtr.get() || dpPtr->processingMethods.empty()) continue;

        BOOST_FOREACH(const ProcessingMethod& pm, dpPtr->processingMethods)
        {
            XMLWriter::Attributes attributes;

            if (pm.hasCVParamChild(MS_peak_picking)) attributes.add("centroided", "1");
            if (pm.hasCVParamChild(MS_deisotoping)) attributes.add("deisotoped", "1");
            if (pm.hasCVParamChild(MS_charge_deconvolution)) attributes.add("chargeDeconvoluted", "1");
            if (pm.hasCVParamChild(MS_thresholding))
            {
                CVParam threshold = pm.cvParam(MS_low_intensity_threshold);
                if (!threshold.empty())
                    attributes.add("intensityCutoff", threshold.value);
            }

            xmlWriter.startElement("dataProcessing", attributes);

            CVParam fileFormatConversion = pm.cvParamChild(MS_file_format_conversion);

            string softwareType = fileFormatConversion.empty() ? "processing" : "conversion";

            if (pm.softwarePtr.get())
                writeSoftware(xmlWriter, pm.softwarePtr, msd, cvTranslator, softwareType);

            write_processingOperation(xmlWriter, pm, MS_data_transformation);

            xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
            BOOST_FOREACH(const UserParam& param, pm.userParams)
            {
                xmlWriter.startElement("comment");
                xmlWriter.characters(param.name + (param.value.empty() ? string() : ": " + param.value));
                xmlWriter.endElement(); // comment
            }
            xmlWriter.popStyle();
            xmlWriter.endElement(); // dataProcessing
        }
    }
}


struct IndexEntry
{
    int scanNumber;
    stream_offset offset;
};


string getPolarity(const Spectrum& spectrum)
{
    string result = "";
    CVParam paramPolarity = spectrum.cvParamChild(MS_scan_polarity);
    if (paramPolarity.empty()) paramPolarity = spectrum.cvParamChild(MS_polarity_OBSOLETE);
    if (paramPolarity.cvid == MS_positive_scan) result = "+";
    if (paramPolarity.cvid == MS_negative_scan) result = "-";
    return result;
}


struct PrecursorInfo
{
    string scanNum;
    string mz;
    string intensity;
    string charge;
    string collisionEnergy;
    string activation;
    double windowWideness;

    bool empty() const 
    {
        return scanNum.empty() && mz.empty() && intensity.empty() && 
               charge.empty() && collisionEnergy.empty() && activation.empty() && windowWideness == 0;
    }
};


vector<PrecursorInfo> getPrecursorInfo(const Spectrum& spectrum, 
                                       const SpectrumListPtr spectrumListPtr,
                                       CVID nativeIdFormat)
{
    vector<PrecursorInfo> result;

    for (vector<Precursor>::const_iterator it=spectrum.precursors.begin();
         it!=spectrum.precursors.end(); ++it)
    {
        PrecursorInfo info;
        if (!it->spectrumID.empty())
        {
            // mzXML scanNumber takes a different form depending on the source's nativeID format
            info.scanNum = id::translateNativeIDToScanNumber(nativeIdFormat, it->spectrumID);
        }

        if (!it->selectedIons.empty())
        { 
            info.mz = it->selectedIons[0].cvParam(MS_selected_ion_m_z).value;
            info.intensity = it->selectedIons[0].cvParam(MS_peak_intensity).value;
            info.charge = it->selectedIons[0].cvParam(MS_charge_state).value;
        }

        if (!it->activation.empty())
        {
            if (it->activation.hasCVParam(MS_ETD))
            {
                info.activation = "ETD";

                if (it->activation.hasCVParam(MS_CID))
                    info.activation += "+SA";
            }
            else if (it->activation.hasCVParam(MS_ECD))
            {
                info.activation = "ECD";
            }
            else if (it->activation.hasCVParam(MS_CID))
            {
                info.activation = "CID";
            }
            else if (it->activation.hasCVParam(MS_HCD))
            {
                info.activation = "HCD";
            }

            if (it->activation.hasCVParam(MS_CID) || it->activation.hasCVParam(MS_HCD))
                info.collisionEnergy = it->activation.cvParam(MS_collision_energy).value;
        }

        info.windowWideness = 0;
        if (!it->isolationWindow.empty())
        {
            CVParam isolationWindowLowerOffset = it->isolationWindow.cvParam(MS_isolation_window_lower_offset);
            CVParam isolationWindowUpperOffset = it->isolationWindow.cvParam(MS_isolation_window_upper_offset);
            if (!isolationWindowLowerOffset.empty() && !isolationWindowUpperOffset.empty())
                info.windowWideness = fabs(isolationWindowLowerOffset.valueAs<double>()) + isolationWindowUpperOffset.valueAs<double>();
        }

        if (!info.empty()) result.push_back(info);
    }

    return result;
}


void write_precursors(XMLWriter& xmlWriter, const vector<PrecursorInfo>& precursorInfo)
{
    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);

    for (vector<PrecursorInfo>::const_iterator it=precursorInfo.begin(); 
         it!=precursorInfo.end(); ++it)
    {    
        XMLWriter::Attributes attributes;
        if (!it->scanNum.empty())
            attributes.add("precursorScanNum", it->scanNum);
        if (it->intensity.empty())
            attributes.add("precursorIntensity", "0"); // required attribute
        else
            attributes.add("precursorIntensity", it->intensity);
        if (!it->charge.empty())
            attributes.add("precursorCharge", it->charge);
        if (!it->activation.empty())
            attributes.add("activationMethod", it->activation);
        if (it->windowWideness != 0)
            attributes.add("windowWideness", it->windowWideness);

        xmlWriter.startElement("precursorMz", attributes);
        xmlWriter.characters(it->mz, false);
        xmlWriter.endElement();
    }

    xmlWriter.popStyle();
}


void write_peaks(XMLWriter& xmlWriter, const vector<MZIntensityPair>& mzIntensityPairs,
                 const Serializer_mzXML::Config& config)
{
    BinaryDataEncoder::Config bdeConfig = config.binaryDataEncoderConfig;
    bdeConfig.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian; // mzXML always big endian

    BinaryDataEncoder encoder(bdeConfig);
    string encoded;
    size_t binaryByteCount; // size before base64 encoding
    XMLWriter::Attributes attributes;

    if (!mzIntensityPairs.empty())
        encoder.encode(reinterpret_cast<const double*>(&mzIntensityPairs[0]), 
                       mzIntensityPairs.size()*2, encoded, &binaryByteCount);
    else
    {
        binaryByteCount = 0;
        attributes.add("xsi:nil", "true");
    }

    string precision = bdeConfig.precision == BinaryDataEncoder::Precision_32 ? "32" : "64";
    if (bdeConfig.compression == BinaryDataEncoder::Compression_Zlib)
    {
        attributes.add("compressionType", "zlib");
        attributes.add("compressedLen", binaryByteCount);
    }
    else
    {
        attributes.add("compressionType", "none");
        attributes.add("compressedLen", "0");
    }

    attributes.add("precision", precision);
    attributes.add("byteOrder", "network");
    attributes.add("contentType", "m/z-int");

    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner |
                        XMLWriter::StyleFlag_AttributesOnMultipleLines);
    xmlWriter.startElement("peaks", attributes);
    xmlWriter.characters(encoded, false);
    xmlWriter.endElement();
    xmlWriter.popStyle();
}


IndexEntry write_scan(XMLWriter& xmlWriter,
                      CVID nativeIdFormat,
                      const Spectrum& spectrum,
                      const SpectrumListPtr spectrumListPtr,
                      const Serializer_mzXML::Config& config,
                      map<InstrumentConfigurationPtr, int>& instrumentIndexByPtr)
{
    IndexEntry result;
    result.offset = xmlWriter.positionNext();

    // mzXML scanNumber takes a different form depending on the source's nativeID format
    if (MS_multiple_peak_list_nativeID_format == nativeIdFormat)  // 0-based
    {
        result.scanNumber = spectrum.index+1;  // mzXML is 1-based
    }
    else
    {
        string scanNumberStr = id::translateNativeIDToScanNumber(nativeIdFormat, spectrum.id);
        if (scanNumberStr.empty())
            result.scanNumber = spectrum.index+1; // scanNumber is a 1-based index for some nativeID formats
        else
            result.scanNumber = lexical_cast<int>(scanNumberStr);
    }
    // get info

    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];

    CVParam spectrumTypeParam = spectrum.cvParamChild(MS_spectrum_type);

    string scanType;
    switch (spectrumTypeParam.cvid)
    {
        case MS_MSn_spectrum:
        case MS_MS1_spectrum:
            scanType = "Full";
            break;

        case MS_CRM_spectrum: scanType = "CRM"; break;
        case MS_SIM_spectrum: scanType = "SIM"; break;
        case MS_SRM_spectrum: scanType = "SRM"; break;
        case MS_precursor_ion_spectrum: scanType = "Q1"; break;
        case MS_constant_neutral_gain_spectrum: case MS_constant_neutral_loss_spectrum: scanType = "Q3"; break;
        default: break;
    }

    //string scanEvent = scan.cvParam(MS_preset_scan_configuration).value;
    string msLevel = spectrum.cvParam(MS_ms_level).value;
    string polarity = getPolarity(spectrum);
    string retentionTime = getRetentionTime(scan);
    string lowMz = spectrum.cvParam(MS_lowest_observed_m_z).value;
    string highMz = spectrum.cvParam(MS_highest_observed_m_z).value;
    string basePeakMz = spectrum.cvParam(MS_base_peak_m_z).value;
    string basePeakIntensity = spectrum.cvParam(MS_base_peak_intensity).value;
    string totIonCurrent = spectrum.cvParam(MS_total_ion_current).value;
    string filterLine = spectrum.cvParam(MS_filter_string).value;
	string compensationVoltage;
    if (spectrum.hasCVParam(MS_FAIMS_compensation_voltage))
        compensationVoltage = spectrum.cvParam(MS_FAIMS_compensation_voltage).value;
    bool isCentroided = spectrum.hasCVParam(MS_centroid_spectrum);

    vector<PrecursorInfo> precursorInfo = getPrecursorInfo(spectrum, spectrumListPtr, nativeIdFormat);

    vector<MZIntensityPair> mzIntensityPairs;
    spectrum.getMZIntensityPairs(mzIntensityPairs);

    // write out xml

    XMLWriter::Attributes attributes;
    attributes.add("num", result.scanNumber);
    //if (!scanEvent.empty())
    //    attributes.add("scanEvent", scanEvent);
    if (!scanType.empty())
        attributes.add("scanType", scanType);

    if (!filterLine.empty())
        attributes.add("filterLine", filterLine);

    // TODO: write this attribute only when SpectrumList_PeakPicker has processed the spectrum
    attributes.add("centroided", isCentroided ? "1" : "0");

    attributes.add("msLevel", msLevel);
    attributes.add("peaksCount", mzIntensityPairs.size());
    if (!polarity.empty())
        attributes.add("polarity", polarity);
    attributes.add("retentionTime", retentionTime);
    if (!precursorInfo.empty())
    {
        if(!precursorInfo[0].collisionEnergy.empty())
            attributes.add("collisionEnergy", precursorInfo[0].collisionEnergy);
    }
    if (!lowMz.empty())
        attributes.add("lowMz", lowMz);
    if (!highMz.empty())
        attributes.add("highMz", highMz);
    if (!basePeakMz.empty())
        attributes.add("basePeakMz", basePeakMz);
    if (!basePeakIntensity.empty())
        attributes.add("basePeakIntensity", basePeakIntensity);
    if (!totIonCurrent.empty())
        attributes.add("totIonCurrent", totIonCurrent);
    if (!compensationVoltage.empty())
        attributes.add("compensationVoltage", compensationVoltage);

    if (scan.instrumentConfigurationPtr.get())
        attributes.add("msInstrumentID", instrumentIndexByPtr[scan.instrumentConfigurationPtr]);

    xmlWriter.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
    xmlWriter.startElement("scan", attributes);
    xmlWriter.popStyle();

    write_precursors(xmlWriter, precursorInfo);
    write_peaks(xmlWriter, mzIntensityPairs, config);

    // write userParams as arbitrary nameValue elements
    BOOST_FOREACH(const UserParam& userParam, spectrum.userParams)
    {
        attributes.clear();
        attributes.add("name", userParam.name);
        attributes.add("value", userParam.value);
        xmlWriter.startElement("nameValue", attributes, XMLWriter::EmptyElement);
    }

    xmlWriter.endElement(); // scan

    return result;
}


void write_scans(XMLWriter& xmlWriter, const MSData& msd, 
                 const Serializer_mzXML::Config& config, vector<IndexEntry>& index,
                 const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
                 map<InstrumentConfigurationPtr, int>& instrumentIndexByPtr)
{
    SpectrumListPtr sl = msd.run.spectrumListPtr;
    if (!sl.get()) return;

    CVID defaultNativeIdFormat = id::getDefaultNativeIDFormat(msd);
    SpectrumWorkerThreads spectrumWorkers(*sl);

    for (size_t i=0; i<sl->size(); i++)
    {
        // send progress updates, handling cancel

        IterationListener::Status status = IterationListener::Status_Ok;

        if (iterationListenerRegistry)
            status = iterationListenerRegistry->broadcastUpdateMessage(
                IterationListener::UpdateMessage(i, sl->size()));

        if (status == IterationListener::Status_Cancel)
            break;

        //SpectrumPtr spectrum = sl->spectrum(i, true);
        SpectrumPtr spectrum = spectrumWorkers.processBatch(i);

        // Thermo spectra not from "controllerType=0 controllerNumber=1" are ignored
        if (defaultNativeIdFormat == MS_Thermo_nativeID_format &&
            spectrum->id.find("controllerType=0 controllerNumber=1") != 0)
            continue;

        // scans from a source file other than the default are ignored;
        // note: multiple parentFile elements in mzXML are intended to represent
        //       the data processing history of a single source file
        if (spectrum->sourceFilePtr.get() &&
            spectrum->sourceFilePtr != msd.run.defaultSourceFilePtr)
            continue;

        // write the spectrum
        index.push_back(write_scan(xmlWriter, defaultNativeIdFormat, *spectrum, msd.run.spectrumListPtr, config, instrumentIndexByPtr));

    }
}


void write_index(XMLWriter& xmlWriter, const vector<IndexEntry>& index)
{
    XMLWriter::Attributes attributes;
    attributes.add("name", "scan");
    xmlWriter.startElement("index", attributes);

    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
    for (vector<IndexEntry>::const_iterator it=index.begin(); it!=index.end(); ++it)
    {
        attributes.clear();
        attributes.add("id", it->scanNumber);
        xmlWriter.startElement("offset", attributes);
        xmlWriter.characters(lexical_cast<string>(it->offset), false);
        xmlWriter.endElement(); // offset
    }
    xmlWriter.popStyle();
     
    xmlWriter.endElement(); // index
}


} // namespace


void Serializer_mzXML::Impl::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    SHA1OutputObserver sha1OutputObserver;
    XMLWriter::Config config;
    config.outputObserver = &sha1OutputObserver;
    XMLWriter xmlWriter(os, config);

    string xmlData = "version=\"1.0\" encoding=\"ISO-8859-1\""; // TODO: UTF-8 ?
    xmlWriter.processingInstruction("xml", xmlData);

    start_mzXML(xmlWriter);

    map<InstrumentConfigurationPtr, int> instrumentIndexByPtr;

    start_msRun(xmlWriter, msd);
    write_parentFile(xmlWriter, msd);  
    write_msInstruments(xmlWriter, msd, cvTranslator_, instrumentIndexByPtr);
    write_dataProcessing(xmlWriter, msd, cvTranslator_);
    vector<IndexEntry> index;
    write_scans(xmlWriter, msd, config_, index, iterationListenerRegistry, instrumentIndexByPtr);
    xmlWriter.endElement(); // msRun 

    stream_offset indexOffset = xmlWriter.positionNext();

    if (config_.indexed && msd.run.spectrumListPtr && msd.run.spectrumListPtr->size() > 0)
    {
        write_index(xmlWriter, index);

        xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
        xmlWriter.startElement("indexOffset");
        xmlWriter.characters(lexical_cast<string>(indexOffset), false);
        xmlWriter.endElement();
        xmlWriter.popStyle();
    }

    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
    xmlWriter.startElement("sha1");
    xmlWriter.characters(sha1OutputObserver.hash(), false);
    xmlWriter.endElement();
    xmlWriter.popStyle();

    xmlWriter.endElement(); // mzXML
}


//
// read() implementation
//


namespace {


void splitFilename(const string& fullpath, string& path, string& basename)
{
    string::size_type lastSlash = fullpath.find_last_of("/\\");
    if (lastSlash==string::npos || lastSlash==fullpath.size()-1)
    {
        path.clear();
        basename = fullpath; 
        return;
    }

    path = fullpath.substr(0, lastSlash);
    basename = fullpath.substr(lastSlash+1);
}


CVID translate_parentFilenameToSourceFileType(const string& name)
{
    string fileExtension = bal::to_lower_copy(bfs::extension(name));

    // check for known vendor formats
    if (fileExtension == ".raw")
    {
        // (Mass)Wolf-MRM or other non-compliant Waters converters might
        // conflict with this case, i.e. the extension could be from a Waters .raw directory
        // instead of a Thermo RAW file; these aberrant cases will be fixed globally by fillInMetadata()
        return MS_Thermo_RAW_format;
    }
    else if (fileExtension == ".dat")                           return MS_Waters_raw_format;
    else if (fileExtension == ".wiff")                          return MS_ABI_WIFF_format;
    else if (fileExtension == ".yep")                           return MS_Bruker_Agilent_YEP_format;
    else if (fileExtension == ".baf")                           return MS_Bruker_BAF_format;
    else if (name == "fid")                                     return MS_Bruker_FID_format;
    else if (bal::iequals(name, "msprofile.bin"))               return MS_Agilent_MassHunter_format;
    else if (bal::iequals(name, "mspeak.bin"))                  return MS_Agilent_MassHunter_format;
    else if (bal::iequals(name, "msscan.bin"))                  return MS_Agilent_MassHunter_format;
    else if (fileExtension == ".t2d")                           return MS_SCIEX_TOF_TOF_T2D_format;

    // check for known open formats
    else if (fileExtension == ".mzdata")                        return MS_PSI_mzData_format;
    else if (fileExtension == ".mgf")                           return MS_Mascot_MGF_format;
    else if (fileExtension == ".dta")                           return MS_DTA_format;
    else if (fileExtension == ".pkl")                           return MS_Micromass_PKL_format;
    else if (fileExtension == ".mzxml")                         return MS_ISB_mzXML_format;
    else if (bal::iends_with(name, ".mz.xml"))                  return MS_ISB_mzXML_format;
    else if (fileExtension == ".mzml")                          return MS_mzML_format;

    // This case is nasty for several reasons:
    // 1) a .d suffix almost certainly indicates a directory, not a file (so no SHA-1)
    // 2) the same suffix is used by multiple different formats (Agilent/Bruker YEP, Bruker BAF, Agilent ms*.bin)
    // 3) all the formats use the same nativeID style ("scan=123") so just treat it like an mzXML source
    // Therefore this "file" extension is quite useless.
    else if (fileExtension == ".d")                             return MS_ISB_mzXML_format;

    else                                                        return CVID_Unknown;
}


CVID translateSourceFileTypeToNativeIdFormat(CVID sourceFileType)
{
    switch (sourceFileType)
    {
        // for these sources we treat the scan number as the nativeID
        case MS_Thermo_RAW_format:            return MS_Thermo_nativeID_format;
        case MS_Bruker_Agilent_YEP_format:    return MS_Bruker_Agilent_YEP_nativeID_format;
        case MS_Bruker_BAF_format:            return MS_Bruker_BAF_nativeID_format;
        case MS_ISB_mzXML_format:             return MS_scan_number_only_nativeID_format;
        case MS_PSI_mzData_format:            return MS_spectrum_identifier_nativeID_format;
        case MS_Mascot_MGF_format:            return MS_multiple_peak_list_nativeID_format;
        case MS_DTA_format:                   return MS_scan_number_only_nativeID_format;
        case MS_Agilent_MassHunter_format:    return MS_Agilent_MassHunter_nativeID_format;

        // for these sources we must assume the scan number came from the index
        case MS_ABI_WIFF_format:
        case MS_Bruker_FID_format:
        case MS_SCIEX_TOF_TOF_T2D_format:
        case MS_Waters_raw_format:
        case MS_Micromass_PKL_format:
            return MS_scan_number_only_nativeID_format;

        // in other cases, assume the source file doesn't contain instrument data
        case CVID_Unknown:
            return MS_no_nativeID_format;

        default:
            throw runtime_error("[Serializer_mzXML::translateSourceFileTypeToNativeIdFormat] unknown file type");
    }
}


void process_parentFile(const string& fileName, const string& fileType,
                        const string& fileSha1, MSData& msd)
{
    string name, location;
    splitFilename(fileName, location, name);
    
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile));
    SourceFile& sf = *msd.fileDescription.sourceFilePtrs.back();

    sf.id = name;
    sf.name = name;
    sf.location = location;

    if (fileType != "RAWData" && fileType != "processedData")
        throw runtime_error("[Serializer_mzXML::process_parentFile] invalid value for fileType attribute");

    // TODO: if fileSha1 is empty or invalid, log a warning
    sf.set(MS_SHA_1, fileSha1);
}


SoftwarePtr registerSoftware(MSData& msd, 
                             const string& type, const string& name, const string& version, 
                             const CVTranslator& cvTranslator)
{
    SoftwarePtr result;

    // see if we already registered this Software 
    for (vector<SoftwarePtr>::const_iterator it=msd.softwarePtrs.begin();
         it!=msd.softwarePtrs.end(); ++it)
    {
        CVParam softwareParam = (*it)->cvParamChild(MS_software);

        if (softwareParam.cvid == cvTranslator.translate(name) &&
            (*it)->version == version)
            result = *it;
    }

    // create a new entry
    if (!result.get()) 
    {
        result = SoftwarePtr(new Software);
        msd.softwarePtrs.push_back(result); 
    }

    result->id = name + " software";
    LegacyAdapter_Software adapter(result, msd, cvTranslator);
    adapter.name(name);
    adapter.version(version);
    adapter.type(type);

    return result;
}


class HandlerScanFileContent : public SAXParser::Handler
{
    MSData& msd_;
    bool hasCentroidDataProcessing;
    bool hasCentroidScan;
    bool hasProfileScan;

    public:

    HandlerScanFileContent(MSData& msd, bool hasCentroidDataProcessing)
    :   msd_(msd), hasCentroidDataProcessing(hasCentroidDataProcessing), hasCentroidScan(false), hasProfileScan(false)
    {
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "scan")
            throw runtime_error("[Serializer_mzXML::HandlerScanFileContent] Index offset does not point at <scan> element.");

        string msLevel, centroided, scanType;

        getAttribute(attributes, "msLevel", msLevel);
        getAttribute(attributes, "scanType", scanType);
        getAttribute(attributes, "centroided", centroided);
        //TODO: use this: getAttribute(attributes, "deisotoped", deisotoped);
        //TODO: use this: getAttribute(attributes, "chargeDeconvoluted", chargeDeconvoluted);

        // set spectrum type by scanType attribute (assume MSn/Full if absent)
        boost::to_lower(scanType);
        boost::trim(msLevel);
        if (scanType.empty() || scanType == "full" || scanType == "zoom")
            msd_.fileDescription.fileContent.set(msLevel == "1" ? MS_MS1_spectrum : MS_MSn_spectrum);
        else if (scanType == "q1")
            msd_.fileDescription.fileContent.set(MS_precursor_ion_spectrum);
        else if (scanType == "q3")
            msd_.fileDescription.fileContent.set(MS_product_ion_spectrum);
        else if (scanType == "sim" ||
                 scanType == "srm" ||
                 scanType == "mrm" || // HACK: mzWiff (ABI) and wolf-mrm (Waters) use this value
                 scanType == "multiplereaction" || // HACK: Trapper (Agilent) uses this value
                 scanType == "crm")
        {
            // SIM/SRM spectra are accessed as chromatograms
        }

        if (!hasCentroidScan || !hasProfileScan)
        {
            // if the global data processing says spectra were centroided, the default
            // spectrum representation is centroid, otherwise profile
            if (centroided.empty())
            {
                if (!hasCentroidScan && hasCentroidDataProcessing)
                {
                    hasCentroidScan = true;
                    msd_.fileDescription.fileContent.set(MS_centroid_spectrum);
                }
                else if (!hasProfileScan && !hasCentroidDataProcessing)
                {
                    hasProfileScan = true;
                    msd_.fileDescription.fileContent.set(MS_profile_spectrum);
                }
            }
            // non-empty centroided attribute overrides the default spectrum representation
            else if (!hasCentroidScan && centroided == "1")
            {
                hasCentroidScan = true;
                msd_.fileDescription.fileContent.set(MS_centroid_spectrum);
            }
            else if (!hasProfileScan && centroided == "0")
            {
                hasProfileScan = true;
                msd_.fileDescription.fileContent.set(MS_profile_spectrum);
            }
        }

        return Status::Done;
    }
};

void fillInMetadata(MSData& msd)
{
    // check for (Mass)Wolf-MRM metadata: multiple parentFiles with the same .raw URI
    set<string> uniqueURIs;
    BOOST_FOREACH(SourceFilePtr& sf, msd.fileDescription.sourceFilePtrs)
    {
        pair<set<string>::iterator, bool> ir = uniqueURIs.insert((sf->location.empty() ? "" : sf->location + '/') + sf->name);
        if (!ir.second)
        {
            // found duplicate URI: remove all the .raw sourceFiles (leave only the mzXML)
            SourceFilePtr firstSourceFile = msd.fileDescription.sourceFilePtrs[0];
            msd.fileDescription.sourceFilePtrs.assign(1, firstSourceFile);
            break;
        }
    }

    // add nativeID type and source file type to the remaining source files
    set<string> uniqueIDs;
    BOOST_FOREACH(SourceFilePtr& sf, msd.fileDescription.sourceFilePtrs)
    {
        CVID sourceFileType = translate_parentFilenameToSourceFileType(sf->name);
        // TODO: if sourceFileType is CVID_Unknown, log a warning
        sf->set(sourceFileType);
        sf->set(translateSourceFileTypeToNativeIdFormat(sourceFileType));

        if (sourceFileType == MS_Bruker_FID_format || sourceFileType == MS_SCIEX_TOF_TOF_T2D_format)
        {       
            // each source file is translated to a run ID and added to a set of potential ids;
            // if they all have a common prefix, that is used as the id, otherwise it stays empty
            string runId = translate_SourceFileTypeToRunID(*sf, sourceFileType);
            if (!runId.empty())
                uniqueIDs.insert(runId);
        }
        else
        {
            if (msd.id.empty())
                msd.id = msd.run.id = translate_SourceFileTypeToRunID(*sf, sourceFileType);
        }
    }

    string lcp = longestCommonPrefix(uniqueIDs);
    if (!lcp.empty())
    {
        // part of the prefix after the source name might need to be trimmed off, e.g.:
        // path/to/source/1A/1/1SRef/fid, path/to/source/1B/1/1SRef/fid, lcp: path/to/source/1, run id: source (not "1")
        // path/to/source/1A/1/1SRef/fid, path/to/source/2A/1/1SRef/fid, lcp: path/to/source/, run id: source
        if (*lcp.rbegin() == '/')
            msd.id = msd.run.id = BFS_STRING(bfs::path(lcp).leaf());
        else
            msd.id = msd.run.id = BFS_STRING(bfs::path(lcp).parent_path().leaf());
    }
}


struct Handler_msInstrument : public SAXParser::Handler
{
    InstrumentConfiguration* instrumentConfiguration;

    Handler_msInstrument(MSData& msd, const CVTranslator& cvTranslator)
    :   instrumentConfiguration(0), msd_(msd), cvTranslator_(cvTranslator)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!instrumentConfiguration)
            throw runtime_error("[Serializer_mzXML::Handler_msInstrument] Null instrumentConfiguration.");

        string value;
        getAttribute(attributes, "value", value);

        if (name=="msInstrument")
        {
            manufacturer_ = model_ = ionisation_ = analyzer_ = detector_ = "";
            return Status::Ok;
        }
        else if (name == "instrument") // older mzXML
        {
            manufacturer_ = model_ = ionisation_ = analyzer_ = detector_ = "";
            getAttribute(attributes, "manufacturer", manufacturer_);
            getAttribute(attributes, "model", model_);
            getAttribute(attributes, "ionisation", ionisation_);
            getAttribute(attributes, "msType", analyzer_);
            return Status::Ok;
        }
        else if (name == "msManufacturer")
        {
            manufacturer_ = value;
            return Status::Ok;
        }
        else if (name == "msModel")
        {
            model_ = value;
            return Status::Ok;
        }
        else if (name == "msIonisation")
        {
            ionisation_ = value;
            return Status::Ok;
        }
        else if (name == "msMassAnalyzer")
        {
            analyzer_ = value;
            return Status::Ok;
        }
        else if (name == "msDetector")
        {
            detector_ = value;
            return Status::Ok;
        }
        else if (name == "msResolution")
        {
            // TODO: use this to set instrument resolution?
            return Status::Ok;
        }
        else if (name == "software")
        {
            string type, name, version;
            getAttribute(attributes, "type", type);
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "version", version);
            instrumentConfiguration->softwarePtr = registerSoftware(msd_, type, name, version, cvTranslator_);
            return Status::Ok;
        }
        else if (name == "operator")
        {
            // TODO: use this to make a Contact
            return Status::Ok;
        }
        else if (name == "nameValue")
        {
            // TODO: use this?
            return Status::Ok;
        }
        else if (name == "comment")
        {
            // TODO: use this?
            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::Handler_msInstrument] Unexpected element name: " + name).c_str());
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name=="msInstrument" || name=="instrument")
        {
            if (!instrumentConfiguration)
                throw runtime_error("[Serializer_mzXML::Handler_msInstrument] Null instrumentConfiguration.");

            instrumentConfiguration->componentList.push_back(Component(ComponentType_Source, 1));
            instrumentConfiguration->componentList.push_back(Component(ComponentType_Analyzer, 1));
            instrumentConfiguration->componentList.push_back(Component(ComponentType_Detector, 1));

            LegacyAdapter_Instrument adapter(*instrumentConfiguration, cvTranslator_);
            adapter.manufacturerAndModel(manufacturer_, model_); 

            if(adapter.model() == "LTQ Orbitrap XL" && analyzer_ == "FTMS") 
                analyzer_ = "orbitrap"; // hack to set analyzer_ correctly for LTQ ORBI

            adapter.ionisation(ionisation_);
            adapter.analyzer(analyzer_);
            adapter.detector(detector_);
        }

        return Status::Ok;
    }
 
    private:

    MSData& msd_;
    const CVTranslator& cvTranslator_;

    string manufacturer_;
    string model_;
    string ionisation_;
    string analyzer_;
    string detector_;
};


struct Handler_dataProcessing : public SAXParser::Handler
{
    bool hasCentroidDataProcessing;

    Handler_dataProcessing(MSData& msd, const CVTranslator& cvTranslator)
    :   hasCentroidDataProcessing(false), msd_(msd), cvTranslator_(cvTranslator)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "dataProcessing")
        {
            string centroided, deisotoped;
            getAttribute(attributes, "centroided", centroided);
            getAttribute(attributes, "deisotoped", deisotoped);

            hasCentroidDataProcessing = centroided == "1";

            // TODO: term for charge-deconvolution?

            if (hasCentroidDataProcessing || deisotoped == "1")
            {
                DataProcessingPtr dpPtr(new DataProcessing("dataProcessing"));
                msd_.dataProcessingPtrs.push_back(dpPtr);

                ProcessingMethod method;
                method.order = 0;
                if (hasCentroidDataProcessing) method.set(MS_peak_picking);
                if (deisotoped == "1") method.set(MS_deisotoping);
                dpPtr->processingMethods.push_back(method);
            }

            return Status::Ok;
        }
        else if (name == "software")
        {
            string type, name, version;
            getAttribute(attributes, "type", type);
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "version", version);
            registerSoftware(msd_, type, name, version, cvTranslator_);
            return Status::Ok;
        }
        else if (name == "processingOperation")
        {   // ignore its only attribute, which is "comment"
            return Status::Ok;
        }
        else if (name == "comment")
        {
            // just ignore
            return Status::Ok;
        }

        throw runtime_error(("[Serializer_mzXML::Handler_dataProcessing] Unexpected element name: " + name).c_str());
    }

    private:
    MSData& msd_;
    const CVTranslator& cvTranslator_;
};


class Handler_mzXML : public SAXParser::Handler
{
    public:

    bool hasCentroidDataProcessing;

    Handler_mzXML(MSData& msd, const CVTranslator& cvTranslator)
    :   msd_(msd), 
        handler_msInstrument_(msd, cvTranslator), 
        handler_dataProcessing_(msd, cvTranslator)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "mzXML" || name == "msRun")
        {
            return Status::Ok;
        }
        else if (name == "parentFile")
        {
            string fileName, fileType, fileSha1;
            getAttribute(attributes, "fileName", fileName);
            getAttribute(attributes, "fileType", fileType);
            getAttribute(attributes, "fileSha1", fileSha1);
            process_parentFile(fileName, fileType, fileSha1, msd_);
            return Status::Ok;
        }
        else if (name=="msInstrument" || name=="instrument")
        {
            string id;
            getAttribute(attributes, "msInstrumentID", id);
            if (id.empty()) getAttribute(attributes, "id", id);
            if (id.empty()) getAttribute(attributes, "ID", id); // hack: id or ID
            if (id.empty()) id = "IC1"; // xml:ID cannot be empty
            msd_.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration(id)));
            handler_msInstrument_.instrumentConfiguration = msd_.instrumentConfigurationPtrs.back().get();
            return Status(Status::Delegate, &handler_msInstrument_);
        }
        else if (name == "dataProcessing")
        {
            return Status(Status::Delegate, &handler_dataProcessing_);
        }
        else if (name == "scan" || name == "index" || name == "sha1")
        {
            // all file-level metadata has been parsed, but there are some gaps to fill
            fillInMetadata(msd_);
            hasCentroidDataProcessing = handler_dataProcessing_.hasCentroidDataProcessing;
            return Status::Done;
        }

        throw runtime_error(("[Serializer_mzXML::Handler_mzXML] Unexpected element name: " + name).c_str());
    }

    private:
    MSData& msd_;
    Handler_msInstrument handler_msInstrument_;
    Handler_dataProcessing handler_dataProcessing_;
};

} // namespace


void Serializer_mzXML::Impl::read(shared_ptr<istream> is, MSData& msd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_mzXML::read()] Bad istream.");

    is->seekg(0);
    Handler_mzXML handler(msd, cvTranslator_);
    SAXParser::parse(*is, handler);

    msd.run.spectrumListPtr = SpectrumList_mzXML::create(is, msd, config_.indexed);

    HandlerScanFileContent handlerScanFileContent(msd, handler.hasCentroidDataProcessing);
    for (size_t i=0; i < msd.run.spectrumListPtr->size(); ++i)
    {
        bio::stream_offset offset = msd.run.spectrumListPtr->spectrumIdentity(i).sourceFilePosition;
        is->seekg(bio::offset_to_position(offset));

        SAXParser::parse(*is, handlerScanFileContent);
    }
}


//
// Serializer_mzXML
//


PWIZ_API_DECL Serializer_mzXML::Serializer_mzXML(const Config& config)
:   impl_(new Impl(config))
{}


PWIZ_API_DECL void Serializer_mzXML::write(ostream& os, const MSData& msd,
   const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(os, msd, iterationListenerRegistry);
}


PWIZ_API_DECL void Serializer_mzXML::read(shared_ptr<istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Serializer_mzXML::Config& config)
{
    os << config.binaryDataEncoderConfig 
       << " indexed=\"" << boolalpha << config.indexed << "\"";
    return os;
}


} // namespace msdata
} // namespace pwiz


