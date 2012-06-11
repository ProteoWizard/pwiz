//
// pwiz_RAMPAdapter.cpp
//
// $Id$
//
// a lightweight wrapper allowing SWIG to wrap some useful pwiz code
// Q: why a wrapper wrapper?  A: SWIG can't handle namespaces
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


#include "pwiz_RAMPAdapter.hpp"
#include "../../../data/msdata/RAMPAdapter.hpp"


    /// constructor
    pwiz_RAMPAdapter::pwiz_RAMPAdapter(const std::string& filename) {
		m_guts = new pwiz::msdata::RAMPAdapter(filename);
	}

    /// destructor
    pwiz_RAMPAdapter::~pwiz_RAMPAdapter() {
		delete m_guts;
	}

    /// returns the number of scans stored in the data file
    size_t pwiz_RAMPAdapter::scanCount() const {
		return m_guts->scanCount();
	}
    
    /// converts a scan number to a 0-based index; 
    /// returns scanCount() if scanNumber is not found
    size_t pwiz_RAMPAdapter::index(int scanNumber) const  {
		return m_guts->index(scanNumber);
	}

    /// fills in RAMP ScanHeaderStruct for a specified scan
    // index arg is a 0-based index into the scan table, as opposed to the scan ID
	// (use the pwiz_RAMPAdapter::index(int scanNumber) to get that value)
        void pwiz_RAMPAdapter::getScanHeader(size_t index, ScanHeaderStruct& result) const {
		m_guts->getScanHeader(index, result);
	}

    /// fills in m/z-intensity pair array for a specified scan 
	// index arg is a 0-based index into the scan table, as opposed to the scan ID
	// (use the pwiz_RAMPAdapter::index(int scanNumber) to get that value)
    void pwiz_RAMPAdapter::getScanPeaks(size_t index, std::vector<double>& result) const {
		m_guts->getScanPeaks(index,result);
	}

    /// fills in RAMP RunHeaderStruct 
    void pwiz_RAMPAdapter::getRunHeader(RunHeaderStruct& result) const {
		m_guts->getRunHeader(result);
	}

    /// fills in RAMP InstrumentHeaderStruct
    void pwiz_RAMPAdapter::getInstrument(InstrumentStruct& result) const {
		m_guts->getInstrument(result);
	}



