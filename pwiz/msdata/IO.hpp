//
// IO.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _IO_HPP_
#define _IO_HPP_


#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"
#include "minimxml/XMLWriter.hpp"
#include "boost/iostreams/positioning.hpp"


namespace pwiz {
namespace msdata {


namespace IO {


void write(minimxml::XMLWriter& writer, const CV& cv);
void read(std::istream& is, CV& cv);
    

void write(minimxml::XMLWriter& writer, const UserParam& userParam);
void read(std::istream& is, UserParam& userParam);
    

void write(minimxml::XMLWriter& writer, const CVParam& cv);
void read(std::istream& is, CVParam& cv);
    

void write(minimxml::XMLWriter& writer, const ParamGroup& paramGroup);
void read(std::istream& is, ParamGroup& paramGroup);
    

void write(minimxml::XMLWriter& writer, const FileContent& fc);
void read(std::istream& is, FileContent& fc);
    

void write(minimxml::XMLWriter& writer, const SourceFile& sf);
void read(std::istream& is, SourceFile& sf);
    

void write(minimxml::XMLWriter& writer, const Contact& c);
void read(std::istream& is, Contact& c);
    

void write(minimxml::XMLWriter& writer, const FileDescription& fd); 
void read(std::istream& is, FileDescription& fd); 
    

void write(minimxml::XMLWriter& writer, const Sample& sf);
void read(std::istream& is, Sample& sf);
    

void write(minimxml::XMLWriter& writer, const Source& source);
void read(std::istream& is, Source& source);
    

void write(minimxml::XMLWriter& writer, const Analyzer& analyzer);
void read(std::istream& is, Analyzer& analyzer);
    

void write(minimxml::XMLWriter& writer, const Detector& detector);
void read(std::istream& is, Detector& detector);
    

void write(minimxml::XMLWriter& writer, const ComponentList& componentList);
void read(std::istream& is, ComponentList& componentList);
    

void write(minimxml::XMLWriter& writer, const Software& software);
void read(std::istream& is, Software& software);
    

void write(minimxml::XMLWriter& writer, const Instrument& instrument);
void read(std::istream& is, Instrument& instrument);
    

void write(minimxml::XMLWriter& writer, const ProcessingMethod& processingMethod);
void read(std::istream& is, ProcessingMethod& processingMethod);
    

void write(minimxml::XMLWriter& writer, const DataProcessing& dataProcessing);
void read(std::istream& is, DataProcessing& dataProcessing);
    

void write(minimxml::XMLWriter& writer, const Acquisition& acquisition);
void read(std::istream& is, Acquisition& acquisition);
    

void write(minimxml::XMLWriter& writer, const AcquisitionList& acquisitionList);
void read(std::istream& is, AcquisitionList& acquisitionList);
    

void write(minimxml::XMLWriter& writer, const IonSelection& ionSelection);
void read(std::istream& is, IonSelection& ionSelection);
    

void write(minimxml::XMLWriter& writer, const Activation& activation);
void read(std::istream& is, Activation& activation);
    

void write(minimxml::XMLWriter& writer, const Precursor& precursor);
void read(std::istream& is, Precursor& precursor);
    

void write(minimxml::XMLWriter& writer, const SelectionWindow& selectionWindow);
void read(std::istream& is, SelectionWindow& selectionWindow);
    

void write(minimxml::XMLWriter& writer, const Scan& scan);
void read(std::istream& is, Scan& scan);
    

void write(minimxml::XMLWriter& writer, const SpectrumDescription& spectrumDescription);
void read(std::istream& is, SpectrumDescription& spectrumDescription);
    

void write(minimxml::XMLWriter& writer, const BinaryDataArray& binaryDataArray,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config());
void read(std::istream& is, BinaryDataArray& binaryDataArray);
    

enum BinaryDataFlag {IgnoreBinaryData, ReadBinaryData};


void write(minimxml::XMLWriter& writer, const Spectrum& spectrum,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config());
void read(std::istream& is, Spectrum& spectrum, 
          BinaryDataFlag binaryDataFlag = IgnoreBinaryData);


void write(minimxml::XMLWriter& writer, const SpectrumList& spectrumList, 
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0);
void read(std::istream& is, SpectrumListSimple& spectrumListSimple);


enum SpectrumListFlag {IgnoreSpectrumList, ReadSpectrumList};


void write(minimxml::XMLWriter& writer, const Run& run,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0);
void read(std::istream& is, Run& run,
          SpectrumListFlag spectrumListFlag = IgnoreSpectrumList);


void write(minimxml::XMLWriter& writer, const MSData& msd,
           const BinaryDataEncoder::Config& config = BinaryDataEncoder::Config(),
           std::vector<boost::iostreams::stream_offset>* spectrumPositions = 0);
void read(std::istream& is, MSData& msd,
          SpectrumListFlag spectrumListFlag = IgnoreSpectrumList);


} // namespace IO


} // namespace msdata
} // namespace pwiz


#endif // _IO_HPP_


