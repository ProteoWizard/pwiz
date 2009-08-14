//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TRADATA_HPP_
#define _TRADATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "../msdata/MSData.hpp"
#include <vector>
#include <string>


namespace pwiz {
namespace tradata {


// these types are used verbatim from MSData
using msdata::CVParam;
using msdata::UserParam;


/// The base class for elements that may contain cvParams or userParams
struct PWIZ_API_DECL ParamContainer
{
    /// a collection of controlled vocabulary terms
    std::vector<CVParam> cvParams;

    /// a collection of uncontrolled user terms
    std::vector<UserParam> userParams;
    
    /// finds cvid in the container:
    /// - returns first CVParam result such that (result.cvid == cvid); 
    /// - if not found, returns CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParam(CVID cvid) const; 

    /// finds child of cvid in the container:
    /// - returns first CVParam result such that (result.cvid is_a cvid); 
    /// - if not found, CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParamChild(CVID cvid) const; 

    /// returns true iff cvParams contains exact cvid (recursive)
    bool hasCVParam(CVID cvid) const;

    /// returns true iff cvParams contains a child (is_a) of cvid (recursive)
    bool hasCVParamChild(CVID cvid) const;

    /// finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam userParam(const std::string&) const; 

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, const std::string& value = "", CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    template <typename value_type>
    void set(CVID cvid, value_type value, CVID units = CVID_Unknown)
    {
        set(cvid, boost::lexical_cast<std::string>(value), units);
    }

    /// returns true iff the element contains no params or param groups
    bool empty() const;

    /// clears the collections
    void clear();

    /// returns true iff this and that have the exact same cvParams and userParams
    bool operator==(const ParamContainer& that) const;

    /// returns !(this==that)
    bool operator!=(const ParamContainer& that) const;
};


/// special case for bool (outside the class for gcc 3.4, and inline for msvc)
template<>
inline void ParamContainer::set<bool>(CVID cvid, bool value, CVID units)
{
    set(cvid, (value ? "true" : "false"), units);
}


struct PWIZ_API_DECL Contact : public ParamContainer
{
    /// Identifier for the contact to be used for referencing within a document
    std::string id;

