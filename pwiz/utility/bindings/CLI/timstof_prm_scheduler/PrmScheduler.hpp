//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2020 Matt Chambers
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

#ifndef _PRMSCHEDULER_HPP_CLI_
#define _PRMSCHEDULER_HPP_CLI_

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "../common/SharedCLI.hpp"
#pragma warning( pop )

#include "prmscheduler.h"
#include <vector>

#define DEFINE_INTERNAL_BASE_CODE_CUSTOM_DTOR(CLIType, NativeType) \
public:   System::IntPtr void_base() {return (System::IntPtr) base_;} \
INTERNAL: CLIType(NativeType* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          CLIType(NativeType* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          !CLIType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(CLIType)) delete this;} \
          NativeType* base_; \
          System::Object^ owner_; \
          NativeType& base() {return *base_;}

//virtual ~CLIType() { LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), (owner_ == nullptr)) if (owner_ == nullptr) { SAFEDELETE(base_); } } \

bool operator== (::PrmMethodInfo const& lhs, ::PrmMethodInfo const& rhs)
{
    return lhs.frame_rate == rhs.frame_rate &&
        lhs.mobility_gap == rhs.mobility_gap &&
        lhs.one_over_k0_lower_limit == rhs.one_over_k0_lower_limit &&
        lhs.one_over_k0_upper_limit == rhs.one_over_k0_upper_limit;
}

bool operator== (::PrmTimeSegments const& lhs, ::PrmTimeSegments const& rhs)
{
    return lhs.time_in_seconds_begin == rhs.time_in_seconds_end &&
           lhs.time_in_seconds_end == rhs.time_in_seconds_end;
}

bool operator== (::PrmPasefSchedulingEntry const& lhs, ::PrmPasefSchedulingEntry const& rhs)
{
    return lhs.target_id == rhs.target_id && lhs.time_segment_id == rhs.time_segment_id;
}

bool operator== (::PrmVisualizationDataPoint const& lhs, ::PrmVisualizationDataPoint const& rhs)
{
    return lhs.x == rhs.x && lhs.y == rhs.y;
}

namespace pwiz {
namespace CLI {
namespace Bruker {
namespace PrmScheduling {

public ref class MethodInfo
{
    DEFINE_INTERNAL_BASE_CODE(MethodInfo, ::PrmMethodInfo);

    public:

    ///<summary> mobility_gap : 1 / k0 distance required between two targets to make sure
    ///                the quadrupole setting is OK</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, mobility_gap);

    ///<summary> frame_rate : as long as the tims ramp is the same for all frames this
    ///              value specifies the number of frames per second</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, frame_rate);

    ///<summary> one_over_k0_lower_limit : lower limit of 1 / k0 which can be measured based
    ///                           on the template method</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, one_over_k0_lower_limit);

    ///<summary> one_over_k0_upper_limit : upper limit of 1 / k0 which can be measured based
    ///                           on the template method</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, one_over_k0_upper_limit);
};

public ref class AdditionalMeasurementParameters
{
    DEFINE_INTERNAL_BASE_CODE(AdditionalMeasurementParameters, ::PrmAdditionalMeasurementParameters);

    public:

    AdditionalMeasurementParameters() : base_(new ::PrmAdditionalMeasurementParameters) {}

    ///<summary> ms1_repetition_time : time in seconds between two MS-1 frames if they are in competition with
    ///                  MS-2 frames. If no targets are defined, MS-1 frames are done instead</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, ms1_repetition_time);

    DEFINE_PRIMITIVE_PROPERTY(bool, bool, default_pasef_collision_energies);
};


public ref class MeasurementMode
{
    DEFINE_INTERNAL_BASE_CODE_CUSTOM_DTOR(MeasurementMode, ::PrmMeasurementMode);

    virtual ~MeasurementMode()
    {
        LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), (owner_ == nullptr));
        if (owner_ == nullptr) { SAFEDELETE(base_); }
    }

    public:

    MeasurementMode() : base_(new ::PrmMeasurementMode)
    {
    }

