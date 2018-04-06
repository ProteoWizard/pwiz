//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#ifndef _RAMPADAPTER_HPP_
#define _RAMPADAPTER_HPP_

#include <stdlib.h>
#include "pwiz/utility/misc/Export.hpp"
#include "ramp.h"
#include "boost/shared_ptr.hpp"
#include <string>
#include <vector>


namespace pwiz {
namespace msdata {


/// adapter to provide RAMP-friendly access to MSData library 
class PWIZ_API_DECL RAMPAdapter 
{
    public:

    /// constructor
    RAMPAdapter(const std::string& filename);

    /// returns the number of scans stored in the data file
    size_t scanCount() const;
    
    /// converts a scan number to a 0-based index; 
    /// returns scanCount() if scanNumber is not found
    size_t index(int scanNumber) const;

    /// returns the scan number for a specified scan
    int getScanNumber(size_t index) const;

    /// fills in RAMP ScanHeaderStruct for a specified scan
    ///
    /// you can optionally preload the peaklists too, but the 
    /// RAMP interface this emulates doesn't normally do that,
    /// so defaulting reservePeaks to true would be a nasty surprise
    /// performance-wise to anyone switching over from actual RAMP
    void getScanHeader(size_t index, ScanHeaderStruct& result, bool reservePeaks = false) const;

    /// fills in m/z-intensity pair array for a specified scan 
    void getScanPeaks(size_t index, std::vector<double>& result) const;

    /// fills in RAMP RunHeaderStruct 
    void getRunHeader(RunHeaderStruct& result) const;

    /// fills in RAMP InstrumentHeaderStruct
    void getInstrument(InstrumentStruct& result) const;

    private:
    class Impl; 
    boost::shared_ptr<Impl> impl_;
    RAMPAdapter(RAMPAdapter& that);
    RAMPAdapter& operator=(RAMPAdapter& that);
};


} // namespace msdata
} // namespace pwiz


#endif // _RAMPADAPTER_HPP_

