//
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


#ifndef _TRADATA_READER_HPP_CLI_
#define _TRADATA_READER_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "TraData.hpp"
#include "pwiz/data/tradata/DefaultReaderList.hpp"
#include "pwiz/data/tradata/Reader.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace tradata {


public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TraDataList, pwiz::tradata::TraDataPtr, TraData, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// interface for file readers
public ref class Reader
{
    DEFINE_INTERNAL_BASE_CODE(Reader, pwiz::tradata::Reader);

    public:

    /// return true iff Reader can handle the file;
    /// Reader may filter based on filename and/or head of the file
    virtual bool accept(System::String^ filename,
                        System::String^ head);

    /// fill in the TraData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraData^ result);

    /// fill in the TraData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraData^ result,
                      int runIndex);

    /// fill in the TraData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraDataList^ results);
};


/// Reader container (composite pattern).
///
/// The template get&lt;reader_type>() gives access to child Readers by type, to facilitate
/// Reader-specific configuration at runtime.
///
public ref class ReaderList : public Reader
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::tradata, ReaderList, Reader);

    public:

    /// returns child name iff some child identifies, else empty string
	virtual System::String^ identify(System::String^ filename);

    /// returns child name iff some child identifies, else empty string
    virtual System::String^ identify(System::String^ filename,
                                     System::String^ head);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      TraData^ result);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      TraData^ result,
                      int runIndex);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraData^ result) override;

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraData^ result,
                      int runIndex) override;

    /// fill in the TraDataList with TraData for all samples
    virtual void read(System::String^ filename,
                      TraDataList^ results);

    /// fill in the TraDataList with TraData for all samples
    virtual void read(System::String^ filename,
                      System::String^ head,
                      TraDataList^ results) override;

    static property ReaderList^ DefaultReaderList { ReaderList^ get(); }
};


} // namespace tradata
} // namespace CLI
} // namespace pwiz


#endif // _READER_HPP_CLI_
