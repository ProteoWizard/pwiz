//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "MSDataMerger.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "Diff.hpp"
#include "References.hpp"


using boost::shared_ptr;
using namespace pwiz::util;


namespace pwiz {
namespace msdata {

namespace {

void mergeParamContainers(ParamContainer& target, const ParamContainer& source)
{
    Diff<ParamContainer> diff(target, source);
    if (diff)
    {
        const ParamContainer& source_minus_target = diff.b_a;
        target.cvParams.insert(target.cvParams.end(), source_minus_target.cvParams.begin(), source_minus_target.cvParams.end());
        target.userParams.insert(target.userParams.end(), source_minus_target.userParams.begin(), source_minus_target.userParams.end());
        target.paramGroupPtrs.insert(target.paramGroupPtrs.end(), source_minus_target.paramGroupPtrs.begin(), source_minus_target.paramGroupPtrs.end());
    }
}

class SpectrumListMerger : public SpectrumList
{
    struct IndexEntry : public SpectrumIdentity
    {
        SpectrumListPtr spectrumListPtr;
        SourceFilePtr sourceFilePtr;
        size_t originalIndex;
    };

    const MSData& msd_;
    vector<MSDataPtr> inputMSDataPtrs_;
    vector<IndexEntry> index_;
    map<string, IndexList> idToIndexes_;

    public:
    SpectrumListMerger(const MSData& msd, const vector<MSDataPtr>& inputs)
    : msd_(msd), inputMSDataPtrs_(inputs)
    {
        BOOST_FOREACH(const MSDataPtr& input, inputs)
            for (size_t i=0; i < input->run.spectrumListPtr->size(); ++i)
            {
                SpectrumPtr s = input->run.spectrumListPtr->spectrum(i);
                Spectrum& oldIdentity = *s;

                idToIndexes_[oldIdentity.id].push_back(index_.size());

                IndexEntry ie;
                ie.id = oldIdentity.id;
                ie.index = index_.size();
                ie.originalIndex = i;
                ie.spectrumListPtr = input->run.spectrumListPtr;

                // because of the high chance of duplicate ids, sourceFilePtrs are always explicit
                SourceFilePtr oldSourceFilePtr = oldIdentity.sourceFilePtr.get() ? oldIdentity.sourceFilePtr : input->run.defaultSourceFilePtr;
                if (oldSourceFilePtr.get())
                    ie.sourceFilePtr = SourceFilePtr(new SourceFile(input->run.id + "_" + oldSourceFilePtr->id));

                index_.push_back(ie);
            }
    }

    virtual size_t size() const {return index_.size();}

    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const
    {
        if (index >= size())
            throw runtime_error("[SpectrumListMerger::spectrumIdentity()] Bad index: " + lexical_cast<string>(index));

        return index_[index];
    }

    virtual size_t find(const string& id) const
    {
        map<string, IndexList>::const_iterator itr = idToIndexes_.find(id);
        if (itr == idToIndexes_.end())
            return size();
        return itr->second[0]; // TODO: address duplicate ids when sourceFilePtr is disregarded...
    }

    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const
    {
        if (index >= size())
            throw runtime_error("[SpectrumListMerger::spectrum()] Bad index: " + lexical_cast<string>(index));

        const IndexEntry& ie = index_[index];
        SpectrumPtr result = ie.spectrumListPtr->spectrum(ie.originalIndex, getBinaryData);
        result->index = ie.index;

        // because of the high chance of duplicate ids, sourceFilePtrs are always explicit
        result->sourceFilePtr = ie.sourceFilePtr;

        // resolve references into MSData::*Ptrs that may be invalidated after merging
        References::resolve(*result, msd_);

        return result;
    }
};

} // namespace


PWIZ_API_DECL MSDataMerger::MSDataMerger(const vector<MSDataPtr>& inputs)
: inputMSDataPtrs_(inputs)
{
    // MSData::id and Run::id are set to the longest common prefix of all inputs' Run::ids,
    // or if there is no common prefix, to a concatenation of all inputs' Run::ids
    vector<string> runIds;

    // Run::startTimeStamp is set to the earliest timestamp
    vector<blt::local_date_time> runTimestamps;

    BOOST_FOREACH(const MSDataPtr& input, inputs)
    {
        const MSData& msd = *input;

        // merge fileDescription/sourceFilePtrs (prepend each source file with its source run id)
        BOOST_FOREACH(const SourceFilePtr& sourceFilePtr, msd.fileDescription.sourceFilePtrs)
        {
            this->fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile(*sourceFilePtr)));
            SourceFile& sf = *this->fileDescription.sourceFilePtrs.back();
            sf.id = msd.run.id + "_" + sf.id;
        }

        runIds.push_back(msd.run.id);

        if (!msd.run.startTimeStamp.empty())
            runTimestamps.push_back(decode_xml_datetime(msd.run.startTimeStamp));

        DiffConfig config;
        config.ignoreSpectra = config.ignoreChromatograms = true;
        Diff<MSData, msdata::DiffConfig> diff(*this, msd, config);

        // merge cvs
        this->cvs.insert(this->cvs.end(),
                         diff.b_a.cvs.begin(),
                         diff.b_a.cvs.end());

        // merge fileDescription/fileContent
        mergeParamContainers(this->fileDescription.fileContent, diff.b_a.fileDescription.fileContent);

        // merge fileDescription/contacts
        this->fileDescription.contacts.insert(this->fileDescription.contacts.end(),
                                              diff.b_a.fileDescription.contacts.begin(),
                                              diff.b_a.fileDescription.contacts.end());

        // merge file-level shared *Ptrs

        this->paramGroupPtrs.insert(this->paramGroupPtrs.end(),
                                    diff.b_a.paramGroupPtrs.begin(),
                                    diff.b_a.paramGroupPtrs.end());

        this->samplePtrs.insert(this->samplePtrs.end(),
                                diff.b_a.samplePtrs.begin(),
                                diff.b_a.samplePtrs.end());

        this->softwarePtrs.insert(this->softwarePtrs.end(),
                                  diff.b_a.softwarePtrs.begin(),
                                  diff.b_a.softwarePtrs.end());

        this->instrumentConfigurationPtrs.insert(this->instrumentConfigurationPtrs.end(),
                                                 diff.b_a.instrumentConfigurationPtrs.begin(),
                                                 diff.b_a.instrumentConfigurationPtrs.end());

        this->dataProcessingPtrs.insert(this->dataProcessingPtrs.end(),
                                        diff.b_a.dataProcessingPtrs.begin(),
                                        diff.b_a.dataProcessingPtrs.end());

        // merge run?
    }

    string lcp = pwiz::util::longestCommonPrefix(runIds);

    // trim typical separator characters from the end of the LCP
    bal::trim_right_if(lcp, bal::is_any_of(" _-."));

    if (lcp.empty())
        this->id = this->run.id = "merged-spectra";
    else
        this->id = this->run.id = lcp;

    if (!runTimestamps.empty())
        this->run.startTimeStamp = encode_xml_datetime(*std::min_element(runTimestamps.begin(), runTimestamps.end()));

    this->run.spectrumListPtr = SpectrumListPtr(new SpectrumListMerger(*this, inputMSDataPtrs_));
}


} // namespace msdata
} // namespace pwiz
