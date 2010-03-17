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

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../common/ParamTypes.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend
#endif

#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/data/proteome/Peptide.hpp"
#include "pwiz/data/proteome/Version.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace proteome {


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


public ref class Chemistry
{
    public:

    /// <summary>
	/// the mass of a proton in unified atomic mass units
	/// </summary>
    static property double Proton { double get(); }

    /// <summary>
	/// the mass of a neutron in unified atomic mass units
	/// </summary>
    static property double Neutron { double get(); }

    /// <summary>
	/// the mass of an electron in unified atomic mass units
	/// </summary>
    static property double Electron { double get(); }
};

ref class Fragmentation;
ref class ModificationMap;

/// <summary>
/// settings to enable parsing of inline modifications in peptide sequences
/// </summary>
public enum class ModificationParsing
{
    ModificationParsing_Off, /// any non-AA characters will cause an exception
    ModificationParsing_ByFormula, /// oxidized P in peptide: PEP(O)TIDE
    ModificationParsing_ByMass, /// PEP(15.94)TIDE or PEP(15.94,15.99)TIDE
    ModificationParsing_Auto /// either by formula or by mass
};

/// <summary>
/// the delimiter expected to signify an inline modification
/// </summary>
public enum class ModificationDelimiter
{
    ModificationDelimiter_Parentheses, /// '(' and ')'
    ModificationDelimiter_Brackets, /// '[' and ']'
    ModificationDelimiter_Braces /// '{' and '}'
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


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ModificationBaseList, pwiz::proteome::Modification, Modification, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

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

} // namespace proteome
} // namespace CLI
} // namespace pwiz

#endif // _PROTEOME_HPP_CLI_
