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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#ifndef _EMBEDDER_HPP_
#define _EMBEDDER_HPP_


#include <string>
#include <vector>
#include <map>
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <boost/date_time.hpp>
#include <boost/shared_ptr.hpp>
#include <boost/enum.hpp>
#include "sqlite3pp.h"
#include "XIC.hpp"


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE

BOOST_ENUM(QuantitationMethod,
    (None)
    (LabelFree)
    (ITRAQ4plex)
    (ITRAQ8plex)
    (TMT2plex)
    (TMT6plex)
    (TMT10plex)
);

// allow enum values to be the LHS of an equality expression
BOOST_ENUM_DOMAIN_OPERATORS(QuantitationMethod);


namespace Embedder {


using std::string;
using std::vector;
using std::map;
using std::pair;
using pwiz::chemistry::MZTolerance;
namespace sqlite = sqlite3pp;


struct QuantitationConfiguration
{
    QuantitationConfiguration(QuantitationMethod quantitationMethod = QuantitationMethod::None,
                              MZTolerance reporterIonMzTolerance = MZTolerance(0.015, MZTolerance::MZ),
                              bool normalizeIntensities = true);

    QuantitationMethod quantitationMethod;
    MZTolerance reporterIonMzTolerance;
    bool normalizeIntensities;

    operator std::string() const;
};


/// the default source extensions to search for, ordered by descending priority
string defaultSourceExtensionPriorityList();

/// search for source files of the idpDB using the given search path, using the default source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const map<int, QuantitationConfiguration>& quantitationMethodBySource = map<int, QuantitationConfiguration>(),
           pwiz::util::IterationListenerRegistry* ilr = 0);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed a MZ5 representation of the source's spectra in the MSDataBytes column of the idpDB
void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const string& sourceExtensionPriorityList,
           const map<int, QuantitationConfiguration>& quantitationMethodBySource = map<int, QuantitationConfiguration>(),
           pwiz::util::IterationListenerRegistry* ilr = 0);

/// search for source files of the idpDB using the given search path, using the default source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const map<int, QuantitationConfiguration>& quantitationMethodBySource = map<int, QuantitationConfiguration>(),
                   pwiz::util::IterationListenerRegistry* ilr = 0);

/// search for source files of the idpDB using the given search path, using the provided source extensions,
/// and embed scan start times of the source's spectra in the ScanTimeInSeconds column of the idpDB
void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const string& sourceExtensionPriorityList,
                   const map<int, QuantitationConfiguration>& quantitationMethodBySource = map<int, QuantitationConfiguration>(),
                   pwiz::util::IterationListenerRegistry* ilr = 0);

/// adds a mapping of source group to sample names; the sample names are in ascending order of isobaric quantitation channel reporter ion mass
void embedIsobaricSampleMapping(const string& idpDbFilepath, const map<string, vector<string> >& isobaricSampleMap);

/// retrieves the mapping of source group to sample names; the sample names are in ascending order of isobaric quantitation channel reporter ion mass
map<string, vector<string> > getIsobaricSampleMapping(const string& idpDbFilepath);

/// checks whether the given idpDB has embedded gene metadata (although it may not necessarily be the most up-to-date)
bool hasGeneMetadata(const string& idpDbFilepath);

/// checks whether the given idpDB connection has embedded gene metadata (although it may not necessarily be the most up-to-date)
bool hasGeneMetadata(sqlite3* idpDbConnection);

/// embed gene metadata in the idpDB: i.e. gene symbol, name, family, taxonomy, and chromosome location
void embedGeneMetadata(const string& idpDbFilepath, pwiz::util::IterationListenerRegistry* ilr = 0);

/// remove gene metadata, if any is present, from the idpDB: i.e. gene symbol, name, family, taxonomy, and chromosome location
void dropGeneMetadata(const string& idpDbFilepath);

/// extract the MSDataBytes of the given source from the idpDB to the specified output filepath
void extract(const string& idpDbFilepath, const string& sourceName, const string& outputFilepath);

/// embed ms1 info from all sources
void EmbedMS1Metrics(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const string& sourceExtensionPriorityList,
           const map<int, QuantitationConfiguration>& quantitationMethodBySource,
           const map<int, XIC::XICConfiguration>& xicConfigBySource,
           pwiz::util::IterationListenerRegistry* ilr);
} // namespace Embedder
END_IDPICKER_NAMESPACE


#endif // _EMBEDDER_HPP_

