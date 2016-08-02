//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "DelimWriter.hpp"

#include <boost/algorithm/string/join.hpp>
#include <vector>

#include <iostream>

namespace {

const std::string rtCV = "MS:1001114";
const std::string peakCV = "MS:1000797";

} // anonymous namespace

namespace pwiz {
namespace identdata {

using namespace std;
using namespace boost::algorithm;

PWIZ_API_DECL ostream* DelimWriter::writeHeaders()
{
    if (os_)
        (*os_) << "#retention time\tscan\tm/z\tscore\tpeptide\tprotein\n";
    
    return os_;
}

// Attempt #2 using SpectrumIdentificationList
PWIZ_API_DECL ostream* DelimWriter::write(const IdentData& mzid)
{
    if (headers_)
        writeHeaders();
    
    // For each protein, print out a line for each peptide used as
    // evidence.

    // Path so far:
    // /IdentData/DataCollection/AnalysisData/SpectrumIdentificationList

    // For now, leave the file blank if there are no entries.
    if (!mzid.dataCollection.analysisData.spectrumIdentificationList.size())
        return os_;

    return write(mzid.dataCollection.analysisData.spectrumIdentificationList);
}

PWIZ_API_DECL ostream* DelimWriter::write(const SpectrumIdentificationList& sil)
{
    // May need to grab the m/z measure from the fragment table if the
    // fragmentation tables are going to be searched.

    return write(sil.spectrumIdentificationResult);
}

PWIZ_API_DECL ostream* DelimWriter::write(const SpectrumIdentificationResult& sir)
{
    // Fetch the retention time.
    //
    // Retention time should be kept in the cvParams under accession
    // "MS:1001114"
    CVParam rtParam = sir.cvParam(cvTermInfo(rtCV).cvid);
    if (rtParam.cvid != CVID_Unknown)
    {
        ostringstream oss;

        oss << rtParam.value;
        if (rtParam.units != CVID_Unknown)
            oss << " " << cvTermInfo(rtParam.units).name;

        current_line.push_back(oss.str());
    }

    // Fetch scan
    //
    // Since this may be a list of peaks, this may not be correct. In
    // that case, current_line will need to be expanded into a vector
    // of line_type. All children will need to do an expansion for
    // child with multiple siblings.
    CVParam peakParam = sir.cvParam(cvTermInfo(peakCV).cvid);
    if (peakParam.cvid != CVID_Unknown)
        current_line.push_back(peakParam.value);

    if (sir.spectrumIdentificationItem.size())
        write(sir.spectrumIdentificationItem);

    // Clean up the local caches for the any additional elements.
    current_line.clear();
    
    return os_;
}

PWIZ_API_DECL ostream* DelimWriter::write(const SpectrumIdentificationItem& sii)
{
    // Fetch m/z
    //
    // Assuming the experimental mass is to be used.
    //
    // TODO Add a commandline switch to select
    // experimental/calculated.
    ostringstream oss;
    oss << sii.experimentalMassToCharge;
    current_line.push_back(oss.str());
    
    CVParam cvParam = sii.cvParamChild(
        MS_PSM_level_search_engine_specific_statistic);

    if (cvParam.cvid != CVID_Unknown)
        current_line.push_back(cvParam.value);
    else
        // Using an empty string as the null character
        current_line.push_back("");
    
    if (sii.peptideEvidencePtr.size())
        write(sii.peptideEvidencePtr);

    return os_;
}

PWIZ_API_DECL ostream* DelimWriter::write(const PeptideEvidence& pe)
{
    if (!pe.dbSequencePtr.get())
    {
        cerr << "No DBSequence reference\n";
        return os_;
    }

    // We don't append to the current line because there may be many
    // sibling PeptideEvidence tags that will build on the previous
    // fields.
    //
    // NOTE: A possible bug may develop if the smalltalk style returns
    // are used for custom field selection and ordering. This will
    // need to be addressed at that time.
    line_type line;
    line.assign(current_line.begin(), current_line.end());

    line.push_back(pe.dbSequencePtr->seq);
    current_line.clear();

    // TODO check if this is the correct entry, or should we use
    // the value for cvParam "MS:1001088"?
    line.push_back(pe.dbSequencePtr->accession);

    string separator(&delim_, 1);
    string line_str = join(line, separator);

    if (os_)
        (*os_) << line_str << "\n";

    return os_;
}

PWIZ_API_DECL ostream* DelimWriter::write(const line_type& line)
{
    string separator(&delim_, 1);
    
    if (os_)
        (*os_) << join(line, separator) << "\n";
    
    return os_;
}

template<>
PWIZ_API_DECL ostream* DelimWriter::write<string>(
    const vector<string>& lines)
{
    string separator("\n");
    
    if (os_)
        (*os_) << join(lines, separator);
    
    return os_;
}

PWIZ_API_DECL DelimWriter::operator bool() const
{
    return os_->good();
}

} // namespace identdata 
} // namespace pwiz


