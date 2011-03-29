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


#include "Qonverter.hpp"
#include "../Qonverter.hpp"


namespace IDPicker {


using namespace System::Runtime::InteropServices;
//using namespace pwiz::CLI::util;
typedef NativeIDPicker::Qonverter NativeQonverter;


struct ProgressMonitorForwarder : public NativeQonverter::ProgressMonitor
{
    typedef void (__stdcall *QonversionProgressCallback)(int, int, bool&);
    QonversionProgressCallback managedFunctionPtr;

    ProgressMonitorForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<QonversionProgressCallback>(managedFunctionPtr))
    {}

    virtual void operator() (UpdateMessage& updateMessage) const
    {
        if (managedFunctionPtr != NULL)
        {
            managedFunctionPtr(updateMessage.qonvertedAnalyses,
                               updateMessage.totalAnalyses,
                               updateMessage.cancel);
        }
    }
};

//#pragma managed
private delegate void QonversionProgressEventWrapper(int qonvertedAnalyses, int totalAnalyses, bool& cancel);

void Qonverter::marshal(int qonvertedAnalyses, int totalAnalyses, bool& cancel)
{
    try
    {
        QonversionProgressEventArgs^ eventArgs = gcnew QonversionProgressEventArgs();
        eventArgs->QonvertedAnalyses = qonvertedAnalyses;
        eventArgs->TotalAnalyses = totalAnalyses;
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
    NativeQonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeQonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = NativeQonverter::QonverterMethod::get_by_index((size_t) itr.Value->QonverterMethod).get();
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;

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
            Marshal::GetFunctionPointerForDelegate(handler).ToPointer());

        try {qonverter.qonvert(ToStdString(idpDbFilepath), *progressMonitor);} CATCH_AND_FORWARD
        delete progressMonitor;

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Qonvert(System::IntPtr idpDb)
{
    NativeQonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeQonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = NativeQonverter::QonverterMethod::get_by_index((size_t) itr.Value->QonverterMethod).get();
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;

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
            Marshal::GetFunctionPointerForDelegate(handler).ToPointer());

        sqlite3* foo = (sqlite3*) idpDb.ToPointer();
        pin_ptr<sqlite3> idpDbPtr = foo;
        try {qonverter.qonvert(idpDbPtr, *progressMonitor);} CATCH_AND_FORWARD
        delete progressMonitor;

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Reset(String^ idpDbFilepath)
{
    NativeQonverter qonverter;

    try {qonverter.reset(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Reset(System::IntPtr idpDb)
{
    NativeQonverter qonverter;

    sqlite3* foo = (sqlite3*) idpDb.ToPointer();
    pin_ptr<sqlite3> idpDbPtr = foo;
    try {qonverter.reset(idpDbPtr);} CATCH_AND_FORWARD
}


} // namespace IDPicker

