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


#include "ChromatogramList_ABI.hpp"


#ifdef PWIZ_READER_ABI
#include "Reader_ABI_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL ChromatogramList_ABI::ChromatogramList_ABI(const MSData& msd, WiffFilePtr wifffile,
                                                         const ExperimentsMap& experimentsMap, int sample)
:   msd_(msd),
    wifffile_(wifffile),
    experimentsMap_(experimentsMap),
    sample(sample),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_ABI::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_ABI::createIndex, this));
    return size_;
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_ABI::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_ABI::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_ABI::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t ChromatogramList_ABI::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_ABI::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_ABI::chromatogram(size_t index, bool getBinaryData) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_ABI::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_ABI::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    
    // allocate a new Chromatogram
    IndexEntry& ie = index_[index];
    ChromatogramPtr result = ChromatogramPtr(new Chromatogram);
    if (!result.get())
        throw std::runtime_error("[ChromatogramList_Thermo::chromatogram()] Allocation error.");

    result->index = index;
    result->id = ie.id;
    result->set(ie.chromatogramType);

    switch (ie.chromatogramType)
    {
        case MS_TIC_chromatogram:
        {
            map<double, double> fullFileTIC;

            int sampleCount = wifffile_->getSampleCount();
            for (int i=1; i <= sampleCount; ++i)
            {
                try
                {
                    int periodCount = wifffile_->getPeriodCount(i);
                    for (int ii=1; ii <= periodCount; ++ii)
                    {
                        //Console::WriteLine("Sample {0}, Period {1}", i, ii);

                        int experimentCount = wifffile_->getExperimentCount(i, ii);
                        for (int iii=1; iii <= experimentCount; ++iii)
                        {
                            ExperimentPtr msExperiment = (i == sample
                                ? experimentsMap_.find(pair<int, int>(ii, iii))->second
                                : wifffile_->getExperiment(i, ii, iii));

                            // add current experiment TIC to full file TIC
                            vector<double> times, intensities;
                            msExperiment->getTIC(times, intensities);
                            for (int iiii = 0, end = intensities.size(); iiii < end; ++iiii)
                                fullFileTIC[times[iiii]] += intensities[iiii];
                        }
                    }
                }
                catch (exception&)
                {
                    // TODO: log warning
                }
            }

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);

            if (getBinaryData)
            {
                BinaryDataArrayPtr timeArray = result->getTimeArray();
                BinaryDataArrayPtr intensityArray = result->getIntensityArray();

                timeArray->data.reserve(fullFileTIC.size());
                intensityArray->data.reserve(fullFileTIC.size());
                for (map<double, double>::iterator itr = fullFileTIC.begin();
                     itr != fullFileTIC.end();
                     ++itr)
                {
                    timeArray->data.push_back(itr->first);
                    intensityArray->data.push_back(itr->second);
                }
            }

            result->defaultArrayLength = fullFileTIC.size();
        }
        break;

        case MS_SRM_chromatogram:
        {
            ExperimentPtr experiment = ie.experiment;
            pwiz::vendor_api::ABI::Target target;
            experiment->getSRM(ie.transition, target);

            // TODO: move to global scan settings or leave out entirely?
            result->userParams.push_back(UserParam("MS_dwell_time", lexical_cast<string>(target.dwellTime /* milliseconds->seconds */ / 1000.0), "xs:float"));

            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ie.q1, MS_m_z);
            //result->precursor.isolationWindow.set(MS_isolation_window_lower_offset, ie.q1, MS_m_z);
            //result->precursor.isolationWindow.set(MS_isolation_window_upper_offset, ie.q1, MS_m_z);
            result->precursor.activation.set(MS_CID);
            result->precursor.activation.set(MS_collision_energy, target.collisionEnergy, UO_electronvolt);
            result->precursor.activation.userParams.push_back(UserParam("MS_declustering_potential", lexical_cast<string>(target.declusteringPotential), "xs:float"));

            result->product.isolationWindow.set(MS_isolation_window_target_m_z, ie.q3, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_lower_offset, ie.q3, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_upper_offset, ie.q3, MS_m_z);

            CVID polarityType = ABI::translate(experiment->getPolarity());
            if (polarityType != CVID_Unknown)
                result->set(polarityType);

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);

            vector<double> times, intensities;
            experiment->getSIC(ie.transition, times, intensities);
            result->defaultArrayLength = times.size();

            if (getBinaryData)
            {
                BinaryDataArrayPtr timeArray = result->getTimeArray();
                BinaryDataArrayPtr intensityArray = result->getIntensityArray();
                std::swap(timeArray->data, times);
                std::swap(intensityArray->data, intensities);
            }
        }
        break;

        case MS_SIM_chromatogram:
        {
            ExperimentPtr experiment = ie.experiment;
            pwiz::vendor_api::ABI::Target target;
            experiment->getSIM(ie.transition, target);

            // TODO: move to global scan settings or leave out entirely?
            result->userParams.push_back(UserParam("MS_dwell_time", lexical_cast<string>(target.dwellTime /* milliseconds->seconds */ / 1000.0), "xs:float"));

            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ie.q1, MS_m_z);
            //result->precursor.isolationWindow.set(MS_isolation_window_lower_offset, ie.q1, MS_m_z);
            //result->precursor.isolationWindow.set(MS_isolation_window_upper_offset, ie.q1, MS_m_z);
            result->precursor.activation.set(MS_CID);
            result->precursor.activation.set(MS_collision_energy, target.collisionEnergy, UO_electronvolt);
            result->precursor.activation.userParams.push_back(UserParam("MS_declustering_potential", lexical_cast<string>(target.declusteringPotential), "xs:float"));

            CVID polarityType = ABI::translate(experiment->getPolarity());
            if (polarityType != CVID_Unknown)
                result->set(polarityType);

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);

            vector<double> times, intensities;
            experiment->getSIC(ie.transition, times, intensities);
            result->defaultArrayLength = times.size();

            if (getBinaryData)
            {
                BinaryDataArrayPtr timeArray = result->getTimeArray();
                BinaryDataArrayPtr intensityArray = result->getIntensityArray();
                std::swap(timeArray->data, times);
                std::swap(intensityArray->data, intensities);
            }
        }
        break;
    }

    return result;
}


