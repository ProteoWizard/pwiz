#pragma once

/** \file
 *
 * Definition of the public C API for the PRM-PASEF scheduling DLL
 *
 */

#include "configuration/visibility_decl.h"

#ifdef DE_BDAL_CPP_PRM_SCHEDULER_BUILDING_DLL
#define BdalPRMSchedulerDllSpec BDAL_VIS_EXPORT_DECL
#else
#define BdalPRMSchedulerDllSpec BDAL_VIS_IMPORT_DECL
#endif

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

    /// This structure contains parameters exported or generated from a template
    /// acquisition method. They are thought to be used for the prm scheduling
    /// and for the selection of targets by the software used for the experimental 
    /// design
    typedef struct {
        //< mobility_gap : 1 / k0 distance required between two targets to make sure
        //<                the quadrupole setting is OK
        double mobility_gap;

        //< frame_rate : as long as the tims ramp is the same for all frames this
        //<              value specifies the number of frames per second
        double frame_rate;

        //< one_over_k0_lower_limit : lower limit of 1 / k0 which can be measured based
        //<                           on the template method
        double one_over_k0_lower_limit;

        //< one_over_k0_upper_limit : upper limit of 1 / k0 which can be measured based
        //<                           on the template method
        double one_over_k0_upper_limit;
    } PrmMethodInfo;

    /// This structure contains user parameters which define the PRM measurement in addition
    /// to the scheduled target list
    typedef struct {
        //< let the engine use the default collision energy as they are defined for PASEF
        //< experiments and ramped as a function of mobility
        bool default_pasef_collision_energies;
        //< ms1_repetition_time : time in seconds between two MS-1 frames if they are in competition with
        //<                  MS-2 frames. If no targets are defined, MS-1 frames are done instead
        double ms1_repetition_time;
    } PrmAdditionalMeasurementParameters;

    /// Definitions for a collision energy ramp of a direct injection experiment
    typedef struct {
        //< flag whether CE ramping or normal scheduling
        bool do_collision_energy_ramping;
        //< lowest value of the CE ramp
        double min_collision_energy;
        //< largest value of the CE ramp
        double max_collision_energy;
        //< number of steps of the ramp
        uint32_t number_of_steps;
        //< time to measure each of the steps (in seconds), minimum is one second
        double time_per_step;
    } PrmCollisionEnergyRamp;

    /// Definition of measurement mode specific parameters if targets should be
    /// measured in different modes. Not supported yet by the acquisition software
    /// This is just accumulation time for now, but other parameters might come
    typedef struct {
        //< accumulation time for the TIMS cell if this parameter has to be
        //< changed between different frames. Unit is milliseconds
        double accumulation_time;
    } PrmMeasurementMode;

    /// PrmTimeSegments represent the time interval for which the original
    /// targets are the same and therefore the same set of frames should be
    /// measured again and again
    typedef struct {
        //< begin of this rt segment
        double time_in_seconds_begin;

        //< end of this rt segment
        double time_in_seconds_end;
    } PrmTimeSegments;


    /// This table represents the input target data for the prm scheduling
    typedef struct {
        //< The isolation m / z(in the m / z calibration state that is used during
        //< acquisition).The quadrupole is tuned to this m/z during fragmentation
        double isolation_mz;

        //< Specifies the total 3 - dB width of the isolation window(in m / z units), the center
        //< of which is given by 'isolation_mz'.
        double isolation_width;

        //< Inverse reduced mobility(1 / K0), lower limit of the range
        double one_over_k0_lower_limit;

        //< Inverse reduced mobility(1 / K0), upper limit of the range
        double one_over_k0_upper_limit;

        //< begin of retention time window for this target
        double time_in_seconds_begin;

        //< end of retention time window for this target
        double time_in_seconds_end;

        //< Collision energy(in eV).If the collision energy is not specified it
        //< will be defined by the control software based on the settings of the PASEF method.
        //< In that case the collision energy is a function of the mobility, ignoring m / z and charge state
        //< A negative value indicates an unspecified collision energy.
        double collision_energy;

        // NOT YET:
        ////< number of measurement modes to be used for this target. 
        ////< If only one mode is used with the default parameters,
        ////< and there is no specification of modes, this is set to 0
        //uint32_t number_of_specified_measurement_mode_ids;

        ////< ids (starting with 0) of the measurement modes to be used for this
        ////< target, if multiple different measurement modes are requested
        //uint32_t *specified_measurement_mode_ids;

        // //////// - Block of additional target characteristics :

        //< Inverse reduced mobility(1 / K0), value for the apex of the target compound
        double one_over_k0;

        //< The monoisotopic m / z
        double monoisotopic_mz;

        //< The retention time apex of the target compound
        double time_in_seconds;

        //< The charge state
        int32_t charge;
    } PrmInputTarget;


    /// This structure presents the PASEF scheduling information for one
    /// target of a frame to be measured within a certain rt segment using
    /// a specified measurement mode. Each row represents a target which
    /// has to be measured as part of a certain frame during a given
    /// retention time interval / segment. A target can be measured in multiple
    /// rt segments and also multiple frames within one rt segment
    typedef struct {
        //< Number assigned to uniquely identify a frame only(!) within
        //< a certain retention time segment
        uint32_t frame_id;

        //< id for the target, which is the index of the target in the
        //< PrmInputTarget table. 
        uint32_t target_id;

        //< Id of the time segment for which the given target should be
        //< measured as part of the given frame. the id is an index for
        //< the array of time segments
        uint32_t time_segment_id;

        //< Measurement mode for this frame if there is more than one
        //< and parameters have to be changed. The id is an index to
        //< the array with the given PrmMeasurementModes (start with 0)
        uint32_t measurement_mode_id;
    } PrmPasefSchedulingEntry;

    /// This enum gives the numbers for the generation of metrics of
    /// the prm-PASEF scheduling, which are thought for visualization
    /// the function prm_get_visualization takes a uint32_t to avoid
    /// inter-language conversion problems.
    typedef enum {
        CONCURRENT_FRAMES = 0 // get the number of frames which have to be measured
                              // concurrently in each retention time segment, one after the other
                              // x = start and end rt of each time segment
                              // y = number of concurrent frames
        , TARGETS_PER_FRAME = 1 // get the average number of targets per frame for
                                // each retention time segment
        , REDUNDANCY_OF_TARGETS = 2 // average number of times a target is measured in a time segment
                                    // to fill up otherwise unused mobility space: total number of 
                                    // fragmentations devided by the number of unique targets
        , MEAN_SAMPLING_TIMES = 3 // average time between two measurements of a target in seconds, 
                                  // x is the target id
        , MAX_SAMPLING_TIMES = 4 // max time between two measurements of a target in seconds, 
                                  // x is the target id
    } PrmPasefSchedulingMetrics;

    typedef struct {
        double x; // usually but not limited to the time in min of a retention time segment point
        double y; // function value, e.g. a number of frames
    } PrmVisualizationDataPoint;

    /// This structure holds the properties of retention time standard, except for
    /// for the fragment ions which are modeled in PrmRetentionTimeStandardFragment
    /// Done that away to avoid arrays which give memory management problems ...
    typedef struct {
        //< id for the target of this standard, which is the index of the target in the
        //< PrmInputTarget table. The standards have to be scheduled like other targets do
        uint32_t target_id;
        //< threshold for the intensity summed up in the m/z ranges of the fragments ion
        double intensity_threshold;
        //< the retention time in seconds to be used as the reference value of this target
        double reference_time_in_seconds;
    } PrmRetentionTimeStandard;

    /// This structure holds the fragment ion m/z and relative intensity in percent of the
    /// most intensive fragment of each retention time standard.
    typedef struct {
        //< id of the retention time standard which is the order of the standard in which it
        //< is added
        uint32_t retention_time_standard_id;
        //< m/z value to use for the fragment ion (might be monoisotopic or not)
        double mz;
        //< relative intensity in percent of the largest used fragment ion of this standard
        double relative_intensity_percentage;
    } PrmRetentionTimeStandardFragment;

    /// Open the prmsqlite file, read the PrmMethodInfo data and 
    /// erase eventually exisiting scheduling results which will later on
    /// be written to this file.
    ///
    /// On success, returns a non-zero instance handle that needs to be passed to
    /// subsequent API calls, in particular to the required call to prm_scheduling_file_close
    ///
    /// On failure, returns 0, and you can use tims_get_last_error_string() to obtain a
    /// string describing the problem.
    ///
    /// \param scheduling_file_path is the path to the *.prmsqlite file in the 
    /// filesystem, in UTF-8 encoding. If any file extension but .prmsqlite or no file extension
    /// is given appends .prmsqlite
    ///
    BdalPRMSchedulerDllSpec uint64_t prm_scheduling_file_open(
        const char *scheduling_file_name
    );

    /// Close data set and free allocated memory associated with this handle
    ///
    /// \param handle obtained by scheduling_file_open(); passing 0 is ok and has no effect.
    ///
    BdalPRMSchedulerDllSpec void prm_scheduling_file_close(
        uint64_t handle
    );

    /// Return the last error as a string (thread-local).
    ///
    /// \param buf pointer to a buffer into which the error string will be written.
    ///
    /// \param actual_buffer_size length of the buffer
    ///
    /// \returns the actual length of the error message (including the final zero
    /// byte). If this is longer than the input parameter 'len', you know that the
    /// returned error string was truncated to fit in the provided buffer.
    ///
    BdalPRMSchedulerDllSpec uint32_t prm_scheduling_get_last_error_string(
        char *buf, 
        uint32_t actual_buffer_size
    );

    /// function type for a receiver that takes the PrmMethodInfo
    typedef void(prm_method_info_function)(
        const PrmMethodInfo *prm_method_info, //< the method info to transfer
        void *user_data //< capture emulation for the user space for the data
        );

    /// Get the PrmMethodInfo data obtained from the scheduler.sql file
    ///
    /// On success, returns 1 and you can request the results as a data structure using
    /// scheduling_prm_scheduling_results() and write it to the open sqlite file
    /// using scheduling_prm_write_scheduling_results()
    ///
    /// On failure, returns 0, and you can use scheduling_get_last_error_string() to obtain a
    /// string describing the problem.
    ///
    BdalPRMSchedulerDllSpec uint32_t prm_scheduling_get_method_info(
        uint64_t handle,
        prm_method_info_function *callback, //< callback accepting the method info
        void *user_data //< will be passed to callback, emulating a capture
    );

    /// Set the user parameters which define the PRM measurement in addition
    /// to the scheduled target list
    BdalPRMSchedulerDllSpec uint32_t prm_scheduling_set_additional_measurement_parameters(
        uint64_t handle,
        PrmAdditionalMeasurementParameters *parameters //< measurement parameters to set
    );

    /// Set the parameters for a direct injection collision energy ramp experiment
    ///
    /// In that case the retention times and CE values from the input data will be ignored
    /// and all targets will get measured again and again with an encreasing collision energy.
    /// Due to the complexity of the scheduling for large numbers of concurrent targets the frames
    /// will not filled up to measure targets multiple times.
    /// To disable this mode, set a NULL pointer or disable the collision_energy_ramping by flag
    BdalPRMSchedulerDllSpec uint32_t prm_scheduling_set_collision_energy_ramp_parameters(
        uint64_t handle,
        PrmCollisionEnergyRamp *parameters //< parameters for collision energy ramp
    );

    /// Add a single input target for scheduling
    ///
    /// \param prm_input_target target definition
    /// \param external_id string with an external identifier, can be nullptr or empty
    /// \param description string with a software specific description for the target, e.g. a sequence
    ///
    /// There is no clear() function yet, use prm_scheduling_close() and open() if different
    /// inputs are required.
    ///
    /// \returns error code
    /// On success, returns 1 
    /// On failure, returns 0, and you can use scheduling_get_last_error_string() to obtain a
    /// string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_add_input_target(
        uint64_t handle,
        const PrmInputTarget* prm_input_target,
        const char* external_id,
        const char* description
    );

    /// Add a single measurement mode
    ///
    /// Optional measurement modes allow the measurement of frames with different
    /// sets of parameters, especially the accumulation time
    ///
    /// \param prm_measurement_mode specification of a measurement mode, must not be nullptr
    /// \param external_id string with an external identifier, can be nullptr or empty empty
    ///        External Id typically specified by the software which is used to
    ///        design the PRM experiment(Skyline, SpectroDive).This id is
    ///        thought as a key to data structures within these tools
    ///
    /// There is no clear() function, use prm_scheduling_close() and open() if different
    /// inputs are required.
    ///
    /// \returns error code
    /// On success, returns 1 
    /// On failure, returns 0, and you can use scheduling_get_last_error_string() to obtain a
    /// string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_add_measurement_mode(
        uint64_t handle,
        const PrmMeasurementMode* prm_measurement_mode,
        const char* external_id
    );

    /// function type for a callback function checking whether calculation should be canceled
    /// and giving the percentage of progress
    /// true: cancel calculation
    /// false: continue
    typedef bool(prm_progress_cancel_function)(
        double progress_percentage,
        void *user_data_progress //< capture emulation for the user space for the progress
        );

    /// Determine a prm-PASEF target scheduling for the given targets
    ///
    /// \returns error code
    /// On success, returns 1 and you can request the results as a data structure using
    /// scheduling_prm_scheduling_results() and write it to the open sqlite file
    /// using scheduling_prm_write_scheduling_results()
    ///
    /// On failure, returns 0, and you can use scheduling_get_last_error_string() to obtain a
    /// string describing the problem.
    ///
    BdalPRMSchedulerDllSpec uint32_t prm_scheduling_prm_targets(
        uint64_t handle,
        prm_progress_cancel_function *callback_progress_cancel,
        void *user_data_progress //< will be passed to callback, emulating a capture
    );

    /// function type that takes over the PrmPasefSchedulingEntrys
    typedef void(prm_pasef_scheduling_entry_function)(
        uint32_t num_entries, //< number of scheduling entries
        const PrmPasefSchedulingEntry *prm_pasef_scheduling_entries, //< array of entries
        void *user_data //< capture emulation for the user space for the data
        );

    /// get the number of scheduling entries
    BdalPRMSchedulerDllSpec uint32_t prm_get_num_scheduling_entries(
        uint64_t handle
    );

    /// function type that takes over the PrmTimeSegments
    typedef void(prm_time_segments_function)(
        uint32_t num_entries, //< number of time segments
        const PrmTimeSegments *prm_time_segments, //< array of time segments
        void *user_data //< capture emulation for the user space for the data
        );

    /// get the number of time segments
    BdalPRMSchedulerDllSpec uint32_t prm_get_num_time_segments(
        uint64_t handle
    );

    /// get the prm scheduling results for further evaluation and visualization
    ///
    /// \param num_pasef_scheduling_entries
    BdalPRMSchedulerDllSpec uint32_t prm_get_scheduling(
        uint64_t handle,
        prm_pasef_scheduling_entry_function *callback_scheduling_entries,
        prm_time_segments_function *callback_time_segments,
        void *user_data_scheduling_entry, //< will be passed to callback, emulating a capture
        void *user_data_time_segments_entry //< will be passed to callback, emulating a capture
    );

    /// write the prm scheduling results to the given prmsqlite file
    ///
    /// On success, returns 1
    /// On failure, returns 0 and you can use scheduling_get_last_error_string() to
    /// obtain a string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_write_scheduling(
        uint64_t handle
    );

    /// get the number of value pairs for the visualization of the scheduling
    ///
    /// On success the number of data points are returned, required to allocate
    /// the data container taking up the values in the user function
    /// On failure, returns 0 and you can use scheduling_get_last_error_string() to
    /// obtain a string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_calculate_visualization(
        uint64_t handle,
        uint32_t pasef_scheduling_metric //< selected metric, see PRMPasefSchedulingMetric enum
    );

    /// function type that takes over the data for visualization
    typedef void(prm_visualization_points_function)(
        uint32_t num_entries, //< number of data points
        const PrmVisualizationDataPoint *data_points, //< data points for visualization
        void *user_data //< capture emulation for the user space for the data
        );

    /// get numeric values for a summary style visualization of the scheduling results
    /// the selection and calculation of the visualization is done in prm_calculate_visualization()
    ///
    /// On success, returns 1
    /// On failure, returns 0 and you can use scheduling_get_last_error_string() to
    /// obtain a string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_get_visualization(
        uint64_t handle,
        prm_visualization_points_function *callback_visualization_points,
        void *user_data_visualization_points //< will be passed to callback, emulating a capture
    );

    /// add a retention time standard
    ///
    /// In addition to the values given here you also have to specify the fragment ions for
    /// the standards. Consistency of the ids will be checked when prm_scheduling_prm_targets()
    /// is called. 
    /// In this function only a second definition with the same retention_time_standard_id
    /// will be rejected here.
    /// The order in which the retention time standards are added is important, since the
    /// addition of the fragment ion properties relates to it!
    ///
    /// On success, returns 1
    /// On failure, returns 0 and you can use scheduling_get_last_error_string() to
    /// obtain a string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_add_retention_time_standard(
        uint64_t handle,
        const PrmRetentionTimeStandard *retention_time_standard
    );

    /// add a retention time standard fragment
    ///
    /// Add the information required for the retention time fragments. The
    /// retention_time_standard_id is defined by the order in which the standards
    /// are added with prm_add_retention_time_standard. The first one has id = 0.
    /// Consistency of the ids will be checked when prm_get_scheduling() or 
    /// prm_write_scheduling() is called. A second fragment with the same m/z will
    /// be rejected if the id is the same.
    ///
    /// On success, returns 1
    /// On failure, returns 0 and you can use scheduling_get_last_error_string() to
    /// obtain a string describing the problem.
    BdalPRMSchedulerDllSpec uint32_t prm_add_retention_time_standard_fragment(
        uint64_t handle,
        const PrmRetentionTimeStandardFragment *retention_time_standard_fragment
    );


#ifdef __cplusplus
}
#endif


/* Local Variables:  */
/* mode: c           */
/* End:              */
