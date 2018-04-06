//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _MODIFICATION_HPP_
#define _MODIFICATION_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "Peptide.hpp"
#include <string>
#include "pwiz/utility/misc/virtual_map.hpp"
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace proteome {

/// represents a post-translational modification (PTM)
/// modification formula or masses must be provided at instantiation
class PWIZ_API_DECL Modification
{
    public:

    /// constructs a zero-mass modification (provided for MSVC compatibility)
    Modification();

    Modification(const chemistry::Formula& formula);
    Modification(double monoisotopicDeltaMass,
                 double averageDeltaMass);
    Modification(const Modification&);
    Modification& operator=(const Modification&);
    ~Modification();

    /// returns true iff the mod was constructed with formula
    bool hasFormula() const;

    /// returns the difference formula;
    /// throws runtime_error if hasFormula() = false
    const chemistry::Formula& formula() const;

    double monoisotopicDeltaMass() const;
    double averageDeltaMass() const;

    /// returns true iff delta masses are equal
    bool operator==(const Modification& rhs) const;

    /// returns true iff this mod has smaller delta masses
    bool operator<(const Modification& rhs) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
};


/// represents a list of modifications on a single amino acid
class PWIZ_API_DECL ModificationList
    : public std::vector<Modification> // TODO: make virtual wrapper
{
    public:

    ModificationList();
    ModificationList(const Modification& mod);
    ModificationList(const std::vector<Modification>& mods);

    /// returns the sum of the monoisotopic delta masses of all modifications in the list
    double monoisotopicDeltaMass() const;

    /// returns the sum of the average delta masses of all modifications in the list
    double averageDeltaMass() const;

    /// returns true iff the list has equal modifications
    bool operator==(const ModificationList& rhs) const;

    /// returns true iff the list has fewer modifications or one that's lesser than in the rhs list
    bool operator<(const ModificationList& rhs) const;
};


/// maps peptide/protein sequence indexes (0-based) to a modification list
/// * ModificationMap::NTerminus() returns the index for specifying N terminal mods
/// * ModificationMap::CTerminus() returns the index for specifying C terminal mods
class PWIZ_API_DECL ModificationMap
    : public pwiz::util::virtual_map<int, ModificationList>
{
    public:

    ~ModificationMap();

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

    static int NTerminus();
    static int CTerminus();

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

    /// returns true iff the map has the same modifications
    virtual bool operator==(const ModificationMap& rhs) const;

    /// returns true iff the map has fewer modified positions or one of the positions is less than in the rhs map
    virtual bool operator<(const ModificationMap& rhs) const;

    private:

    ModificationMap();
    ModificationMap(const ModificationMap& other);
    ModificationMap& operator=(const ModificationMap&);
    class Impl;
    boost::shared_ptr<Impl> impl_;
    virtual void swap(ModificationMap&);
    friend class Peptide::Impl; // allow only Peptide::Impl to construct a ModificationMap
};


} // namespace proteome
} // namespace pwiz


#endif // _MODIFICATION_HPP_
