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


#ifndef _PEPTIDE_HPP_
#define _PEPTIDE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace proteome {

class ModificationMap;
class Fragmentation;

/// settings to enable parsing of inline modifications in peptide sequences
enum PWIZ_API_DECL ModificationParsing
{
    ModificationParsing_Off, /// any non-AA characters will cause an exception
    ModificationParsing_ByFormula, /// oxidized P in peptide: PEP(O)TIDE
    ModificationParsing_ByMass, /// PEP(15.94)TIDE or PEP(15.94,15.99)TIDE
    ModificationParsing_Auto /// either by formula or by mass
};

/// the delimiter expected to signify an inline modification
enum PWIZ_API_DECL ModificationDelimiter
{
    ModificationDelimiter_Parentheses, /// '(' and ')'
    ModificationDelimiter_Brackets, /// '[' and ']'
    ModificationDelimiter_Braces /// '{' and '}'
};

#define MODIFICATION_PARSING_ARGUMENTS \
    ModificationParsing mp = ModificationParsing_Off, \
    ModificationDelimiter md = ModificationDelimiter_Parentheses

/// represents a peptide or polypeptide (a sequence of amino acids)
class PWIZ_API_DECL Peptide
{
    public:

    Peptide(const std::string& sequence = "", MODIFICATION_PARSING_ARGUMENTS);
    Peptide(const char* sequence, MODIFICATION_PARSING_ARGUMENTS);
    Peptide(std::string::const_iterator begin, std::string::const_iterator end, MODIFICATION_PARSING_ARGUMENTS);
    Peptide(const char* begin, const char* end, MODIFICATION_PARSING_ARGUMENTS);
    Peptide(const Peptide&);
    Peptide& operator=(const Peptide&);
    virtual ~Peptide();

    /// returns the sequence of amino acids making up the peptide
    const std::string& sequence() const;

    /// if modified = false: returns the composition formula of sequence()+water
    /// if modified = true: returns the composition formula of sequence()+modifications()+water
    /// throws an exception if modified = true and any modification has only mass information
    chemistry::Formula formula(bool modified = false) const;

    /// if charge = 0: returns neutral mass
    /// if charge > 0: returns charged m/z
    /// if modified = false: returns the monoisotopic mass of sequence()+water
    /// if modified = true: returns the monoisotopic mass of sequence()+modifications()+water
    double monoisotopicMass(int charge = 0, bool modified = true) const;

    /// if charge = 0: returns neutral mass
    /// if charge > 0: returns charged m/z
    /// if modified = false: returns the molecular weight of sequence()+water
    /// if modified = true: returns the molecular weight of sequence()+modifications()+water
    double molecularWeight(int charge = 0, bool modified = true) const;

    /// the map of sequence offsets (0-based) to modifications;
    /// modifications can be added or removed from the peptide with this map
    ModificationMap& modifications();

    /// the map of sequence offsets (0-based) to modifications
    const ModificationMap& modifications() const;

    /// returns a fragmentation model for the peptide;
    /// fragment masses can calculated as mono/avg and as modified/unmodified
    Fragmentation fragmentation(bool monoisotopic = true, bool modified = true) const;

    /// returns true iff peptide sequences and modifications are equal
    bool operator==(const Peptide& rhs) const;

    /// returns true iff this peptide has a lesser sequence length, sequence,
    /// modifications length, or modifications
    bool operator<(const Peptide& rhs) const;

    private:
    friend class ModificationMap; // allow ModificationMap to befriend Peptide::Impl
    friend class Fragmentation;
    class Impl;
    boost::shared_ptr<Impl> impl_;
};


/// provides fragment ion masses for a peptide
class PWIZ_API_DECL Fragmentation
{
    public:

    Fragmentation(const Peptide& peptide,
                  bool monoisotopic,
                  bool modified);
    Fragmentation(const Fragmentation&);
    ~Fragmentation();

    /// returns the a ion of length <length>;
    /// example: a(1) returns the a1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double a(size_t length, size_t charge = 0) const;

    /// returns the b ion of length <length>
    /// example: b(1) returns the b1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double b(size_t length, size_t charge = 0) const;

    /// returns the c ion of length <length>
    /// example: c(1) returns the c1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double c(size_t length, size_t charge = 0) const;

    /// returns the x ion of length <length>
    /// example: x(1) returns the x1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double x(size_t length, size_t charge = 0) const;

    /// returns the y ion of length <length>
    /// example: y(1) returns the y1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double y(size_t length, size_t charge = 0) const;

    /// returns the z ion of length <length>
    /// example: z(1) returns the z1 ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double z(size_t length, size_t charge = 0) const;

    /// returns the z radical ion of length <length>
    /// example: zRadical(1) returns the z1* ion
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double zRadical(size_t length, size_t charge = 0) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
};


} // namespace proteome
} // namespace pwiz


// include here for user convenience
#include "Modification.hpp"


#endif // _PEPTIDE_HPP_

