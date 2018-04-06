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


#ifndef _TRADATA_REFERENCES_HPP_
#define _TRADATA_REFERENCES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"


namespace pwiz {
namespace tradata {


/// functions for resolving references from objects into the internal MSData lists
namespace References {


PWIZ_API_DECL void resolve(RetentionTime& retentionTime, const TraData& td);
PWIZ_API_DECL void resolve(Prediction& prediction, const TraData& td);
PWIZ_API_DECL void resolve(Configuration& configuration, const TraData& td);
PWIZ_API_DECL void resolve(Peptide& peptide, const TraData& td);
PWIZ_API_DECL void resolve(Transition& transition, const TraData& td);
PWIZ_API_DECL void resolve(Target& target, const TraData& td);


/// Resolve internal references in a TraData object.
PWIZ_API_DECL void resolve(TraData& td);


} // namespace References


} // namespace tradata
} // namespace pwiz


#endif // _TRADATA_REFERENCES_HPP_

