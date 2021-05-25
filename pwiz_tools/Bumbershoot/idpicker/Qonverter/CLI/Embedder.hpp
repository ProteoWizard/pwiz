//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "Qonverter.hpp"
#using <system.dll>
#pragma warning( pop )


namespace IDPicker {


using namespace System;
using namespace System::Collections::Generic;


public enum class QuantitationMethod
{
    None = 0,
    LabelFree,
    ITRAQ4plex,
    ITRAQ8plex,
    TMT2plex,
    TMT6plex,
    TMT10plex,
    TMT11plex,
    TMTpro16plex
};


public ref struct Embedder abstract
{

static property int MAX_ITRAQ_REPORTER_IONS { int get(); }
static property int MAX_TMT_REPORTER_IONS { int get(); }

ref struct QuantitationConfiguration
{
    QuantitationConfiguration(/*QuantitationMethod::None, MZTolerance(10, MZTolerance::PPM)*/);
    QuantitationConfiguration(QuantitationMethod quantitationMethod, String^ settingsString);
    virtual String^ ToString() override;

    property QuantitationMethod QuantitationMethod;
    property pwiz::CLI::chemistry::MZTolerance^ ReporterIonMzTolerance;
    property bool NormalizeIntensities;
};

ref struct XICConfiguration
{
    XICConfiguration();
    XICConfiguration(String^ representitiveString);
    virtual String^ ToString() override;

    property bool AlignRetentionTime;
    property String^ RTFolder;
    property double MaxQValue;
    property int MonoisotopicAdjustmentMin;
    property int MonoisotopicAdjustmentMax;
    property int RetentionTimeLowerTolerance;
    property int RetentionTimeUpperTolerance;
    property pwiz::CLI::chemistry::MZTolerance^ ChromatogramMzLowerOffset;
    property pwiz::CLI::chemistry::MZTolerance^ ChromatogramMzUpperOffset;
};


/// the default source extensions to search for, ordered by descending priority
static property String^ DefaultSourceExtensionPriorityList { String^ get(); }

/// search for source files of the idpDB using the given search path, using the default source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
static void Embed(String^ idpDbFilepath,
                  String^ sourceSearchPath,
                  IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                  pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
static void Embed(String^ idpDbFilepath,
                  String^ sourceSearchPath,
                  String^ sourceExtensionPriorityList,
                  IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                  pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
static void EmbedScanTime(String^ idpDbFilepath,
                          String^ sourceSearchPath,
                          IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                          pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
static void EmbedScanTime(String^ idpDbFilepath,
                          String^ sourceSearchPath,
                          String^ sourceExtensionPriorityList,
                          IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                          pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// adds a mapping of source group to sample names; the sample names are in ascending order of isobaric quantitation channel reporter ion mass
static void EmbedIsobaricSampleMapping(String^ idpDbFilepath, IDictionary<String^, IList<String^>^>^ isobaricSampleMap);

/// retrieves the mapping of source group to sample names; the sample names are in ascending order of isobaric quantitation channel reporter ion mass
static IDictionary<String^, IList<String^>^>^ GetIsobaricSampleMapping(String^ idpDbFilepath);

/// checks whether the given idpDB has embedded gene metadata (although it may not necessarily be the most up-to-date)
static bool HasGeneMetadata(String^ idpDbFilepath);

/// embed gene metadata in the idpDB: i.e. gene symbol, name, family, taxonomy, and chromosome location
static void EmbedGeneMetadata(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// remove gene metadata, if any is present, from the idpDB: i.e. gene symbol, name, family, taxonomy, and chromosome location
static void DropGeneMetadata(String^ idpDbFilepath);

/// extract the MSDataBytes of the given source from the idpDB to the specified output filepath
static void Extract(String^ idpDbFilepath, String^ sourceName, String^ outputFilepath);


/// search for source files of the idpDB using the given search path, using the default source extensions,
/// and embed XIC metrics in a new table called PeptideQuantitation within the idpDB
static void EmbedMS1Metrics(String^ idpDbFilepath,
                    String^ sourceSearchPath,
                     String^ sourceExtensionPriorityList,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     IDictionary<int, XICConfiguration^>^ xicConfigurationBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr);

};


} // namespace IDPicker
