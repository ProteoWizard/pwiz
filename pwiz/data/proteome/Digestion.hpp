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
#include "boost/shared_ptr.hpp"
#include <string>
#include <limits>
#include <set>


namespace pwiz {
namespace proteome {


using namespace pwiz::cv;


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
                    bool CTerminusIsSpecific, 
                    std::string NTerminusPrefix = "",
                    std::string CTerminusSuffix = "");

    DigestedPeptide(const Peptide& peptide,
                    size_t offset,
                    size_t missedCleavages,
                    bool NTerminusIsSpecific,
                    bool CTerminusIsSpecific, 
                    std::string NTerminusPrefix = "",
                    std::string CTerminusSuffix = "");

    DigestedPeptide(const DigestedPeptide&);
    DigestedPeptide& operator=(const DigestedPeptide&);
    virtual ~DigestedPeptide();

    /// returns the zero-based offset of the N terminus of the peptide
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
    std::string NTerminusPrefix() const;

    /// returns residue following digestion site
    std::string CTerminusSuffix() const;

    /// returns true iff peptide sequences, masses, and all digestion metadata are equal
    bool operator==(const DigestedPeptide& rhs) const;

    private:
    size_t offset_;
    size_t missedCleavages_;
    bool NTerminusIsSpecific_;
    bool CTerminusIsSpecific_;
    std::string NTerminusPrefix_;
    std::string CTerminusSuffix_;
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

        bool clipNTerminalMethionine;

        Config(int maximumMissedCleavages = 100000,
               //double minimumMass = 0,
               //double maximumMass = 100000,
               int minimumLength = 0,
               int maximumLength = 100000,
               Specificity minimumSpecificity = FullySpecific,
               bool clipNTerminalMethionine = true);
    };

    /// returns the set of predefined cleavage agents defined in the PSI-MS CV
    static const std::set<CVID>& getCleavageAgents();

    /// returns the names of the set of predefined cleavage agents defined in the PSI-MS CV
    static const std::vector<std::string>& getCleavageAgentNames();

    /// returns the cvid of the specified cleavage agent using a case-insensitive search,
    /// or CVID_Unknown if the agent is not found
    static CVID getCleavageAgentByName(const std::string& agentName);

    /// returns the cvid of the specified cleavage agent looking it up by the Perl regular expression,
    /// or CVID_Unknown if the agent is not found (the regex pattern must match exactly)
    static CVID getCleavageAgentByRegex(const std::string& agentRegex);

    /// returns the official PSI Perl regular expression defining the places in a
    /// polypeptide or protein that the agent will cut.
    static const std::string& getCleavageAgentRegex(CVID agentCvid);

    /// returns a modified version of a cleavage agent regex where any ambiguous AA symbols (BJXZ)
    /// are augmented with their unambiguous counterparts (e.g. B -> [BND])
    static std::string disambiguateCleavageAgentRegex(const std::string& cleavageAgentRegex);

    /// specifies digestion occurs by a commonly used cleavage agent
    Digestion(const Peptide& polypeptide,
              CVID cleavageAgent,
              const Config& config = Config());

    /// specifies digestion occurs by a combination of commonly used cleavage agents
    Digestion(const Peptide& polypeptide,
              const std::vector<CVID>& cleavageAgents,
              const Config& config = Config());

    /// specifies digestion occurs by a user-specified, zero-width Perl regular expression 
    /// example: "(?<=K)" means "cleaves after K"
    /// example: "((?<=D))|((?=D))" means "cleaves before or after D"
    /// example: "(?=[DE])" means "cleaves before D or E"
    /// example: "(?<=[FYWLKR])(?!P)" means "cleaves after any single residue from FYWLKR except when it is followed by P"
    Digestion(const Peptide& polypeptide,
              const std::string& cleavageAgentRegex,
              const Config& config = Config());

    /// specifies digestion occurs by a combination of user-specified, zero-width Perl regular expressions
    /// example: "(?<=K)" means "cleaves after K"
    /// example: "((?<=D))|((?=D))" means "cleaves before or after D"
    /// example: "(?=[DE])" means "cleaves before D or E"
    /// example: "(?<=[FYWLKR])(?!P)" means "cleaves after any single residue from FYWLKR except when it is followed by P"
    Digestion(const Peptide& polypeptide,
              const std::vector<std::string>& cleavageAgentRegexes,
              const Config& config = Config());

    /// returns all instances of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    std::vector<DigestedPeptide> find_all(const Peptide& peptide) const;

    /// returns the first instance of the given peptide in the polypeptide under digestion;
    /// if offsetHint is provided, the search will begin at that offset;
    /// throws runtime_error if no instance of the peptide is found;
    /// note: the filters set in Digestion::Config are respected!
    DigestedPeptide find_first(const Peptide& peptide, size_t offsetHint = 0) const;


    ~Digestion();


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

    typedef const_iterator iterator;

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
