//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
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


#ifndef _PROTEOME_HPP_CLI_
#define _PROTEOME_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )

//#pragma unmanaged
#include "pwiz/data/common/cv.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/data/proteome/Peptide.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/Version.hpp"
//#pragma managed

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../common/ParamTypes.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend
#endif

#include "../chemistry/chemistry.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace proteome {


using namespace pwiz::CLI::cv;


/// <summary>
/// version information for the proteome namespace
/// </summary>
public ref class Version
{
    public:
    static int Major() {return pwiz::proteome::Version::Major();}
    static int Minor() {return pwiz::proteome::Version::Minor();}
    static int Revision() {return pwiz::proteome::Version::Revision();}
    static System::String^ LastModified() {return gcnew System::String(pwiz::proteome::Version::LastModified().c_str());}
    static System::String^ ToString() {return gcnew System::String(pwiz::proteome::Version::str().c_str());}
};


/// <summary>enumeration of the amino acids</summary>
public enum class AminoAcid
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


/// <summary>scope for accessing information about the amino acids</summary>
public ref struct AminoAcidInfo abstract sealed {


/// <summary>struct for holding data for a single amino acid</summary>
ref class Record 
{
    DEFINE_INTERNAL_BASE_CODE(Record, pwiz::proteome::AminoAcid::Info::Record);

    public:

    property System::String^ name { System::String^ get(); }
    property System::String^ abbreviation { System::String^ get(); }
    property System::Char symbol { System::Char get(); }
    property pwiz::CLI::chemistry::Formula^ residueFormula { pwiz::CLI::chemistry::Formula^ get(); }
    property pwiz::CLI::chemistry::Formula^ formula { pwiz::CLI::chemistry::Formula^ get(); }
};


/// <summary>returns the amino acid's Record by type</summary>
static Record^ record(AminoAcid aminoAcid);


/// <summary>returns the amino acid's Record by symbol (may throw) </summary>
static Record^ record(System::Char symbol);


};


ref class Fragmentation;
ref class ModificationMap;

/// <summary>
/// settings to enable parsing of inline modifications in peptide sequences
/// </summary>
public enum class ModificationParsing
{
    ModificationParsing_Off, /// <summary>any non-AA characters will cause an exception</summary>
    ModificationParsing_ByFormula, /// <summary>oxidized P in peptide: PEP(O)TIDE</summary>
    ModificationParsing_ByMass, /// <summary>PEP(15.94)TIDE or PEP(15.94,15.99)TIDE</summary>
    ModificationParsing_Auto /// <summary>either by formula or by mass</summary>
};

/// <summary>
/// the delimiter expected to signify an inline modification
/// </summary>
public enum class ModificationDelimiter
{
    ModificationDelimiter_Parentheses, /// <summary>'(' and ')'</summary>
    ModificationDelimiter_Brackets, /// <summary>'[' and ']'</summary>
    ModificationDelimiter_Braces /// <summary>'{' and '}'</summary>
};

