#define PWIZ_SOURCE

#ifdef PWIZ_READER_THERMO
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "ChromatogramList_Thermo.hpp"
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

ChromatogramList_Thermo::ChromatogramList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile), indexInitialized_(BOOST_ONCE_INIT)
{
}


PWIZ_API_DECL size_t ChromatogramList_Thermo::size() const
{
    boost::call_once(indexInitialized_, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    return index_.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Thermo::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


PWIZ_API_DECL size_t ChromatogramList_Thermo::find(const string& id) const
{
    boost::call_once(indexInitialized_, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, bool getBinaryData) const 
{
    boost::call_once(indexInitialized_, boost::bind(&ChromatogramList_Thermo::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    const IndexEntry& ci = index_[index];
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;

    int mode = 0;

    switch (ci.controllerType)
    {
        case Controller_MS:
            rawfile_->setCurrentController(ci.controllerType, ci.controllerNumber);
            if (ci.id == "TIC") // generate TIC for entire run
            {
                mode = 1;
                result->set(MS_TIC_chromatogram);
            }
            else if(ci.id.find("SIC") == 0) // generate SIC for <precursor>
            {
                mode = 2;
                result->set(MS_SIC_chromatogram);
            }
            else if(ci.id.find(',') == string::npos) // generate SRM TIC for <precursor>
            {
                mode = 3;
                result->set(MS_mass_chromatogram);
            }
            else // generate SRM SIC for transition <precursor>,<product>
            {
                mode = 4;
                result->set(MS_SIC_chromatogram);
            }
            break;

        case Controller_PDA:
            rawfile_->setCurrentController(ci.controllerType, ci.controllerNumber);
            result->set(MS_absorption_chromatogram);
            mode = 5; // generate "Total Scan" chromatogram for entire run
            break;

        case Controller_Analog:
            rawfile_->setCurrentController(ci.controllerType, ci.controllerNumber);
            result->set(MS_mass_chromatogram); // TODO: is this right?
            mode = 6; // generate "ECD" chromatogram for entire run
    }

    switch (mode)
    {
        default:
        case 0:
            break;

        case 1: // generate TIC for entire run
        {
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_TIC, Operator_None, Type_MassRange,
                "", "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 2: // generate SIC for <precursor>
        {
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                index_[index].filter, "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 3: // generate SRM TIC for <precursor>
        {
            vector<string> tokens;
            bal::split(tokens, ci.id, bal::is_any_of(" "));
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_TIC, Operator_None, Type_MassRange,
                "ms2 " + tokens[2], "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 4: // generate SRM SIC for transition <precursor>,<product>
        {
            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.q1, MS_m_z);

            ScanFilter filterParser;
            filterParser.parse(ci.filter);
            result->precursor.activation.set(translate(filterParser.activationType_));
            if (filterParser.activationType_ == ActivationType_CID)
                result->precursor.activation.set(MS_collision_energy, filterParser.cidEnergy_[0]);

            result->product.isolationWindow.set(MS_isolation_window_target_m_z, ci.q3, MS_m_z);
            result->product.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
            result->product.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

            string q1 = (format("%.10g") % ci.q1).str();
            string q3Range = (format("%.10g-%.10g")
                              % (ci.q3 - ci.q3Offset)
                              % (ci.q3 + ci.q3Offset)
                             ).str();

            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                "ms2 " + q1, q3Range, "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 5: // generate "Total Scan" chromatogram for entire run
        {
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_TotalScan, Operator_None, Type_MassRange,
                "", "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_minute, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 6: // generate "ECD" chromatogram for entire run
        {
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
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
                    idMap_[ci.id] = ci.index;

                    // for certain filter types, support additional chromatograms
                    auto_ptr<StringArray> filterArray = rawfile_->getFilters();
                    ScanFilter filterParser;
                    for (size_t i=0, ic=filterArray->size(); i < ic; ++i)
                    {
                        string filterString = filterArray->item(i);
                        if (filterString.find("ms2") == string::npos)
                            continue;

                        filterParser.initialize();
                        filterParser.parse(filterString);

                        if (!filterParser.cidParentMass_.empty())
                        {
                            switch (filterParser.scanType_)
                            {
                                case ScanType_SRM:
                                {
                                    string precursorMZ = (format("%.10g") % filterParser.cidParentMass_[0]).str();
                                    index_.push_back(IndexEntry());
                                    IndexEntry& ci = index_.back();
                                    ci.controllerType = (ControllerType) controllerType;
                                    ci.controllerNumber = n;
                                    ci.filter = filterString;
                                    ci.index = index_.size()-1;
                                    ci.id = "SRM TIC " + precursorMZ;
                                    ci.q1 = filterParser.cidParentMass_[0];
                                    idMap_[ci.id] = ci.index;

                                    for (size_t j=0, jc=filterParser.scanRangeMin_.size(); j < jc; ++j)
                                    {
                                        index_.push_back(IndexEntry());
                                        IndexEntry& ci = index_.back();
                                        ci.controllerType = (ControllerType) controllerType;
                                        ci.controllerNumber = n;
                                        ci.filter = filterString;
                                        ci.index = index_.size()-1;
                                        ci.q1 = filterParser.cidParentMass_[0];
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

                                default:
                                case ScanType_Full:
                                /*{
                                    string precursorMZ = lexical_cast<string>(filterParser.cidParentMass_[0]);
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

#endif // PWIZ_READER_THERMO
