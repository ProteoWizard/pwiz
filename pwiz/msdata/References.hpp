//
// References.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _REFERENCES_HPP_
#define _REFERENCES_HPP_


#include "MSData.hpp"


namespace pwiz {
namespace msdata {


/// functions for resolving references from objects into the internal MSData lists
namespace References {


void resolve(ParamContainer& paramContainer, const MSData& msd);
void resolve(FileDescription& fileDescription, const MSData& msd);
void resolve(ComponentList& componentList, const MSData& msd);
void resolve(Instrument& instrument, const MSData& msd);
void resolve(DataProcessing& dataProcesssing, const MSData& msd);
void resolve(Acquisition& acquisition, const MSData& msd);
void resolve(AcquisitionList& acquisitionList, const MSData& msd);
void resolve(Precursor& precursor, const MSData& msd);
void resolve(Scan& scan, const MSData& msd);
void resolve(SpectrumDescription& spectrumDescription, const MSData& msd);
void resolve(BinaryDataArray& binaryDataArray, const MSData& msd);
void resolve(Spectrum& spectrum, const MSData& msd);
void resolve(Run& run, const MSData& msd);


///
/// Resolve internal references in an MSData object.
///
/// For an MSData object using a SpectrumListSimple to hold Spectrum objects in memory,
/// these references will be resolved as well.
///  
/// File-backed SpectrumList implementations using lazy evaluation of a Spectrum need 
/// to call resolve(spectrum, msd) before returning it to the client.
///
void resolve(MSData& msd);


} // namespace References


} // namespace msdata
} // namespace pwiz


#endif // _REFERENCES_HPP_

