//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#ifndef _AMINOACID_HPP_
#define _AMINOACID_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include <string>
#include <memory>


namespace pwiz {
namespace proteome {


/// scope for types related to amino acids
namespace AminoAcid {


/// enumeration of the amino acids 
enum PWIZ_API_DECL Type 
{
    Alanine,
    Cysteine,
    AsparticAcid,
    GlutamicAcid,
    Phenylalanine,
    Glycine,
    Histidine,
    Isoleucine,
    Lysine,
    Leucine,
    Methionine,
    Asparagine,
    Proline,
    Glutamine,
    Arginine,
    Serine,
    Threonine,
    Valine,
    Tryptophan,
    Tyrosine,
    Selenocysteine,
    AspX,
    GlutX,
    Unknown
};


/// class for accessing information about the amino acids
namespace Info
{


/// struct for holding data for a single amino acid
struct PWIZ_API_DECL Record 
{
    std::string name; 
    std::string abbreviation; 
    char symbol; 
    chemistry::Formula residueFormula;
    chemistry::Formula formula;
    double abundance;
};


/// returns the amino acid's Record by type
PWIZ_API_DECL const Record& record(Type type);


/// returns the amino acid's Record by symbol (may throw) 
PWIZ_API_DECL const Record& record(char symbol);


} // namespace Info
} // namespace AminoAcid


} // namespace proteome
} // namespace pwiz


#endif // _AMINOACID_HPP_  

