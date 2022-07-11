//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "ChromatogramList_Thermo.hpp"


#ifdef PWIZ_READER_THERMO
#include "Reader_Thermo_Detail.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/range/algorithm/for_each.hpp>


using namespace pwiz::cv;


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Thermo;

ChromatogramList_Thermo::ChromatogramList_Thermo(const MSData& msd, RawFilePtr rawfile, const Reader::Config& config)
:   msd_(msd), rawfile_(rawfile), indexInitialized_(util::init_once_flag_proxy), config_(config)
{
}


PWIZ_API_DECL size_t ChromatogramList_Thermo::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    return index_.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Thermo::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


PWIZ_API_DECL size_t ChromatogramList_Thermo::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, DetailLevel detailLevel) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    const IndexEntry& ci = index_[index];
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;
    result->set(ci.chromatogramType);
    if (ci.polarityType != CVID_Unknown)
        result->set(ci.polarityType);

    bool getBinaryData = detailLevel == DetailLevel_FullData;

    try
    {
        rawfile_->setCurrentController(ci.controllerType, ci.controllerNumber);

        switch (ci.chromatogramType)
        {
            default:
                break;

            case MS_TIC_chromatogram:
            {
                if (detailLevel < DetailLevel_FullMetadata)
                    return result;

                CVID intensityUnits = ci.controllerType == Controller_MS ? MS_number_of_detector_counts : UO_picoampere;
                ChromatogramDataPtr cd = rawfile_->getChromatogramData(Type_TIC, ci.filter, 0, 0, 0, rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData)
                {
                    result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, intensityUnits);

                    if (ci.controllerType == Controller_MS)
                    {
                        auto msLevelArray = boost::make_shared<IntegerDataArray>();
                        result->integerDataArrayPtrs.emplace_back(msLevelArray);
                        msLevelArray->set(MS_non_standard_data_array, "ms level", UO_dimensionless_unit);
                        msLevelArray->data.resize(cd->times().size());
                        for (size_t i = 0; i < cd->times().size(); ++i)
                            msLevelArray->data[i] = rawfile_->getMSOrder(rawfile_->scanNumber(cd->times()[i]));
                    }

                    if (intensityUnits == UO_picoampere)
                    {
                        // Thermo seems to store CAD intensities as attoAmps but shows them as picoAmps in QualBrowser;
                        boost::range::for_each(result->getIntensityArray()->data, [&](auto& v) {v *= 1e-6;});
                    }
                }
                else result->defaultArrayLength = cd->size();
            }
            break;

            case MS_SIC_chromatogram: // generate SIC for <precursor>
            {
                if (detailLevel < DetailLevel_FullMetadata)
                    return result;

                ChromatogramDataPtr cd = rawfile_->getChromatogramData(Type_MassRange, index_[index].filter, 0, 100000, 0, rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData) result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, MS_number_of_detector_counts);
                else result->defaultArrayLength = cd->size();
            }
            break;

            /*case 3: // generate SRM TIC for <precursor>
            {
                vector<string> tokens;
                bal::split(tokens, ci.id, bal::is_any_of(" "));
                ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                    Type_TIC, Operator_None, Type_MassRange,
                    "ms2 " + tokens[2], "", "", 0,
                    0, rawfile_->rt(rawfile_->value(NumSpectra)),
                    Smoothing_None, 0);
                pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
                if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_detector_counts);
                else result->defaultArrayLength = cd->size();
            }
            break;*/

            case MS_SRM_chromatogram:
            {
                result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.q1, MS_m_z);
                ScanInfoPtr scanInfo = rawfile_->getScanInfoFromFilterString(ci.filter);
                ActivationType activationType = scanInfo->activationType();
                if (activationType == ActivationType_Unknown)
                    activationType = ActivationType_CID; // assume CID
                string polarity = polarityStringForFilter(ci.polarityType);

                bool hasSupplemental = scanInfo->supplementalActivationType() == ActivationType_ETD;

                setActivationType(activationType, hasSupplemental ? scanInfo->supplementalActivationType() : ActivationType_Unknown, result->precursor.activation);
                if (activationType == ActivationType_CID)
                    result->precursor.activation.set(MS_collision_energy, scanInfo->precursorActivationEnergy(0));
                if (hasSupplemental && !(scanInfo->supplementalActivationEnergy() > 0))
                    result->precursor.activation.set(MS_supplemental_collision_energy, scanInfo->supplementalActivationEnergy());

                result->product.isolationWindow.set(MS_isolation_window_target_m_z, ci.q3, MS_m_z);
                result->product.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
                result->product.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

                if (detailLevel < DetailLevel_FullMetadata)
                    return result;

                string q1 = (format("%.10g", std::locale::classic()) % ci.q1).str();
                string q3Range = (format("%.10g-%.10g", std::locale::classic())
                                  % (ci.q3 - ci.q3Offset)
                                  % (ci.q3 + ci.q3Offset)
                                 ).str();

                ChromatogramDataPtr cd = rawfile_->getChromatogramData(Type_MassRange,
                    polarity + "SRM ms2 " + q1 + " [" + q3Range + "]", ci.q3 - ci.q3Offset, ci.q3 + ci.q3Offset, 0,
                    rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData) result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, MS_number_of_detector_counts);
                else result->defaultArrayLength = cd->size();
            }
            break;

            case MS_SIM_chromatogram:
            {
                result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.q1, MS_m_z);
                result->precursor.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
                result->precursor.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

                if (detailLevel < DetailLevel_FullMetadata)
                    return result;

                string q1Range = (format("%.10g-%.10g", std::locale::classic())
                                  % (ci.q1 - ci.q3Offset)
                                  % (ci.q1 + ci.q3Offset)
                                 ).str();

                ChromatogramDataPtr cd = rawfile_->getChromatogramData(Type_MassRange,
                    "SIM ms [" + q1Range + "]", ci.q1 - ci.q3Offset, ci.q1 + ci.q3Offset, 0,
                    rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData) result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, MS_number_of_detector_counts);
                else result->defaultArrayLength = cd->size();
            }
            break;

            case MS_absorption_chromatogram: // PDA: generate "Total Scan" chromatogram for entire run
            {
                ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                    Type_TotalScan, "", 0, 0, 0,
                    rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData) result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, UO_absorbance_unit);
                else result->defaultArrayLength = cd->size();
            }
            break;

            case MS_emission_chromatogram: // UV: generate "ECD" chromatogram for entire run
            {
                ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                    Type_ECD, "", 0, 0, 0,
                    rawfile_->getFirstScanTime(), rawfile_->getLastScanTime());
                if (getBinaryData) result->setTimeIntensityArrays(cd->times(), cd->intensities(), UO_minute, UO_absorbance_unit);
                else result->defaultArrayLength = cd->size();
            }
            break;
        }

        return result;
    }
    catch (exception& e)
    {
        throw runtime_error("[ChromatogramList_Thermo::chromatogram()] Error retrieving chromatogram \"" + result->id + "\": " + e.what());
    }
    catch (...)
    {
        throw runtime_error("[ChromatogramList_Thermo::chromatogram()] Unknown exception retrieving chromatogram \"" + result->id + "\"");
    }
}


