;//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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

#ifndef _TRADATA_HPP_CLI_
#define _TRADATA_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../common/ParamTypes.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend

    // list of friend assemblies that are permitted to access MSData's internal members
    [assembly:System::Runtime::CompilerServices::InternalsVisibleTo("pwiz_bindings_cli_analysis")];
#endif

#include "pwiz/data/tradata/TraData.hpp"
#include "pwiz/data/tradata/Version.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace tradata {


using namespace data;

public ref class Contact : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Contact, ParamContainer);
	public:

	/// <summary>
    /// an identifier for this file.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	Contact();
	Contact(System::String^ id);
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Contact references; implements the IList&lt;Contact&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ContactList, pwiz::tradata::ContactPtr, Contact, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


public ref class Publication : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Publication, ParamContainer);
	public:

	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	Publication();
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Publications; implements the IList&lt;Publication&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(PublicationList, pwiz::tradata::Publication, Publication, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Software : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Software, ParamContainer);
	public:

	/// <summary>
    /// an identifier for this software that is unique across all SoftwareTypes.
    /// </summary>
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	/// <summary>
    /// the software version.
    /// </summary>
	property System::String^ version
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	Software();
    Software(System::String^ _id);
    Software(System::String^ _id, CVParam^ param, System::String^ version);

    /// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Software references; implements the IList&lt;Software&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SoftwareList, pwiz::tradata::SoftwarePtr, Software, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);

public ref class RetentionTime : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, RetentionTime, ParamContainer);
	public:
	
	/// <summary>
    /// reference to a previously defined software element.
    /// </summary>
    property Software^ software
    {
        Software^ get();
    }
	
	RetentionTime();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of RetentionTimes; implements the IList&lt;RetentionTime&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(RetentionTimeList, pwiz::tradata::RetentionTime, RetentionTime, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Prediction : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Prediction, ParamContainer);
	public:
	
	/// <summary>
    /// reference to a previously defined software element.
    /// </summary>
    property Software^ software
    {
        Software^ get();
    }
	
	property Contact^ contact
    {
        Contact^ get();
    }
	
	Prediction();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;	
};

public ref class Evidence : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Evidence, ParamContainer);
	public:
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
	
	Evidence();
	
    bool empty() new;
};

public ref class Validation : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Validation, ParamContainer);
	public:
	
	Validation();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Validations; implements the IList&lt;Validation&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ValidationList, pwiz::tradata::Validation, Validation, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Instrument : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Instrument, ParamContainer);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	Instrument();
	Instrument(System::String^ id);
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Instrument references; implements the IList&lt;Instrument&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(InstrumentList, pwiz::tradata::InstrumentPtr, Instrument, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);

public ref class Configuration : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Configuration, ParamContainer);
	public:
	
	property ValidationList^ validations
    {
        ValidationList^ get();
    }
	
	property Contact^ contact
    {
        Contact^ get();
    }
	
	property Instrument^ instrument
    {
        Instrument^ get();
    }
	
	Configuration();
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ConfigurationList, pwiz::tradata::Configuration, Configuration, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Interpretation : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Interpretation, ParamContainer);
	public:
	
	Interpretation();
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Interpretations; implements the IList&lt;Interpretation&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(InterpretationList, pwiz::tradata::Interpretation, Interpretation, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Protein : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Protein, ParamContainer);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property System::String^ sequence
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	Protein();
	Protein(System::String^ id);
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Protein references; implements the IList&lt;Protein&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ProteinList, pwiz::tradata::ProteinPtr, Protein, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);

/// <summary>
/// A molecule modification specification.
/// If n modifications are present on the peptide, there should be n instances of the modification element.
/// If multiple modifications are provided as cvParams, it is assumed the modification is ambiguous,
/// i.e. one modification or the other. If no cvParams are provided it is assumed that the delta has not been
/// matched to a known modification.
/// </summary>
public ref class Modification : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Modification, ParamContainer);
	
	public:
	
	property int location
	{
		int get();
        void set(int value);
	}
	
	property double monoisotopicMassDelta
	{
		double get();
        void set(double value);
	}
	
	property double averageMassDelta
	{
		double get();
        void set(double value);
	}	
	
	Modification();
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Modifications; implements the IList&lt;Modification&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ModificationList, pwiz::tradata::Modification, Modification, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Peptide : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Peptide, ParamContainer);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property System::String^ sequence
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property ModificationList^ modifications
	{
        ModificationList^ get();
    }
	
	property ProteinList^ proteins
	{
        ProteinList^ get();
    }
	
	property RetentionTimeList^ retentionTimes
	{
        RetentionTimeList^ get();
    }
	
	property Evidence^ evidence
	{
        Evidence^ get();
    }
	
	Peptide();
	Peptide(System::String^ id);
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Peptide references; implements the IList&lt;Peptide&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(PeptideList, pwiz::tradata::PeptidePtr, Peptide, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);

