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

#include "Embedder.hpp"
#include "../Embedder.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace IDPicker {


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::chemistry;
namespace NativeEmbedder = NativeIDPicker::Embedder;


String^ Embedder::DefaultSourceExtensionPriorityList::get() { return ToSystemString(NativeEmbedder::defaultSourceExtensionPriorityList); }


namespace {

NativeEmbedder::QuantitationConfiguration makeNativeQuantitationConfiguration(Embedder::QuantitationConfiguration^ managedQuantitationConfig)
{
    return NativeEmbedder::QuantitationConfiguration(NativeIDPicker::QuantitationMethod::get_by_value((int) managedQuantitationConfig->QuantitationMethod).get(),
                                                     pwiz::chemistry::MZTolerance(managedQuantitationConfig->ReporterIonMzTolerance->value,
                                                                                  (pwiz::chemistry::MZTolerance::Units) managedQuantitationConfig->ReporterIonMzTolerance->units));
}

} // namespace


Embedder::QuantitationConfiguration::QuantitationConfiguration()
{
    NativeEmbedder::QuantitationConfiguration nativeConfig;
    this->QuantitationMethod = (IDPicker::QuantitationMethod) nativeConfig.quantitationMethod.value();
    ReporterIonMzTolerance = gcnew MZTolerance(nativeConfig.reporterIonMzTolerance.value, (MZTolerance::Units) nativeConfig.reporterIonMzTolerance.units);
}


void Embedder::Embed(String^ idpDbFilepath,
                     String^ sourceSearchPath,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embed(ToStdString(idpDbFilepath),
                              ToStdString(sourceSearchPath),
                              nativeQuantitationMethodBySource,
                              ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD
}

void Embedder::Embed(String^ idpDbFilepath,
                     String^ sourceSearchPath,
                     String^ sourceExtensionPriorityList,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embed(ToStdString(idpDbFilepath),
                              ToStdString(sourceSearchPath),
                              ToStdString(sourceExtensionPriorityList),
                              nativeQuantitationMethodBySource,
                              ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD
}


void Embedder::EmbedScanTime(String^ idpDbFilepath,
                             String^ sourceSearchPath,
                             IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                             pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embedScanTime(ToStdString(idpDbFilepath),
                                      ToStdString(sourceSearchPath),
                                      nativeQuantitationMethodBySource,
                                      ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD
}

void Embedder::EmbedScanTime(String^ idpDbFilepath,
                             String^ sourceSearchPath,
                             String^ sourceExtensionPriorityList,
                             IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                             pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embedScanTime(ToStdString(idpDbFilepath),
                                      ToStdString(sourceSearchPath),
                                      ToStdString(sourceExtensionPriorityList),
                                      nativeQuantitationMethodBySource,
                                      ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD
}


bool Embedder::HasGeneMetadata(String^ idpDbFilepath)
{
    try
    {
        return NativeEmbedder::hasGeneMetadata(ToStdString(idpDbFilepath));
    }
    CATCH_AND_FORWARD
}


void Embedder::EmbedGeneMetadata(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        NativeEmbedder::embedGeneMetadata(ToStdString(idpDbFilepath),
                                          ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD
}


void Embedder::Extract(String^ idpDbFilepath, String^ sourceName, String^ outputFilepath)
{
    try
    {
        NativeEmbedder::extract(ToStdString(idpDbFilepath), ToStdString(sourceName), ToStdString(outputFilepath));
    }
    CATCH_AND_FORWARD
}


} // namespace IDPicker
