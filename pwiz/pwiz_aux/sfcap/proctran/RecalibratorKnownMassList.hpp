//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _RECALIBRATORKNOWNMASSLIST_HPP_
#define _RECALIBRATORKNOWNMASSLIST_HPP_


#include "Recalibrator.hpp"
#include "KnownMassList.hpp"


namespace pwiz {
namespace pdanalysis {


/// Recalibration, where calibration parameters are calculated via least  
/// squares with a know mass list 
class RecalibratorKnownMassList : public Recalibrator
{
    public:

    RecalibratorKnownMassList(const KnownMassList& kml);
    ~RecalibratorKnownMassList();

    protected:

    virtual CalibrationParameters calculateCalibrationParameters(const Scan& scan) const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
    RecalibratorKnownMassList(const RecalibratorKnownMassList& that);
    RecalibratorKnownMassList& operator=(const RecalibratorKnownMassList& that);
};


} // namespace pdanalysis 
} // namespace pwiz


#endif // _RECALIBRATORKNOWNMASSLIST_HPP_

