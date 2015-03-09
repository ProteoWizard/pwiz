//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#ifndef _IO_HPP_
#define _IO_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "boost/iostreams/positioning.hpp"


namespace pwiz {
namespace msdata {



/// Identifying information for a spectrum
/// subclassed to add private information for faster file IO in mzML and mzXML
struct PWIZ_API_DECL SpectrumIdentityFromXML : SpectrumIdentity
{
    /// for efficient read of peak lists after previous read of
    /// scan header in mzML and mzXML - avoids reparsing the header
    mutable boost::iostreams::stream_offset sourceFilePositionForBinarySpectrumData;
    SpectrumIdentityFromXML() : SpectrumIdentity(), sourceFilePositionForBinarySpectrumData((boost::iostreams::stream_offset)-1) {}
};

/// Identifying information for a spectrum as read from mzML or mzXML
/// subclassed to add private information for faster file IO in mzXML
struct PWIZ_API_DECL SpectrumIdentityFromMzXML : SpectrumIdentityFromXML
{
    /// for efficient read of peak lists after previous read of
    /// scan header in mzXML - avoids reparsing the header
    mutable unsigned int peaksCount;
    SpectrumIdentityFromMzXML() : SpectrumIdentityFromXML(), peaksCount(0) {}
};


namespace IO {


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv);
PWIZ_API_DECL void read(std::istream& is, CV& cv);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const UserParam& userParam);
PWIZ_API_DECL void read(std::istream& is, UserParam& userParam);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CVParam& cv);
PWIZ_API_DECL void read(std::istream& is, CVParam& cv);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ParamGroup& paramGroup);
PWIZ_API_DECL void read(std::istream& is, ParamGroup& paramGroup);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FileContent& fc);
PWIZ_API_DECL void read(std::istream& is, FileContent& fc);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf);
PWIZ_API_DECL void read(std::istream& is, SourceFile& sf);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Contact& c);
PWIZ_API_DECL void read(std::istream& is, Contact& c);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FileDescription& fd); 
PWIZ_API_DECL void read(std::istream& is, FileDescription& fd); 
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sf);
PWIZ_API_DECL void read(std::istream& is, Sample& sf);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Component& component);
PWIZ_API_DECL void read(std::istream& is, Component& component);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ComponentList& componentList);
PWIZ_API_DECL void read(std::istream& is, ComponentList& componentList);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Software& software);
PWIZ_API_DECL void read(std::istream& is, Software& software);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const InstrumentConfiguration& instrumentConfiguration);
PWIZ_API_DECL void read(std::istream& is, InstrumentConfiguration& instrumentConfiguration);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProcessingMethod& processingMethod);
PWIZ_API_DECL void read(std::istream& is, ProcessingMethod& processingMethod);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataProcessing& dataProcessing);
PWIZ_API_DECL void read(std::istream& is, DataProcessing& dataProcessing);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Target& t);
PWIZ_API_DECL void read(std::istream& is, Target& t);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanSettings& scanSettings);
PWIZ_API_DECL void read(std::istream& is, ScanSettings& scanSettings);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IsolationWindow& isolationWindow);
PWIZ_API_DECL void read(std::istream& is, IsolationWindow& isolationWindow);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SelectedIon& selectedIon);
PWIZ_API_DECL void read(std::istream& is, SelectedIon& selectedIon);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Activation& activation);
PWIZ_API_DECL void read(std::istream& is, Activation& activation);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Precursor& precursor);
PWIZ_API_DECL void read(std::istream& is, Precursor& precursor, const std::map<std::string,std::string>* legacyIdRefToNativeId = 0);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Product& product);
PWIZ_API_DECL void read(std::istream& is, Product& product);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanWindow& selectionWindow);
PWIZ_API_DECL void read(std::istream& is, ScanWindow& selectionWindow);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Scan& scan, const MSData& msd);
PWIZ_API_DECL void read(std::istream& is, Scan& scan);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanList& scanList, const MSData& msd);
PWIZ_API_DECL void read(std::istream& is, ScanList& scanList);
    

PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const BinaryDataArray& binaryDataArray,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config());
PWIZ_API_DECL void read(std::istream& is, BinaryDataArray& binaryDataArray, const MSData* msd = 0);
    
//
// enum for preference in binary data read - ignore, read, read only binary if possible
//
enum PWIZ_API_DECL BinaryDataFlag {IgnoreBinaryData, ReadBinaryData, ReadBinaryDataOnly };


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Spectrum& spectrum, const MSData& msd,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config());
PWIZ_API_DECL
void read(std::istream& is, Spectrum& spectrum, 
          BinaryDataFlag binaryDataFlag = IgnoreBinaryData,
          int version = 0,
          const std::map<std::string,std::string>* legacyIdRefToNativeId = 0,
          const MSData* msd = 0,
          const SpectrumIdentityFromXML *id = 0);


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Chromatogram& chromatogram,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config());
PWIZ_API_DECL
void read(std::istream& is, Chromatogram& chromatogram, 
          BinaryDataFlag binaryDataFlag = IgnoreBinaryData);


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const SpectrumList& spectrumList, const MSData& msd,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, SpectrumListSimple& spectrumListSimple);


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const ChromatogramList& chromatogramList, 
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* chromatogramPositions = 0,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, ChromatogramListSimple& chromatogramListSimple);


enum PWIZ_API_DECL SpectrumListFlag {IgnoreSpectrumList, ReadSpectrumList};


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Run& run, const MSData& msd,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0,
           std::vector<boost::iostreams::stream_offset>* chromatogramPositions = 0,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL
void read(std::istream& is, Run& run,
          SpectrumListFlag spectrumListFlag = IgnoreSpectrumList);


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const MSData& msd,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0,
           std::vector<boost::iostreams::stream_offset>* chromatogramPositions = 0,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL
void read(std::istream& is, MSData& msd,
          SpectrumListFlag spectrumListFlag = IgnoreSpectrumList);


} // namespace IO


} // namespace msdata
} // namespace pwiz


#endif // _IO_HPP_