/// <summary>
/// represents a peptide (sequence of amino acids)
/// </summary>
public ref class Peptide
{
    DEFINE_INTERNAL_BASE_CODE(Peptide, pwiz::proteome::Peptide);

    public:
    Peptide();
    Peptide(System::String^ sequence);
    Peptide(System::String^ sequence, ModificationParsing mp);
    Peptide(System::String^ sequence, ModificationParsing mp, ModificationDelimiter md);

    /// <summary>
    /// returns the sequence of amino acids using standard single character symbols
    /// </summary>
    property System::String^ sequence {System::String^ get();}

    /// <summary>
    /// returns the unmodified chemical composition of the peptide (sequence()+water)
    /// </summary>
    System::String^ formula();

    /// <summary>
    /// returns the (possibly modified) chemical composition of the peptide
    /// <para>- if modified = false: returns the composition formula of sequence()+water</para>
    /// <para>- if modified = true: returns the composition formula of sequence()+modifications()+water</para>
    /// <para>- note: throws an exception if modified = true and any modification has only mass information</para>
    /// </summary>
    System::String^ formula(bool modified);

    /// <summary>
    /// returns the monoisotopic mass of the modified peptide at neutral charge (sequence()+modifications()+water)
    /// </summary>
    double monoisotopicMass();

    /// <summary>
    /// returns the monoisotopic mass of the (possibly modified) peptide at neutral charge
    /// <para>- if modified = false: returns the monoisotopic mass of sequence()+water</para>
    /// <para>- if modified = true: returns the monoisotopic mass of sequence()+modifications()+water</para>
    /// </summary>
    double monoisotopicMass(bool modified);

    /// <summary>
    /// returns the monoisotopic mass of the modified peptide at charge &lt;charge&gt;
    /// <para>- if charge = 0: returns the monoisotopic mass of the modified peptide at neutral charge (sequence()+modifications()+water)</para>
    /// </summary>
    double monoisotopicMass(int charge);

    /// <summary>
    /// <para>if charge = 0: returns neutral mass</para>
    /// <para>if charge > 0: returns charged m/z</para>
    /// <para>if modified = false: returns the monoisotopic mass of sequence()+water</para>
    /// <para>if modified = true: returns the monoisotopic mass of sequence()+modifications()+water</para>
    /// </summary>
    double monoisotopicMass(bool modified, int charge);

    /// <summary>
    /// returns the molecular weight of the modified peptide at neutral charge (sequence()+modifications()+water)
    /// </summary>
    double molecularWeight();

    /// <summary>
    /// returns the molecular weight of the (possibly modified) peptide at neutral charge
    /// <para>- if modified = false: returns the molecular weight of sequence()+water</para>
    /// <para>- if modified = true: returns the molecular weight of sequence()+modifications()+water</para>
    /// </summary>
    double molecularWeight(bool modified);

    /// <summary>
    /// returns the molecular weight of the modified peptide at charge &lt;charge&gt;
    /// <para>- if charge = 0: returns the molecular weight of the modified peptide at neutral charge (sequence()+modifications()+water)</para>
    /// </summary>
    double molecularWeight(int charge);

    /// <summary>
    /// <para>- if charge = 0: returns neutral mass</para>
    /// <para>- if charge > 0: returns charged m/z</para>
    /// <para>- if modified = false: returns the molecular weight of sequence()+water</para>
    /// <para>- if modified = true: returns the molecular weight of sequence()+modifications()+water</para>
    /// </summary>
    double molecularWeight(bool modified, int charge);

    /// <summary>
    /// the map of sequence offsets (0-based) to modifications;
    /// <para>- modifications can be added or removed from the peptide with this map</para>
    /// </summary>
    ModificationMap^ modifications();

    /// <summary>
    /// returns a fragmentation model for the peptide;
    /// <para>- fragment masses can calculated as mono/avg and as modified/unmodified</para>
    /// </summary>
    Fragmentation^ fragmentation(bool monoisotopic, bool modified);
};


/// <summary>
/// provides fragment ion masses for a peptide
/// </summary>
public ref class Fragmentation
{
    DEFINE_INTERNAL_BASE_CODE(Fragmentation, pwiz::proteome::Fragmentation);

    public:
    Fragmentation(Peptide^ peptide,
                  bool monoisotopic,
                  bool modified);

    /// <summary>
    /// returns the a ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double a(int length, int charge);

    /// <summary>
    /// returns the b ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double b(int length, int charge);

    /// <summary>
    /// returns the c ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double c(int length, int charge);

    /// <summary>
    /// returns the x ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double x(int length, int charge);

    /// <summary>
    /// returns the y ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double y(int length, int charge);

    /// <summary>
    /// returns the z ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double z(int length, int charge);

    /// <summary>
    /// returns the z radical ion of length &lt;length&gt;
    /// <para>- if &lt;charge&gt; = 0: returns neutral mass</para>
    /// <para>- if &lt;charge&gt; > 0: returns charged m/z</para>
    /// </summary>
    double zRadical(int length, int charge);
};


