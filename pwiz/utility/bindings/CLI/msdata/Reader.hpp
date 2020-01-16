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
#include "MSData.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {

using namespace System::Collections::Generic;

public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MSDataList, pwiz::msdata::MSDataPtr, MSData, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// a window specifying an m/z and optionally a range of ion mobility values
/// </summary>
public ref class MzMobilityWindow
{
    public:

    MzMobilityWindow(double mz) : mz(gcnew System::Nullable<double>(mz)) {}
    MzMobilityWindow(double mz, System::Tuple<double, double>^ mobilityBounds) : mz(gcnew System::Nullable<double>(mz)), mobilityBounds(mobilityBounds) {}
    MzMobilityWindow(System::Tuple<double, double>^ mobilityBounds) : mobilityBounds(mobilityBounds) {}
    MzMobilityWindow(double mz, double mobility, double mobilityTolerance) : MzMobilityWindow(mz, gcnew System::Tuple<double, double>(mobility - mobilityTolerance, mobility + mobilityTolerance)) {}
    MzMobilityWindow(double mobility, double mobilityTolerance) : MzMobilityWindow(gcnew System::Tuple<double, double>(mobility - mobilityTolerance, mobility + mobilityTolerance)) {}

    System::Nullable<double>^ mz;
    System::Tuple<double, double>^ mobilityBounds;
};


/// <summary>
/// configuration struct for readers
/// </summary>
public ref class ReaderConfig 
{
    public:

    /// return Selected Ion Monitoring as spectra
    bool simAsSpectra;

    /// return Selected Reaction Monitoring as spectra
    bool srmAsSpectra;

	/// when true, allows for skipping 0 length checks (and thus skip re-reading data for ABI)
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

    /// when nonzero, if reader can enumerate only spectra of ms level, it will (currently only supported by Bruker TDF)
    int preferOnlyMsLevel;

    /// when true, MS2 spectra without precursor/isolation information will be included in the output (currently only affects Bruker PASEF data)
    bool allowMsMsWithoutPrecursor;


    /// temporary(?) variable to avoid needing to regenerate Bruker test data
    bool sortAndJitter;

    /// when non-empty, only scans from precursors matching one of the included m/z and/or mobility windows will be enumerated; MS1 scans are affected only by the mobility filter
    System::Collections::Generic::IList<MzMobilityWindow^>^ isolationMzAndMobilityFilter;

    /// when true, global TIC and BPC chromatograms consist of only MS1 spectra (thus the number of time points cannot be assumed to be equal to the number of spectra)
    bool globalChromatogramsAreMs1Only;

    ReaderConfig()
    : simAsSpectra(false)
    , srmAsSpectra(false)
    , acceptZeroLengthSpectra(false)
    , ignoreZeroIntensityPoints(false)
    , combineIonMobilitySpectra(false)
    , unknownInstrumentIsError(false)
    , adjustUnknownTimeZonesToHostTimeZone(true)
    , preferOnlyMsLevel(0)
    , allowMsMsWithoutPrecursor(true)
    , sortAndJitter(false)
    , globalChromatogramsAreMs1Only(false)
    {
    }
};

/// interface for file readers
public ref class Reader
{
    DEFINE_INTERNAL_BASE_CODE(Reader, pwiz::msdata::Reader);

    public:

    /// return true iff Reader can handle the file;
    /// Reader may filter based on filename and/or head of the file
    virtual bool accept(System::String^ filename,
                        System::String^ head);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result,
                      int runIndex);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSDataList^ results);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result,
                      ReaderConfig^ config);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result,
                      int runIndex,
                      ReaderConfig^ config);

    /// fill in the MSData structure
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSDataList^ results,
                      ReaderConfig^ config);

    /// fill in the MSData structure
    virtual array<System::String^>^ readIds(System::String^ filename,
                                            System::String^ head);
};


/// Reader container (composite pattern).
///
/// The template get&lt;reader_type>() gives access to child Readers by type, to facilitate
/// Reader-specific configuration at runtime.
///
public ref class ReaderList : public Reader
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ReaderList, Reader);

    public:

    /// returns child name iff some child identifies, else empty string
	virtual System::String^ identify(System::String^ filename);

    /// returns child name iff some child identifies, else empty string
    virtual System::String^ identify(System::String^ filename,
                                     System::String^ head);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      MSData^ result);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      MSData^ result,
                      int runIndex);

    virtual void read(System::String^ filename,
                      MSData^ result,
                      int runIndex,
                      ReaderConfig^ config);

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result) override;

    /// delegates to first child that accepts
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSData^ result,
                      int runIndex) override;

    /// fill in the MSDataList with MSData for all samples
    virtual void read(System::String^ filename,
                      MSDataList^ results);

    virtual void read(System::String^ filename,
                      MSDataList^ results,
                      ReaderConfig^ config);

    /// fill in the MSDataList with MSData for all samples
    virtual void read(System::String^ filename,
                      System::String^ head,
                      MSDataList^ results) override;

    /// get MSData.Ids
    virtual array<System::String^>^ readIds(System::String^ filename);

    /// get MSData.Ids
    virtual array<System::String^>^ readIds(System::String^ filename,
                                            System::String^ head) override;

    /// returns getType() for all contained Readers
    IList<System::String^>^ getTypes();

    /// returns getCvType() for all contained Readers
    IList<CVID>^ getCvTypes();

    /// returns the file extensions, if any, that the contained Readers support, including the leading period;
    /// note that comparing file extensions is not as robust as using the identify() method
    virtual IList<System::String^>^ getFileExtensions();

    /// returns a map of Reader types to file extensions, if any, that the contained Readers support, including the leading period;
    /// note that comparing file extensions is not as robust as using the identify() method
    IDictionary<System::String^, IList<System::String^>^>^ getFileExtensionsByType();

    static property ReaderList^ FullReaderList { ReaderList^ get(); }
};


} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _READER_HPP_CLI_