    ///<summary> External Id typically specified by the software which is used to
    /// design the PRM experiment(Skyline, SpectroDive).This id is
    /// thought as a key to data structures within these tools
    ///</summary>
    //DEFINE_C_STRING_PROPERTY(external_id);

    ///<summary> accumulation time for the TIMS cell if this parameter has to be
    /// changed between different frames. Unit is milliseconds
    ///</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, accumulation_time);
};

public ref class TimeSegments
{
    DEFINE_INTERNAL_BASE_CODE(TimeSegments, ::PrmTimeSegments);

    public:
    ///<summary>summary>begin of this rt segment</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, time_in_seconds_begin);

    ///<summary>summary> end of this rt segment</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, time_in_seconds_end);
};

public ref class InputTarget
{
    DEFINE_INTERNAL_BASE_CODE_CUSTOM_DTOR(InputTarget, ::PrmInputTarget);

    virtual ~InputTarget()
    {
        LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), (owner_ == nullptr));
        if (owner_ == nullptr) { SAFEDELETE(base_); }
    }

    public:

    InputTarget() : base_(new ::PrmInputTarget)
    {
    }

    ///<summary> The isolation m / z(in the m / z calibration state that is used during
    /// acquisition).The quadrupole is tuned to this m/z during fragmentation</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, isolation_mz);

    ///<summary> Specifies the total 3 - dB width of the isolation window(in m / z units), the center
    /// of which is given by 'isolation_mz'.</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, isolation_width);

    ///<summary> Inverse reduced mobility(1 / K0), lower limit of the range</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, one_over_k0_lower_limit);

    ///<summary> Inverse reduced mobility(1 / K0), upper limit of the range</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, one_over_k0_upper_limit);

    ///<summary> begin of retention time window for this target</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, time_in_seconds_begin);

    ///<summary> end of retention time window for this target</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, time_in_seconds_end);

    ///<summary> Collision energy(in eV).If the collision energy is not specified it
    /// will be defined by the control software based on the settings of the PASEF method.
    /// In that case the collision energy is a function of the mobility, ignoring m / z and charge state
    /// A negative value indicates an unspecified collision energy.</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, collision_energy);

    // NOT YET:
    ///// number of measurement modes to be used for this target. 
    ///// If only one mode is used with the default parameters,
    ///// and there is no specification of modes, this is set to 0
    //uint32_t number_of_specified_measurement_mode_ids;

    ///// ids (starting with 0) of the measurement modes to be used for this
    ///// target, if multiple different measurement modes are requested
    //uint32_t *specified_measurement_mode_ids;

    // //////// - Block of additional target characteristics :

    ///<summary> Inverse reduced mobility(1 / K0), value for the apex of the target compound</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, one_over_k0);

    ///<summary> The monoisotopic m / z</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, monoisotopic_mz);

    ///<summary> The retention time apex of the target compound</summary>
    DEFINE_PRIMITIVE_PROPERTY(double, double, time_in_seconds);

    ///<summary> The charge state</summary>
    DEFINE_PRIMITIVE_PROPERTY(int32_t, System::Int32, charge);
};

public ref class PasefSchedulingEntry
{
    DEFINE_INTERNAL_BASE_CODE(PasefSchedulingEntry, ::PrmPasefSchedulingEntry);

    public:
    ///<summary> Number assigned to uniquely identify a frame only(!) within a certain retention time segment</summary>
    DEFINE_PRIMITIVE_PROPERTY(uint32_t, System::UInt32, frame_id);

    ///<summary> id for the target, which is the index of the target in the PrmInputTarget table.</summary>
    DEFINE_PRIMITIVE_PROPERTY(uint32_t, System::UInt32, target_id);

    ///<summary>
    /// Id of the time segment for which the given target should be
    /// measured as part of the given frame. the id is an index for
    /// the array of time segments
    ///</summary>
    DEFINE_PRIMITIVE_PROPERTY(uint32_t, System::UInt32, time_segment_id);

    ///<summary>
    /// Measurement mode for this frame if there is more than one
    /// and parameters have to be changed. The id is an index to
    /// the array with the given PrmMeasurementModes (start with 0)
    ///</summary>
    DEFINE_PRIMITIVE_PROPERTY(uint32_t, System::UInt32, measurement_mode_id);
};

