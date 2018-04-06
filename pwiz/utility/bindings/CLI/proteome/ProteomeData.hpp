//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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

#ifndef _PROTEOMEDATA_HPP_CLI_
#define _PROTEOMEDATA_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "pwiz/data/proteome/ProteomeData.hpp"
#include "proteome.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace proteome {


/// <summary>
/// TODO: Protein comment.
/// </summary>
public ref class Protein : public Peptide
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::proteome, Protein, Peptide);

    public:

    Protein(System::String^ id, int index, System::String^ description, System::String^ sequence);

    bool empty();

    DEFINE_STRING_PROPERTY(id)
    DEFINE_PRIMITIVE_PROPERTY(size_t, int, index)
    DEFINE_STRING_PROPERTY(description)
};


/// <summary>
/// A list of protein indexes; implements the IList&lt;int&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(IndexList, size_t, int, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE);


/// <summary>
/// TODO: ProteinList comment.
/// </summary>
public ref class ProteinList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::proteome, ProteinList);

    public:
    
    /// <summary>
    /// returns the number of proteins
    /// </summary>
    virtual int size();

    /// <summary>
    /// returns true iff (size() == 0)
    /// </summary>
    virtual bool empty();

    /// <summary>
    /// find id in the protein index (returns size() on failure)
    /// </summary>
    virtual int find(System::String^ id);

    virtual IndexList^ findKeyword(System::String^ keyword);
    virtual IndexList^ findKeyword(System::String^ keyword, bool caseSensitive);

    /// <summary>
    /// retrieve a protein by index with amino acid sequence
    /// <para>- client may assume the underlying Protein^ is valid</para>
    /// </summary>
    virtual Protein^ protein(int index);

    /// <summary>
    /// retrieve a protein by index
    /// <para>- amino acid sequence will be available if (getSequence == true)</para>
    /// <para>- client may assume the underlying Protein^ is valid</para>
    /// </summary>
    virtual Protein^ protein(int index, bool getSequence);
};



/// <summary>
/// A list of Protein references; implements the IList&lt;Protein&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Proteins, pwiz::proteome::ProteinPtr, Protein, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// Simple writeable in-memory implementation of ProteinList.
/// <para>- protein() returns internal Protein references.</para>
/// </summary>
public ref class ProteinListSimple : public ProteinList
{
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::proteome, ProteinListSimple, ProteinList);

    public:

    DEFINE_REFERENCE_PROPERTY(Proteins, proteins)

    ProteinListSimple();

    // ProteinList implementation

    virtual int size() override;
    virtual bool empty() override;
    virtual Protein^ protein(int index) override;
    virtual Protein^ protein(int index, bool getSequence) override;
};


public ref class ProteomeData
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::proteome, ProteomeData);

    public:

    DEFINE_STRING_PROPERTY(id)
    DEFINE_OWNED_SHARED_REFERENCE_PROPERTY(pwiz::proteome::ProteinListPtr, ProteinList, proteinListPtr, proteinList)

    ProteomeData();

    bool empty();
};


} // namespace proteome
} // namespace CLI
} // namespace pwiz

#endif // _PROTEOMEDATA_HPP_CLI_
