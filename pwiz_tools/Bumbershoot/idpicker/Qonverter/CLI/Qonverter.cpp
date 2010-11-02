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


namespace IDPicker {


using namespace System::Runtime::InteropServices;

typedef void (__stdcall *QonversionProgressCallback)(int, int, bool&);


struct ProgressMonitorForwarder : public NativeIDPicker::Qonverter::ProgressMonitor
{
    QonversionProgressCallback managedFunctionPtr;

    ProgressMonitorForwarder(void* managedFunctionPtr)
        : managedFunctionPtr(static_cast<QonversionProgressCallback>(managedFunctionPtr))
    {}

    virtual void operator() (UpdateMessage& updateMessage) const
    {
        if (managedFunctionPtr != NULL)
        {
            managedFunctionPtr(updateMessage.QonvertedAnalyses,
                               updateMessage.TotalAnalyses,
                               updateMessage.Cancel);
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
    NativeIDPicker::Qonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeIDPicker::Qonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = (NativeIDPicker::Qonverter::QonverterMethod) itr.Value->QonverterMethod;
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;

        for each (KeyValuePair<String^, Settings::ScoreInfo^> itr2 in itr.Value->ScoreInfoByName)
        {
            NativeIDPicker::Qonverter::Settings::ScoreInfo& scoreInfo = qonverterSettings.scoreInfoByName[ToStdString(itr2.Key)];
            scoreInfo.weight = itr2.Value->Weight;
            scoreInfo.order = (NativeIDPicker::Qonverter::Settings::Order) itr2.Value->Order;
            scoreInfo.normalizationMethod = (NativeIDPicker::Qonverter::Settings::NormalizationMethod) itr2.Value->NormalizationMethod;
        }
    }

    //if (!ReferenceEquals(%*QonversionProgress, nullptr))
    {
        QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
        ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
            Marshal::GetFunctionPointerForDelegate(handler).ToPointer());

        try {qonverter.Qonvert(ToStdString(idpDbFilepath), *progressMonitor);} CATCH_AND_FORWARD
        delete progressMonitor;

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}

void Qonverter::Qonvert(System::IntPtr idpDb)
{
    NativeIDPicker::Qonverter qonverter;
    qonverter.logQonversionDetails = LogQonversionDetails;

    for each (KeyValuePair<int, Settings^> itr in SettingsByAnalysis)
    {
        NativeIDPicker::Qonverter::Settings& qonverterSettings = qonverter.settingsByAnalysis[itr.Key];
        qonverterSettings.decoyPrefix = ToStdString(itr.Value->DecoyPrefix);
        qonverterSettings.qonverterMethod = (NativeIDPicker::Qonverter::QonverterMethod) itr.Value->QonverterMethod;
        qonverterSettings.rerankMatches = itr.Value->RerankMatches;

        for each (KeyValuePair<String^,Settings::ScoreInfo^> itr2 in itr.Value->ScoreInfoByName)
        {
            NativeIDPicker::Qonverter::Settings::ScoreInfo& scoreInfo = qonverterSettings.scoreInfoByName[ToStdString(itr2.Key)];
            scoreInfo.weight = itr2.Value->Weight;
            scoreInfo.order = (NativeIDPicker::Qonverter::Settings::Order) itr2.Value->Order;
            scoreInfo.normalizationMethod = (NativeIDPicker::Qonverter::Settings::NormalizationMethod) itr2.Value->NormalizationMethod;
        }
    }

    //if (!ReferenceEquals(%*QonversionProgress, nullptr))
    {
        QonversionProgressEventWrapper^ handler = gcnew QonversionProgressEventWrapper(this, &Qonverter::marshal);
        ProgressMonitorForwarder* progressMonitor = new ProgressMonitorForwarder(
            Marshal::GetFunctionPointerForDelegate(handler).ToPointer());

        sqlite3* foo = (sqlite3*) idpDb.ToPointer();
        pin_ptr<sqlite3> idpDbPtr = foo;
        try {qonverter.Qonvert(idpDbPtr, *progressMonitor);} CATCH_AND_FORWARD
        delete progressMonitor;

        GC::KeepAlive(handler);
    }
    //else
    //    try {swq.Qonvert(ToStdString(idpDbFilepath));} CATCH_AND_FORWARD
}


} // namespace IDPicker

