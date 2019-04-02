//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#include "MSFSpecReader.h"


namespace BiblioSpec {

MSFSpecReader::MSFSpecReader(const char* xmlFilename,
                             vector<double>* mzs, vector<float>* intensities) : SAXHandler(),
    mzs_(mzs), intensities_(intensities), peakReadingState_(false)
{
    this->setFileName(xmlFilename); // this is for the saxhandler
}

MSFSpecReader::MSFSpecReader(string& xmlData,
                             vector<double>* mzs, vector<float>* intensities) : SAXHandler(),
    mzs_(mzs), intensities_(intensities), peakReadingState_(false)
{
    this->setXmlData(xmlData); // this is for the saxhandler
}

MSFSpecReader::~MSFSpecReader()
{
}

/**
 * Called by saxhandler when a new xml start tag is reached.
 */
void MSFSpecReader::startElement(const XML_Char* name, const XML_Char** attr)
{
    if (isElement("PeakCentroids", name))
    {
        peakReadingState_ = true;
    }
    else if (peakReadingState_ && isElement("Peak", name))
    {
        mzs_->push_back(getDoubleRequiredAttrValue("X", attr));
        intensities_->push_back(getDoubleRequiredAttrValue("Y", attr));
    }
}

/**
 * Called by saxhandler when the closing tag of an xml element is reached.
 */
void MSFSpecReader::endElement(const XML_Char* name)
{
    if (isElement("PeakCentroids", name))
    {
        peakReadingState_ = false;
    }
}

} // namespace
