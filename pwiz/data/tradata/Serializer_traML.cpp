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


#define PWIZ_SOURCE

#include "Serializer_traML.hpp"
#include "IO.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace tradata {


using minimxml::XMLWriter;
using boost::iostreams::stream_offset;
using namespace pwiz::minimxml;


void Serializer_traML::write(ostream& os, const TraData& td) const
{
    // instantiate XMLWriter

    XMLWriter::Config xmlConfig;
    XMLWriter xmlWriter(os, xmlConfig);

    string xmlData = "version=\"1.0\" encoding=\"ISO-8859-1\"";
    xmlWriter.processingInstruction("xml", xmlData);

    IO::write(xmlWriter, td);
}


void Serializer_traML::read(shared_ptr<istream> is, TraData& td) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_traML::read()] Bad istream.");

    is->seekg(0);

    IO::read(*is, td);
}


} // namespace tradata
} // namespace pwiz


