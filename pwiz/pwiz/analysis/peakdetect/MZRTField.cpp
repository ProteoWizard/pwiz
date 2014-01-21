//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#define PWIZ_SOURCE
#include "MZRTField.hpp"
#include "pwiz/utility/misc/Std.hpp"
                                                                                                     

namespace pwiz {
namespace analysis {


using namespace pwiz::minimxml;


PWIZ_API_DECL ostream& operator<<(ostream& os, const PeakelField& peakelField)
{
    XMLWriter writer(os);

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(peakelField.size())));
    writer.startElement("peakelField", attributes);

    for (PeakelField::const_iterator it=peakelField.begin(); it!=peakelField.end(); ++it)
        (*it)->write(writer);
    
    writer.endElement();

    return os;
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const FeatureField& featureField)
{
    XMLWriter writer(os);

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(featureField.size())));
    writer.startElement("featureField", attributes);

    for (FeatureField::const_iterator it=featureField.begin(); it!=featureField.end(); ++it)
        (*it)->write(writer);
    
    writer.endElement();

    return os;
}


} // namespace analysis
} // namespace pwiz



