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
    TMT6plex
};


public ref struct Embedder abstract
{

/// the default source extensions to search for, ordered by descending priority
static property String^ DefaultSourceExtensionPriorityList { String^ get(); }

/// search for source files of the idpDB using the given search path, using the default source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
static void Embed(String^ idpDbFilepath,
                  String^ sourceSearchPath,
                  IDictionary<int, QuantitationMethod>^ quantitationMethodBySource,
                  pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
static void Embed(String^ idpDbFilepath,
                  String^ sourceSearchPath,
                  String^ sourceExtensionPriorityList,
                  IDictionary<int, QuantitationMethod>^ quantitationMethodBySource,
                  pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
static void EmbedScanTime(String^ idpDbFilepath,
                          String^ sourceSearchPath,
                          IDictionary<int, QuantitationMethod>^ quantitationMethodBySource,
                          pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
static void EmbedScanTime(String^ idpDbFilepath,
                          String^ sourceSearchPath,
                          String^ sourceExtensionPriorityList,
                          IDictionary<int, QuantitationMethod>^ quantitationMethodBySource,
                          pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// embed gene metadata in the idpDB: i.e. gene symbol, name, family, taxonomy, and chromosome location
static void EmbedGeneMetadata(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr);

/// extract the MSDataBytes of the given source from the idpDB to the specified output filepath
static void Extract(String^ idpDbFilepath, String^ sourceName, String^ outputFilepath);

};


} // namespace IDPicker
