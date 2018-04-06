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


#ifndef _PEPTIDEIDMAP_HPP_
#define _PEPTIDEIDMAP_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "PeptideID.hpp"
#include <map>


namespace pwiz {
namespace peptideid {


class PWIZ_API_DECL PeptideIDMap : public PeptideID,
                                   public std::map<std::string, PeptideID::Record>
{
    public:
           
    virtual Record record(const pwiz::peptideid::PeptideID::Location& location) const;

    virtual PeptideID::Iterator begin() const;
    
    virtual PeptideID::Iterator end() const;
};


} // namespace peptideid
} // namespace pwiz


#endif // _PEPTIDEIDMAP_HPP_

