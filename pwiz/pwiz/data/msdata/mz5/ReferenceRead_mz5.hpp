//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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

#ifndef REFERENCEREAD_MZ5_HPP_
#define REFERENCEREAD_MZ5_HPP_

#include "../../common/cv.hpp"
#include "../../common/ParamTypes.hpp"
#include "../MSData.hpp"
#include "Datastructures_mz5.hpp"
#include "Connection_mz5.hpp"
#include <boost/smart_ptr.hpp>
#include <string>
#include <vector>
#include <map>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * This class is a helper class to read and convert a mz5 file to a MSData object.
 */
class ReferenceRead_mz5
{
public:
    /**
     * Default constructor.
     * @param msd this MSData object will be filled
     */
    ReferenceRead_mz5(pwiz::msdata::MSData& msd);

    /**
     * Getter.
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::cv::CVID getCVID(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::data::ParamGroupPtr getParamGroupPtr(const unsigned long) const;

    void fill(std::vector<pwiz::msdata::CVParam>& cv, std::vector<
            pwiz::msdata::UserParam>& user, std::vector<
            pwiz::msdata::ParamGroupPtr>& param, const unsigned long& cvstart,
            const unsigned long& cvend, const unsigned long& usrstart,
            const unsigned long& usrend, const unsigned long& refstart,
            const unsigned long& refend) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::SourceFilePtr getSourcefilePtr(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::SamplePtr getSamplePtr(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::SoftwarePtr getSoftwarePtr(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::ScanSettingsPtr getScanSettingPtr(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::InstrumentConfigurationPtr getInstrumentPtr(
            const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    pwiz::msdata::DataProcessingPtr
            getDataProcessingPtr(const unsigned long) const;

    /**
     * Getter
     * @param index
     * @return object with this index. Can throw out_of_range.
     */
    std::string getSpectrumId(const unsigned long) const;

    /**
     * Adds a spectrum index pair to an internal map.
     * @param name
     * @param index
     */
    void addSpectrumIndexPair(const std::string& name,
            const unsigned long index) const;

    /**
     * Sets internal controlled vocabulary map.
     */
    void setCVRefMZ5(CVRefMZ5*, size_t);

    /**
     * Fills the internal MSData reference with the data from an mz5 file.
     * @param connection open mz5 connection to a mz5 file
     */
    void fill(boost::shared_ptr<Connection_mz5>& connection);

    pwiz::msdata::DataProcessingPtr
    getDefaultChromatogramDP(const size_t index);

    pwiz::msdata::DataProcessingPtr getDefaultSpectrumDP(const size_t index);

private:
    /**
     * Reference to MSData object.
     */
    pwiz::msdata::MSData& msd_;

    /**
     * Following are helper maps and lists for reading.
     */
    mutable std::vector<CVRefMZ5> cvrefs_;
    mutable std::map<unsigned long, pwiz::cv::CVID> bbmapping_;
    mutable std::vector<CVParamMZ5> cvParams_;
    mutable std::vector<UserParamMZ5> usrParams_;
    mutable std::vector<RefMZ5> refParms_;
    mutable std::map<unsigned long, std::string> spectrumIndex_;

    mutable unsigned long defaultChromatogramDataProcessingRefID_,
            defaultSpectrumDataProcessingRefID_;
};

}
}
}

#endif /* REFERENCEREAD_MZ5_HPP_ */
