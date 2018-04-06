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


#ifndef _READER_HPP_CLI_
#define _READER_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "ProteomeData.hpp"
#include "pwiz/data/proteome/Reader.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace proteome {


/// interface for file readers
public ref class Reader
{
    DEFINE_INTERNAL_BASE_CODE(Reader, pwiz::proteome::Reader);

    public:

    /// return true iff Reader can handle the file;
    /// Reader may filter based on filename and/or head of the file
    virtual bool accept(System::String^ filename,
                        System::String^ head);

    /// return file type iff Reader recognizes the file, else empty;
	/// note: for formats requiring a 3rd party DLL identify() should
	/// return non-empty if it recognized the format, even though reading
	/// may fail if the 3rd party DLL isn't actually present
    /// Reader may filter based on URI and/or contents of the file
    virtual System::String^ identify(System::String^ filename,
                                     System::String^ head);

    /// fill in the ProteomeData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      ProteomeData^ result);
};


/// Reader container (composite pattern).
///
/// The template get&lt;reader_type>() gives access to child Readers by type, to facilitate
/// Reader-specific configuration at runtime.
///
public ref class ReaderList : public Reader
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::proteome, ReaderList, Reader);

    public:

    /// returns child name iff some child identifies, else empty string
	virtual System::String^ identify(System::String^ filename);

    /// returns child name iff some child identifies, else empty string
    virtual System::String^ identify(System::String^ filename,
                                     System::String^ head) override;

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      ProteomeData^ result);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      System::String^ head,
                      ProteomeData^ result) override;

    static property ReaderList^ DefaultReaderList { ReaderList^ get(); }
};


} // namespace proteome
} // namespace CLI
} // namespace pwiz


#endif // _READER_HPP_CLI_
