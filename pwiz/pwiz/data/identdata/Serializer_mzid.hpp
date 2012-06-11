//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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


#ifndef _SERIALIZER_MZID_HPP_
#define _SERIALIZER_MZID_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {

/// MZIDData <-> mzIdentML stream serialization
class PWIZ_API_DECL Serializer_mzIdentML
{
    public:

    /// Serializer_mzIdentML configuration
    struct PWIZ_API_DECL Config
    {
        bool readSequenceCollection;
        bool readAnalysisData;

        Config() : readSequenceCollection(true), readAnalysisData(true) {}
    };

    Serializer_mzIdentML(const Config& config = Config()) : config_(config) {}

    /// write MZIDData object to ostream as mzIdentML
    void write(std::ostream& os, const IdentData& mzid,
               const pwiz::util::IterationListenerRegistry* = 0) const;

    /// read in MZIDData object from a mzIdentML istream
    void read(boost::shared_ptr<std::istream> is, IdentData& mzid,
              const pwiz::util::IterationListenerRegistry* = 0) const;

    private:
    const Config config_;
    Serializer_mzIdentML(Serializer_mzIdentML&);
    Serializer_mzIdentML& operator=(Serializer_mzIdentML&);
};

} // namespace pwiz 
} // namespace identdata 

#endif // _SERIALIZER_MZID_HPP_
