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
#include "Logger.hpp"

#pragma unmanaged
#include "../Embedder.hpp"
#include "../XIC.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"
#pragma managed

namespace IDPicker {


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::chemistry;
using namespace System::Text::RegularExpressions;
using namespace System::IO;
namespace NativeEmbedder = NativeIDPicker::Embedder;
namespace NativeXIC = NativeIDPicker::XIC;


String^ Embedder::DefaultSourceExtensionPriorityList::get() { return ToSystemString(NativeEmbedder::defaultSourceExtensionPriorityList()); }


namespace {

NativeEmbedder::QuantitationConfiguration makeNativeQuantitationConfiguration(Embedder::QuantitationConfiguration^ managedQuantitationConfig)
{
    return NativeEmbedder::QuantitationConfiguration(NativeIDPicker::QuantitationMethod::get_by_value((int) managedQuantitationConfig->QuantitationMethod).get(),
                                                     pwiz::chemistry::MZTolerance(managedQuantitationConfig->ReporterIonMzTolerance->value,
                                                                                  (pwiz::chemistry::MZTolerance::Units) managedQuantitationConfig->ReporterIonMzTolerance->units),
                                                     managedQuantitationConfig->NormalizeIntensities);
}

NativeXIC::XICConfiguration makeNativeXICConfiguration(Embedder::XICConfiguration^ managedXICConfig)
{
    return NativeXIC::XICConfiguration(managedXICConfig->AlignRetentionTime, ToStdString(managedXICConfig->RTFolder), managedXICConfig->MaxQValue,
                     IntegerSet(managedXICConfig->MonoisotopicAdjustmentMin, managedXICConfig->MonoisotopicAdjustmentMax),
                     (size_t)managedXICConfig->RetentionTimeLowerTolerance, (size_t)managedXICConfig->RetentionTimeUpperTolerance,
                     pwiz::chemistry::MZTolerance(managedXICConfig->ChromatogramMzLowerOffset->value,
                                                 (pwiz::chemistry::MZTolerance::Units) managedXICConfig->ChromatogramMzLowerOffset->units),
                     pwiz::chemistry::MZTolerance(managedXICConfig->ChromatogramMzUpperOffset->value,
                                                 (pwiz::chemistry::MZTolerance::Units) managedXICConfig->ChromatogramMzUpperOffset->units));
}

} // namespace


Embedder::QuantitationConfiguration::QuantitationConfiguration()
{
    NativeEmbedder::QuantitationConfiguration nativeConfig;
    this->QuantitationMethod = (IDPicker::QuantitationMethod) nativeConfig.quantitationMethod.value();
    ReporterIonMzTolerance = gcnew MZTolerance(nativeConfig.reporterIonMzTolerance.value, (MZTolerance::Units) nativeConfig.reporterIonMzTolerance.units);
    NormalizeIntensities = nativeConfig.normalizeIntensities;
}

Embedder::QuantitationConfiguration::QuantitationConfiguration(IDPicker::QuantitationMethod quantitationMethod, String^ settingsString)
{
    String^ pattern = "([\\d\\.]+)(\\w+) ; (\\d)";
    Regex^ rx = gcnew Regex(pattern);
    Match^ match = rx->Match(settingsString);
    if (match->Success)
    {
        MZTolerance::Units units = match->Groups[2]->Value->ToLower() == "mz" ? MZTolerance::Units::MZ : MZTolerance::Units::PPM;
        ReporterIonMzTolerance = gcnew MZTolerance(Double::Parse(match->Groups[1]->Value), units);
        NormalizeIntensities = (match->Groups[3]->Value == "1");
    }
    else
    {
        NativeEmbedder::QuantitationConfiguration nativeConfig;
        ReporterIonMzTolerance = gcnew MZTolerance(nativeConfig.reporterIonMzTolerance.value, (MZTolerance::Units) nativeConfig.reporterIonMzTolerance.units);
        NormalizeIntensities = nativeConfig.normalizeIntensities;
    }
    this->QuantitationMethod = quantitationMethod;
}

String^ Embedder::QuantitationConfiguration::ToString()
{
    return ReporterIonMzTolerance->value.ToString() + ReporterIonMzTolerance->units.ToString()->ToLower() + " ; " + (NormalizeIntensities ? "1" : "0");
}


Embedder::XICConfiguration::XICConfiguration()
{
    NativeXIC::XICConfiguration nativeConfig;

    // TODO: fix IntegerSet to make it easier to get the min and max
    IntegerSet::const_iterator itr = nativeConfig.MonoisotopicAdjustmentSet.begin();
    MonoisotopicAdjustmentMin = *itr;
    for (; itr != nativeConfig.MonoisotopicAdjustmentSet.end(); ++itr)
        MonoisotopicAdjustmentMax = *itr;

    RetentionTimeLowerTolerance = nativeConfig.RetentionTimeLowerTolerance;
    RetentionTimeUpperTolerance = nativeConfig.RetentionTimeUpperTolerance;
    MaxQValue = nativeConfig.MaxQValue;
    ChromatogramMzLowerOffset = gcnew MZTolerance(nativeConfig.ChromatogramMzLowerOffset.value, (MZTolerance::Units) nativeConfig.ChromatogramMzLowerOffset.units);
    ChromatogramMzUpperOffset = gcnew MZTolerance(nativeConfig.ChromatogramMzUpperOffset.value, (MZTolerance::Units) nativeConfig.ChromatogramMzUpperOffset.units);
    AlignRetentionTime = false;
    RTFolder = "";
}

Embedder::XICConfiguration::XICConfiguration(String^ representitiveString)
{
    NativeXIC::XICConfiguration nativeConfig;
    String^ pattern = "\\[(-?\\d+),(-?\\d+)\\] ; \\[(-?\\d+),(-?\\d+)\\] ; \\[(-?[\\d\\.]+)(\\w+),(-?[\\d\\.]+)(\\w+)\\]";
    Regex^ rx = gcnew Regex( pattern );
    Match^ match = rx->Match( representitiveString );
    if(match->Success) 
    {
        MonoisotopicAdjustmentMin = Int32::Parse(match->Groups[1]->Value);
        MonoisotopicAdjustmentMax = Int32::Parse(match->Groups[2]->Value);
        RetentionTimeLowerTolerance = -Int32::Parse(match->Groups[3]->Value);
        RetentionTimeUpperTolerance = Int32::Parse(match->Groups[4]->Value);
        double ChromatogramMzLowerOffsetValue = -Double::Parse(match->Groups[5]->Value);
        double ChromatogramMzUpperOffsetValue = Double::Parse(match->Groups[7]->Value);
        
        if (match->Groups[6]->Value->ToLower() == "mz")
            ChromatogramMzLowerOffset = gcnew MZTolerance(ChromatogramMzLowerOffsetValue, MZTolerance::Units::MZ);
        else
            ChromatogramMzLowerOffset = gcnew MZTolerance(ChromatogramMzLowerOffsetValue, MZTolerance::Units::PPM);
            
        if (match->Groups[8]->Value->ToLower() == "mz")
            ChromatogramMzUpperOffset = gcnew MZTolerance(ChromatogramMzUpperOffsetValue, MZTolerance::Units::MZ);
        else
            ChromatogramMzUpperOffset = gcnew MZTolerance(ChromatogramMzUpperOffsetValue, MZTolerance::Units::PPM);
            
        pattern = "\\[(-?\\d+),(-?\\d+)\\] ; \\[(-?\\d+),(-?\\d+)\\] ; \\[(-?[\\d\\.]+)(\\w+),(-?[\\d\\.]+)(\\w+)\\] ; ([\\d.]+) ; (\\d?)";
        rx = gcnew Regex( pattern );
        match = rx->Match( representitiveString );
        if (match->Success)
        {
            MaxQValue = Double::Parse(match->Groups[9]->Value);
            if (match->Groups[10]->Value == "1")
                AlignRetentionTime = true;
            else
                AlignRetentionTime = false;
        }
    }
    else
    {
        // TODO: fix IntegerSet to make it easier to get the min and max
        IntegerSet::const_iterator itr = nativeConfig.MonoisotopicAdjustmentSet.begin();
        MonoisotopicAdjustmentMin = *itr;
        for (; itr != nativeConfig.MonoisotopicAdjustmentSet.end(); ++itr)
            MonoisotopicAdjustmentMax = *itr;

        RetentionTimeLowerTolerance = nativeConfig.RetentionTimeLowerTolerance;
        RetentionTimeUpperTolerance = nativeConfig.RetentionTimeUpperTolerance;
        ChromatogramMzLowerOffset = gcnew MZTolerance(nativeConfig.ChromatogramMzLowerOffset.value, (MZTolerance::Units) nativeConfig.ChromatogramMzLowerOffset.units);
        ChromatogramMzUpperOffset = gcnew MZTolerance(nativeConfig.ChromatogramMzUpperOffset.value, (MZTolerance::Units) nativeConfig.ChromatogramMzUpperOffset.units);
        MaxQValue = nativeConfig.MaxQValue;
        AlignRetentionTime = nativeConfig.AlignRetentionTime;
    }
}

String^ Embedder::XICConfiguration::ToString()
{
    String^ align = "0";
    if (AlignRetentionTime == true)
        align = "1";
    return String::Format("[{0},{1}] ; [{2},{3}] ; [{4}{5},{6}{7}] ; {8} ; {9}",
                          MonoisotopicAdjustmentMin, MonoisotopicAdjustmentMax,
                          RetentionTimeLowerTolerance, RetentionTimeUpperTolerance,
                          ChromatogramMzLowerOffset->value, ChromatogramMzLowerOffset->units.ToString()->ToLower(),
                          ChromatogramMzUpperOffset->value, ChromatogramMzUpperOffset->units.ToString()->ToLower(),
                          MaxQValue, align);
}

void Embedder::Embed(String^ idpDbFilepath,
                     String^ sourceSearchPath,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embed(ToStdString(idpDbFilepath),
                              ToStdString(sourceSearchPath),
                              nativeQuantitationMethodBySource,
                              ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}

void Embedder::Embed(String^ idpDbFilepath,
                     String^ sourceSearchPath,
                     String^ sourceExtensionPriorityList,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

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
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


void Embedder::EmbedScanTime(String^ idpDbFilepath,
                             String^ sourceSearchPath,
                             IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                             pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        NativeEmbedder::embedScanTime(ToStdString(idpDbFilepath),
                                      ToStdString(sourceSearchPath),
                                      nativeQuantitationMethodBySource,
                                      ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}

void Embedder::EmbedScanTime(String^ idpDbFilepath,
                             String^ sourceSearchPath,
                             String^ sourceExtensionPriorityList,
                             IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                             pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

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
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


void Embedder::EmbedIsobaricSampleMapping(String^ idpDbFilepath, IDictionary<String^, IList<String^>^>^ isobaricSampleMap)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        map<string, vector<string> > nativeIsobaricSampleMap;
        for each (KeyValuePair<String^, IList<String^>^>^ kvp in isobaricSampleMap)
        {
            vector<string>& sampleNames = nativeIsobaricSampleMap[ToStdString(kvp->Key)];
            for each (String^ s in kvp->Value)
                sampleNames.push_back(ToStdString(s));
        }
        NativeEmbedder::embedIsobaricSampleMapping(ToStdString(idpDbFilepath), nativeIsobaricSampleMap);
    }
    CATCH_AND_FORWARD;
}

IDictionary<String^, IList<String^>^>^ Embedder::GetIsobaricSampleMapping(String^ idpDbFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        map<string, vector<string> > nativeIsobaricSampleMap = NativeEmbedder::getIsobaricSampleMapping(ToStdString(idpDbFilepath));

        IDictionary<String^, IList<String^>^>^ isobaricSampleMap = gcnew Dictionary<String^, IList<String^>^>();
        for (const auto& kvp : nativeIsobaricSampleMap)
        {
            IList<String^>^ sampleNames = gcnew List<String^>();
            isobaricSampleMap[ToSystemString(kvp.first)] = sampleNames;
            for each (const string& s in kvp.second)
                sampleNames->Add(ToSystemString(s));
        }
        return isobaricSampleMap;
    }
    CATCH_AND_FORWARD;
    return nullptr;
}


bool Embedder::HasGeneMetadata(String^ idpDbFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        return NativeEmbedder::hasGeneMetadata(ToStdString(idpDbFilepath));
    }
    CATCH_AND_FORWARD;
    return false;
}


void Embedder::EmbedGeneMetadata(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        String^ currentDirectory = Directory::GetCurrentDirectory();
        Directory::SetCurrentDirectory(Path::GetDirectoryName(Environment::GetCommandLineArgs()[0]));
        NativeEmbedder::embedGeneMetadata(ToStdString(idpDbFilepath),
                                          ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        Directory::SetCurrentDirectory(currentDirectory);
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


void Embedder::DropGeneMetadata(String^ idpDbFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        return NativeEmbedder::dropGeneMetadata(ToStdString(idpDbFilepath));
    }
    CATCH_AND_FORWARD;
}


void Embedder::Extract(String^ idpDbFilepath, String^ sourceName, String^ outputFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        NativeEmbedder::extract(ToStdString(idpDbFilepath), ToStdString(sourceName), ToStdString(outputFilepath));
    }
    CATCH_AND_FORWARD
}


void Embedder::EmbedMS1Metrics(String^ idpDbFilepath,
                     String^ sourceSearchPath,
                     String^ sourceExtensionPriorityList,
                     IDictionary<int, QuantitationConfiguration^>^ quantitationMethodBySource,
                     IDictionary<int, XICConfiguration^>^ xicConfigurationBySource,
                     pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        map<int, NativeIDPicker::Embedder::QuantitationConfiguration> nativeQuantitationMethodBySource;
        map<int, NativeXIC::XICConfiguration> nativeXICConfigurationBySource;
        for each (KeyValuePair<int, QuantitationConfiguration^>^ kvp in quantitationMethodBySource)
            nativeQuantitationMethodBySource[(int) kvp->Key] = makeNativeQuantitationConfiguration(kvp->Value);
        for each (KeyValuePair<int, XICConfiguration^>^ kvp in xicConfigurationBySource)
            nativeXICConfigurationBySource[(int) kvp->Key] = makeNativeXICConfiguration(kvp->Value);
        NativeEmbedder::EmbedMS1Metrics(ToStdString(idpDbFilepath),
                                        ToStdString(sourceSearchPath),
                                        ToStdString(sourceExtensionPriorityList),
                                        nativeQuantitationMethodBySource,
                                        nativeXICConfigurationBySource,
                                        ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}



} // namespace IDPicker
