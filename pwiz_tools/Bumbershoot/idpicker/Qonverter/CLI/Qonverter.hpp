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


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "pwiz/utility/bindings/CLI/common/SharedCLI.hpp"

#pragma unmanaged
#include "../SchemaUpdater.hpp"
#pragma managed

#using <system.dll>
#pragma warning( pop )


#undef CATCH_AND_FORWARD
#define CATCH_AND_FORWARD \
    catch (NativeIDPicker::cancellation_exception&) {} \
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
using namespace System::Collections::Generic;


public ref struct Qonverter
{
    enum class QonverterMethod
    {
        StaticWeighted,
        SVM,
        PartitionedSVM = SVM,
        SingleSVM,
        MonteCarlo,
    };

    enum class Kernel
    {
        Linear,
        Polynomial,
        RBF,
        Sigmoid
    };

    [System::FlagsAttribute]
    enum class ChargeStateHandling
    {
        Ignore = (1<<0),
        Partition = (1<<1),
        Feature = (1<<2) // SVM only
    };

    [System::FlagsAttribute]
    enum class TerminalSpecificityHandling
    {
        Ignore = (1<<0),
        Partition = (1<<1),
        Feature = (1<<2) // SVM only
    };

    enum class MissedCleavagesHandling
    {
        Ignore,
        Feature // SVM only
    };

    enum class MassErrorHandling
    {
        Ignore,
        Feature // SVM only
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

        /// should ranks within a spectrum be rearranged based on the total score?
        property bool RerankMatches;
        
        /// how should charge state be used during qonversion?
        property ChargeStateHandling ChargeStateHandling;

        /// how should terminal specificity be used during qonversion?
        property TerminalSpecificityHandling TerminalSpecificityHandling;

        /// how should missed cleavages be used during qonversion?
        property MissedCleavagesHandling MissedCleavagesHandling;

        /// how should mass error be used during qonversion?
        property MassErrorHandling MassErrorHandling;

        /// for SVM qonversion, what kind of kernel should be used?
        property Kernel Kernel;

        /// for HillClimber and MonteCarlo qonversion, what FDR are we trying to maximize results for?
        property double MaxFDR;

        /// what score names are expected and how should they be weighted and normalized?
        property IDictionary<String^, ScoreInfo^>^ ScoreInfoByName;
    };

    property IDictionary<int, Settings^>^ SettingsByAnalysis;
    property bool LogQonversionDetails;

    ref struct QonversionProgressEventArgs : EventArgs
    {
        property int QonvertedAnalyses;
        property int TotalAnalyses;
        property String^ Message;
        property bool Cancel;
    };

    delegate void QonversionProgressEventHandler(Object^ sender, QonversionProgressEventArgs^ e);

    event QonversionProgressEventHandler^ QonversionProgress;

    Qonverter();

    void Qonvert(String^ idpDbFilepath);
    void Qonvert(System::IntPtr idpDb);

    void Reset(String^ idpDbFilepath);
    void Reset(System::IntPtr idpDb);

    internal: void marshal(int qonvertedAnalyses, int totalAnalyses, const char* message, bool& cancel);
};


} // namespace IDPicker
