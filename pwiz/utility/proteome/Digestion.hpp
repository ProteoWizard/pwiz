//
// Digestion.hpp 
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


#ifndef _DIGESTION_HPP_
#define _DIGESTION_HPP_


#include "utility/misc/Export.hpp"
#include "Chemistry.hpp"
#include "Peptide.hpp"
#include <string>
#include <memory>
#include <limits>


namespace pwiz {
namespace proteome {


/// enumeration of commonly used proteolytic enzymes or enzyme-like proteases
enum PWIZ_API_DECL ProteolyticEnzyme
{
    ProteolyticEnzyme_Trypsin, /// cleaves after K/R
    ProteolyticEnzyme_Chymotrypsin, /// cleaves after F/W/Y
    ProteolyticEnzyme_Clostripain, /// cleaves after R
    ProteolyticEnzyme_CyanogenBromide, /// cleaves after M when C-terminal residue is not S/T
    ProteolyticEnzyme_Pepsin /// cleaves after F/W/Y
};


/// enumerates the peptides from proteolytic digestion of a polypeptide or protein;
class PWIZ_API_DECL Digestion
{
    public:

    /// sets constraints for valid peptides produced by iterating the digestion
    struct PWIZ_API_DECL Config
    {
        int maximumMissedCleavages;

        //double minimumMass;
        //double maximumMass;

        int minimumLength;
        int maximumLength;

        Config(int maximumMissedCleavages = 100000,
               //double minimumMass = 0,
               //double maximumMass = 100000,
               int minimumLength = 0,
               int maximumLength = 100000);
    };

    struct Motif;

    /// specifies digestion occurs by a commonly used enzyme
    Digestion(const Peptide& peptide,
              ProteolyticEnzyme enzyme,
              const Config& config = Config());

    /// specifies digestion occurs by a combination of commonly used enzymes
    Digestion(const Peptide& peptide,
              const std::vector<ProteolyticEnzyme>& enzymes,
              const Config& config = Config());

    /// specifies digestion occurs by a user-specified motif
    Digestion(const Peptide& peptide,
              const Motif& motif,
              const Config& config = Config());

    /// specifies digestion occurs by a combination of user-specified motifs
    Digestion(const Peptide& peptide,
              const std::vector<Motif>& motifs,
              const Config& config = Config());

    ~Digestion();

    /// represents a rule to test whether a peptide bond is a valid digestion site
    struct PWIZ_API_DECL Motif
    {
        /// a motif is a kind of filter describing which amino acids are valid
        /// on the N and C termini of a digestion site
        /// Notes:
        ///  - the semantics are slightly different than "cut / no cut"
        ///  - the filter is specified with a limited Perl regular expression
        ///  - the filter must have exactly one '|' indicating where the cleavage occurs
        ///  - the supported amino acid symbol alphabet is: [ABCDEFGHIKLMNPQRSTVWXYZ]
        ///  - use the "[<residue set>]" syntax to match any residue in the set
        ///  - use the "[^<residue set>]" syntax to match any residue NOT in the set
        ///  - 'X' is a wildcard indicating any amino acid or a terminus
        ///  - '.' is synonymous with 'X' and is also allowed
        ///  - '{' and '}' indicate the N and C termini of the polypeptide being cleaved (respectively)
        ///  - '{' and '}' are implicitly valid as digestion sites unless explicitly disallowed
        ///  - filters can evaluate to test more than one residue (but most will not)
        ///  - empty filters evaluate to 'X|X'
        ///  - if the '|' is on the edge of the filter, any amino acid or a terminus is allowed
        ///
        /// Examples:
        ///  - Digest with trypsin: "[KR]|X", "[KR]|.", or "[KR]|"
        ///  - Digest with trypsin, disallowing N terminal P: "[KR]|[^P]"
        ///  - Digest after M at protein N terminus: "{M|X"
        ///  - Digest with cyanogen bromide, disallowing N terminal S/T: "M|[^ST]"
        ///  - Digest after T when it is 5 residues away from G: "GXXXXXT|X"
        Motif(const std::string& motif);
        Motif(const char* motif);
        Motif(const Motif& other);
        Motif& operator=(const Motif& rhs);
        ~Motif();

        /// returns true iff the sequence can be cleaved between offset and offset+1 using this motif;
        /// offset < 0 tests digestion at the N terminus
        /// offset = sequence.length()-1 tests digestion at the C terminus
        bool testSite(const std::string& sequence, int offset) const;

        private:
        class Impl;
        std::auto_ptr<Impl> impl_;
    };

    /// provides forward-only, read-only iteration to enumerate peptides
    class PWIZ_API_DECL const_iterator
    {
        public:
        const_iterator(const const_iterator& rhs);
        ~const_iterator();

        const Peptide& operator*() const;
        const Peptide* operator->() const;
        const_iterator& operator++();
        const_iterator operator++(int);
        bool operator!=(const const_iterator& that) const; 
        bool operator==(const const_iterator& that) const; 

        typedef std::forward_iterator_tag iterator_category;
        typedef Peptide value_type;
        typedef size_t difference_type;
        typedef value_type* pointer;
        typedef value_type& reference;

        private:
        const_iterator();
        const_iterator(const Digestion& digestion);

        friend class Digestion;
        class Impl;
        std::auto_ptr<Impl> impl_;
    };

    const_iterator begin() const;
    const_iterator end() const;

    private:
	friend class const_iterator;
	friend class const_iterator::Impl;
    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace proteome
} // namespace pwiz


#endif // _CLEAVAGE_HPP_
