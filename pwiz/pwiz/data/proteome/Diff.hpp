//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _PROTEOME_DIFF_HPP_
#define _PROTEOME_DIFF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"


namespace pwiz { namespace proteome { struct DiffConfig; } }


namespace pwiz {
namespace data {


using namespace proteome;


namespace diff_impl {

PWIZ_API_DECL
void diff(const ProteinList& a,
          const ProteinList& b,
          ProteinListSimple& a_b,
          ProteinListSimple& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteomeData& a,
          const ProteomeData& b,
          ProteomeData& a_b,
          ProteomeData& b_a,
          const DiffConfig& config);

} // namespace diff_impl
} // namespace data
} // namespace pwiz


// this include must come after the above declarations or GCC won't see them
#include "pwiz/data/common/diff_std.hpp"


namespace pwiz {
namespace proteome {


/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig : public pwiz::data::BaseDiffConfig
{
    bool ignoreMetadata;

    DiffConfig()
    :   BaseDiffConfig(1e-6),
        ignoreMetadata(false)
    {}
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const data::Diff<ProteomeData, DiffConfig>& diff);


} // namespace proteome
} // namespace pwiz


#endif // _PROTEOME_DIFF_HPP_
