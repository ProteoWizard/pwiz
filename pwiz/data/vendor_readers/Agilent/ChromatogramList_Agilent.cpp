//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#include "ChromatogramList_Agilent.hpp"


#ifdef PWIZ_READER_AGILENT
#include "Reader_Agilent_Detail.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

ChromatogramList_Agilent::ChromatogramList_Agilent(MassHunterDataPtr rawfile)
:   rawfile_(rawfile), indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_Agilent::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Agilent::createIndex, this));
    return index_.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Agilent::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Agilent::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Agilent::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


PWIZ_API_DECL size_t ChromatogramList_Agilent::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Agilent::createIndex, this));
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Agilent::chromatogram(size_t index, bool getBinaryData) const 
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Agilent::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Agilent::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    const IndexEntry& ci = index_[index];
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;

    result->set(ci.chromatogramType);

    switch (ci.chromatogramType)
    {
        default:
            break;

        case MS_TIC_chromatogram:
        {
            if (getBinaryData)
            {
                result->setTimeIntensityArrays(vector<double>(), vector<double>(), UO_minute, MS_number_of_detector_counts);
                result->getTimeArray()->data.assign(rawfile_->getTicTimes().begin(), rawfile_->getTicTimes().end());
                result->getIntensityArray()->data.assign(rawfile_->getTicIntensities().begin(), rawfile_->getTicIntensities().end());

                result->defaultArrayLength = result->getTimeArray()->data.size();
            }
            else
                result->defaultArrayLength = rawfile_->getTicTimes().size();
        }
        break;

        case MS_SRM_chromatogram:
        {
            pwiz::vendor_api::Agilent::ChromatogramPtr chromatogramPtr(rawfile_->getChromatogram(ci.transition));

            CVID polarityType = Agilent::translateAsPolarityType(chromatogramPtr->getIonPolarity());
            if (polarityType != CVID_Unknown)
                result->set(polarityType);
            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.transition.Q1, MS_m_z);
            result->precursor.activation.set(MS_CID);
            result->precursor.activation.set(MS_collision_energy, chromatogramPtr->getCollisionEnergy());

            result->product.isolationWindow.set(MS_isolation_window_target_m_z, ci.transition.Q3, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

            if (getBinaryData)
            {
                result->setTimeIntensityArrays(vector<double>(), vector<double>(), UO_minute, MS_number_of_detector_counts);

                automation_vector<double> xArray;
                chromatogramPtr->getXArray(xArray);
                result->getTimeArray()->data.assign(xArray.begin(), xArray.end());

                automation_vector<float> yArray;
                chromatogramPtr->getYArray(yArray);
                result->getIntensityArray()->data.assign(yArray.begin(), yArray.end());

                result->defaultArrayLength = xArray.size();
            }
            else
                result->defaultArrayLength = chromatogramPtr->getTotalDataPoints();
        }
        break;

        case MS_SIM_chromatogram:
        {
            pwiz::vendor_api::Agilent::ChromatogramPtr chromatogramPtr(rawfile_->getChromatogram(ci.transition));
            CVID polarityType = Agilent::translateAsPolarityType(chromatogramPtr->getIonPolarity());
            if (polarityType != CVID_Unknown)
                result->set(polarityType);

            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.transition.Q1, MS_m_z);

            if (getBinaryData)
            {
                result->setTimeIntensityArrays(vector<double>(), vector<double>(), UO_minute, MS_number_of_detector_counts);

                automation_vector<double> xArray;
                chromatogramPtr->getXArray(xArray);
                result->getTimeArray()->data.assign(xArray.begin(), xArray.end());

                automation_vector<float> yArray;
                chromatogramPtr->getYArray(yArray);
                result->getIntensityArray()->data.assign(yArray.begin(), yArray.end());

                result->defaultArrayLength = xArray.size();
            }
            else
                result->defaultArrayLength = chromatogramPtr->getTotalDataPoints();
        }
        break;
    }

    return result;
}

template <typename T>
struct nosci_policy : boost::spirit::karma::real_policies<T>
{
    static unsigned int precision(T) { return 9; }
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::fixed; }
};

PWIZ_API_DECL void ChromatogramList_Agilent::createIndex() const
{
    using namespace boost::spirit::karma;
    typedef real_generator<double, nosci_policy<double> > nosci_type;
    static const nosci_type nosci = nosci_type();

    // support file-level TIC for all file types
    index_.push_back(IndexEntry());
    IndexEntry& ci = index_.back();
    ci.index = index_.size()-1;
    ci.chromatogramType = MS_TIC_chromatogram;
    ci.id = "TIC";
    idMap_[ci.id] = ci.index;

    const set<Transition>& transitions = rawfile_->getTransitions();

    BOOST_FOREACH(const Transition& transition, transitions)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ci = index_.back();
        ci.index = index_.size()-1;
        ci.chromatogramType = transition.type == Transition::MRM ? MS_SRM_chromatogram
                                                                 : MS_SIM_chromatogram;
        ci.transition = transition;
        std::string polarity = polarityStringForFilter((transition.ionPolarity == IonPolarity_Negative) ? MS_negative_scan : MS_positive_scan);
        std::back_insert_iterator<std::string> sink(ci.id);
        if (ci.chromatogramType == MS_SRM_chromatogram)
        {
            generate(sink, "SRM SIC Q1=" << nosci << " Q3=" << nosci << " start=" << nosci << " end=" << nosci,
                     transition.Q1,
                     transition.Q3,
                     transition.acquiredTimeRange.start,
                     transition.acquiredTimeRange.end
                    );
        }
        else
            generate(sink, "SIM SIC Q1=" << nosci << " start=" << nosci << " end=" << nosci,
                     transition.Q1,
                     transition.acquiredTimeRange.start,
                     transition.acquiredTimeRange.end
                    );
        idMap_[ci.id] = ci.index;
    }
}

} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_AGILENT

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_Agilent::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_Agilent::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_Agilent::find(const string& id) const {return 0;}
ChromatogramPtr ChromatogramList_Agilent::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_AGILENT
