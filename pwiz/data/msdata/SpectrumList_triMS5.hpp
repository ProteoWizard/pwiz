//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2019 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
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
#ifndef _SpectrumList_triMS5_HPP_
#define _SpectrumList_triMS5_HPP_


#include "SpectrumListBase.hpp"
#include "triMS5/ReferenceRead_triMS5.hpp"
#include "triMS5/Configuration_triMS5.hpp"
#include "triMS5/Connection_triMS5.hpp"


namespace pwiz {
namespace msdata {


/// implementation of SpectrumList, backed by a triMS5 file
class PWIZ_API_DECL SpectrumList_triMS5 : public SpectrumListBase
{
    public:
    /**
     * Creates a smart pointer holding the SpectrumList.
     * @param readPtr Helper object to read triMS5 files
     * @param connectionPtr connection to triMS5 file
     * @param msd MSData object
     */
    static SpectrumListPtr create(boost::shared_ptr<triMS5::ReferenceRead_triMS5> readPtr, boost::shared_ptr<triMS5::Connection_triMS5> connectionPtr, const MSData& msd);

    /**
     * Destructor.
     */
    virtual ~SpectrumList_triMS5();
};


} // namespace msdata
} // namespace pwiz


#endif //  _SpectrumList_triMS5_HPP_
