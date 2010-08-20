//
// $Id$
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

#include "pwiz/utility/misc/Export.hpp"
#include <map>
#include <boost/shared_ptr.hpp>

#include "PeptideID.hpp"

namespace pwiz {
namespace peptideid {

/// This class allows access to identified proteins in PeptideProphet files.

/// A PeptideID_pepXML object is contructed with either the path to a
/// PeptideProphet format file (*.pep.xml), or an std::istream open to
/// the beginning of a pep.xml file.

class PWIZ_API_DECL PeptideID_pepXml : public PeptideID
{
public:
    /// Constructor taking path to input file in std::string.
    PeptideID_pepXml(const std::string& filename);

    /// Constructor taking path to input file from const char*.
    PeptideID_pepXml(const char* filename);

    /// Constructor taking std::istream as input.
    PeptideID_pepXml(std::istream* in);

    /// Destructor.
    virtual ~PeptideID_pepXml() {}

    /// Returns the Record object associated with the given nativeID.

    /// A range_error is thrown if the nativeID isn't associated with
    /// a Record.
    virtual Record record(const Location& location) const;

    virtual std::multimap<double, boost::shared_ptr<PeptideID::Record> >::const_iterator
        record(double retention_time_sec) const;

    virtual boost::shared_ptr<Iterator> iterator() const;
private:
    class Impl;
    boost::shared_ptr<Impl> pimpl;

};

} // namespace peptideid
} // namespace pwiz

#endif // _PEPTIDEID_PEPXML_HPP_
