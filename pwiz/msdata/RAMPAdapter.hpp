//
// RAMPAdapter.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _RAMPADAPTER_HPP_
#define _RAMPADAPTER_HPP_


#include "ramp.h"
#include "boost/shared_ptr.hpp"
#include <string>
#include <vector>


namespace pwiz {
namespace msdata {


/// adapter to provide RAMP-friendly access to MSData library 
class RAMPAdapter 
{
    public:

    /// constructor
    RAMPAdapter(const std::string& filename);

    /// returns the number of scans stored in the data file
    size_t scanCount() const;
    
    /// converts a scan number to a 0-based index; 
    /// returns scanCount() if scanNumber is not found
    size_t index(int scanNumber) const;

    /// fills in RAMP ScanHeaderStruct for a specified scan
    void getScanHeader(size_t index, ScanHeaderStruct& result) const;

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

