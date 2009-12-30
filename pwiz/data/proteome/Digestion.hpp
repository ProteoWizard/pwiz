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


#ifndef _DIGESTION_HPP_
#define _DIGESTION_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "Peptide.hpp"
#include <boost/regex.hpp>
#include "boost/shared_ptr.hpp"
#include <string>
#include <limits>
#include <set>


namespace pwiz {
namespace proteome {


using namespace pwiz::cv;


/// enumeration of commonly used proteolytic enzymes or enzyme-like proteases
enum PWIZ_API_DECL ProteolyticEnzyme
{
    ProteolyticEnzyme_Trypsin, /// cleaves after K/R
    ProteolyticEnzyme_Chymotrypsin, /// cleaves after F/W/Y
    ProteolyticEnzyme_Clostripain, /// cleaves after R
    ProteolyticEnzyme_CyanogenBromide, /// cleaves after M when C-terminal residue is not S/T
    ProteolyticEnzyme_Pepsin /// cleaves after F/W/Y
};


/// peptide subclass that contains extra metadata provided by digestion
class PWIZ_API_DECL DigestedPeptide : public Peptide
{
    public:

    DigestedPeptide(const std::string& sequence);
    DigestedPeptide(const char* sequence);

    DigestedPeptide(std::string::const_iterator begin,
                    std::string::const_iterator end,
                    size_t offset,
                    size_t missedCleavages,
                    bool NTerminusIsSpecific,
                    bool CTerminusIsSpecific);

    DigestedPeptide(std::string::const_iterator begin,
		    std::string::const_iterator end,
		    size_t offset,
		    size_t missedCleavages,
		    bool NTerminusIsSpecific,
		    bool CTerminusIsSpecific, 
		    std::string nTermPrefix,
		    std::string cTermSuffix );

    DigestedPeptide(const DigestedPeptide&);
    DigestedPeptide& operator=(const DigestedPeptide&);
    ~DigestedPeptide();

    /// returns the offset of the N terminus of the peptide
    /// in the polypeptide from which it was digested
    size_t offset() const;

    /// returns the number of missed cleavage sites in the peptide
    size_t missedCleavages() const;

    /// returns the number of termini that matched to the digestion rules
    size_t specificTermini() const;

    /// returns true iff the N terminus matched the digestion rules
    bool NTerminusIsSpecific() const;

    /// returns true iff the C terminus matched the digestion rules
    bool CTerminusIsSpecific() const;

    /// returns residue preceding digestion site
    std::string nTermPrefix() const;

    /// returns residue following digestion site
    std::string cTermSuffix() const;



    private:
    size_t offset_;
    size_t missedCleavages_;
    bool NTerminusIsSpecific_;
    bool CTerminusIsSpecific_;
    std::string nTermPrefix_;
    std::string cTermSuffix_;
};


/// enumerates the peptides from proteolytic digestion of a polypeptide or protein;
class PWIZ_API_DECL Digestion
{
    public:

    /// sets the number of peptide termini that must match to a digestion motif
    /// note: castable to int; i.e. non=0, semi=1, fully=2
    enum PWIZ_API_DECL Specificity
    {
        NonSpecific = 0, /// neither termini must match digestion motif(s)
        SemiSpecific = 1, /// either or both termini must match digestion motif(s)
        FullySpecific = 2 /// both termini must match digestion motif(s)
    };

    /// sets constraints for valid peptides produced by iterating the digestion
    struct PWIZ_API_DECL Config
    {
        int maximumMissedCleavages;

        //double minimumMass;
        //double maximumMass;

        int minimumLength;
        int maximumLength;

        Specificity minimumSpecificity; 

        Config(int maximumMissedCleavages = 100000,
               //double minimumMass = 0,
               //double maximumMass = 100000,
               int minimumLength = 0,
               int maximumLength = 100000,
               Specificity minimumSpecificity = FullySpecific);
    };

    struct Motif;

    /// returns the set of predefined cleavage agents defined in the PSI-MS CV
    static const std::set<CVID>& getCleavageAgents();

    /// returns the names of the set of predefined cleavage agents defined in the PSI-MS CV
    static const std::vector<std::string>& getCleavageAgentNames();

    /// returns the cvid of the specified cleavage agent using a case-insensitive search,
    /// or CVID_Unknown if the agent is not found
    static CVID getCleavageAgentByName(const std::string& agentName);

    /// returns the Perl regular expression defining the places in a
    /// polypeptide or protein that the agent will cut.
    static const std::string& getCleavageAgentRegex(CVID agentCvid);

    /// specifies digestion occurs by a commonly used cleavage agent
    Digestion(const Peptide& peptide,
              CVID cleavageAgent,
              const Config& config = Config());

    /// specifies digestion occurs by a combination of commonly used cleavage agents
    Digestion(const Peptide& peptide,
              const std::vector<CVID>& cleavageAgents,
              const Config& config = Config());

    /// specifies digestion occurs by a user-specified, zero-width Perl regular expression 
    /// example: "(?<=K)" means "cleaves after K"
    /// example: "((?<=D))|((?=D))" means "cleaves before or after D"
    /// example: "(?=[DE])" means "cleaves before D or E"
    /// example: "(?<=[FYWLKR])(?!P)" means "cleaves after any single residue from FYWLKR except when it is followed by P"
    Digestion(const Peptide& peptide,
              const boost::regex& cleavageAgentRegex,
              const Config& config = Config());


    // DEPRECATED CONSTRUCTORS ////////////////////////////

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
        boost::shared_ptr<Impl> impl_;
    };

    private:
    class Impl; // forward-declared for const_iterator

    public:

    /// provides forward-only, read-only iteration to enumerate peptides
    class PWIZ_API_DECL const_iterator
    {
        public:
        const_iterator(const const_iterator& rhs);
        ~const_iterator();

        const DigestedPeptide& operator*() const;
        const DigestedPeptide* operator->() const;
        const_iterator& operator++();
        const_iterator operator++(int);
        bool operator!=(const const_iterator& that) const; 
        bool operator==(const const_iterator& that) const; 

        typedef std::forward_iterator_tag iterator_category;
        typedef DigestedPeptide value_type;
        typedef size_t difference_type;
        typedef value_type* pointer;
        typedef value_type& reference;

        private:
        const_iterator();
        const_iterator(const Digestion& digestion);

        friend class Digestion;
        friend class Digestion::Impl;

        class Impl;
        boost::shared_ptr<Impl> impl_;
    };

    const_iterator begin() const;
    const_iterator end() const;

    private:
	friend class const_iterator;
	friend class const_iterator::Impl;
    boost::shared_ptr<Impl> impl_;
};


} // namespace proteome
} // namespace pwiz


#endif // _DIGESTION_HPP_