PWIZ_API_DECL void ChromatogramList_ABI::createIndex() const
{
    index_.push_back(IndexEntry());
    IndexEntry& ie = index_.back();
    ie.index = index_.size()-1;
    ie.id = "TIC";
    ie.chromatogramType = MS_TIC_chromatogram;
    idToIndexMap_[ie.id] = ie.index;

    pwiz::vendor_api::ABI::Target target;

    int periodCount = wifffile_->getPeriodCount(sample);
    for (int ii=1; ii <= periodCount; ++ii)
    {
        //Console::WriteLine("Sample {0}, Period {1}", sample, ii);

        int experimentCount = wifffile_->getExperimentCount(sample, ii);
        for (int iii=1; iii <= experimentCount; ++iii)
        {
            ExperimentPtr msExperiment = experimentsMap_.find(pair<int, int>(ii, iii))->second;

            for (int iiii = 0; iiii < (int) msExperiment->getSRMSize(); ++iiii)
            {
                msExperiment->getSRM(iiii, target);

                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.chromatogramType = MS_SRM_chromatogram;
                ie.q1 = target.Q1;
                ie.q3 = target.Q3;
                ie.sample = sample;
                ie.period = ii;
                ie.experiment = msExperiment;
                ie.transition = iiii;
                ie.index = index_.size()-1;

                std::ostringstream oss;
                oss << polarityStringForFilter(ABI::translate(ie.experiment->getPolarity())) <<
                        "SRM SIC Q1=" << ie.q1 <<
                       " Q3=" << ie.q3 <<
                       " sample=" << ie.sample <<
                       " period=" << ie.period <<
                       " experiment=" << ie.experiment->getExperimentNumber() <<
                       " transition=" << ie.transition;
                ie.id = oss.str();
                idToIndexMap_[ie.id] = ie.index;
            }

            for (int iiii = 0; iiii < (int)msExperiment->getSIMSize(); ++iiii)
            {
                msExperiment->getSIM(iiii, target);

                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.chromatogramType = MS_SIM_chromatogram;
                ie.q1 = target.Q1;
                ie.q3 = 0;
                ie.sample = sample;
                ie.period = ii;
                ie.experiment = msExperiment;
                ie.transition = iiii;
                ie.index = index_.size() - 1;

                std::ostringstream oss;
                oss << polarityStringForFilter(ABI::translate(ie.experiment->getPolarity())) <<
                    "SIM SIC Q1=" << ie.q1 <<
                    " sample=" << ie.sample <<
                    " period=" << ie.period <<
                    " experiment=" << ie.experiment->getExperimentNumber() <<
                    " transition=" << ie.transition;
                ie.id = oss.str();
                idToIndexMap_[ie.id] = ie.index;
            }
        }
    }

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_ABI

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_ABI::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_ABI::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_ABI::find(const std::string& id) const {return 0;}
ChromatogramPtr ChromatogramList_ABI::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI
