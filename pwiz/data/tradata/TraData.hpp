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
#include "pwiz/data/common/cv.hpp"
#include "pwiz/data/common/ParamTypes.hpp"
#include <vector>
#include <string>


namespace pwiz {
namespace tradata {


using namespace pwiz::data;


/// returns a default list of CVs used in an TraML document;
/// currently includes PSI-MS, Unit Ontology, and UNIMOD
PWIZ_API_DECL std::vector<CV> defaultCVList();


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
    /// Software used to determine the retention time
    SoftwarePtr softwarePtr;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// Information about a prediction for a suitable transition using some software
struct PWIZ_API_DECL Prediction : public ParamContainer
{
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
    /// returns true iff contain no params
    bool empty() const;
};


/// Information about the state of validation of a transition on a given instrument model
struct PWIZ_API_DECL Validation : public ParamContainer
{
    /// returns true iff contain no params
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
    /// returns true iff contains no params
    bool empty() const;
};


struct PWIZ_API_DECL Protein : public ParamContainer
{
    /// Identifier for the protein to be used for referencing within a document
    std::string id;

    /// Amino acid sequence of the protein
    std::string sequence;

    Protein(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Protein> ProteinPtr;


/// A molecule modification specification.
/// If n modifications are present on the peptide, there should be n instances of the modification element.
/// If multiple modifications are provided as cvParams, it is assumed the modification is ambiguous,
/// i.e. one modification or the other. If no cvParams are provided it is assumed that the delta has not been
/// matched to a known modification.
struct PWIZ_API_DECL Modification : public ParamContainer
{
    Modification();

    /// Location of the modification within the peptide sequence, counted from the N-terminus, starting at position 1. Specific modifications to the N-terminus should be given the location 0. Modification to the C-terminus should be given as peptide length + 1.
    int location;

    /// Atomic mass delta when assuming only the most common isotope of elements in Daltons.
	double monoisotopicMassDelta;

    /// Atomic mass delta when considering the natural distribution of isotopes in Daltons.
    double averageMassDelta;

    /// returns true iff all members are zero and contain no params
    bool empty() const;
};


/// Peptide for which one or more transitions are intended to identify
struct PWIZ_API_DECL Peptide : public ParamContainer
{
    /// Identifier for the peptide to be used for referencing within a document
    std::string id;

    /// Amino acid sequence of the peptide being described
    std::string sequence;

    /// List of modifications on this peptide
    std::vector<Modification> modifications;

    /// Reference to zero or more proteins which this peptide is intended to identify
    std::vector<ProteinPtr> proteinPtrs;

    /// List of retention time information entries
    std::vector<RetentionTime> retentionTimes;

    Evidence evidence;

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

    /// List of retention time information entries
    std::vector<RetentionTime> retentionTimes;

    Compound(const std::string& id = "");

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};

typedef boost::shared_ptr<Compound> CompoundPtr;


/// Precursor (Q1) of the transition
struct PWIZ_API_DECL Precursor : public ParamContainer
{
    /// returns true iff contains no params
    bool empty() const;
};


/// Product (Q3) of the transition
struct PWIZ_API_DECL Product : public ParamContainer
{
    /// returns true iff contains no params
    bool empty() const;
};


struct PWIZ_API_DECL Transition : public ParamContainer
{
    /// String label for this transition
    std::string id;

    /// Reference to a peptide which this transition is intended to identify
    PeptidePtr peptidePtr;

    /// Reference to a compound for this transition
    CompoundPtr compoundPtr;

    /// Precursor (Q1) of the transition
    Precursor precursor;

    /// Product (Q3) of the transition
    Product product;

    /// Information about a prediction for a suitable transition using some software
    Prediction prediction;

    /// Information about predicted or calibrated retention time
    RetentionTime retentionTime;

    /// List of possible interprations of fragment ions for a transition
    std::vector<Interpretation> interpretationList;

    /// List of insutrument configurations used in the validation or optimization of the transitions
    std::vector<Configuration> configurationList;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// A peptide or compound that is to be included or excluded from a target list of precursor m/z values.
struct PWIZ_API_DECL Target : public ParamContainer
{
	/// String label for this target
    std::string id;

    /// Reference to a peptide for which this target is the trigger
    PeptidePtr peptidePtr;

    /// Reference to a compound for which this target is the trigger
    CompoundPtr compoundPtr;
    
    /// Precursor (Q1) of the target
    Precursor precursor;

    /// Information about predicted or calibrated retention time
    RetentionTime retentionTime;

    /// List of instrument configurations used in the validation or optimization of the target
    std::vector<Configuration> configurationList;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


/// List of precursor m/z targets to include or exclude
struct PWIZ_API_DECL TargetList : public ParamContainer
{
    /// List of precursor m/z targets to exclude
    std::vector<Target> targetExcludeList;

    /// List of precursor m/z targets to include
    std::vector<Target> targetIncludeList;

    /// returns true iff all members are empty and contain no params
    bool empty() const;
};


namespace IO {struct HandlerTraData;} // forward declaration for friend


struct PWIZ_API_DECL TraData
{
    /// for internal use: not currently in the schema
    std::string id;

    /// List of controlled vocabularies used in a TraML document
    /// note: one of the <cv> elements in this list MUST be the PSI MS controlled vocabulary. All <cvParam> elements in the document MUST refer to one of the <cv> elements in this list.
    std::vector<CV> cvs;

    /// List of contacts referenced in the generation or validation of transitions
    std::vector<ContactPtr> contactPtrs;

    /// List of publications from which the transitions were collected or wherein they are published
    std::vector<Publication> publications;
    
    /// List of instruments on which transitions are validated
    std::vector<InstrumentPtr> instrumentPtrs;
    
    /// List of software packages used in the generation of one of more transitions described in the document
    std::vector<SoftwarePtr> softwarePtrs;

    /// List of proteins for which one or more transitions are intended to identify
    std::vector<ProteinPtr> proteinPtrs;

    /// List of compounds (including peptides) for which one or more transitions are intended to identify
    std::vector<PeptidePtr> peptidePtrs;
    std::vector<CompoundPtr> compoundPtrs;

    /// List of transitions
    std::vector<Transition> transitions;

    /// List of precursor m/z targets to include or exclude
    TargetList targets;

    /// returns true iff all members are empty
    bool empty() const;

    /// returns the version of this traML document;
    /// for a document created programmatically, the version is the current release version of traML;
    /// for a document created from a file/stream, the version is the schema version read from the file/stream
    const std::string& version() const;

    TraData();
    virtual ~TraData();

    private:
    // no copying
    TraData(const TraData&);
    TraData& operator=(const TraData&);

    protected:
    std::string version_; // schema version read from the file/stream
    friend struct IO::HandlerTraData;
};


typedef boost::shared_ptr<TraData> TraDataPtr;


} // tradata
} // pwiz


#endif // _TRADATA_HPP_
