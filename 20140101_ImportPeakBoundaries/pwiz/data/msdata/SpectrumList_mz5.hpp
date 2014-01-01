//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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


#ifndef _SPECTRUMLIST_MZ5_HPP_
#define _SPECTRUMLIST_MZ5_HPP_


#include "SpectrumListBase.hpp"
#include "mz5/Datastructures_mz5.hpp"
#include "mz5/ReferenceRead_mz5.hpp"
#include "mz5/Configuration_mz5.hpp"
#include "mz5/Connection_mz5.hpp"


namespace pwiz {
namespace msdata {


/// implementation of SpectrumList, backed by an mz5 file
class PWIZ_API_DECL SpectrumList_mz5 : public SpectrumListBase
{
    public:
    /**
     * Creates a smart pointer holding the SpectrumList.
     * @param readPtr Helper object to read mz5 files
     * @param connectionPtr connection to mz5 file
     * @param msd MSData object
     */
    static SpectrumListPtr create(boost::shared_ptr<mz5::ReferenceRead_mz5> readPtr,
                                  boost::shared_ptr<mz5::Connection_mz5> connectionPtr,
                                  const MSData& msd);

    /**
     * Destructor.
     */
    virtual ~SpectrumList_mz5();
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLIST_MZ5_HPP_
