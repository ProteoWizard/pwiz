//
// $Id$
//
//
// Original author: Jarrett Egertson <jegertso .@. uw.edu>
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

#ifndef _DEMUXTYPES_HPP
#define _DEMUXTYPES_HPP

#include "pwiz/data/msdata/MSData.hpp"
#include <Eigen>

namespace pwiz {
namespace msdata {
    typedef boost::shared_ptr<const msdata::SpectrumList> SpectrumList_const_ptr;
    typedef boost::shared_ptr<const msdata::Spectrum> Spectrum_const_ptr;
    typedef boost::shared_ptr<const BinaryDataArray> BinaryDataArray_const_ptr;
} // namespace msdata
} // namespace pwiz

namespace DemuxTypes
{
    using namespace Eigen;
    typedef double DemuxScalar;
    typedef Matrix<DemuxScalar, Dynamic, Dynamic> MatrixType;
    typedef boost::shared_ptr<MatrixType> MatrixPtr;
} // namespace DemuxTypes
#endif