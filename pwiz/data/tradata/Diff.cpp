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

#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


namespace pwiz {
namespace data {
namespace diff_impl {




PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Publication& a,
          const Publication& b,
          Publication& a_b,
          Publication& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Instrument& a,
          const Instrument& b,
          Instrument& a_b,
          Instrument& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Software& a,
          const Software& b,
          Software& a_b,
          Software& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
	if (!config.ignoreVersions)
        diff(a.version, b.version, a_b.version, b_a.version, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Protein& a,
          const Protein& b,
          Protein& a_b,
          Protein& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.sequence, b.sequence, a_b.sequence, b_a.sequence, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const RetentionTime& a,
          const RetentionTime& b,
          RetentionTime& a_b,
          RetentionTime& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
}


PWIZ_API_DECL
void diff(const Evidence& a,
          const Evidence& b,
          Evidence& a_b,
          Evidence& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Modification& a,
          const Modification& b,
          Modification& a_b,
          Modification& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff_integral(a.location, b.location, a_b.location, b_a.location, config);
    diff_floating(a.monoisotopicMassDelta, b.monoisotopicMassDelta, a_b.monoisotopicMassDelta, b_a.monoisotopicMassDelta, config);
    diff_floating(a.averageMassDelta, b.averageMassDelta, a_b.averageMassDelta, b_a.averageMassDelta, config);
}


PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.sequence, b.sequence, a_b.sequence, b_a.sequence, config);
    vector_diff_deep(a.proteinPtrs, b.proteinPtrs, a_b.proteinPtrs, b_a.proteinPtrs, config);
    vector_diff_diff(a.modifications, b.modifications, a_b.modifications, b_a.modifications, config);
    vector_diff_diff(a.retentionTimes, b.retentionTimes, a_b.retentionTimes, b_a.retentionTimes, config);
    diff(a.evidence, b.evidence, a_b.evidence, b_a.evidence, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Compound& a,
          const Compound& b,
          Compound& a_b,
          Compound& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    vector_diff_diff(a.retentionTimes, b.retentionTimes, a_b.retentionTimes, b_a.retentionTimes, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Prediction& a,
          const Prediction& b,
          Prediction& a_b,
          Prediction& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.contactPtr, b.contactPtr, a_b.contactPtr, b_a.contactPtr, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
}


PWIZ_API_DECL
void diff(const Validation& a,
          const Validation& b,
          Validation& a_b,
          Validation& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Interpretation& a,
          const Interpretation& b,
          Interpretation& a_b,
          Interpretation& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Configuration& a,
          const Configuration& b,
          Configuration& a_b,
          Configuration& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.contactPtr, b.contactPtr, a_b.contactPtr, b_a.contactPtr, config);
    ptr_diff(a.instrumentPtr, b.instrumentPtr, a_b.instrumentPtr, b_a.instrumentPtr, config);
    vector_diff_diff(a.validations, b.validations, a_b.validations, b_a.validations, config);
}


PWIZ_API_DECL
void diff(const Transition& a,
          const Transition& b,
          Transition& a_b,
          Transition& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.precursor, b.precursor, a_b.precursor, b_a.precursor, config);
    diff(a.product, b.product, a_b.product, b_a.product, config);
    diff(a.prediction, b.prediction, a_b.prediction, b_a.prediction, config);
    diff(a.retentionTime, b.retentionTime, a_b.retentionTime, b_a.retentionTime, config);
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    ptr_diff(a.compoundPtr, b.compoundPtr, a_b.compoundPtr, b_a.compoundPtr, config);
    vector_diff_diff(a.interpretationList, b.interpretationList, a_b.interpretationList, b_a.interpretationList, config);
    vector_diff_diff(a.configurationList, b.configurationList, a_b.configurationList, b_a.configurationList, config);

    // provide name for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Target& a,
          const Target& b,
          Target& a_b,
          Target& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.precursor, b.precursor, a_b.precursor, b_a.precursor, config);
    diff(a.retentionTime, b.retentionTime, a_b.retentionTime, b_a.retentionTime, config);
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    ptr_diff(a.compoundPtr, b.compoundPtr, a_b.compoundPtr, b_a.compoundPtr, config);
    vector_diff_diff(a.configurationList, b.configurationList, a_b.configurationList, b_a.configurationList, config);

    // provide name for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const TraData& a,
          const TraData& b,
          TraData& a_b,
          TraData& b_a,
          const DiffConfig& config)
{
    string a_b_version, b_a_version;

    diff(a.id, b.id, a_b.id, b_a.id, config);
	if (!config.ignoreVersions)
        diff(a.version(), b.version(), a_b_version, b_a_version, config);
    vector_diff_diff(a.cvs, b.cvs, a_b.cvs, b_a.cvs, config);
    vector_diff_deep(a.contactPtrs, b.contactPtrs, a_b.contactPtrs, b_a.contactPtrs, config);
    vector_diff_diff(a.publications, b.publications, a_b.publications, b_a.publications, config);
    vector_diff_deep(a.instrumentPtrs, b.instrumentPtrs, a_b.instrumentPtrs, b_a.instrumentPtrs, config);
    vector_diff_deep(a.softwarePtrs, b.softwarePtrs, a_b.softwarePtrs, b_a.softwarePtrs, config);
    vector_diff_deep(a.proteinPtrs, b.proteinPtrs, a_b.proteinPtrs, b_a.proteinPtrs, config);
    vector_diff_deep(a.peptidePtrs, b.peptidePtrs, a_b.peptidePtrs, b_a.peptidePtrs, config);
    vector_diff_deep(a.compoundPtrs, b.compoundPtrs, a_b.compoundPtrs, b_a.compoundPtrs, config);
    vector_diff_diff(a.transitions, b.transitions, a_b.transitions, b_a.transitions, config);
    vector_diff_diff(a.targets.targetExcludeList, b.targets.targetExcludeList, a_b.targets.targetExcludeList, b_a.targets.targetExcludeList, config);
    vector_diff_diff(a.targets.targetIncludeList, b.targets.targetIncludeList, a_b.targets.targetIncludeList, b_a.targets.targetIncludeList, config);

    // provide context
    if (!a_b.empty() || !b_a.empty() ||
        !a_b_version.empty() || !b_a_version.empty()) 
    {
        a_b.id = a.id + (a_b_version.empty() ? "" : " (" + a_b_version + ")");
        b_a.id = b.id + (b_a_version.empty() ? "" : " (" + b_a_version + ")");
    }
}

} // namespace diff_impl
} // namespace data


namespace tradata {

PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const data::Diff<TraData, DiffConfig>& diff)
{
  using namespace diff_impl;

  TextWriter write(os,1);

  if(!diff.a_b.empty() || !diff.b_a.empty())
  {
      os<<"+\n";
      write(diff.a_b);
      os<<"-\n";
      write(diff.b_a);
  }

    return os;

}

} // namespace tradata
} // namespace pwiz
