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
#include "utility/misc/virtual_map.hpp"

namespace pwiz {
namespace proteome {

class Modification;
class ModificationMap;
class Fragmentation;

/// represents a peptide (sequence of amino acids)
class PWIZ_API_DECL Peptide
{
    public:

    Peptide(const std::string& sequence);
    Peptide(const char* sequence);
    Peptide(std::string::const_iterator begin, std::string::const_iterator end);
    Peptide(const char* begin, const char* end);
    Peptide(const Peptide&);
    Peptide& operator=(const Peptide&);
    ~Peptide();

    /// returns the sequence of amino acids making up the peptide
    const std::string& sequence() const;

    /// if modified = false: returns the composition formula of sequence()+water
    /// if modified = true: returns the composition formula of sequence()+modifications()+water
    /// throws an exception if modified = true and any modification has only mass information
    Chemistry::Formula formula(bool modified = false) const;

    /// if modified = false: returns the monoisotopic mass of sequence()+water
    /// if modified = true: returns the monoisotopic mass of sequence()+modifications()+water
    /// if charge = 0: returns neutral mass
    /// if charge > 0: returns charged m/z
    double monoisotopicMass(bool modified = true, int charge = 0) const;

    /// if modified = false: returns the molecular weight of sequence()+water
    /// if modified = true: returns the molecular weight of sequence()+modifications()+water
    /// if charge = 0: returns neutral mass
    /// if charge > 0: returns charged m/z
    double molecularWeight(bool modified = true, int charge = 0) const;

    /// the map of sequence offsets (0-based) to modifications;
    /// modifications can be added or removed from the peptide with this map
    ModificationMap& modifications();

    /// the map of sequence offsets (0-based) to modifications
    const ModificationMap& modifications() const;

    /// returns a fragmentation model for the peptide;
    /// fragment masses can calculated as mono/avg and as modified/unmodified
    Fragmentation fragmentation(bool monoisotopic = true, bool modified = true) const;

    private:
    friend class ModificationMap;
    friend class Fragmentation;
    class Impl;
    std::auto_ptr<Impl> impl_;
};


/// represents a post-translational modification (PTM)
class PWIZ_API_DECL Modification
{
    public:

    Modification(const Chemistry::Formula& formula);
    Modification(double monoisotopicDeltaMass,
                 double averageDeltaMass);
    Modification(const Modification&);
    Modification& operator=(const Modification&);
    ~Modification();

    /// returns true iff the mod was constructed with formulae
    bool hasFormula() const;

    /// returns the difference formula;
    /// throws runtime_error if hasFormula() = false
    const Chemistry::Formula& formula() const;

    double monoisotopicDeltaMass() const;
    double averageDeltaMass() const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


/// represents a list of modifications
typedef std::vector<Modification> ModificationList;


/// maps peptide/protein sequence indexes (0-based) to a modification list
/// * ModificationMap::NTerminus is the index for specifying N terminal mods
/// * ModificationMap::CTerminus is the index for specifying C terminal mods
class PWIZ_API_DECL ModificationMap
    : public pwiz::util::virtual_map<int, ModificationList>
{
    public:

    static const int NTerminus;
    static const int CTerminus;

    /// Erases all elements from the self.
    virtual void clear();

    /// Deletes the map element pointed to by the iterator position.
    virtual void erase(iterator position);

    /// If the iterators start and finish point to the same map and last is reachable from first, all elements in the range [start, finish) are deleted from the map.
    virtual void erase(iterator start, iterator finish);

    /// Deletes the element with the key value x from the map, if one exists. Returns 1 if x existed in the map, 0 otherwise.
    virtual size_type erase(const key_type& x);

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted. A position may be supplied as a hint regarding where to do the insertion. If the insertion is done right after position, then it takes amortized constant time. Otherwise it takes O(log N) time.
    virtual std::pair<iterator, bool> insert(const value_type& x);

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted. A position may be supplied as a hint regarding where to do the insertion. If the insertion is done right after position, then it takes amortized constant time. Otherwise it takes O(log N) time.
    virtual iterator insert(iterator position, const value_type& x);


    private:

    ModificationMap(Peptide* peptide);
    Peptide* peptide;
    ModificationMap(const ModificationMap&);
    ModificationMap& operator=(const ModificationMap&);
    virtual void swap(ModificationMap&);
    friend class Peptide::Impl;
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

    /// returns the a ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double a(size_t length, size_t charge = 0) const;

    /// returns the b ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double b(size_t length, size_t charge = 0) const;

    /// returns the c ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double c(size_t length, size_t charge = 0) const;

    /// returns the x ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double x(size_t length, size_t charge = 0) const;

    /// returns the y ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double y(size_t length, size_t charge = 0) const;

    /// returns the z ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    double z(size_t length, size_t charge = 0) const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace proteome
} // namespace pwiz


#endif // _PEPTIDE_HPP_

