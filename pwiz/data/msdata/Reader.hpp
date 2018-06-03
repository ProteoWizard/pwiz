//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _READER_HPP_
#define _READER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "MSData.hpp"
#include <string>
#include <stdexcept>


namespace pwiz {
namespace msdata {

/// interface for file readers
class PWIZ_API_DECL Reader
{
    public:


    /// Reader configuration
    struct PWIZ_API_DECL Config
    {
        /// when true, sets certain vendor readers to produce SIM transitions as spectra instead of chromatograms
        bool simAsSpectra;

        /// when true, sets certain vendor readers to produce SRM transitions as spectra instead of chromatograms
        bool srmAsSpectra;

		/// when true, allows for skipping 0 length checks (and thus skip re-reading data for Sciex)
		bool acceptZeroLengthSpectra;

        /// when true, allows certain vendor readers to produce profile data without zero intensity samples flanking each peak profile
        bool ignoreZeroIntensityPoints;

        /// when true, all drift bins/scans in a frame/block are written in combined form instead of as individual spectra
        bool combineIonMobilitySpectra;

        /// when true, if a reader cannot identify an instrument, an exception will be thrown asking users to report it
        bool unknownInstrumentIsError;

        /// when true, if a reader does not know what time zone was used to record a time, it will assume the time refers to the host's local time;
        /// when false, the reader will treat times with unknown time zone as UTC
        bool adjustUnknownTimeZonesToHostTimeZone;

        /// progress listener for when initializing a file takes a long time,
        /// or the reader has to run a long process before continuing,
        /// such as centroiding all spectra at once instead of one at a time
        pwiz::util::IterationListenerRegistry* iterationListenerRegistry;

        /// when nonzero, if reader can enumerate only spectra of ms level, it will (currently only supported by Bruker TDF)
        int preferOnlyMsLevel;

        Config();
        Config(const Config& rhs);
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
	/// return non-empty if it recognized the format, even though reading
	/// may fail if the 3rd party DLL isn't actually present
    /// Reader may filter based on filename and/or head of the file
    virtual std::string identify(const std::string& filename,
                                 const std::string& head) const = 0;

    /// fill in the MSData structure from the first (or only) sample
    virtual void read(const std::string& filename,
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const = 0;

    /// fill in a vector of MSData structures; provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const = 0;

    /// fill in a vector of MSData.Id values; provides support for multi-run input files
    virtual void readIds(const std::string& filename,
                         const std::string& head,
                         std::vector<std::string>& dataIds,
                         const Config& config = Config()) const;

    /// returns a unique string identifying the reader type
	virtual const char* getType() const = 0;

    virtual ~Reader(){}
};

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
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void readIds(const std::string& filename,
                         std::vector<std::string>& results,
                         const Config& config = Config()) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void readIds(const std::string& filename,
                         const std::string& head,
                         std::vector<std::string>& results,
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

	virtual const char* getType() const {return "ReaderList";} // satisfy inheritance
};


/// returns a list containing the lhs and rhs as readers
PWIZ_API_DECL ReaderList operator +(const ReaderPtr& lhs, const ReaderPtr& rhs);


/// tries to identify a filepath using the provided Reader or ReaderList;
/// returns the CVID file format of the specified filepath,
/// or CVID_Unknown if the file format has no CV term or the filepath doesn't exist
PWIZ_API_DECL CVID identifyFileFormat(const ReaderPtr& reader, const std::string& filepath);


} // namespace msdata
} // namespace pwiz


#endif // _READER_HPP_

