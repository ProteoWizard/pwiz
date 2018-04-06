//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _MZID_READER_HPP_
#define _MZID_READER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include <string>
#include <stdexcept>
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {

/// interface for file readers
class PWIZ_API_DECL Reader
{
    public:


    /// HACK: provide an option to read only file-level metadata;
    ///       once we have an enumerable ResultList implementation
    ///       this will be deprecated
    struct PWIZ_API_DECL Config
    {
        bool ignoreSequenceCollectionAndAnalysisData;
        bool ignoreProteinDetectionList;
        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry;

        Config(bool ignoreSequenceCollectionAndAnalysisData = false, bool ignoreProteinDetectionList = false)
        :   ignoreSequenceCollectionAndAnalysisData(ignoreSequenceCollectionAndAnalysisData),
            ignoreProteinDetectionList(ignoreProteinDetectionList),
            iterationListenerRegistry(NULL)
        {}
    };


    /// return true iff Reader recognizes the file as one it should handle
	/// that's not to say one it CAN handle, necessarily, as in Thermo on linux,
	/// see comment for identify() below
    bool accept(const std::string& filename,
                const std::string& head) const
	{
		return (identify(filename,head).length() != 0);
	}

    /// return file type iff Reader recognizes the file, else empty;
	/// note: for formats requiring a 3rd party DLL identify() should
	/// return true if it recognized the format, even though reading
	/// may fail if the 3rd party DLL isn't actually present
    /// Reader may filter based on filename and/or head of the file
    virtual std::string identify(const std::string& filename,
                                 const std::string& head) const = 0;

    /// fill in a vector of IdentData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      IdentData& results,
                      const Config& config = Config()) const;

    /// fill in a vector of IdentData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentData& results,
                      const Config& config = Config()) const = 0;

    /// fill in a vector of IdentData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      IdentDataPtr& results,
                      const Config& config = Config()) const;

    /// fill in a vector of IdentData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentDataPtr& results,
                      const Config& config = Config()) const = 0;

    /// fill in a vector of IdentData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config = Config()) const = 0;

	virtual const char *getType() const = 0; // what kind of reader are you?

    virtual ~Reader(){}
};

typedef boost::shared_ptr<Reader> ReaderPtr;

class PWIZ_API_DECL ReaderFail : public std::runtime_error // reader failure exception
{
    public:

    ReaderFail(const std::string& error)
    :   std::runtime_error(("[ReaderFail] " + error).c_str()),
		error_(error)
    {}

    virtual const std::string& error() const {return error_;}
    virtual ~ReaderFail() throw() {}

    private:
    std::string error_;
};

typedef boost::shared_ptr<Reader> ReaderPtr;


///
/// Reader container (composite pattern).
///
/// The template get<reader_type>() gives access to child Readers by type, to facilitate
/// Reader-specific configuration at runtime.
///
class PWIZ_API_DECL ReaderList : public Reader,
                                 public std::vector<ReaderPtr>
{
    public:

    /// returns child name iff some child identifies, else empty string
	virtual std::string identify(const std::string& filename) const;

    /// returns child name iff some child identifies, else empty string
	virtual std::string identify(const std::string& filename,
                                 const std::string& head) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      IdentData& result,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentData& result,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      IdentDataPtr& result,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentDataPtr& result,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename,
                      std::vector<IdentDataPtr>& results,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config = Config()) const;

    /// appends all of the rhs operand's Readers to the list
    ReaderList& operator +=(const ReaderList& rhs);

    /// appends the rhs Reader to the list
    ReaderList& operator +=(const ReaderPtr& rhs);

    /// returns a concatenated list of all the Readers from the lhs and rhs operands
    ReaderList operator +(const ReaderList& rhs) const;

    /// returns a concatenated list of all the Readers from the lhs and rhs operands
    ReaderList operator +(const ReaderPtr& rhs) const;

    /// returns pointer to Reader of the specified type
    template <typename reader_type>
    reader_type* get()
    {
        for (iterator it=begin(); it!=end(); ++it)
        {
            reader_type* p = dynamic_cast<reader_type*>(it->get());
            if (p) return p;
        }

        return 0;
    }

    /// returns const pointer to Reader of the specified type
    template <typename reader_type>
    const reader_type* get() const
    {
        return const_cast<ReaderList*>(this)->get<reader_type>();
    }

	virtual const char *getType() const {return "ReaderList";} // satisfy inheritance

};


/// returns a list containing the lhs and rhs as readers
PWIZ_API_DECL ReaderList operator +(const ReaderPtr& lhs, const ReaderPtr& rhs);


} // namespace identdata
} // namespace pwiz


#endif // _MZID_READER_HPP_
