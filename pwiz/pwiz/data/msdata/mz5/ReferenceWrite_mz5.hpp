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

#ifndef REFERENCEWRITE_MZ5_HPP_
#define REFERENCEWRITE_MZ5_HPP_

#include "../../common/cv.hpp"
#include "../../common/ParamTypes.hpp"
#include "../MSData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "Datastructures_mz5.hpp"
#include "Connection_mz5.hpp"
#include <string>
#include <vector>
#include <map>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * This class is a helper class for converting and writing mz5 files in mz5 format.
 */
class ReferenceWrite_mz5
{
public:
    /**
     * Default constructor.
     * @param msd MSData input object
     */
    ReferenceWrite_mz5(const pwiz::msdata::MSData& msd);

    /**
     * Stores a CVID into internal maps and returns the corresponding index.
     * @param cvid CVID input
     * @return index in list
     */
    unsigned long getCVRefId(const pwiz::cv::CVID cvid) const;

    /**
     * Stores a parameter group into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getParamGroupId(const pwiz::data::ParamGroup&,
            const ParamGroupMZ5* pg5 = 0) const;

    /**
     * Stores a parameter container into internal maps and returns the corresponding index.
     * @return index in list
     */
    void
    getIndizes(unsigned long& cvstart, unsigned long& cvend,
            unsigned long& usrstart, unsigned long& usrend,
            unsigned long& refstart, unsigned long& refend, const std::vector<
                    pwiz::msdata::CVParam>& cvs, const std::vector<
                    pwiz::msdata::UserParam>& usrs, const std::vector<
                    pwiz::msdata::ParamGroupPtr>& groups) const;

    /**
     * Stores a source file into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getSourceFileId(const pwiz::msdata::SourceFile&,
            const SourceFileMZ5* sf5 = 0) const;

    /**
     * Stores a sample into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getSampleId(const pwiz::msdata::Sample&, const SampleMZ5* s5 =
            0) const;

    /**
     * Stores a software into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long
            getSoftwareId(const pwiz::msdata::Software&, const SoftwareMZ5* s5 =
                    0) const;

    /**
     * Stores a scan setting element into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getScanSettingId(const pwiz::msdata::ScanSettings&,
            const ScanSettingMZ5* ss5 = 0) const;

    /**
     * Stores a instrument configuration into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getInstrumentId(const pwiz::msdata::InstrumentConfiguration&,
            const InstrumentConfigurationMZ5* ic5 = 0) const;

    /**
     * Stores a data processing element into internal maps and returns the corresponding index.
     * @return index in list
     */
    unsigned long getDataProcessingId(const pwiz::msdata::DataProcessing&,
            const DataProcessingMZ5* dp5 = 0) const;

    /**
     * Stores a spectrum into internal maps and returns the corresponding index.
     * @return index in list
     */
    void addSpectrumIndexPair(const std::string&, const unsigned long) const;

    /**
     * Resolves a spectrum name and returns the corresponding index.
     * @return index in list
     */
    unsigned long getSpectrumIndex(const std::string&) const;

    /**
     *
     */
    void
            writeTo(
                    Connection_mz5& connection,
                    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);

private:
    /**
     * Reads and writes all raw spectra using an existing mz5 connection.
     * @param connection mz5 connection object
     * @param bdl binary data list of a MSData object
     * @param spl spetrum data list of a MSData object
     */
    pwiz::util::IterationListener::Status
            readAndWriteSpectra(
                    Connection_mz5& connection,
                    std::vector<BinaryDataMZ5>& bdl,
                    std::vector<SpectrumMZ5>& spl,
                    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);

    /**
     * Reads and writes all chromtograms using an existing mz5 connection.
     * @param connection mz5 connection object
     * @param bdl binary data list of a MSData object
     * @param cpl chromatogram list of a MSData object
     */
    pwiz::util::IterationListener::Status
            readAndWriteChromatograms(
                    Connection_mz5& connection,
                    std::vector<BinaryDataMZ5>& bdl,
                    std::vector<ChromatogramMZ5>& cpl,
                    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry);

    /**
     * Internal reference to the MSData object.
     */
    const pwiz::msdata::MSData& msd_;

    /**
     * Following lists are used as internal storage container.
     */
    // TODO mutable?
    mutable std::vector<CVRefMZ5> cvrefs_;
    mutable std::map<pwiz::cv::CVID, unsigned long> cvToIndexMapping_;
    mutable std::map<unsigned long, pwiz::cv::CVID> cvFromIndexMapping_;

    //TODO can we get rid of all vectors?
    mutable std::vector<ParamGroupMZ5> paramGroupList_;
    mutable std::map<std::string, unsigned long> paramGroupMapping_;

    mutable std::vector<CVParamMZ5> cvParams_;
    mutable std::vector<UserParamMZ5> usrParams_;
    mutable std::vector<RefMZ5> refParms_;

    mutable std::vector<SourceFileMZ5> sourceFileList_;
    mutable std::map<std::string, unsigned long> sourceFileMapping_;

    mutable std::vector<SampleMZ5> sampleList_;
    mutable std::map<std::string, unsigned long> sampleMapping_;

    mutable std::vector<SoftwareMZ5> softwareList_;
    mutable std::map<std::string, unsigned long> softwareMapping_;

    mutable std::vector<ScanSettingMZ5> scanSettingList_;
    mutable std::map<std::string, unsigned long> scanSettingMapping_;

    mutable std::vector<InstrumentConfigurationMZ5> instrumentList_;
    mutable std::map<std::string, unsigned long> instrumentMapping_;

    mutable std::vector<DataProcessingMZ5> dataProcessingList_;
    mutable std::map<std::string, unsigned long> dataProcessingMapping_;

    mutable std::map<std::string, unsigned long> spectrumMapping_;

    mutable std::vector<ContVocabMZ5> contvacb_;
    mutable std::vector<ParamListMZ5> fileContent_;
    mutable std::vector<ParamListMZ5> contacts_;

    mutable std::vector<RunMZ5> rl_;
};

}
}
}

#endif /* REFERENCEWRITE_MZ5_HPP_ */
