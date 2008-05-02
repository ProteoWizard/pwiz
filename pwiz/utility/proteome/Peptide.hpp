//
// Peptide.hpp 
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _PEPTIDE_HPP_
#define _PEPTIDE_HPP_


#include "utility/misc/Export.hpp"
#include "Chemistry.hpp"
#include <string>
#include <memory>


namespace pwiz {
namespace proteome {


/// class representing a peptide
class PWIZ_API_DECL Peptide
{
    public:

    Peptide(const std::string& sequence);
    ~Peptide();

    std::string sequence() const;
    Chemistry::Formula formula() const;

    // vector<?> trypsinDigest() const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
    Peptide(const Peptide&);
    Peptide& operator=(const Peptide&);
};


} // namespace pwiz
} // namespace proteome


#endif // _PEPTIDE_HPP_