    Contact(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Contact> ContactPtr;


struct PWIZ_API_DECL Publication : public ParamContainer
{
    /// Identifier for the publication to be used for referencing within a document
    std::string id;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


struct PWIZ_API_DECL Software : public ParamContainer
{
    /// Identifier for the software to be used for referencing within a document
    std::string id;

    /// Version of the software program described
    std::string version;

    Software(const std::string& _id = "");

    Software(const std::string& _id,
             const CVParam& _param,
             const std::string& _version);

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Software> SoftwarePtr;


struct PWIZ_API_DECL RetentionTime : public ParamContainer
{
    RetentionTime()
        :   normalizedRetentionTime(0),
            localRetentionTime(0),
            predictedRetentionTime(0)
    {}

    /// Normalization standard used to generate the normalized retention time (e.g. H-PINS)
    std::string normalizationStandard;

    /// Retention time normalized to some standard reference
    double normalizedRetentionTime;

    /// Retention time calibrated to the local instrumental setup
    double localRetentionTime;

    /// Software used to predict a retention time
    SoftwarePtr predictedRetentionTimeSoftwarePtr;

    /// Retention time predicted by a software algorithm
    double predictedRetentionTime;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// Information about a prediction for a suitable transition using some software
struct PWIZ_API_DECL Prediction : public ParamContainer
{
    Prediction()
        :   recommendedTransitionRank(0),
            relativeIntensity(0),
            intensityRank(0)
    {}

    /// Ordinal rank of recommendation of transitions for this peptide
    unsigned int recommendedTransitionRank;

    /// Description of the source from which this prediction is derived
    std::string transitionSource;

    /// Relative intensity of the peak in the source from which this prediction is derived
    double relativeIntensity;

    /// Ordinal rank of intensity of the peak in the source from which this prediction is derived
    unsigned int intensityRank;

    /// Reference to a software package from which this prediction is derived
    SoftwarePtr softwarePtr;

    /// Reference to a contact person that generated this prediction
    ContactPtr contactPtr;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// Information about empirical mass spectrometer observations of the peptide
struct PWIZ_API_DECL Evidence : public ParamContainer
{
    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// Information about the state of validation of a transition on a given instrument model
struct PWIZ_API_DECL Validation : public ParamContainer
{
    Validation()
        :   recommendedTransitionRank(0),
            relativeIntensity(0),
            intensityRank(0)
    {}

    /// Recommended rank for this transition based on validation information
    unsigned int recommendedTransitionRank;

    /// Description of how this transitions was validated
    std::string transitionSource;

    /// Relative intensity of the peaks among sibling transitions for this peptide
    double relativeIntensity;

    /// Rank of the intensity of the peaks among sibling transitions for this peptide
    unsigned int intensityRank;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// Instrument on which transitions are validated
struct PWIZ_API_DECL Instrument : public ParamContainer
{
    /// Identifier for the instrument to be used for referencing within a document
    std::string id;

    Instrument(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Instrument> InstrumentPtr;


/// Instrument configuration used in the validation or optimization of the transitions
struct PWIZ_API_DECL Configuration : public ParamContainer
{
    std::vector<Validation> validations;

    /// Reference to a contact person originating this information
    ContactPtr contactPtr;

    /// Reference to an instrument for which this configuration information is appropriate
    InstrumentPtr instrumentPtr;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// A possible interpration of the product ion for a transition
struct PWIZ_API_DECL Interpretation : public ParamContainer
{
    Interpretation()
        :   productOrdinal(0),
            mzDelta(0),
            primary(false)
    {}

    /// Fragment ion series of the product ion (e.g. b, y, c, z, etc.)
    std::string productSeries;

    /// Ordinal for the product ion (e.g. 8 for a y8 ion)
    int productOrdinal;

    /// Additional adjustment of the fragment series and ordinal (e.g. -18^2 for a double water loss)
    std::string productAdjustment;

    /// Difference in m/z of the transition product ion m/z (Q3) minus the calculated m/z for this interpretation
    double mzDelta;

    /// True if this interpretation is considered the most likely of all possible listed interpretation of the product ion
    bool primary;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


struct PWIZ_API_DECL Protein : public ParamContainer
{
    /// Amino acid sequence of the protein
    std::string sequence;

    /// Name of the protein which one or more transitions are intended to identify
    std::string name;

    /// Accession number of the protein which one or more transitions are intended to identify
    std::string accession;

    /// Description of the protein which one or more transitions are intended to identify
    std::string description;

    /// Identifier for the protein to be used for referencing within a document
    std::string id;

    /// Arbitrary comment about this protein
    std::string comment;

    Protein(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Protein> ProteinPtr;


/// Peptide for which one or more transitions are intended to identify
struct PWIZ_API_DECL Peptide : public ParamContainer
{
    RetentionTime retentionTime;
    Evidence evidence;

    /// Label common to two or more transitions intended to group them for later analysis, typically corresponding transitions for light and heavy versions of a peptide
    std::string groupLabel;

    /// Identifier for the peptide to be used for referencing within a document
    std::string id;

    /// Sequence of the peptide without any mass modification information
    std::string unmodifiedSequence;

    /// Sequence of the peptide with modified amino acids denoted by having the new total mass in square brackets following the amino acid letter
    std::string modifiedSequence;

    /// Labeling category of the peptide, typically heavy or light
    std::string labelingCategory;

    /// Reference to a protein which this peptide is intended to identify
    ProteinPtr proteinPtr;

    Peptide(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Peptide> PeptidePtr;


/// Chemical compound other than a peptide for which one or more transitions 
struct PWIZ_API_DECL Compound : public ParamContainer
{
    /// Identifier for the compound to be used for referencing within a document
    std::string id;

    /// Information about predicted or calibrated retention times
    RetentionTime retentionTime;

    Compound(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Compound> CompoundPtr;


/// Precursor (Q1) of the transition
struct PWIZ_API_DECL Precursor
{
    Precursor() : charge(0), mz(0) {}

    /// Charge of the precursor for this transition
    unsigned int charge;

    /// Precursor m/z value for this transition
    double mz;
};


/// Product (Q3) of the transition
struct PWIZ_API_DECL Product
{
    Product() : charge(0), mz(0) {}

    /// Charge of the product for this transition
    unsigned int charge;

    /// Product m/z value for this transition
    double mz;
};


struct PWIZ_API_DECL Transition
{
    /// Precursor (Q1) of the transition
    Precursor precursor;

    /// Product (Q3) of the transition
    Product product;

    /// Information about a prediction for a suitable transition using some software
    Prediction prediction;

    /// List of possible interprations of fragment ions for a transition
    std::vector<Interpretation> interpretationList;

    /// List of insutrument configurations used in the validation or optimization of the transitions
    std::vector<Configuration> configurationList;

    /// Reference to a peptide which this transition is intended to identify
    PeptidePtr peptidePtr;

    /// Reference to a compound for this transition
    CompoundPtr compoundPtr;

    /// String label for this transition
    std::string name;

    /// returns true iff all members are empty
    bool empty() const;
};


struct PWIZ_API_DECL TraData
{
    /// the version of this traML document.
    std::string version;

    /// List of controlled vocabularies used in a TraML document
    /// note: one of the <cv> elements in this list MUST be the PSI MS controlled vocabulary. All <cvParam> elements in the document MUST refer to one of the <cv> elements in this list.
    std::vector<CV> cvs;

    /// List of contacts referenced in the generation or validation of transitions
    std::vector<ContactPtr> contactPtrs;

    std::vector<Publication> publications;
    
    /// List of instruments on which transitions are validated
    std::vector<InstrumentPtr> instrumentPtrs;
    
    /// List of software packages used in the generation of one of more transitions described in the document
    std::vector<SoftwarePtr> softwarePtrs;

    /// List of proteins for which one or more transitions are intended to identify
    std::vector<ProteinPtr> proteinPtrs;

    /// compoundList
    std::vector<PeptidePtr> peptidePtrs;
    std::vector<CompoundPtr> compoundPtrs;

    /// List of transitions
    std::vector<Transition> transitions;

    /// returns true iff all members are empty
    bool empty() const;
};


typedef boost::shared_ptr<TraData> TraDataPtr;


} // tradata
} // pwiz


#endif // _TRADATA_HPP_
