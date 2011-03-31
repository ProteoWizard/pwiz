//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//


#include "Parser.hpp"
#include "../Parser.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/foreach_field.hpp"


namespace IDPicker {


using namespace System::Runtime::InteropServices;
using namespace pwiz::CLI::util;
typedef NativeIDPicker::Qonverter NativeQonverter;
typedef NativeIDPicker::Parser NativeParser;


struct ImportSettingsForwarder : public NativeParser::ImportSettingsCallback
{
    typedef void (__stdcall *ImportSettingsCallback)(Object^ sender, Parser::ImportSettingsEventArgs^ e);
    ImportSettingsCallback managedFunctionPtr;

    ImportSettingsForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<ImportSettingsCallback>(managedFunctionPtr))
    {}

    virtual void operator() (const vector<NativeParser::ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const
    {
        if (managedFunctionPtr != NULL)
        {
            Parser::ImportSettingsEventArgs^ args = gcnew Parser::ImportSettingsEventArgs();
            args->DistinctAnalyses = gcnew List<Parser::Analysis^>();

            BOOST_FOREACH(const NativeParser::ConstAnalysisPtr& nativeAnalysis, distinctAnalyses)
            {
                Parser::Analysis^ analysis = gcnew Parser::Analysis();
                analysis->name = ToSystemString(nativeAnalysis->name);
                analysis->softwareName = ToSystemString(nativeAnalysis->softwareName);
                analysis->softwareVersion = ToSystemString(nativeAnalysis->softwareVersion);
                analysis->startTime = System::DateTime::Now;//(nativeAnalysis->startTime);

                typedef pair<string, string> AnalysisParameter;
                analysis->parameters = gcnew Dictionary<String^, String^>();
                BOOST_FOREACH(const AnalysisParameter& parameter, nativeAnalysis->parameters)
                    analysis->parameters[ToSystemString(parameter.first)] = ToSystemString(parameter.second);

                analysis->filepaths = gcnew List<String^>();
                BOOST_FOREACH(const string& filepath, nativeAnalysis->filepaths)
                    analysis->filepaths->Add(ToSystemString(filepath));

                analysis->importSettings = gcnew Parser::Analysis::ImportSettings();
                analysis->importSettings->proteinDatabaseFilepath = ToSystemString(nativeAnalysis->importSettings.proteinDatabaseFilepath);

                args->DistinctAnalyses->Add(analysis);
            }

            managedFunctionPtr(nullptr, args);
            cancel = args->Cancel;

            // copy each analysis' importSettings back to the native code
            for (size_t i=0; i < distinctAnalyses.size(); ++i)
            {
                const NativeParser::Analysis& nativeAnalysis = *distinctAnalyses[i];
                Parser::Analysis^ analysis = args->DistinctAnalyses[i];
                nativeAnalysis.importSettings.proteinDatabaseFilepath = ToStdString(analysis->importSettings->proteinDatabaseFilepath);

                NativeQonverter::Settings& nativeQonverterSettings = nativeAnalysis.importSettings.qonverterSettings;
                Qonverter::Settings^ qonverterSettings = analysis->importSettings->qonverterSettings;
                nativeQonverterSettings.decoyPrefix = ToStdString(qonverterSettings->DecoyPrefix);
                nativeQonverterSettings.qonverterMethod = NativeQonverter::QonverterMethod::get_by_index((size_t) qonverterSettings->QonverterMethod).get();
                nativeQonverterSettings.rerankMatches = qonverterSettings->RerankMatches;
                nativeQonverterSettings.kernel = NativeQonverter::Kernel::get_by_index((size_t) qonverterSettings->Kernel).get();
                nativeQonverterSettings.massErrorHandling = NativeQonverter::MassErrorHandling::get_by_index((size_t) qonverterSettings->MassErrorHandling).get();
                nativeQonverterSettings.missedCleavagesHandling = NativeQonverter::MissedCleavagesHandling::get_by_index((size_t) qonverterSettings->MissedCleavagesHandling).get();
                nativeQonverterSettings.terminalSpecificityHandling = NativeQonverter::TerminalSpecificityHandling::get_by_index((size_t) qonverterSettings->TerminalSpecificityHandling).get();
                nativeQonverterSettings.chargeStateHandling = NativeQonverter::ChargeStateHandling::get_by_index((size_t) qonverterSettings->ChargeStateHandling).get();

                for each (KeyValuePair<String^, Qonverter::Settings::ScoreInfo^> itr in qonverterSettings->ScoreInfoByName)
                {
                    NativeQonverter::Settings::ScoreInfo& scoreInfo = nativeQonverterSettings.scoreInfoByName[ToStdString(itr.Key)];
                    scoreInfo.weight = itr.Value->Weight;
                    scoreInfo.order = NativeQonverter::Settings::Order::get_by_index((size_t) itr.Value->Order).get();
                    scoreInfo.normalizationMethod = NativeQonverter::Settings::NormalizationMethod::get_by_index((size_t) itr.Value->NormalizationMethod).get();
                }
            }
        }
    }
};


void Parser::marshal(Object^ sender, ImportSettingsEventArgs^ e)
{
    try
    {
        ImportSettings(this, e);
    }
    catch (Exception^ ex)
    {
        throw runtime_error(ToStdString(ex->Message));
    }
}


void Parser::marshal(Object^ sender, IterationEventArgs^ e)
{
    try
    {
        ParsingProgress(this, e);
    }
    catch (Exception^ ex)
    {
        throw runtime_error(ToStdString(ex->Message));
    }
}


void Parser::Parse(IEnumerable<String^>^ inputFilepaths)
{
    vector<string> nativeInputFilepaths;
    for each (String^ filepath in inputFilepaths)
        nativeInputFilepaths.push_back(ToStdString(filepath));

    NativeIDPicker::Parser parser;

    Parser::ImportSettingsEventHandler^ handler = gcnew Parser::ImportSettingsEventHandler(this, &Parser::marshal);
    parser.importSettingsCallback.reset(new ImportSettingsForwarder(Marshal::GetFunctionPointerForDelegate(handler).ToPointer()));

    IterationEventHandler^ handler2 = gcnew IterationEventHandler(this, &Parser::marshal);
    IterationListenerForwarder forwarder2(Marshal::GetFunctionPointerForDelegate(handler2).ToPointer());
    parser.iterationListenerRegistry.addListener(forwarder2, 100u /* iterations */);

    try {parser.parse(nativeInputFilepaths);} CATCH_AND_FORWARD

    GC::KeepAlive(handler);
    GC::KeepAlive(handler2);
}


void Parser::Parse(String^ inputFilepath)
{
    Parse(gcnew array<String^> {inputFilepath});
}


} // namespace IDPicker
