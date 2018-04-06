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


#ifndef _READER_BRUKER_DETAIL_HPP_ 
#define _READER_BRUKER_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include <string>
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace vendor_api {
namespace Bruker {

struct CompassData;
typedef boost::shared_ptr<CompassData> CompassDataPtr;

}
}
}


namespace pwiz {
namespace msdata {
namespace detail {
namespace Bruker {


PWIZ_API_DECL enum Reader_Bruker_Format
{
    Reader_Bruker_Format_Unknown,
    Reader_Bruker_Format_FID,
    Reader_Bruker_Format_YEP,
    Reader_Bruker_Format_BAF,
    Reader_Bruker_Format_U2,
    Reader_Bruker_Format_BAF_and_U2,
    Reader_Bruker_Format_TDF
};


/// returns Bruker format of 'path' if it is a Bruker directory;
/// otherwise returns empty string
PWIZ_API_DECL Reader_Bruker_Format format(const std::string& path);


PWIZ_API_DECL std::vector<InstrumentConfiguration> createInstrumentConfigurations(pwiz::vendor_api::Bruker::CompassDataPtr rawfile);
PWIZ_API_DECL cv::CVID translateAsInstrumentSeries(pwiz::vendor_api::Bruker::CompassDataPtr rawfile);
PWIZ_API_DECL cv::CVID translateAsAcquisitionSoftware(pwiz::vendor_api::Bruker::CompassDataPtr rawfile);


} // namespace Bruker
} // namespace detail
} // namespace msdata
} // namespace pwiz


#ifdef PWIZ_READER_BRUKER
#include "pwiz_aux/msrc/utility/vendor_api/Bruker/CompassData.hpp"
using namespace pwiz::vendor_api::Bruker;
#endif


#endif // _READER_BRUKER_DETAIL_HPP_
