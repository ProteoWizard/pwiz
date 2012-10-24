//
// $Id$
//
//
// Original author: Brian Pratt <brian.pratt .@. insilicos.com>
//  after Serializer_pepXML by Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2012 Spielberg Family Center for Applied Proteomics
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


#ifndef _SERIALIZER_PROTXML_HPP_
#define _SERIALIZER_PROTXML_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {


/// MZIDData <-> pepXML stream serialization
class PWIZ_API_DECL Serializer_protXML
{
    public:

    /// Serializer_protXML configuration
    struct PWIZ_API_DECL Config
    {
        bool readSpectrumQueries;

        Config(bool readSpectrumQueries = true) : readSpectrumQueries(readSpectrumQueries) {}
    };

    Serializer_protXML(const Config& config = Config()) : config_(config) {}

    /// write MZIDData object to ostream as pepXML
    void write(std::ostream& os, const IdentData& mzid, const std::string& filepath,
               const pwiz::util::IterationListenerRegistry* = 0) const;

    /// read in MZIDData object from a protXML istream
    void read(boost::shared_ptr<std::istream> is, IdentData& mzid,
              std::vector<std::string> *sourceFilesPtr, // if non-null, just read the SourceFiles info and return it here
              const pwiz::util::IterationListenerRegistry* = 0) const; 

    private:
    const Config config_;
    Serializer_protXML(Serializer_protXML&);
    Serializer_protXML& operator=(Serializer_protXML&);
};

} // namespace identdata
} // namespace pwiz 

#endif // _SERIALIZER_PROTXML_HPP_