/// <summary>
/// Gives progress information during scheduling algorithm. Return true to cancel and false to continue.
/// </summary>
public delegate bool ProgressUpdate(double progressPercentage);


/// <summary>
/// This enum gives the numbers for the generation of metrics of
/// the prm-PASEF scheduling, which are thought for visualization
/// the function prm_get_visualization takes a uint32_t to avoid
/// inter-language conversion problems.
/// </summary>
public enum class SchedulingMetrics
{
    /// <summary>
    /// get the number of frames which have to be measured concurrently in each retention time segment, one after the other
    /// x = start and end rt of each time segment
    /// y = number of concurrent frames
    /// </summary>
    CONCURRENT_FRAMES = 0

    /// <summary>
    /// get the average number of targets per frame for each retention time segment
    /// </summary>
    , TARGETS_PER_FRAME = 1

    /// <summary>
    /// average number of times a target is measured in a time segment to fill up otherwise unused mobility space: total number of fragmentations devided by the number of unique targets
    /// </summary>
    , REDUNDANCY_OF_TARGETS = 2

    /// <summary>
    /// average time between two measurements of a target in seconds, x is the target id
    /// </summary>
    , MEAN_SAMPLING_TIMES = 3

    /// <summary>
    /// max time between two measurements of a target in seconds x is the target id
    /// </summary>
    , MAX_SAMPLING_TIMES = 4 
};

public ref class VisualizationDataPoint
{
    DEFINE_INTERNAL_BASE_CODE(VisualizationDataPoint, ::PrmVisualizationDataPoint);

    public:
    /// <summary>
    /// usually but not limited to the time in min of a retention time segment point
    /// </summary>
    DEFINE_PRIMITIVE_PROPERTY(double, System::Double, x);

    /// <summary>
    /// function value, e.g. a number of frames
    /// </summary>
    DEFINE_PRIMITIVE_PROPERTY(double, System::Double, y);
};


extern "C" {
    void getMethodInfo(const ::PrmMethodInfo *prm_method_info, void *user_data)
    {
        auto& methodInfo = *((std::vector<::PrmMethodInfo>*) user_data);
        methodInfo.push_back(*prm_method_info);
    }

    void getTimeSegments(uint32_t num_entries, const ::PrmTimeSegments *prm_time_segments, void *user_data)
    {
        auto& segments = *((std::vector<::PrmTimeSegments>*) user_data);
        std::copy(&prm_time_segments[0], prm_time_segments + num_entries, segments.begin());
    }

    void getSchedulingEntries(uint32_t num_entries, const ::PrmPasefSchedulingEntry *prm_scheduling_entries, void *user_data)
    {
        auto& entries = *((std::vector<::PrmPasefSchedulingEntry>*) user_data);
        std::copy(&prm_scheduling_entries[0], prm_scheduling_entries + num_entries, entries.begin());
    }

    typedef bool(__stdcall *ProgressUpdateCallback)(double percentage);

    bool progressUpdateCallback(double progressPercentage, void* user_data)
    {
        auto progressUpdateFunctionPtr = (ProgressUpdateCallback)user_data;
        return progressUpdateFunctionPtr(progressPercentage);
    }

    void getSchedulingMetrics(uint32_t num_entries, const ::PrmVisualizationDataPoint *data_points, void *user_data)
    {
        auto& entries = *((std::vector<::PrmVisualizationDataPoint>*) user_data);
        std::copy(&data_points[0], data_points + num_entries, entries.begin());
    }
}

