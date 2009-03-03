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

    const ChromatogramIdentity& ci = index_[index].first;
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;

    int mode = 0;

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
    }
    else // generate SRM SIC for transition <precursor>,<product>
    {
        mode = 4;
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
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_second, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 2: // generate SIC for <precursor>
        {
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                index_[index].second, "", "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_second, MS_number_of_counts);
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
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_second, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;

        case 4: // generate SRM SIC for transition <precursor>,<product>
        {
            vector<string> tokens;
            bal::split(tokens, ci.id, bal::is_any_of(" "));
            bal::split(tokens, tokens[2], bal::is_any_of(","));
            double productMZ = lexical_cast<double>(tokens[1]);
            boost::format mzRange("%f-%f");
            mzRange % (productMZ-0.05) % (productMZ+0.05);
            auto_ptr<ChromatogramData> cd = rawfile_->getChromatogramData(
                Type_BasePeak, Operator_None, Type_MassRange,
                "ms2 " + tokens[0], mzRange.str(), "", 0,
                0, rawfile_->rt(rawfile_->value(NumSpectra)),
                Smoothing_None, 0);
            pwiz::msdata::TimeIntensityPair* data = reinterpret_cast<pwiz::msdata::TimeIntensityPair*>(cd->data());
            if (getBinaryData) result->setTimeIntensityPairs(data, cd->size(), UO_second, MS_number_of_counts);
            else result->defaultArrayLength = cd->size();
        }
        break;
    }

    return result;
}


PWIZ_API_DECL void ChromatogramList_Thermo::createIndex() const
{
    // support file-level TIC for all file types
    index_.push_back(make_pair(ChromatogramIdentity(), ""));
    ChromatogramIdentity& ci = index_.back().first;
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
                    index_.push_back(make_pair(ChromatogramIdentity(), filterString));
                    ChromatogramIdentity& ci = index_.back().first;
                    ci.index = index_.size()-1;
                    ci.id = "SRM TIC " + precursorMZ;
                    idMap_[ci.id] = ci.index;

                    for (size_t j=0, jc=filterParser.scanRangeMin_.size(); j < jc; ++j)
                    {
                        index_.push_back(make_pair(ChromatogramIdentity(), filterString));
                        ChromatogramIdentity& ci = index_.back().first;
                        ci.index = index_.size()-1;
                        ci.id = (format("SRM SIC %s,%.10g")
                                 % precursorMZ
                                 % ((filterParser.scanRangeMin_[j] + filterParser.scanRangeMax_[j]) / 2.0)
                                ).str();
                        idMap_[ci.id] = ci.index;
                    }
                }
                break;

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
