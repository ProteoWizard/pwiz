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


#ifndef _SERIALIZER_TRAML_HPP_
#define _SERIALIZER_TRAML_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"


namespace pwiz {
namespace tradata {


/// TraData <-> traML stream serialization
class PWIZ_API_DECL Serializer_traML
{
    public:
    Serializer_traML() {}

    /// write TraData object to ostream as traML
    void write(std::ostream& os, const TraData& td) const;

    /// read in TraData object from a traML istream
    void read(boost::shared_ptr<std::istream> is, TraData& td) const;

    private:
    Serializer_traML(Serializer_traML&);
    Serializer_traML& operator=(Serializer_traML&);
};


} // namespace tradata
} // namespace pwiz


#endif // _SERIALIZER_TRAML_HPP_