PWIZ_API_DECL ChromatogramList_Thermo::IndexEntry& ChromatogramList_Thermo::addChromatogram(const string& id, ControllerType controllerType, int controllerNumber, CVID chromatogramType, const string& filter) const
{
    index_.push_back(IndexEntry());
    IndexEntry& ci = index_.back();
    ci.controllerType = controllerType;
    ci.controllerNumber = controllerNumber;
    ci.filter = filter;
    ci.index = index_.size() - 1;
    ci.id = id;
    ci.chromatogramType = chromatogramType;
    ci.polarityType = CVID_Unknown;
    idMap_[ci.id] = ci.index;
    return ci;
}


PWIZ_API_DECL void ChromatogramList_Thermo::createIndex() const
{
    for (int controllerType = Controller_MS;
         controllerType < Controller_Count;
         ++controllerType)
    {
        long numControllers = rawfile_->getNumberOfControllersOfType((ControllerType) controllerType);
        for (long n=1; n <= numControllers; ++n)
        {
            try
            {
                rawfile_->setCurrentController((ControllerType)controllerType, n);
            }
            catch (exception& e)
            {
                // TODO: add warn_once for chromatograms
                cerr << "[ChromatogramList_Thermo::createIndex] error setting controller to " << ControllerTypeStrings[controllerType] << ": " << e.what() << endl;
                continue;
            }

            // skip this controller if it has no spectra
            if (rawfile_->getLastScanNumber() == 0)
                continue;

            switch ((ControllerType) controllerType)
            {
                case Controller_MS:
                {
                    // support file-level TIC for all file types
                    string globalFilter = config_.globalChromatogramsAreMs1Only ? "Full ms" : "";
                    addChromatogram("TIC", (ControllerType) controllerType, n, MS_TIC_chromatogram, globalFilter);

                    // for certain filter types, support additional chromatograms
                    vector<string> filterArray = rawfile_->getFilters();
                    ScanInfoPtr scanInfo = filterArray.empty() ? ScanInfoPtr() : rawfile_->getScanInfoFromFilterString(filterArray[0]);
                    for (size_t i=0, ic=filterArray.size(); i < ic; ++i)
                    {
                        string filterString = filterArray[i];
                        scanInfo->reinitialize(filterString);

                        switch (scanInfo->scanType())
                        {
                            case ScanType_SRM:
                            {
                                // produce spectra rather than a chromatogram
                                if (config_.srmAsSpectra)
                                    break;

                                string precursorMZ = (format("%.10g", std::locale::classic()) % scanInfo->precursorMZ(0)).str();
                                /*index_.push_back(IndexEntry());
                                IndexEntry& ci = index_.back();
                                ci.controllerType = (ControllerType) controllerType;
                                ci.controllerNumber = n;
                                ci.filter = filterString;
                                ci.index = index_.size()-1;
                                ci.id = "SRM TIC " + precursorMZ;
                                ci.q1 = filterParser.precursorMZs_[0];
                                idMap_[ci.id] = ci.index;*/

                                for (size_t j=0, jc=scanInfo->scanRangeCount(); j < jc; ++j)
                                {
                                    double q1 = scanInfo->precursorMZ(0);
                                    double q3 = (scanInfo->scanRange(j).first + scanInfo->scanRange(j).second) / 2.0;
                                    auto polarityType = translate(scanInfo->polarityType());
                                    string polarity = polarityStringForFilter(polarityType);
                                    string id = (format("%sSRM SIC %s,%.10g", std::locale::classic())
                                             % polarity
                                             % precursorMZ
                                             % q3
                                            ).str();
                                    IndexEntry& ci = addChromatogram(id, (ControllerType)controllerType, n, MS_SRM_chromatogram, filterString);
                                    ci.q1 = q1;
                                    ci.q3 = q3;
                                    ci.polarityType = polarityType;
                                    ci.q3Offset = (scanInfo->scanRange(j).second - scanInfo->scanRange(j).first) / 2.0;
                                }
                            }
                            break; // case ScanType_SRM

                            case ScanType_SIM:
                            {
                                // produce spectra rather than a chromatogram
                                if (config_.simAsSpectra)
                                    break;

                                for (size_t j=0, jc=scanInfo->scanRangeCount(); j < jc; ++j)
                                {
                                    double q1 = (scanInfo->scanRange(j).first + scanInfo->scanRange(j).second) / 2.0;
                                    auto polarityType = translate(scanInfo->polarityType());
                                    string polarity = polarityStringForFilter(polarityType);
                                    string id = (format("%sSIM SIC %.10g", std::locale::classic())
                                             % polarity
                                             % q1
                                            ).str();
                                    IndexEntry& ci = addChromatogram(id, (ControllerType)controllerType, n, MS_SIM_chromatogram, filterString);
                                    ci.q1 = q1;
                                    ci.polarityType = polarityType;

                                    // this should be q1Offset
                                    ci.q3Offset = (scanInfo->scanRange(j).second - scanInfo->scanRange(j).first) / 2.0;
                                }
                            }
                            break; // case ScanType_SIM

                            default:
                            case ScanType_Full:
                            /*{
                                string precursorMZ = lexical_cast<string>(filterParser.precursorMZs_[0]);
                                index_.push_back(make_pair(ChromatogramIdentity(), filterString));
                                ChromatogramIdentity& ci = index_.back().first;
                                ci.index = index_.size()-1;
                                ci.id = "SIC " + precursorMZ;
                                idMap_[ci.id] = ci.index;
                            }*/
                            break;
                        }
                    }
                }
                break; // case Controller_MS

                case Controller_PDA:
                {
                    addChromatogram("PDA " + lexical_cast<string>(n), (ControllerType)controllerType, n, MS_absorption_chromatogram, "");
                }
                break; // case Controller_PDA

                case Controller_UV:
                {
                    auto instrumentData = rawfile_->getInstrumentData();
                    if (bal::ends_with(instrumentData.Units, "AbsorbanceUnits") && (instrumentData.AxisLabelY.empty() || bal::starts_with(instrumentData.AxisLabelY, "UV")))
                    {
                        addChromatogram("UV " + lexical_cast<string>(n), (ControllerType)controllerType, n, MS_emission_chromatogram, "");
                    }
                    else if (bal::ends_with(instrumentData.AxisLabelY, "pA")) // picoamperes?
                    {
                        addChromatogram("CAD " + lexical_cast<string>(n), (ControllerType)controllerType, n, MS_TIC_chromatogram, "");
                    }
                    else
                    {
                        // TODO: pressure/flow chromatogram
                    }
                }
                break; // case Controller_UV

                default:
                    // TODO: are there sensible default chromatograms for other controller types?
                    break;
            }
        }
    }

    /*ostringstream imStream;
    std::auto_ptr<LabelValueArray> imArray = rawfile_->getInstrumentMethods();
    for(size_t i=0, end=imArray->size(); i < end; ++i)
        imStream << imArray->label(i) << imArray->value(i) << endl;
    string im = imStream.str();
    //  Parent   Center    Width   Time   CE   Q1PW   Q3PW   TubeLens
    boost::regex scanEventRegex("^\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+$");*/
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Thermo::xic(double startTime, double endTime, const boost::icl::interval_set<double>& massRanges, int msLevel)
{
    /*string msLevelFilter("ms");
    if (msLevel > 1)
        msLevelFilter += lexical_cast<string>(msLevel);

    stringstream massRange;
    bool first = true;
    BOOST_FOREACH(const boost::icl::interval_set<double>::interval_type& range, massRanges)
    {
        if (!first)
        {
            first = false;
            massRange << ",";
        }
        massRange << range.lower() << "-" << range.upper();
    }

    ChromatogramDataPtr cd = rawfile_->getChromatogramData(msLevelFilter,
                                                           massRange.str(), "",
                                                           0,
                                                           startTime, endTime,
                                                           Smoothing_None, 0);
    pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());*/

    ChromatogramPtr result(new Chromatogram);
    //result->id = (boost::format("XIC %1% %2% [%3%-%4%]") % msLevelFilter % massRange.str() % startTime % endTime).str();
    //result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_detector_counts);
    return result;
}


} // detail
} // msdata
} // pwiz

#else // PWIZ_READER_THERMO

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_Thermo::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_Thermo::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_Thermo::find(const string& id) const {return 0;}
ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}
ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, DetailLevel detailLevel) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_THERMO
