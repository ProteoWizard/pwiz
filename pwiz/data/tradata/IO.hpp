//
// IO.hpp
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


#ifndef _TRADATA_IO_HPP_
#define _TRADATA_IO_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"


namespace pwiz {
namespace tradata {


namespace IO {


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv);
PWIZ_API_DECL void read(std::istream& is, CV& cv);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const UserParam& userParam);
PWIZ_API_DECL void read(std::istream& is, UserParam& userParam);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CVParam& cv);
PWIZ_API_DECL void read(std::istream& is, CVParam& cv);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Software& software);
PWIZ_API_DECL void read(std::istream& is, Software& software);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Precursor& precursor);
PWIZ_API_DECL void read(std::istream& is, Precursor& precursor);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Product& product);
PWIZ_API_DECL void read(std::istream& is, Product& product);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TraData& td);
PWIZ_API_DECL void read(std::istream& is, TraData& td);


} // namespace IO


} // namespace tradata
} // namespace pwiz


#endif // _TRADATA_IO_HPP_


