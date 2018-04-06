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


#ifndef _TRADATA_DIFF_HPP_
#define _TRADATA_DIFF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"


namespace pwiz { namespace tradata { struct DiffConfig; } }


namespace pwiz {
namespace data {
namespace diff_impl {


using namespace tradata;


PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Publication& a,
          const Publication& b,
          Publication& a_b,
          Publication& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const RetentionTime& a,
          const RetentionTime& b,
          RetentionTime& a_b,
          RetentionTime& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Prediction& a,
          const Prediction& b,
          Prediction& a_b,
          Prediction& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Evidence& a,
          const Evidence& b,
          Evidence& a_b,
          Evidence& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Validation& a,
          const Validation& b,
          Validation& a_b,
          Validation& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Instrument& a,
          const Instrument& b,
          Instrument& a_b,
          Instrument& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Configuration& a,
          const Configuration& b,
          Configuration& a_b,
          Configuration& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Software& a,
          const Software& b,
          Software& a_b,
          Software& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Interpretation& a,
          const Interpretation& b,
          Interpretation& a_b,
          Interpretation& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Protein& a,
          const Protein& b,
          Protein& a_b,
          Protein& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Modification& a,
          const Modification& b,
          Modification& a_b,
          Modification& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Compound& a,
          const Compound& b,
          Compound& a_b,
          Compound& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Transition& a,
          const Transition& b,
          Transition& a_b,
          Transition& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Target& a,
          const Target& b,
          Target& a_b,
          Target& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const TraData& a,
          const TraData& b,
          TraData& a_b,
          TraData& b_a,
          const DiffConfig& config);

} // namespace diff_impl
} // namespace data
} // namespace pwiz


// this include must come after the above declarations or GCC won't see them
#include "pwiz/data/common/diff_std.hpp"


namespace pwiz {
namespace tradata {


/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig : public pwiz::data::BaseDiffConfig
{
    DiffConfig()
    :   BaseDiffConfig(1e-6)
    {}
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const data::Diff<TraData, DiffConfig>& diff);

} // namespace tradata
} // namespace pwiz


#endif // _TRADATA_DIFF_HPP_
