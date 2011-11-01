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

#ifndef _PEPTIDEID_FLAT_HPP_
#define _PEPTIDEID_FLAT_HPP_

#include <map>
#include <vector>
#include <string>
#include <iostream>
#include <boost/shared_ptr.hpp>

#include "pwiz/utility/misc/Export.hpp"
#include "PeptideID.hpp"

namespace pwiz {
namespace peptideid {

class PWIZ_API_DECL FlatRecordBuilder
{
public:
    virtual ~FlatRecordBuilder() {}
    
    virtual PeptideID::Record build(const std::vector<std::string>& fields) const;

    virtual bool header() const;

    virtual double epsilon() const;

    virtual bool operator()(const PeptideID::Record& a, const PeptideID::Record& b) const;
    
    virtual bool operator()(const PeptideID::Record& a, const PeptideID::Location& ) const;
};

class PWIZ_API_DECL MSInspectRecordBuilder : public FlatRecordBuilder
{
public:
    virtual ~MSInspectRecordBuilder() {}
    
    virtual PeptideID::Record build(const std::vector<std::string>& fields) const;

    virtual bool header() const;

    virtual double epsilon() const;

    virtual bool operator()(const PeptideID::Record& a, const PeptideID::Record& b) const;

    virtual bool operator()(const PeptideID::Record& a, const PeptideID::Location& ) const;
};

/// This class allows access to peptides listed in a flat tab
/// delimited text file

/// A PeptideID_flat object is contructed with either the path to a
/// tab delimited file (*.txt/*.tab), or an std::istream open to
/// the beginning of a tab delimited file.

class PWIZ_API_DECL PeptideID_flat : public PeptideID
{
public:
    /// Constructor taking path to input file in std::string.
    PeptideID_flat(const std::string& filename,
                   boost::shared_ptr<FlatRecordBuilder> builder =
                   boost::shared_ptr<FlatRecordBuilder>(new FlatRecordBuilder()));

    /// Constructor taking std::istream as input.
    PeptideID_flat(std::istream* in,
                   boost::shared_ptr<FlatRecordBuilder> builder =
                   boost::shared_ptr<FlatRecordBuilder>(new FlatRecordBuilder()));

    /// Destructor.
    virtual ~PeptideID_flat() {}

    /// Returns the Record object associated with the given nativeID.

     /// A range_error is thrown if the nativeID isn't associated with
    /// a Record.
    virtual Record record(const Location& location) const;

    virtual Iterator begin() const;
    virtual Iterator end() const;

private:
    class Impl;
    boost::shared_ptr<Impl> pimpl;
};

} // namespace peptideid
} // namespace pwiz

#endif // _PEPTIDEID_FLAT_HPP_
