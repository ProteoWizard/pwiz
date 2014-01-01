//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#ifndef _REFERENCES_HPP_
#define _REFERENCES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"


namespace pwiz {
namespace msdata {


/// functions for resolving references from objects into the internal MSData lists
namespace References {


PWIZ_API_DECL void resolve(ParamContainer& paramContainer, const MSData& msd);
PWIZ_API_DECL void resolve(FileDescription& fileDescription, const MSData& msd);
PWIZ_API_DECL void resolve(ComponentList& componentList, const MSData& msd);
PWIZ_API_DECL void resolve(InstrumentConfiguration& instrumentConfiguration, const MSData& msd);
PWIZ_API_DECL void resolve(ProcessingMethod& processingMethod, const MSData& msd);
PWIZ_API_DECL void resolve(DataProcessing& dataProcesssing, const MSData& msd);
PWIZ_API_DECL void resolve(ScanSettings& dataProcesssing, const MSData& msd);
PWIZ_API_DECL void resolve(Precursor& precursor, const MSData& msd);
PWIZ_API_DECL void resolve(Product& product, const MSData& msd);
PWIZ_API_DECL void resolve(Scan& scan, const MSData& msd);
PWIZ_API_DECL void resolve(ScanList& List, const MSData& msd);
PWIZ_API_DECL void resolve(BinaryDataArray& binaryDataArray, const MSData& msd);
PWIZ_API_DECL void resolve(Spectrum& spectrum, const MSData& msd);
PWIZ_API_DECL void resolve(Chromatogram& chromatogram, const MSData& msd);
PWIZ_API_DECL void resolve(Run& run, const MSData& msd);


///
/// Resolve internal references in an MSData object.
///
/// For an MSData object using a SpectrumListSimple to hold Spectrum objects in memory,
/// these references will be resolved as well.
///  
/// File-backed SpectrumList implementations using lazy evaluation of a Spectrum need 
/// to call resolve(spectrum, msd) before returning it to the client.
///
PWIZ_API_DECL void resolve(MSData& msd);


} // namespace References


} // namespace msdata
} // namespace pwiz


#endif // _REFERENCES_HPP_

