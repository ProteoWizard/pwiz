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
//


#ifndef _CVTRANSLATOR_HPP_
#define _CVTRANSLATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "cv.hpp"
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace data {


/// translates text to CV terms
class PWIZ_API_DECL CVTranslator
{
    public:

    /// constructor -- dictionary includes all 
    /// CV term names and exact_synonyms 
    CVTranslator();

    /// insert a text-cvid pair into the dictionary
    void insert(const std::string& text, cv::CVID cvid);

    /// translate text -> CVID
    cv::CVID translate(const std::string& text) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    CVTranslator(CVTranslator&);
    CVTranslator& operator=(CVTranslator&);
};


} // namespace data
} // namespace pwiz


#endif // _CVTRANSLATOR_HPP_