public ref class Compound : public ParamContainer
{
	DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Compound, ParamContainer);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property RetentionTimeList^ retentionTimes
	{
        RetentionTimeList^ get();
    }
	
	Compound();
	Compound(System::String^ id);
	
	/// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Compound references; implements the IList&lt;Compound&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CompoundList, pwiz::tradata::CompoundPtr, Compound, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);

public ref class Precursor : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Precursor, ParamContainer);
	public:
	
	Precursor();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

public ref class Product : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Product, ParamContainer);
	public:
	
	Product();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

public ref class Transition : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Transition, ParamContainer);
	
	public:
	
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property Peptide^ peptide
	{
		Peptide^ get();
	}
	
	property Compound^ compound
	{
		Compound^ get();
	}
	
	property Precursor^ precursor
	{
		Precursor^ get();
	}
	
	property Product^ product
	{
		Product^ get();
	}
	
	property Prediction^ prediction
	{
		Prediction^ get();
	}
	
	property RetentionTime^ retentionTime
	{
		RetentionTime^ get();
	}
	
	property InterpretationList^ interpretations
	{
		InterpretationList^ get();
	}
	
	property ConfigurationList^ configurations
	{
		ConfigurationList^ get();
	}
	
	Transition();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Transitions; implements the IList&lt;Transition&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TransitionList, pwiz::tradata::Transition, Transition, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class Target : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, Target, ParamContainer);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	property Peptide^ peptide
	{
		Peptide^ get();
	}
	
	property Compound^ compound
	{
		Compound^ get();
	}
	
	property Precursor^ precursor
	{
		Precursor^ get();
	}
	
	property RetentionTime^ retentionTime
	{
		RetentionTime^ get();
	}
	
	property ConfigurationList^ configurations
	{
		ConfigurationList^ get();
	}
	
	Target();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

/// <summary>
/// A list of Targets; implements the IList&lt;Target&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TargetListList, pwiz::tradata::Target, Target, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

public ref class TargetList : public ParamContainer
{
	DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, TargetList, ParamContainer);
	public:
	
	property TargetListList^ targetExcludeList
	{
		TargetListList^ get();
	}
	
	property TargetListList^ targetIncludeList
	{
		TargetListList^ get();
	}
	
	TargetList();
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};

public ref class TraData
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::tradata, TraData);
	public:
	
	property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }
	
	/// <summary>
    /// container for one or more controlled vocabulary definitions.
    /// <para>- note: one of the &lt;cv&gt; elements in this list MUST be the PSI MS controlled vocabulary. All &lt;cvParam&gt; elements in the document MUST refer to one of the &lt;cv&gt; elements in this list.</para>
    /// </summary>
    property CVList^ cvs
    {
        CVList^ get();
        void set(CVList^ value);
    }
	
	property ContactList^ contacts
    {
        ContactList^ get();
        void set(ContactList^ value);
    }
	
	property PublicationList^ publications
    {
        PublicationList^ get();
        void set(PublicationList^ value);
    }
	
	property InstrumentList^ instruments
    {
        InstrumentList^ get();
        void set(InstrumentList^ value);
    }
	
	property SoftwareList^ softwareList
    {
        SoftwareList^ get();
        void set(SoftwareList^ value);
    }
	
	property ProteinList^ proteins
    {
        ProteinList^ get();
        void set(ProteinList^ value);
    }
	
	property PeptideList^ peptides
    {
        PeptideList^ get();
        void set(PeptideList^ value);
    }
	
	property CompoundList^ compounds
    {
        CompoundList^ get();
        void set(CompoundList^ value);
    }
	
	property TransitionList^ transitions
    {
        TransitionList^ get();
        void set(TransitionList^ value);
    }
	
	property TargetList^ targets
    {
        TargetList^ get();
        void set(TargetList^ value);
    }
	
	/// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty();
	
	System::String^ version();
	
	TraData();
};

} // namespace msdata
} // namespace CLI
} // namespace pwiz

#endif // _MSDATA_HPP_CLI_