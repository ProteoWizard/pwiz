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
// Contributor(s): Surendra Dasari
//


#ifndef _QONVERTER_HPP_
#define _QONVERTER_HPP_


#include <vector>
#include <map>
#include <utility>
#include <string>
#include "../Lib/SQLite/sqlite3.h"

using std::vector;
using std::map;
using std::pair;
using std::string;


namespace IDPicker {


#ifdef __cplusplus_cli
namespace native {
#endif


struct StaticWeightQonverter
{
    string decoyPrefix;
    map<string, double> scoreWeights;
    double nTerminusIsSpecificWeight;
    double cTerminusIsSpecificWeight;
    bool rerankMatches;
    bool logQonversionDetails;

    StaticWeightQonverter();

    struct ProgressMonitor
    {
        struct UpdateMessage
        {
            int QonvertedAnalyses;
            int TotalAnalyses;
            bool Cancel;
        };

        virtual void operator() (UpdateMessage& updateMessage) const {};
    };

    void Qonvert(const string& idpDbFilepath, const ProgressMonitor& progressMonitor = ProgressMonitor());
    void Qonvert(sqlite3* idpDb, const ProgressMonitor& progressMonitor = ProgressMonitor());
};


#ifdef __cplusplus_cli
} // namespace native
#endif


} // namespace IDPicker


#ifdef __cplusplus_cli

#pragma managed( push, on )
#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "../freicore/pwiz_src/pwiz/utility/bindings/CLI/common/SharedCLI.hpp"
#using <system.dll>
#pragma warning( pop )
#undef CATCH_AND_FORWARD
#define CATCH_AND_FORWARD \
    catch (std::bad_cast& e) { throw gcnew System::InvalidCastException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::bad_alloc& e) { throw gcnew System::OutOfMemoryException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::out_of_range& e) { throw gcnew System::IndexOutOfRangeException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::invalid_argument& e) { throw gcnew System::ArgumentException(ToSystemString(e.what())); } \
    catch (std::runtime_error& e) { throw gcnew System::Exception(ToSystemString(e.what())); } \
    catch (std::exception& e) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unhandled exception: " + ToSystemString(e.what())); } \
    catch (_com_error& e) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unhandled COM error: " + gcnew System::String(e.ErrorMessage())); } \
    catch (System::Exception^) { throw; } \
    catch (...) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unknown exception"); }

using namespace System;
using namespace System::ComponentModel;
using namespace System::Collections::Generic;


namespace IDPicker {


public ref struct StaticWeightQonverter
{
    property String^ DecoyPrefix;
    property IDictionary<String^, double>^ ScoreWeights;
    property double NTerminusIsSpecificWeight;
    property double CTerminusIsSpecificWeight;
    property bool RerankMatches;
    property bool LogQonversionDetails;

    ref struct QonversionProgressEventArgs : CancelEventArgs
    {
        property int QonvertedAnalyses;
        property int TotalAnalyses;
    };

    delegate void QonversionProgressEventHandler(Object^ sender, QonversionProgressEventArgs^ e);

    event QonversionProgressEventHandler^ QonversionProgress;

    StaticWeightQonverter();

    void Qonvert(String^ idpDbFilepath);
    void Qonvert(System::IntPtr idpDb);

    internal: void marshal(int qonvertedAnalyses, int totalAnalyses, bool& cancel);
};


} // namespace IDPicker


#pragma managed( pop )

#endif // __cplusplus_cli


#endif // _QONVERTER_HPP_