/// <summary>
/// represents a post-translational modification (PTM)
/// <para>- note: modification formula or masses must be provided at instantiation</para>
/// </summary>
public ref class Modification
{
    DEFINE_INTERNAL_BASE_CODE(Modification, pwiz::proteome::Modification);

    public:
    Modification(System::String^ formula);
    Modification(double monoisotopicDeltaMass,
                 double averageDeltaMass);

    /// <summary>
    /// returns true iff the mod was constructed with formula
    /// </summary>
    bool hasFormula();

    /// <summary>
    /// returns the difference formula;
    /// <para>- note: throws runtime_error if hasFormula() = false</para>
    /// </summary>
    System::String^ formula();

    /// <summary>
    /// returns the monoisotopic delta mass of the modification
    /// </summary>
    double monoisotopicDeltaMass();

    /// <summary>
    /// returns the average delta mass of the modification
    /// </summary>
    double averageDeltaMass();
};


public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ModificationBaseList, pwiz::proteome::Modification, Modification, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

/// represents a list of modifications on a single amino acid
public ref class ModificationList : public ModificationBaseList
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::proteome, ModificationList, ModificationBaseList);

    public:
    ModificationList();
    ModificationList(Modification^ mod);

    /// <summary>
    /// returns the sum of the monoisotopic delta masses of all modifications in the list
    /// </summary>
    double monoisotopicDeltaMass();

    /// <summary>
    /// returns the sum of the average delta masses of all modifications in the list
    /// </summary>
    double averageDeltaMass();
};

DEFINE_VIRTUAL_MAP_WRAPPER(ModificationBaseMap, \
                           int, int, int, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE, \
                           pwiz::proteome::ModificationList, ModificationList, ModificationList^, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

/// <summary>
/// maps peptide/protein sequence indexes (0-based) to a modification list
/// <para>- ModificationMap.NTerminus() returns the index for specifying N terminal mods</para>
/// <para>- ModificationMap.CTerminus() returns the index for specifying C terminal mods</para>
/// </summary>
public ref class ModificationMap : public ModificationBaseMap
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::proteome, ModificationMap, ModificationBaseMap);
    ModificationMap(); // internal default constructor

    public:

    /// <summary>
    /// returns the index for specifying N terminal mods
    /// </summary>
    static int NTerminus();

    /// <summary>
    /// returns the index for specifying C terminal mods
    /// </summary>
    static int CTerminus();
};


/// <summary>
/// peptide subclass that contains extra metadata provided by digestion
/// </summary>
public ref class DigestedPeptide : public Peptide
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::proteome, DigestedPeptide, Peptide)

    public:
    DigestedPeptide(System::String^ sequence);

    DigestedPeptide(System::String^ sequence,
                    int offset,
                    int missedCleavages,
                    bool NTerminusIsSpecific,
                    bool CTerminusIsSpecific);

    /// <summary>
    /// returns the offset of the N terminus of the peptide
    /// in the polypeptide from which it was digested
    /// </summary>
    int offset();

    /// <summary>
    /// returns the number of missed cleavage sites in the peptide
    /// </summary>
    int missedCleavages();

    /// <summary>
    /// returns the number of termini that matched to the digestion rules
    /// </summary>
    int specificTermini();

    /// <summary>
    /// returns true iff the N terminus matched the digestion rules
    /// </summary>
    bool NTerminusIsSpecific();

    /// <summary>
    /// returns true iff the C terminus matched the digestion rules
    /// </summary>
    bool CTerminusIsSpecific();

    /// <summary>
    /// returns residue preceding digestion site
    /// </summary>
    System::String^ NTerminusPrefix();

    /// <summary>
    /// returns residue following digestion site
    /// </summary>
    System::String^ CTerminusSuffix();
};


