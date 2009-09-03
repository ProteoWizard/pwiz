//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#ifndef _MZID_REFERENCES_HPP_
#define _MZID_REFERENCES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"


namespace pwiz {
namespace mziddata {

/// functions for resolving references from objects into the internal MSData lists
namespace References {

PWIZ_API_DECL void resolve(ContactRole& cr, MzIdentML& mzid);
PWIZ_API_DECL void resolve(AnalysisSoftwarePtr asp, MzIdentML& mzid);
PWIZ_API_DECL void resolve(AnalysisSampleCollection& asc, MzIdentML& mzid);
PWIZ_API_DECL void resolve(std::vector<Affiliations>& vaff, std::vector<ContactPtr>& vcp);
PWIZ_API_DECL void resolve(std::vector<ContactPtr>& vcp, MzIdentML& mzid);
PWIZ_API_DECL void resolve(SequenceCollection& sc, MzIdentML& mzid);

PWIZ_API_DECL void resolve(MzIdentML& mzid);

} // namespace References

} // namespace mziddata
} // namespace pwiz


#endif // _MZID_REFERENCES_HPP_

