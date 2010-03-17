//
// pwiz_RAMPAdapter.hpp
//
// $Id$
//
// a lightweight wrapper allowing SWIG to wrap some useful pwiz code
// Q: why wrap a wrapper?  A: SWIG can't handle namespaces
//
//
// Original author: Brian Pratt <Brian.Pratt@insilicos.com>
//
// Copyright 2009 Insilicos LLC All Rights Reserved
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

#define RAMP_STRUCT_DECL_ONLY
#include "../../../data/msdata/RAMPAdapter.hpp"
#include <string>


/// SWIG-friendly wrapper to provide RAMP-friendly access to MSData library 
class pwiz_RAMPAdapter 
{
    public:

    /// constructor
    pwiz_RAMPAdapter(const std::string& filename);
	
	/// destructor
	~pwiz_RAMPAdapter();

    /// returns the number of scans stored in the data file
    size_t scanCount() const;
    
    /// converts a scan number to a 0-based index; 
    /// returns scanCount() if scanNumber is not found
    size_t index(int scanNumber) const;

    /// fills in RAMP ScanHeaderStruct for a specified scan
	// index arg is a 0-based index into the scan table, as opposed to the scan ID
	// (use the pwiz_RAMPAdapter::index(int scanNumber) to get that value)
    void getScanHeader(size_t index, ScanHeaderStruct& result) const;

    /// fills in m/z-intensity pair array for a specified scan 
	// index arg is a 0-based index into the scan table, as opposed to the scan ID
	// (use the pwiz_RAMPAdapter::index(int scanNumber) to get that value)   
    void getScanPeaks(size_t index, std::vector<double>& result) const;

    /// fills in RAMP RunHeaderStruct 
    void getRunHeader(RunHeaderStruct& result) const;

    /// fills in RAMP InstrumentHeaderStruct
    void getInstrument(InstrumentStruct& result) const;
	
	private:
	class pwiz::msdata::RAMPAdapter *m_guts;  // this is where the magic happens

};