/// <summary>
/// enumerates the peptides from proteolytic digestion of a polypeptide or protein;
/// </summary>
public ref class Digestion : public System::Collections::Generic::IEnumerable<DigestedPeptide^>
{
    DEFINE_INTERNAL_BASE_CODE(Digestion, pwiz::proteome::Digestion)

    public:

    /// <summary>
    /// sets the number of peptide termini that must match to a digestion motif
    /// note: castable to int; i.e. non=0, semi=1, fully=2
    /// </summary>
    enum class Specificity
    {
        NonSpecific = 0, /// neither termini must match digestion motif(s)
        SemiSpecific = 1, /// either or both termini must match digestion motif(s)
        FullySpecific = 2 /// both termini must match digestion motif(s)
    };

    /// <summary>
    /// sets constraints for valid peptides produced by iterating the digestion
    /// </summary>
    ref class Config
    {
        public:
        int maximumMissedCleavages;

        //double minimumMass;
        //double maximumMass;

        int minimumLength;
        int maximumLength;

        Specificity minimumSpecificity; 

        /// <summary>
        /// creates the default config:
        /// * 100000 missed cleavages
        /// * no minimum length
        /// * 100000 maximum length
        /// * full terminal specificity
        /// </summary>
        Config();

        Config(int maximumMissedCleavages,
               int minimumLength,
               int maximumLength,
               Specificity minimumSpecificity);
    };

    /// <summary>
    /// returns the set of predefined cleavage agents defined in the PSI-MS CV
    /// </summary>
    static initonly System::Collections::Generic::List<CVID>^ getCleavageAgents();

    /// <summary>
    /// returns the names of the set of predefined cleavage agents defined in the PSI-MS CV
    /// </summary>
    static initonly System::Collections::Generic::List<System::String^>^ getCleavageAgentNames();

    /// <summary>
    /// returns the cvid of the specified cleavage agent using a case-insensitive search,
    /// or CVID_Unknown if the agent is not found
    /// </summary>
    static CVID getCleavageAgentByName(System::String^ agentName);

    /// <summary>
    /// returns the Perl regular expression defining the places in a
    /// polypeptide or protein that the agent will cut.
    /// </summary>
    /// <throws>ArgumentException if the cleavageAgent is not in getCleavageAgents()</throws>
    static System::String^ getCleavageAgentRegex(CVID cleavageAgent);

    /// <summary>
    /// specifies digestion occurs by a commonly used cleavage agent
    /// </summary>
    /// <throws>ArgumentException if cleavageAgent is not in getCleavageAgents()</throws>
    Digestion(Peptide^ peptide, CVID cleavageAgent);

    /// <summary>
    /// specifies digestion occurs by a commonly used cleavage agent
    /// </summary>
    /// <throws>ArgumentException if cleavageAgent is not in getCleavageAgents()</throws>
    Digestion(Peptide^ peptide, CVID cleavageAgent, Config^ config);

    /// <summary>
    /// specifies digestion occurs by a combination of commonly used cleavage agents
    /// </summary>
    /// <throws>ArgumentException if any of cleavageAgents are not in getCleavageAgents()</throws>
    Digestion(Peptide^ peptide, System::Collections::Generic::IEnumerable<CVID>^ cleavageAgents);

    /// <summary>
    /// specifies digestion occurs by a combination of commonly used cleavage agents
    /// </summary>
    /// <throws>ArgumentException if any of cleavageAgents are not in getCleavageAgents()</throws>
    Digestion(Peptide^ peptide, System::Collections::Generic::IEnumerable<CVID>^ cleavageAgents, Config^ config);

    /// <summary>
    /// specifies digestion occurs by a user-specified, zero-width Perl regular expression 
    /// example: "(?&lt;=K)" means "cleaves after K"
    /// example: "((?&lt;=D))|((?=D))" means "cleaves before or after D"
    /// example: "(?=[DE])" means "cleaves before D or E"
    /// example: "(?&lt;=[FYWLKR])(?!P)" means "cleaves after any single residue from FYWLKR except when it is followed by P"
    /// </summary>
    Digestion(Peptide^ peptide, System::String^ cleavageAgentRegex);

    /// <summary>
    /// specifies digestion occurs by a user-specified, zero-width Perl regular expression 
    /// example: "(?&lt;=K)" means "cleaves after K"
    /// example: "((?&lt;=D))|((?=D))" means "cleaves before or after D"
    /// example: "(?=[DE])" means "cleaves before D or E"
    /// example: "(?&lt;=[FYWLKR])(?!P)" means "cleaves after any single residue from FYWLKR except when it is followed by P"
    /// </summary>
    Digestion(Peptide^ peptide, System::String^ cleavageAgentRegex, Config^ config);

    /// <summary>
    /// returns all instances of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    System::Collections::Generic::IList<DigestedPeptide^>^ find_all(Peptide^ peptide);

    /// <summary>
    /// returns all instances of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    System::Collections::Generic::IList<DigestedPeptide^>^ find_all(System::String^ peptide);

    /// <summary>
    /// returns the first instance of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    /// <throws>ArgumentException if no instance of the peptide is found</throws>
    DigestedPeptide^ find_first(Peptide^ peptide);

    /// <summary>
    /// returns the first instance of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    /// <throws>ArgumentException if no instance of the peptide is found</throws>
    DigestedPeptide^ find_first(System::String^ peptide);

    /// <summary>
    /// beginning at the offset hint, returns the first instance of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    /// <throws>ArgumentException if no instance of the peptide is found</throws>
    DigestedPeptide^ find_first(Peptide^ peptide, int offsetHint);

    /// <summary>
    /// beginning at the offset hint, returns the first instance of the given peptide in the polypeptide under digestion;
    /// note: the filters set in Digestion::Config are respected!
    /// </summary>
    /// <throws>ArgumentException if no instance of the peptide is found</throws>
    DigestedPeptide^ find_first(System::String^ peptide, int offsetHint);

    /// <summary>
    /// provides forward-only, read-only iteration to enumerate peptides
    /// </summary>
    ref class Enumerator : public System::Collections::Generic::IEnumerator<DigestedPeptide^>
    {
        internal: Digestion^ digestion_;
                  pwiz::proteome::Digestion* base_;
                  pwiz::proteome::Digestion::const_iterator* itr_;
                  bool isReset_;

        public:

        Enumerator(Digestion^ digestion)
        : digestion_(digestion),
          base_(&digestion->base()),
          itr_(new pwiz::proteome::Digestion::const_iterator(digestion->base().end())),
          isReset_(true)
        {}

        property DigestedPeptide^ Current
        {
            virtual DigestedPeptide^ get()
            {
                // make a copy of the native DigestedPeptide because the reference is transient
                return gcnew DigestedPeptide(new pwiz::proteome::DigestedPeptide(**itr_));
            }
        }

        property System::Object^ Current2
        {
            virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get
            {
                return (System::Object^) this->Current;
            }
        }

        virtual bool MoveNext()
        {
            if (isReset_) {isReset_ = false; *itr_ = base_->begin();}
            else if (*itr_ == base_->end()) return false;
            else ++*itr_;
            return *itr_ != base_->end();
        }

        virtual void Reset() {isReset_ = true; *itr_ = base_->end();}
        ~Enumerator() {delete itr_;}
    };

    virtual System::Collections::Generic::IEnumerator<DigestedPeptide^>^ GetEnumerator() {return gcnew Enumerator(this);}
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(this);}
};


} // namespace proteome
} // namespace CLI
} // namespace pwiz

#endif // _PROTEOME_HPP_CLI_
