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
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//


#include "Qonverter.hpp"
#include "Logger.hpp"

#pragma unmanaged
#include "../Qonverter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#pragma managed


namespace IDPicker {


using namespace System::Runtime::InteropServices;
//using namespace pwiz::CLI::util;
typedef NativeIDPicker::Qonverter NativeQonverter;


struct ProgressMonitorForwarder : public pwiz::util::IterationListener
{
    typedef void (__stdcall *QonversionProgressCallback)(int, int, const char*, bool&);
    QonversionProgressCallback managedFunctionPtr;

    ProgressMonitorForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<QonversionProgressCallback>(managedFunctionPtr))
    {}

    virtual Status update(const UpdateMessage& updateMessage)
    {
        if (managedFunctionPtr != NULL)
        {
            bool cancel = false;
            managedFunctionPtr(updateMessage.iterationIndex,
                               updateMessage.iterationCount,
                               updateMessage.message.c_str(),
                               cancel);
            return cancel ? Status_Cancel : Status_Ok;
        }
        throw runtime_error("NULL managedFunctionPtr in ProgressMonitorForwarder");
    }
};

//#pragma managed
private delegate void QonversionProgressEventWrapper(int qonvertedAnalyses, int totalAnalyses, const char* message, bool& cancel);

void Qonverter::marshal(int qonvertedAnalyses, int totalAnalyses, const char* message, bool& cancel)
{
    try
    {
        QonversionProgressEventArgs^ eventArgs = gcnew QonversionProgressEventArgs();
        eventArgs->QonvertedAnalyses = qonvertedAnalyses;
        eventArgs->TotalAnalyses = totalAnalyses;
        eventArgs->Message = gcnew String(message);
        eventArgs->Cancel = cancel;
        QonversionProgress(this, eventArgs);
        cancel = eventArgs->Cancel;
    }
    catch (Exception^ e)
    {
        throw runtime_error(ToStdString(e->Message));
    }
}

Qonverter::Qonverter()
{
    SettingsByAnalysis = gcnew Dictionary<int, Settings^>();
    LogQonversionDetails = false;
}

void Qonverter::Qonvert(String^ idpDbFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    NativeQonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeQonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = NativeQonverter::QonverterMethod::get_by_index((size_t) itr.Value->QonverterMethod).get();
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;
        qonverterSettings.kernel = NativeQonverter::Kernel::get_by_index((size_t) itr.Value->Kernel).get();
        qonverterSettings.massErrorHandling = NativeQonverter::MassErrorHandling::get_by_index((size_t) itr.Value->MassErrorHandling).get();
        qonverterSettings.missedCleavagesHandling = NativeQonverter::MissedCleavagesHandling::get_by_index((size_t) itr.Value->MissedCleavagesHandling).get();
        qonverterSettings.terminalSpecificityHandling = NativeQonverter::TerminalSpecificityHandling::get_by_value((size_t) itr.Value->TerminalSpecificityHandling).get();
        qonverterSettings.chargeStateHandling = NativeQonverter::ChargeStateHandling::get_by_value((size_t) itr.Value->ChargeStateHandling).get();
        qonverterSettings.maxFDR = itr.Value->MaxFDR;

        for each (KeyValuePair<String^, Settings::ScoreInfo^> itr2 in itr.Value->ScoreInfoByName)
        {
            NativeQonverter::Settings::ScoreInfo& scoreInfo = qonverterSettings.scoreInfoByName[ToStdString(itr2.Key)];
            scoreInfo.weight = itr2.Value->Weight;
            scoreInfo.order = NativeQonverter::Settings::Order::get_by_index((size_t) itr2.Value->Order).get();
            scoreInfo.normalizationMethod = NativeQonverter::Settings::NormalizationMethod::get_by_index((size_t) itr2.Value->NormalizationMethod).get();
        }
    }

    //if (!ReferenceEquals(%*QonversionProgress, nullptr))
    {
        QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
        ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
            Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer());

        IterationListenerRegistry ilr;
        ilr.addListener(IterationListenerPtr(progressMonitor), 1);
        try {qonverter.qonvert(ToStdString(idpDbFilepath), &ilr);} CATCH_AND_FORWARD

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Qonvert(System::IntPtr idpDb)
{
    Logger::Initialize(); // make sure the logger is initialized

    NativeQonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeQonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = NativeQonverter::QonverterMethod::get_by_index((size_t) itr.Value->QonverterMethod).get();
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;
        qonverterSettings.kernel = NativeQonverter::Kernel::get_by_index((size_t) itr.Value->Kernel).get();
        qonverterSettings.massErrorHandling = NativeQonverter::MassErrorHandling::get_by_index((size_t) itr.Value->MassErrorHandling).get();
        qonverterSettings.missedCleavagesHandling = NativeQonverter::MissedCleavagesHandling::get_by_index((size_t) itr.Value->MissedCleavagesHandling).get();
        qonverterSettings.terminalSpecificityHandling = NativeQonverter::TerminalSpecificityHandling::get_by_value((size_t) itr.Value->TerminalSpecificityHandling).get();
        qonverterSettings.chargeStateHandling = NativeQonverter::ChargeStateHandling::get_by_value((size_t) itr.Value->ChargeStateHandling).get();
        qonverterSettings.maxFDR = itr.Value->MaxFDR;

        for each (KeyValuePair<String^,Settings::ScoreInfo^> itr2 in itr.Value->ScoreInfoByName)
        {
            NativeQonverter::Settings::ScoreInfo& scoreInfo = qonverterSettings.scoreInfoByName[ToStdString(itr2.Key)];
            scoreInfo.weight = itr2.Value->Weight;
            scoreInfo.order = NativeQonverter::Settings::Order::get_by_index((size_t) itr2.Value->Order).get();
            scoreInfo.normalizationMethod = NativeQonverter::Settings::NormalizationMethod::get_by_index((size_t) itr2.Value->NormalizationMethod).get();
        }
    }

    //if (!ReferenceEquals(%*QonversionProgress, nullptr))
    {
        QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
        ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
            Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer());

        IterationListenerRegistry ilr;
        ilr.addListener(IterationListenerPtr(progressMonitor), 1);

        sqlite3* foo = (sqlite3*) idpDb.ToPointer();
        pin_ptr<sqlite3> idpDbPtr = foo;
        try {qonverter.qonvert(idpDbPtr, &ilr);} CATCH_AND_FORWARD

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Reset(String^ idpDbFilepath)
{
    Logger::Initialize(); // make sure the logger is initialized

    NativeQonverter qonverter;
    QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
    ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
        Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer());

    IterationListenerRegistry ilr;
    ilr.addListener(IterationListenerPtr(progressMonitor), 1);

    try {qonverter.reset(ToStdString(idpDbFilepath), &ilr);} CATCH_AND_FORWARD
}

void Qonverter::Reset(System::IntPtr idpDb)
{
    Logger::Initialize(); // make sure the logger is initialized

    NativeQonverter qonverter;
    QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
    ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
        Marshal::GetFunctionPointerForDelegate((System::Delegate^) handler).ToPointer());

    IterationListenerRegistry ilr;
    ilr.addListener(IterationListenerPtr(progressMonitor), 1);

    sqlite3* foo = (sqlite3*) idpDb.ToPointer();
    pin_ptr<sqlite3> idpDbPtr = foo;
    try {qonverter.reset(idpDbPtr, &ilr);} CATCH_AND_FORWARD
}


} // namespace IDPicker

