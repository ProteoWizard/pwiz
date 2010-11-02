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


#include "../Qonverter.hpp"

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "../../freicore/pwiz_src/pwiz/utility/bindings/CLI/common/SharedCLI.hpp"
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


namespace IDPicker {


using namespace System;
using namespace System::ComponentModel;
using namespace System::Collections::Generic;


public ref struct Qonverter
{
    enum class QonverterMethod
    {
        StaticWeighted,
        OptimizedMonteCarlo,
        OptimizedPercolator
    };

    ref struct Settings
    {
        enum class NormalizationMethod
        {
            Off,
            Quantile,
            Linear
        };

        enum class Order
        {
            Ascending,
            Descending
        };

        ref struct ScoreInfo
        {
            property double Weight;
            property NormalizationMethod NormalizationMethod;
            property Order Order;
        };

        property QonverterMethod QonverterMethod;
        property String^ DecoyPrefix;
        property bool RerankMatches;
        property IDictionary<String^, ScoreInfo^>^ ScoreInfoByName;
    };

    property IDictionary<int, Settings^>^ SettingsByAnalysis;
    property bool LogQonversionDetails;

    ref struct QonversionProgressEventArgs : CancelEventArgs
    {
        property int QonvertedAnalyses;
        property int TotalAnalyses;
    };

    delegate void QonversionProgressEventHandler(Object^ sender, QonversionProgressEventArgs^ e);

    event QonversionProgressEventHandler^ QonversionProgress;

    Qonverter();

    void Qonvert(String^ idpDbFilepath);
    void Qonvert(System::IntPtr idpDb);

    internal: void marshal(int qonvertedAnalyses, int totalAnalyses, bool& cancel);
};


} // namespace IDPicker
