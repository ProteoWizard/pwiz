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

    /// returns true iff the mod was constructed with formula
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


/// represents a list of modifications on a single amino acid
class PWIZ_API_DECL ModificationList
    : public std::vector<Modification> // TODO: make virtual wrapper
{
    public:

    ModificationList();
    //ModificationList(const Modification& mod);

    /// returns the sum of the monoisotopic delta masses of all modifications in the list
    double monoisotopicDeltaMass() const;

    /// returns the sum of the average delta masses of all modifications in the list
    double averageDeltaMass() const;
};


/// maps peptide/protein sequence indexes (0-based) to a modification list
/// * ModificationMap::NTerminus is the index for specifying N terminal mods
/// * ModificationMap::CTerminus is the index for specifying C terminal mods
class PWIZ_API_DECL ModificationMap
    : public pwiz::util::virtual_map<int, ModificationList>
{
    public:

    // bring the const overloads into scope
    using pwiz::util::virtual_map<int, ModificationList>::begin;
    using pwiz::util::virtual_map<int, ModificationList>::end;
    using pwiz::util::virtual_map<int, ModificationList>::rbegin;
    using pwiz::util::virtual_map<int, ModificationList>::rend;
    using pwiz::util::virtual_map<int, ModificationList>::operator[];
    using pwiz::util::virtual_map<int, ModificationList>::equal_range;
    using pwiz::util::virtual_map<int, ModificationList>::find;
    using pwiz::util::virtual_map<int, ModificationList>::lower_bound;
    using pwiz::util::virtual_map<int, ModificationList>::upper_bound;

    static const int NTerminus;
    static const int CTerminus;

    /// returns the sum of the monoisotopic delta masses of all modifications in the map
    double monoisotopicDeltaMass() const;

    /// returns the sum of the average delta masses of all modifications in the map
    double averageDeltaMass() const;

    /// Returns an iterator pointing to the first element stored in the map. First is defined by the map's comparison operator, Compare.
    virtual iterator begin();

    /// Returns an iterator pointing to the last element stored in the map; in other words, to the off-the-end value.
    virtual iterator end();

    /// Returns a reverse_iterator pointing to the first element stored in the map. First is defined by the map's comparison operator, Compare.
    virtual reverse_iterator rbegin();

    /// Returns a reverse_iterator pointing to the last element stored in the map; in other words, to the off-the-end value).
    virtual reverse_iterator rend();

    /// If an element with the key x exists in the map, then a reference to its associated value is returned. Otherwise the pair x,T() is inserted into the map and a reference to the default object T() is returned.
    virtual mapped_type& operator[](const key_type& x);

    /// Returns the pair (lower_bound(x), upper_bound(x)).
    virtual std::pair<iterator, iterator> equal_range(const key_type& x);

    /// Searches the map for a pair with the key value x and returns an iterator to that pair if it is found. If such a pair is not found the value end() is returned.
    virtual iterator find(const key_type& x);

    /// Returns a reference to the first entry with a key greater than or equal to x.
    virtual iterator lower_bound(const key_type& x);

    /// Returns an iterator for the first entry with a key greater than x.
    virtual iterator upper_bound(const key_type& x);

    /// Erases all elements from the self.
    virtual void clear();

    /// Deletes the map element pointed to by the iterator position.
    virtual void erase(iterator position);

    /// If the iterators start and finish point to the same map and last is reachable from first, all elements in the range [start, finish) are deleted from the map.
    virtual void erase(iterator start, iterator finish);

    /// Deletes the element with the key value x from the map, if one exists. Returns 1 if x existed in the map, 0 otherwise.
    virtual size_type erase(const key_type& x);

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted.
    virtual std::pair<iterator, bool> insert(const value_type& x);

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted. A position may be supplied as a hint regarding where to do the insertion. If the insertion is done right after position, then it takes amortized constant time. Otherwise it takes O(log N) time.
    virtual iterator insert(iterator position, const value_type& x);


    private:

    ModificationMap(Peptide* peptide);
    class Impl;
    std::auto_ptr<Impl> impl_;
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

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace proteome
} // namespace pwiz


#endif // _PEPTIDE_HPP_

