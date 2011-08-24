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


using namespace pwiz::cv;


namespace pwiz {
namespace msdata {
namespace detail {


ChromatogramList_Thermo::ChromatogramList_Thermo(const MSData& msd, RawFilePtr rawfile)
:   msd_(msd), rawfile_(rawfile), indexInitialized_(util::init_once_flag_proxy)
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
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    const IndexEntry& ci = index_[index];
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;
    result->set(ci.chromatogramType);

    rawfile_->setCurrentController(ci.controllerType, ci.controllerNumber);

    switch (ci.chromatogramType)
    {
        default:
            break;

        case MS_TIC_chromatogram:
        {
            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_TIC, Operator_None, Type_MassRange,
                "", "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case MS_SIC_chromatogram: // generate SIC for <precursor>
        {
            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                index_[index].filter, "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
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
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;*/

        case MS_SRM_chromatogram:
        {
            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.q1, MS_m_z);

            ScanFilter filterParser;
            filterParser.parse(ci.filter);
            ActivationType activationType = filterParser.activationType_;
            if (activationType == ActivationType_Unknown)
                activationType = ActivationType_CID; // assume CID

            SetActivationType(activationType, result->precursor.activation);
            if (filterParser.activationType_ == ActivationType_CID)
                result->precursor.activation.set(MS_collision_energy, filterParser.precursorEnergies_[0]);

            result->product.isolationWindow.set(MS_isolation_window_target_m_z, ci.q3, MS_m_z);
            result->product.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
            result->product.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

            string q1 = (format("%.10g") % ci.q1).str();
            string q3Range = (format("%.10g-%.10g")
                              % (ci.q3 - ci.q3Offset)
                              % (ci.q3 + ci.q3Offset)
                             ).str();

            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_MassRange, Operator_None, Type_MassRange,
                "SRM ms2 " + q1, q3Range, "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case MS_SIM_chromatogram:
        {
            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.q1, MS_m_z);
            result->precursor.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
            result->precursor.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

            string q1Range = (format("%.10g-%.10g")
                              % (ci.q1 - ci.q3Offset)
                              % (ci.q1 + ci.q3Offset)
                             ).str();

            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                "SIM ms", q1Range, "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case MS_absorption_chromatogram: // generate "Total Scan" chromatogram for entire run
        {
            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_TotalScan, Operator_None, Type_MassRange,
                "", "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case MS_mass_chromatogram: // generate "ECD" chromatogram for entire run
        {
            ChromatogramDataPtr cd = rawfile_->getChromatogramData(
                Type_ECD, Operator_None, Type_MassRange,
                "", "", "", 0,
                0, std::numeric_limits<double>::max(),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;
    }

    return result;
}


PWIZ_API_DECL void ChromatogramList_Thermo::createIndex() const
{
    for (int controllerType = Controller_MS;
         controllerType <= Controller_UV;
         ++controllerType)
    {
        long numControllers = rawfile_->getNumberOfControllersOfType((ControllerType) controllerType);
        for (long n=1; n <= numControllers; ++n)
        {
            rawfile_->setCurrentController((ControllerType) controllerType, n);

            // skip this controller if it has no spectra
            if (rawfile_->value(NumSpectra) == 0)
                continue;

            switch ((ControllerType) controllerType)
            {
                case Controller_MS:
                {
                    // support file-level TIC for all file types
                    index_.push_back(IndexEntry());
                    IndexEntry& ci = index_.back();
                    ci.controllerType = (ControllerType) controllerType;
                    ci.controllerNumber = n;
                    ci.filter = "";
                    ci.index = index_.size()-1;
                    ci.id = "TIC";
                    ci.chromatogramType = MS_TIC_chromatogram;
                    idMap_[ci.id] = ci.index;

                    // for certain filter types, support additional chromatograms
                    auto_ptr<StringArray> filterArray = rawfile_->getFilters();
                    ScanFilter filterParser;
                    for (size_t i=0, ic=filterArray->size(); i < ic; ++i)
                    {
                        string filterString = filterArray->item(i);
                        filterParser.initialize();
                        filterParser.parse(filterString);

                        switch (filterParser.scanType_)
                        {
                            case ScanType_SRM:
                            {
                                string precursorMZ = (format("%.10g") % filterParser.precursorMZs_[0]).str();
                                /*index_.push_back(IndexEntry());
                                IndexEntry& ci = index_.back();
                                ci.controllerType = (ControllerType) controllerType;
                                ci.controllerNumber = n;
                                ci.filter = filterString;
                                ci.index = index_.size()-1;
                                ci.id = "SRM TIC " + precursorMZ;
                                ci.q1 = filterParser.precursorMZs_[0];
                                idMap_[ci.id] = ci.index;*/

                                for (size_t j=0, jc=filterParser.scanRangeMin_.size(); j < jc; ++j)
                                {
                                    index_.push_back(IndexEntry());
                                    IndexEntry& ci = index_.back();
                                    ci.chromatogramType = MS_SRM_chromatogram;
                                    ci.controllerType = (ControllerType) controllerType;
                                    ci.controllerNumber = n;
                                    ci.filter = filterString;
                                    ci.index = index_.size()-1;
                                    ci.q1 = filterParser.precursorMZs_[0];
                                    ci.q3 = (filterParser.scanRangeMin_[j] + filterParser.scanRangeMax_[j]) / 2.0;
                                    ci.id = (format("SRM SIC %s,%.10g")
                                             % precursorMZ
                                             % ci.q3
                                            ).str();
                                    ci.q3Offset = (filterParser.scanRangeMax_[j] - filterParser.scanRangeMin_[j]) / 2.0;
                                    idMap_[ci.id] = ci.index;
                                }
                            }
                            break; // case ScanType_SRM

                            case ScanType_SIM:
                            {
                                for (size_t j=0, jc=filterParser.scanRangeMin_.size(); j < jc; ++j)
                                {
                                    index_.push_back(IndexEntry());
                                    IndexEntry& ci = index_.back();
                                    ci.chromatogramType = MS_SIM_chromatogram;
                                    ci.controllerType = (ControllerType) controllerType;
                                    ci.controllerNumber = n;
                                    ci.filter = filterString;
                                    ci.index = index_.size()-1;
                                    ci.q1 = (filterParser.scanRangeMin_[j] + filterParser.scanRangeMax_[j]) / 2.0;
                                    ci.id = (format("SIM SIC %.10g")
                                             % ci.q1
                                            ).str();
                                    // this should be q1Offset
                                    ci.q3Offset = (filterParser.scanRangeMax_[j] - filterParser.scanRangeMin_[j]) / 2.0;
                                    idMap_[ci.id] = ci.index;
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
                    // "Total Scan" appears to be the equivalent of the TIC
                    index_.push_back(IndexEntry());
                    IndexEntry& ci = index_.back();
                    ci.controllerType = (ControllerType) controllerType;
                    ci.controllerNumber = n;
                    ci.index = index_.size()-1;
                    ci.id = "Total Scan";
                    ci.chromatogramType = MS_absorption_chromatogram;
                    idMap_[ci.id] = ci.index;
                }
                break; // case Controller_PDA

                case Controller_Analog:
                {
                    // "ECD" appears to be the equivalent of the TIC
                    index_.push_back(IndexEntry());
                    IndexEntry& ci = index_.back();
                    ci.controllerType = (ControllerType) controllerType;
                    ci.controllerNumber = n;
                    ci.index = index_.size()-1;
                    ci.id = "ECD";
                    ci.chromatogramType = MS_emission_chromatogram;
                    idMap_[ci.id] = ci.index;
                }

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

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_THERMO