public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MethodInfoList, ::PrmMethodInfo, MethodInfo, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SchedulingEntryList, ::PrmPasefSchedulingEntry, PasefSchedulingEntry, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TimeSegmentList, ::PrmTimeSegments, TimeSegments, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(DataPointList, ::PrmVisualizationDataPoint, VisualizationDataPoint, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// Information about an ontology or CV source and a short 'lookup' tag to refer to.
/// </summary>
public ref class Scheduler
{
    internal:
    uint64_t handle_;


    public:
    Scheduler(System::String^ scheduling_file_name)
    {
        handle_ = prm_scheduling_file_open(ToStdString(scheduling_file_name).c_str());
        if (!handle_)
            throw gcnew System::Exception(GetLastErrorString());
    }

    ~Scheduler()
    {
        prm_scheduling_file_close(handle_);
    }

    ///<summary> Return the last error as a string (thread-local).</summary>
    System::String^ GetLastErrorString()
    {
        char buf[1024];
        uint32_t resultSize = prm_scheduling_get_last_error_string(&buf[0], 1024);
        return gcnew System::String(buf, 0, resultSize-1);
    }

    ///<summary>
    /// Get the PrmMethodInfo data obtained from the scheduler.sql file
    ///
    /// On success, returns the results as a data structure
    /// </summary>
    MethodInfoList^ GetPrmMethodInfo()
    {
        auto result = gcnew MethodInfoList();
        prm_scheduling_get_method_info(handle_, getMethodInfo, (void*)result->base_);
        return result;
    }

    ///<summary>
    /// Set the user parameters which define the PRM measurement in addition
    /// to the scheduled target list
    /// </summary>
    void SetAdditionalMeasurementParameters(AdditionalMeasurementParameters^ additionalMeasurementParameters)
    {
        prm_scheduling_set_additional_measurement_parameters(handle_, additionalMeasurementParameters->base_);
    }

    ///<summary>
    /// Determine a prm-PASEF target scheduling for the given targets
    ///
    /// \param num_input_targets gives the number of targets to be scheduled
    /// \param prm_input_target_list pointer to the array of target definitions
    /// \param num_prm_measurement_modes number of measurement modes
    /// \param prm_measurement_modes is either a null pointer or is specifying a
    ///        number of measurement modes which each frame has to be measured
    ///        with
    /// </summary>
    void AddInputTarget(InputTarget^ inputTarget, System::String^ external_id, System::String^ description)
    {
        if (!prm_add_input_target(handle_, inputTarget->base_, ToStdString(external_id).c_str(), ToStdString(description).c_str()))
            throw gcnew System::Exception(GetLastErrorString());
    }

    ///<summary>get the prm scheduling results for further evaluation and visualization</summary>
    void GetScheduling(TimeSegmentList^ timeSegmentList, SchedulingEntryList^ schedulingEntryList, ProgressUpdate^ progressUpdateDelegate)
    {
        System::IntPtr callbackPtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(progressUpdateDelegate);
        if (!prm_scheduling_prm_targets(handle_, (prm_progress_cancel_function*)&progressUpdateCallback, callbackPtr.ToPointer()))
        {
            auto lastError = GetLastErrorString();
            if (!lastError->Contains("user request"))
                throw gcnew System::Exception(GetLastErrorString());
        }

        schedulingEntryList->base().resize(prm_get_num_scheduling_entries(handle_));
        timeSegmentList->base().resize(prm_get_num_time_segments(handle_));
        if (!prm_get_scheduling(handle_, getSchedulingEntries, getTimeSegments, (void*) schedulingEntryList->base_, (void*) timeSegmentList->base_))
            throw gcnew System::Exception(GetLastErrorString());
    }

    DataPointList^ GetSchedulingMetrics(SchedulingMetrics metric)
    {
        auto result = gcnew DataPointList();
        auto numDataPoints = prm_calculate_visualization(handle_, (uint32_t)metric);
        if (!numDataPoints)
            throw gcnew System::Exception(GetLastErrorString());
        result->base().resize(numDataPoints);
        if (!prm_get_visualization(handle_, getSchedulingMetrics, (void*) result->base_))
            throw gcnew System::Exception(GetLastErrorString());
        return result;
    }

    /// <summary>
    /// write the prm scheduling results to the given sqlite file
    /// </summary>
    void WriteScheduling();
};


} // namespace PrmScheduling
} // namespace Bruker
} // namespace CLI
} // namespace pwiz

#endif // _PRMSCHEDULER_HPP_CLI_
