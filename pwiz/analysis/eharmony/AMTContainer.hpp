//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// AMTContainer.hpp
///

#ifndef _AMT_CONTAINER_HPP_
#define _AMT_CONTAINER_HPP_

#include "Feature2PeptideMatcher.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

namespace pwiz{
namespace eharmony{

struct AMTContainer
{
    string _id;
    bool rtAdjusted;

    Feature2PeptideMatcher _f2pm;
    PeptideMatcher _pm;

    FdfPtr _fdf;
    PidfPtr _pidf;

    vector<boost::shared_ptr<SpectrumQuery> > _sqs; // HACK TODO: FIX

    void merge(const AMTContainer& that);
    void write(XMLWriter& writer) const;
    void read(istream& is);
  
    bool operator==(const AMTContainer& that);
    bool operator!=(const AMTContainer& that);

};


} // namespace eharmony
} // namespace pwiz



#endif // AMTContainer.hpp
