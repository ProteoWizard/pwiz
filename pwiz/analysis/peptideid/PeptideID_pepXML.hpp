//
// PeptideId_pepXML.hpp
//
//
// Original author: Robert Burke <robert.burke@cshs.org>
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

#ifndef _PEPTIDEID_PEPXML_HPP_
#define _PEPTIDEID_PEPXML_HPP_

#include <boost/shared_ptr.hpp>

#include "PeptideID.hpp"

namespace pwiz {
namespace peptideid {

class PeptideID_pepXml : public PeptideID
{
public:
    
    PeptideID_pepXml(const std::string& filename);
    PeptideID_pepXml(const char* filename);
    PeptideID_pepXml(std::istream* in);
    
    virtual ~PeptideID_pepXml() {}
    
    virtual Record record(const std::string& nativeID) const;
    
private:
    class Impl;
    boost::shared_ptr<Impl> pimpl;

};

} // namespace peptideid
} // namespace pwiz

#endif // _PEPTIDEID_PEPXML_HPP_
