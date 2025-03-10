// MBIConstants.h - All constant keys / enum declarations.
/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2021 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Greg Van Aken                                           *
 * 0.0.0.0
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
#pragma once
#include <string>
#include <map>

namespace MBISDK
{
	/// @class MBISDK::MBIAttr
	/// @brief Set of file attributes (key/values).
	/// @author Greg Van Aken
	namespace MBIAttr
	{
		/// @class MBISDK::MBIAttr::Name
		/// @brief Names present in MBI files (groups / datasets).
		/// @author Greg Van Aken
		namespace Name
		{
			/// @brief HDF5 frame metadata table name
			static constexpr const char* DATA_DESCRIPTION = "data-description"; ///< Detailed (frame) metadata.
			/// @brief HDF5 global metadata table name
			static constexpr const char* GLOBAL_DESCRIPTION = "global-description"; ///< Global (file) metadata.
			/// @brief HDF5 intensity table name
			static constexpr const char* DATA_CUBES = "data-cubes"; ///< All datasets.
			/// @brief HDF5 frame intensity frame specific table name
			static constexpr const char* FRAME_DATA = "frame-%d-data"; ///< The datasets for a single frame p1.
			/// @brief HDF5 frame metadata frame specific table name
			static constexpr const char* FRAME_METADATA = "frame-%d-metadata"; ///< The metadata for a single frame p1.
			/// @brief HDF5 frame data intensity offset table name
			static constexpr const char* INDEX_COUNTS = "index-counts"; ///< Offsets into the data-counts array for each scan.
			/// @brief HDF5 frame data gate offset table name
			static constexpr const char* INDEX_POSITIONS = "index-positions"; ///< Offsets into the data-positions (gateS) array for each scan.
			/// @brief HDF5 frame data table name
			static constexpr const char* DATA_COUNTS = "data-counts"; ///< All of the intensity values recorded in a frame.
			/// @brief HDF5 frame data gate table name
			static constexpr const char* DATA_POSITIONS = "data-positions"; ///< All of the gate positions recorded in a frame.
			/// @brief HDF5 frame trigger timestamps table name
			static constexpr const char* TRIGGER_TIMESTAMPS = "trigger-timestamps"; ///< The timestamps (in seconds) of each scan in a frame.
			/// @brief HDF5 global rt_tic table name
			static constexpr const char* RT_TIC = "rt-tic"; ///< The total ion count for each frame.
			/// @brief HDF5 frame at_tic table name
			static constexpr const char* AT_TIC = "at-tic"; ///< The total ion count for each scan.
			/// @brief HDF5 CCS Calibration traditional metdata name
			static constexpr const char* CAL_CSS_TRADITIONAL = "cal-css-traditional"; ///< Traditional (json) ccs coefficients.
		};

		/// @class MBISDK::MBIAttr::FrameKey
		/// @brief Keys present in Frame Metadata
		/// @author Greg Van Aken
		namespace FrameKey
		{
			/// @brief CAL_DT_POLYNOMIAL metdata name
			static constexpr const char* CAL_DT_POLYNOMIAL     = "cal-dt-polynomial";
			/// @brief CAL_DT_POWER_FLAGS metdata name
			static constexpr const char* CAL_DT_POWER_FLAGS    = "cal-dt-power-flags";
			/// @brief CAL_DT_TRADITIONAL metdata name
			static constexpr const char* CAL_DT_TRADITIONAL    = "cal-dt-traditional"; ///< Traditional (json) ccs coefficients.
			/// @brief CAL_MS_POLYNOMIAL metdata name
			static constexpr const char* CAL_MS_POLYNOMIAL     = "cal-ms-polynomial";
			/// @brief CAL_MS_POWER_FLAGS metdata name
			static constexpr const char* CAL_MS_POWER_FLAGS    = "cal-ms-power-flags";
			/// @brief CAL_MS_TRADITIONAL metdata name
			static constexpr const char* CAL_MS_TRADITIONAL    = "cal-ms-traditional"; ///< Traditional (json) coefficients.
			/// @brief FRM_COLLISION_ENERGY metdata name
			static constexpr const char* FRM_COLLISION_ENERGY  = "frm-collision-energy";
			/// @brief FRM_DT_PERIOD metdata name
			static constexpr const char* FRM_DT_PERIOD         = "frm-dt-period"; ///< Average time (milliseconds) between scans.
			/// @brief FRM_FRAG_ENERGY metdata name
			static constexpr const char* FRM_FRAG_ENERGY       = "frm-frag-energy"; ///< The fragmentation energy
			/// @brief FRM_FRAG_ENERGY_MODE metdata name
			static constexpr const char* FRM_FRAG_ENERGY_MODE  = "frm-frag-energy-mode";
			/// @brief FRM_FRAG_OP_MODE metdata name
			static constexpr const char* FRM_FRAG_OP_MODE      = "frm-frag-op-mode"; ///< The fragmentation operational mode
			/// @brief FRM_INTENSITY_LIMIT metdata name
			static constexpr const char* FRM_INTENSITY_LIMIT   = "frm-intensity-limit";
			/// @brief FRM_METADATA_ID metdata name
			static constexpr const char* FRM_METADATA_ID       = "frm-metadata-id";
			/// @brief FRM_METHOD_NAME metdata name
			static constexpr const char* FRM_METHOD_NAME       = "frm-method-name";
			/// @brief FRM_METHOD_STATE metdata name
			static constexpr const char* FRM_METHOD_STATE      = "frm-method-state";
			/// @brief FRM_MUX_GATE metdata name
			static constexpr const char* FRM_MUX_GATE          = "frm-mux-gate";
			/// @brief FRM_MUX_SEQUENCE metdata name
			static constexpr const char* FRM_MUX_SEQUENCE      = "frm-mux-sequence";
			/// @brief FRM_NUM_SCANS metdata name
			static constexpr const char* FRM_NUM_SCANS         = "frm-num-bin-dt"; ///< Number of scans in the frame.
			/// @brief FRM_NUM_MICROFRAMES metdata name
			static constexpr const char* FRM_NUM_MICROFRAMES   = "frm-num-microframes";
			/// @brief FRM_POLARITY metdata name
			static constexpr const char* FRM_POLARITY          = "frm-polarity";
			/// @brief FRM_START_TIME metdata name
			static constexpr const char* FRM_START_TIME        = "frm-start-time"; ///< The time (seconds) of the first scan in this frame relative to the first scan of the first frame.
			/// @brief FRM_TIMIING_INTENTS metdata name
			static constexpr const char* FRM_TIMIING_INTENTS   = "frm-timing-intents";
			/// @brief KEY_FRAG metdata name
			static constexpr const char* KEY_FRAG              = "key-frag";
			/// @brief SLIM_RF_FUNNEL_POWER metdata name
			static constexpr const char* SLIM_RF_FUNNEL_POWER  = "slim-rf-funnel-power";
			/// @brief SLM_ENTRANCE_OFFSET metdata name
			static constexpr const char* SLM_ENTRANCE_OFFSET   = "slm-entrance-offset";
			/// @brief SLM_EXIT_CL metdata name
			static constexpr const char* SLM_EXIT_CL           = "slm-exit-cl";
			/// @brief SLM_EXIT_IN metdata name
			static constexpr const char* SLM_EXIT_IN           = "slm-exit-in";
			/// @brief SLM_EXIT_OUT metdata name
			static constexpr const char* SLM_EXIT_OUT          = "slm-exit-out";
			/// @brief SLM_FUNNEL_CL metdata name
			static constexpr const char* SLM_FUNNEL_CL         = "slm-funnel-cl";
			/// @brief SLM_FUNNEL_IN metdata name
			static constexpr const char* SLM_FUNNEL_IN         = "slm-funnel-in";
			/// @brief SLM_FUNNEL_OUT metdata name
			static constexpr const char* SLM_FUNNEL_OUT        = "slm-funnel-out";
			/// @brief SLM_OBA_GATE metdata name
			static constexpr const char* SLM_OBA_GATE          = "slm-oba-gate";
			/// @brief SLM_QUAD_BIAS metdata name
			static constexpr const char* SLM_QUAD_BIAS         = "slm-quad-bias";
			/// @brief SLM_RF_BOTTOM_DRIVE metdata name
			static constexpr const char* SLM_RF_BOTTOM_DRIVE   = "slm-rf-bottom-drive";
			/// @brief SLM_RF_BOTTOM_FREQ metdata name
			static constexpr const char* SLM_RF_BOTTOM_FREQ    = "slm-rf-bottom-freq";
			/// @brief SLM_RF_BOTTOM_NEG metdata name
			static constexpr const char* SLM_RF_BOTTOM_NEG     = "slm-rf-bottom-neg";
			/// @brief SLM_RF_BOTTOM_POS metdata name
			static constexpr const char* SLM_RF_BOTTOM_POS     = "slm-rf-bottom-pos";
			/// @brief SLM_RF_BOTTOM_POWER metdata name
			static constexpr const char* SLM_RF_BOTTOM_POWER   = "slm-rf-bottom-power";
			/// @brief SLM_RF_FUNNEL_DRIVE metdata name
			static constexpr const char* SLM_RF_FUNNEL_DRIVE   = "slm-rf-funnel-drive";
			/// @brief SLM_RF_FUNNEL_FREQ metdata name
			static constexpr const char* SLM_RF_FUNNEL_FREQ    = "slm-rf-funnel-freq";
			/// @brief SLM_RF_FUNNEL_NEG metdata name
			static constexpr const char* SLM_RF_FUNNEL_NEG     = "slm-rf-funnel-neg";
			/// @brief SLM_RF_FUNNEL_POS metdata name
			static constexpr const char* SLM_RF_FUNNEL_POS     = "slm-rf-funnel-pos";
			/// @brief SLM_RF_QUAD_DRIVE metdata name
			static constexpr const char* SLM_RF_QUAD_DRIVE     = "slm-rf-quad-drive";
			/// @brief SLM_RF_QUAD_FREQ metdata name
			static constexpr const char* SLM_RF_QUAD_FREQ      = "slm-rf-quad-freq";
			/// @brief SLM_RF_QUAD_NEG metdata name
			static constexpr const char* SLM_RF_QUAD_NEG       = "slm-rf-quad-neg";
			/// @brief SLM_RF_QUAD_POS metdata name
			static constexpr const char* SLM_RF_QUAD_POS       = "slm-rf-quad-pos";
			/// @brief SLM_QUAD_POWER metdata name
			static constexpr const char* SLM_QUAD_POWER        = "slm-rf-quad-power";
			/// @brief SLM_RF_TOP_DRIVE metdata name
			static constexpr const char* SLM_RF_TOP_DRIVE      = "slm-rf-top-drive";
			/// @brief SLM_RF_TOP_FREQ metdata name
			static constexpr const char* SLM_RF_TOP_FREQ       = "slm-rf-top-freq";
			/// @brief SLM_RF_TOP_NEG metdata name
			static constexpr const char* SLM_RF_TOP_NEG        = "slm-rf-top-neg";
			/// @brief SLM_RF_TOP_POS metdata name
			static constexpr const char* SLM_RF_TOP_POS        = "slm-rf-top-pos";
			/// @brief SLM_RF_TOP_POWER metdata name
			static constexpr const char* SLM_RF_TOP_POWER      = "slm-rf-top-power";
			/// @brief SLM_SLIM_BIAS metdata name
			static constexpr const char* SLM_SLIM_BIAS         = "slm-slim-bias";
			/// @brief SLM_SLIM_OFFSET metdata name
			static constexpr const char* SLM_SLIM_OFFSET       = "slm-slim-offset";
			/// @brief SLM_TW_OBA_AMP metdata name
			static constexpr const char* SLM_TW_OBA_AMP        = "slm-tw-oba-amp";
			/// @brief SLM_TW_OBA_AUX metdata name
			static constexpr const char* SLM_TW_OBA_AUX        = "slm-tw-oba-aux";
			/// @brief SLM_TW_OBA_DIR metdata name
			static constexpr const char* SLM_TW_OBA_DIR        = "slm-tw-oba-dir";
			/// @brief SLM_TW_OBA_FREQ metdata name
			static constexpr const char* SLM_TW_OBA_FREQ       = "slm-tw-oba-freq";
			/// @brief SLM_TW_OBA_OFFSET metdata name
			static constexpr const char* SLM_TW_OBA_OFFSET     = "slm-tw-oba-offset";
			/// @brief SLM_TW_OBA_WAVEFORM metdata name
			static constexpr const char* SLM_TW_OBA_WAVEFORM   = "slm-tw-oba-waveform";
			/// @brief SLM_TW_SEP_AMP metdata name
			static constexpr const char* SLM_TW_SEP_AMP        = "slm-tw-sep-amp";
			/// @brief SLM_TW_SEP_AUX metdata name
			static constexpr const char* SLM_TW_SEP_AUX        = "slm-tw-sep-aux";
			/// @brief SLM_TW_SEP_DIR metdata name
			static constexpr const char* SLM_TW_SEP_DIR        = "slm-tw-sep-dir";
			/// @brief SLM_TW_SEP_FREQ metdata name
			static constexpr const char* SLM_TW_SEP_FREQ       = "slm-tw-sep-freq";
			/// @brief SLM_TW_SEP_OFFSET metdata name
			static constexpr const char* SLM_TW_SEP_OFFSET     = "slm-tw-sep-offset";
			/// @brief SLM_TW_SEP_WAVEFORM metdata name
			static constexpr const char* SLM_TW_SEP_WAVEFORM   = "slm-tw-sep-waveform";
			/// @brief SLM_TW_WASTE_AMP metdata name
			static constexpr const char* SLM_TW_WASTE_AMP      = "slm-tw-waste-amp";
			/// @brief SLM_TW_WASTE_AUX metdata name
			static constexpr const char* SLM_TW_WASTE_AUX      = "slm-tw-waste-aux";
			/// @brief SLM_TW_WASTE_DIR metdata name
			static constexpr const char* SLM_TW_WASTE_DIR      = "slm-tw-waste-dir";
			/// @brief SLM_TW_WASTE_FREQ metdata name
			static constexpr const char* SLM_TW_WASTE_FREQ     = "slm-tw-waste-freq";
			/// @brief SLM_TW_WASTE_OFFSET metdata name
			static constexpr const char* SLM_TW_WASTE_OFFSET   = "slm-tw-waste-offset";
			/// @brief SLM_TW_WASTE_WAVEFORM metdata name
			static constexpr const char* SLM_TW_WASTE_WAVEFORM = "slm-tw-waste-waveform";
		};

		namespace FrameValue
		{
			/// @brief HI_LO_FRAG metdata name
			static constexpr const char* HI_LO_FRAG = "HiLoFrag"; ///< Fragmentation op mode of HiLoFrag
		};

		/// @class MBISDK::MBIAttr::TofCalibrationKey
		/// @brief Keys present in tof calibration metadata
		/// @author Greg Van Aken
		namespace TofCalibrationKey
		{
			/// @brief SLOPE metdata name
			static constexpr const char* SLOPE = "slope";
			/// @brief INTERCEPT metdata name
			static constexpr const char* INTERCEPT = "intercept";
			/// @brief RESIDUAL_FIT metdata name
			static constexpr const char* RESIDUAL_FIT = "mz_residual_terms";
		};

		/// @class MBISDK::MBIAttr::CcsCalibrationKey
		/// @brief Keys present in ccs calibration metadata
		/// @author Greg Van Aken
		namespace CcsCalibrationKey
		{
			/// @brief TYPE metdata name
			static constexpr const char* TYPE = "type";
			/// @brief COEFFICIENTS metdata name
			static constexpr const char* COEFFICIENTS = "coefficients";
			/// @brief CSS_COEFFICIENTS_HRIM metdata name
			static constexpr const char* CSS_COEFFICIENTS_HRIM = "css_coefficients";
			/// @brief AT_COEFFICIENTS metdata name
			static constexpr const char* AT_COEFFICIENTS = "at_coefficients";
			/// @brief MZ_COEFFICIENTS metdata name
			static constexpr const char* MZ_COEFFICIENTS = "mz_coefficients";
		};

		/// @class MBISDK::MBIAttr::CcsCalibrationValue
		/// @brief Values present in ccs calibration metadata
		/// @author Greg Van Aken
		namespace CcsCalibrationValue
		{
			/// @brief POLYNOMIAL metdata name
			static constexpr const char* POLYNOMIAL = "polynomial";
			/// @brief UNKNOWN metdata name
			static constexpr const char* UNKNOWN = "unknown";
		};

		/// @class MBISDK::MBIAttr::EyeOnCcsCalibrationKey
		/// @brief Keys present in ccs calibration metadata
		/// @author Greg Van Aken
		namespace EyeOnCcsCalibrationKey
		{
			/// @brief PEAKS metdata name
			static constexpr const char* PEAKS = "peaks";
			/// @brief CSS_COEFFICIENTS metdata name
			static constexpr const char* CSS_COEFFICIENTS = "coefficients";
			/// @brief MIN_CCS metdata name
			static constexpr const char* MIN_CCS = "min";
			/// @brief MAX_CCS metdata name
			static constexpr const char* MAX_CCS = "max";
			/// @brief CCS_DEGREE metdata name
			static constexpr const char* CCS_DEGREE = "degree";
			/// @brief CCS_AT_SURF metdata name
			static constexpr const char* CCS_AT_SURF = "at_surfing";
			/// @brief CCS_CCAPS metdata name
			static constexpr const char* CCS_CCAPS = "ccaps";
			/// @brief GAS_TYPE metdata name
			static constexpr const char* GAS_TYPE = "Mass Flow.gas type";
		};


		/// @class MBISDK::MBIAttr::GlobalKey
		/// @brief Keys present in Global Metadata
		/// @author Greg Van Aken
		namespace GlobalKey
		{
			/// @brief ACQ_AGT_FILEPATH metdata name
			static constexpr const char* ACQ_AGT_FILEPATH              = "acq-agt-filepath";
			/// @brief ACQ_BOARD_TEMP metdata name
			static constexpr const char* ACQ_BOARD_TEMP                = "acq-board-temp";
			/// @brief Data collection mode (SIFF, MIFF, etc)
			static constexpr const char* ACQ_COLLECTION_MODE           = "acq-collection-mode";
			/// @brief ACQ_COMMENTS metdata name
			static constexpr const char* ACQ_COMMENTS                  = "acq-comments";
			/// @brief ACQ_LC_MODEL metdata name
			static constexpr const char* ACQ_LC_MODEL                  = "acq-lc-model";
			/// @brief ACQ_MS_LEVEL metadata name
			static constexpr const char* ACQ_MS_LEVEL                  = "acq-ms-level";
			/// @brief ACQ_MS_METHOD metdata name
			static constexpr const char* ACQ_MS_METHOD                 = "acq-ms-method";
			/// @brief ACQ_MS_MODEL metdata name
			static constexpr const char* ACQ_MS_MODEL                  = "acq-ms-model";
			/// @brief ACQ_NUM_FRAMES metdata name
			static constexpr const char* ACQ_NUM_FRAMES                = "acq-num-frames"; ///< Number of frames in the file.
			/// @brief ACQ_SLIM_METHOD metdata name
			static constexpr const char* ACQ_SLIM_METHOD               = "acq-slim-method";
			/// @brief ACQ_SLIM_MODEL metdata name
			static constexpr const char* ACQ_SLIM_MODEL                = "acq-slim-model";
			/// @brief ACQ_SLIM_PATH_LENGTH metdata name
			static constexpr const char* ACQ_SLIM_PATH_LENGTH          = "acq-slim_path_length";
			/// @brief ACQ_SOFTWARE_VERSION metdata name
			static constexpr const char* ACQ_SOFTWARE_VERSION          = "acq-software-version";
			/// @brief ACQ_TAGS metdata name
			static constexpr const char* ACQ_TAGS                      = "acq-tags";
			/// @brief ACQ_TEMPERATURE_CORRECTION metdata name
			static constexpr const char* ACQ_TEMPERATURE_CORRECTION    = "acq-temperature_correction";
			/// @brief ACQ_TIMESTAMP metdata name
			static constexpr const char* ACQ_TIMESTAMP                 = "acq-timestamp";
			/// @brief ACQ_TUNE_FILE metdata name
			static constexpr const char* ACQ_TUNE_FILE                 = "acq-tune-file";
			/// @brief ACQ_TYPE metdata name
			static constexpr const char* ACQ_TYPE                      = "acq-type";
			/// @brief ACQ_VENDOR_METADATA metdata name
			static constexpr const char* ACQ_VENDOR_METADATA           = "acq-vendor-metadata";
			/// @brief ADC_AVG_MODE_ENABLE metdata name
			static constexpr const char* ADC_AVG_MODE_ENABLE           = "adc-avg-mode-enable";
			/// @brief ADC_AVG_MODE_FREQUENCY metdata name
			static constexpr const char* ADC_AVG_MODE_FREQUENCY        = "adc-avg-mode-frequency";
			/// @brief ADC_AVG_MODE_RESCALE metdata name
			static constexpr const char* ADC_AVG_MODE_RESCALE          = "adc-avg-mode-rescale";
			/// @brief ADC_BASELINE_STABILIZE_ENABLE metdata name
			static constexpr const char* ADC_BASELINE_STABILIZE_ENABLE = "adc-baseline-stabilize-enable";
			/// @brief ADC_BASELINE_STABILIZE_MODE metdata name
			static constexpr const char* ADC_BASELINE_STABILIZE_MODE   = "adc-baseline-stabilize-mode";
			/// @brief ADC_CHANNEL metdata name
			static constexpr const char* ADC_CHANNEL                   = "adc-channel";
			/// @brief ADC_COUPLING metdata name
			static constexpr const char* ADC_COUPLING                  = "adc-coupling";
			/// @brief ADC_DIGITAL_OFFSET metdata name
			static constexpr const char* ADC_DIGITAL_OFFSET            = "adc-digital-offset";
			/// @brief ADC_DRIVER_REV metdata name
			static constexpr const char* ADC_DRIVER_REV                = "adc-driver-rev";
			/// @brief ADC_FIRMWARE_REV metdata name
			static constexpr const char* ADC_FIRMWARE_REV              = "adc-firmware-rev";
			/// @brief ADC_MASS_SPEC_RANGE metdata name
			static constexpr const char* ADC_MASS_SPEC_RANGE           = "adc-mass-spec-range";
			/// @brief ADC_MIN_NANOSECONDS metdata name
			static constexpr const char* ADC_MIN_NANOSECONDS           = "adc-min-nanoseconds";
			/// @brief ADC_MODEL metdata name
			static constexpr const char* ADC_MODEL                     = "adc-model";
			/// @brief ADC_OFFSET metdata name
			static constexpr const char* ADC_OFFSET                    = "adc-offset";
			/// @brief ADC_PULSE_POLARITY metdata name
			static constexpr const char* ADC_PULSE_POLARITY            = "adc-pulse-polarity";
			/// @brief ADC_PULSE_THRESHOLD metdata name
			static constexpr const char* ADC_PULSE_THRESHOLD           = "adc-pulse-threshold";
			/// @brief ADC_RANGE metdata name
			static constexpr const char* ADC_RANGE                     = "adc-range";
			/// @brief ADC_RECORD_SIZE metdata name
			static constexpr const char* ADC_RECORD_SIZE               = "adc-record-size";
			/// @brief ADC_REDUCTION_MODE metdata name
			static constexpr const char* ADC_REDUCTION_MODE            = "adc-reduction-mode";
			/// @brief ADC_SAMPLE_RATE metdata name
			static constexpr const char* ADC_SAMPLE_RATE               = "adc-sample-rate"; ///< Digitizer sample rate.
			/// @brief ADC_SELF_TRIGGER_DUTY_CYCLE metdata name
			static constexpr const char* ADC_SELF_TRIGGER_DUTY_CYCLE   = "adc-self-trigger-duty-cycle";
			/// @brief ADC_SELF_TRIGGER_ENABLE metdata name
			static constexpr const char* ADC_SELF_TRIGGER_ENABLE       = "adc-self-trigger-enable";
			/// @brief ADC_SELF_TRIGGER_FREQUENCY metdata name
			static constexpr const char* ADC_SELF_TRIGGER_FREQUENCY    = "adc-self-trigger-frequency";
			/// @brief ADC_SELF_TRIGGER_POLARITY metdata name
			static constexpr const char* ADC_SELF_TRIGGER_POLARITY     = "adc-self-trigger-polarity";
			/// @brief ADC_STREAMING_MODE metdata name
			static constexpr const char* ADC_STREAMING_MODE            = "adc-streaming-mode";
			/// @brief ADC_ZA_HYSTERESIS metdata name
			static constexpr const char* ADC_ZA_HYSTERESIS             = "adc-zs-hysteresis";
			/// @brief ADC_ZA_POSTGATE_SAMPLES metdata name
			static constexpr const char* ADC_ZA_POSTGATE_SAMPLES       = "adc-zs-postgate-samples";
			/// @brief ADC_ZA_PREGATE_SAMPLES metdata name
			static constexpr const char* ADC_ZA_PREGATE_SAMPLES        = "adc-zs-pregate-samples";
			/// @brief ADC_ZA_THRESHOLD metdata name
			static constexpr const char* ADC_ZA_THRESHOLD              = "adc-zs-threshold";
			/// @brief ADC_ZERO_VALUE metdata name
			static constexpr const char* ADC_ZERO_VALUE                = "adc-zero-value";
			/// @brief CAL_CCS metdata name
			static constexpr const char* CAL_CCS                       = "cal-ccs";
			/// @brief GAS_MASS metdata name
			static constexpr const char* GAS_MASS                      = "gas-mass";
			/// @brief MERGED_FILE metdata name
			static constexpr const char* MERGED_FILE = "merged-file";
			/// @brief READINGS metdata name
			static constexpr const char* READINGS                      = "readings";
			/// @brief SMP_AMOUNT metdata name
			static constexpr const char* SMP_AMOUNT                    = "smp-amount";
			/// @brief SMP_AMOUNT_UNIT metdata name
			static constexpr const char* SMP_AMOUNT_UNIT               = "smp-amount-unit";
			/// @brief SMP_BALANCE_TYPE metdata name
			static constexpr const char* SMP_BALANCE_TYPE              = "smp-balance-type";
			/// @brief SMP_CONCENTRATION metdata name
			static constexpr const char* SMP_CONCENTRATION             = "smp-concentration";
			/// @brief SMP_CONCENTRATION_UNIT metdata name
			static constexpr const char* SMP_CONCENTRATION_UNIT        = "smp-concentration-unit";
			/// @brief SMP_DILUTION_FACTOR metdata name
			static constexpr const char* SMP_DILUTION_FACTOR           = "smp-dilution-factor";
			/// @brief SMP_INJECTION_VOLUME metdata name
			static constexpr const char* SMP_INJECTION_VOLUME          = "smp-injection-volume";
			/// @brief SMP_MOLECULE_CLASS metdata name
			static constexpr const char* SMP_MOLECULE_CLASS            = "smp-molecule-class";
			/// @brief SMP_NAME metdata name
			static constexpr const char* SMP_NAME                      = "smp-name";
			/// @brief SMP_PLATE_CODE metdata name
			static constexpr const char* SMP_PLATE_CODE                = "smp-plate-code";
			/// @brief SMP_PLATE_POSITION metdata name
			static constexpr const char* SMP_PLATE_POSITION            = "smp-plate-position";
			/// @brief SMP_POSITION metdata name
			static constexpr const char* SMP_POSITION                  = "smp-position";
			/// @brief SMP_RACK_CODE metdata name
			static constexpr const char* SMP_RACK_CODE                 = "smp-rack-code";
			/// @brief SMP_SOLVENT metdata name
			static constexpr const char* SMP_SOLVENT                   = "smp-solvent";
			/// @brief SMP_TYPE metdata name
			static constexpr const char* SMP_TYPE                      = "smp-type";
			/// @brief STITCHED_FILE metdata name
			static constexpr const char* STITCHED_FILE                 = "stitched-file";
			/// @brief USR_GROUP metdata name
			static constexpr const char* USR_GROUP                     = "usr-group";
			/// @brief USR_ROLE metdata name
			static constexpr const char* USR_ROLE                      = "usr-role";
			/// @brief USR_USERNAME metdata name
			static constexpr const char* USR_USERNAME                  = "usr-username";

			// This is the list of metadata items which are considered "redundant", in that they do not change from one frame
			// to the next, and can afford to be stored and read from the global metadata list instead.
			// while the text of the metadata item will be the same, the ENUM value will carry the GMD prefix to help
			// associate these entries as global metadata items instead of frame metadata items.
			// To maintain backwards compatability, the frame metadata functions will be changed to seek out the frame metadata value first,
			// and then look for the global metadata item should the frame metadata item not be found
			/// @brief GMD_CAL_DT_POLYNOMIAL metdata name
			static constexpr const char* GMD_CAL_DT_POLYNOMIAL     = "cal-dt-polynomial";
			/// @brief GMD_CAL_DT_POWER_FLAGS metdata name
			static constexpr const char* GMD_CAL_DT_POWER_FLAGS    = "cal-dt-power-flags";
			/// @brief GMD_CAL_DT_TRADITIONAL metdata name
			static constexpr const char* GMD_CAL_DT_TRADITIONAL    = "cal-dt-traditional"; ///< Traditional (json) ccs coefficients.
			/// @brief GMD_CAL_MS_POLYNOMIAL metdata name
			static constexpr const char* GMD_CAL_MS_POLYNOMIAL     = "cal-ms-polynomial";
			/// @brief GMD_CAL_MS_POWER_FLAGS metdata name
			static constexpr const char* GMD_CAL_MS_POWER_FLAGS    = "cal-ms-power-flags";
			/// @brief GMD_CAL_MS_TRADITIONAL metdata name
			static constexpr const char* GMD_CAL_MS_TRADITIONAL    = "cal-ms-traditional"; ///< Traditional (json) coefficients.
			/// @brief GMD_FRM_FRAG_ENERGY_MODE metdata name
			static constexpr const char* GMD_FRM_FRAG_ENERGY_MODE  = "frm-frag-energy-mode";
			/// @brief GMD_FRM_FRAG_OP_MODE metdata name
			static constexpr const char* GMD_FRM_FRAG_OP_MODE      = "frm-frag-op-mode"; ///< The fragmentation operational mode
			/// @brief GMD_FRM_INTENSITY_LIMIT metdata name
			static constexpr const char* GMD_FRM_INTENSITY_LIMIT   = "frm-intensity-limit";
			/// @brief GMD_FRM_METHOD_STATE metdata name
			static constexpr const char* GMD_FRM_METHOD_STATE      = "frm-method-state";
			/// @brief GMD_FRM_MUX_GATE metdata name
			static constexpr const char* GMD_FRM_MUX_GATE          = "frm-mux-gate";
			/// @brief GMD_FRM_MUX_SEQUENCE metdata name
			static constexpr const char* GMD_FRM_MUX_SEQUENCE      = "frm-mux-sequence";
			/// @brief GMD_FRM_NUM_MICROFRAMES metdata name
			static constexpr const char* GMD_FRM_NUM_MICROFRAMES   = "frm-num-microframes";
			/// @brief GMD_FRM_POLARITY metdata name
			static constexpr const char* GMD_FRM_POLARITY          = "frm-polarity";
			/// @brief GMD_FRM_TIMIING_INTENTS metdata name
			static constexpr const char* GMD_FRM_TIMIING_INTENTS   = "frm-timing-intents";
			/// @brief GMD_KEY_FRAG metdata name
			static constexpr const char* GMD_KEY_FRAG              = "key-frag";
			/// @brief GMD_SLIM_RF_FUNNEL_POWER metdata name
			static constexpr const char* GMD_SLIM_RF_FUNNEL_POWER  = "slim-rf-funnel-power";
			/// @brief GMD_SLM_ENTRANCE_OFFSET metdata name
			static constexpr const char* GMD_SLM_ENTRANCE_OFFSET   = "slm-entrance-offset";
			/// @brief GMD_SLM_EXIT_CL metdata name
			static constexpr const char* GMD_SLM_EXIT_CL           = "slm-exit-cl";
			/// @brief GMD_SLM_EXIT_IN metdata name
			static constexpr const char* GMD_SLM_EXIT_IN           = "slm-exit-in";
			/// @brief GMD_SLM_EXIT_OUT metdata name
			static constexpr const char* GMD_SLM_EXIT_OUT          = "slm-exit-out";
			/// @brief GMD_SLM_FUNNEL_CL metdata name
			static constexpr const char* GMD_SLM_FUNNEL_CL         = "slm-funnel-cl";
			/// @brief GMD_SLM_FUNNEL_IN metdata name
			static constexpr const char* GMD_SLM_FUNNEL_IN         = "slm-funnel-in";
			/// @brief GMD_SLM_FUNNEL_OUT metdata name
			static constexpr const char* GMD_SLM_FUNNEL_OUT        = "slm-funnel-out";
			/// @brief GMD_SLM_OBA_GATE metdata name
			static constexpr const char* GMD_SLM_OBA_GATE          = "slm-oba-gate";
			/// @brief GMD_SLM_QUAD_BIAS metdata name
			static constexpr const char* GMD_SLM_QUAD_BIAS         = "slm-quad-bias";
			/// @brief GMD_SLM_RF_BOTTOM_DRIVE metdata name
			static constexpr const char* GMD_SLM_RF_BOTTOM_DRIVE   = "slm-rf-bottom-drive";
			/// @brief GMD_SLM_RF_BOTTOM_FREQ metdata name
			static constexpr const char* GMD_SLM_RF_BOTTOM_FREQ    = "slm-rf-bottom-freq";
			/// @brief GMD_SLM_RF_BOTTOM_NEG metdata name
			static constexpr const char* GMD_SLM_RF_BOTTOM_NEG     = "slm-rf-bottom-neg";
			/// @brief GMD_SLM_RF_BOTTOM_POS metdata name
			static constexpr const char* GMD_SLM_RF_BOTTOM_POS     = "slm-rf-bottom-pos";
			/// @brief GMD_SLM_RF_BOTTOM_POWER metdata name
			static constexpr const char* GMD_SLM_RF_BOTTOM_POWER   = "slm-rf-bottom-power";
			/// @brief GMD_SLM_RF_FUNNEL_DRIVE metdata name
			static constexpr const char* GMD_SLM_RF_FUNNEL_DRIVE   = "slm-rf-funnel-drive";
			/// @brief GMD_SLM_RF_FUNNEL_FREQ metdata name
			static constexpr const char* GMD_SLM_RF_FUNNEL_FREQ    = "slm-rf-funnel-freq";
			/// @brief GMD_SLM_RF_FUNNEL_NEG metdata name
			static constexpr const char* GMD_SLM_RF_FUNNEL_NEG     = "slm-rf-funnel-neg";
			/// @brief GMD_SLM_RF_FUNNEL_POS metdata name
			static constexpr const char* GMD_SLM_RF_FUNNEL_POS     = "slm-rf-funnel-pos";
			/// @brief GMD_SLM_RF_QUAD_DRIVE metdata name
			static constexpr const char* GMD_SLM_RF_QUAD_DRIVE     = "slm-rf-quad-drive";
			/// @brief GMD_SLM_RF_QUAD_FREQ metdata name
			static constexpr const char* GMD_SLM_RF_QUAD_FREQ      = "slm-rf-quad-freq";
			/// @brief GMD_SLM_RF_QUAD_NEG metdata name
			static constexpr const char* GMD_SLM_RF_QUAD_NEG       = "slm-rf-quad-neg";
			/// @brief GMD_SLM_RF_QUAD_POS metdata name
			static constexpr const char* GMD_SLM_RF_QUAD_POS       = "slm-rf-quad-pos";
			/// @brief GMD_SLM_QUAD_POWER metdata name
			static constexpr const char* GMD_SLM_QUAD_POWER        = "slm-rf-quad-power";
			/// @brief GMD_SLM_RF_TOP_DRIVE metdata name
			static constexpr const char* GMD_SLM_RF_TOP_DRIVE      = "slm-rf-top-drive";
			/// @brief GMD_SLM_RF_TOP_FREQ metdata name
			static constexpr const char* GMD_SLM_RF_TOP_FREQ       = "slm-rf-top-freq";
			/// @brief GMD_SLM_RF_TOP_NEG metdata name
			static constexpr const char* GMD_SLM_RF_TOP_NEG        = "slm-rf-top-neg";
			/// @brief GMD_SLM_RF_TOP_POS metdata name
			static constexpr const char* GMD_SLM_RF_TOP_POS        = "slm-rf-top-pos";
			/// @brief GMD_SLM_RF_TOP_POWER metdata name
			static constexpr const char* GMD_SLM_RF_TOP_POWER      = "slm-rf-top-power";
			/// @brief GMD_SLM_SLIM_BIAS metdata name
			static constexpr const char* GMD_SLM_SLIM_BIAS         = "slm-slim-bias";
			/// @brief GMD_SLM_SLIM_OFFSET metdata name
			static constexpr const char* GMD_SLM_SLIM_OFFSET       = "slm-slim-offset";
			/// @brief GMD_SLM_TW_OBA_AMP metdata name
			static constexpr const char* GMD_SLM_TW_OBA_AMP        = "slm-tw-oba-amp";
			/// @brief GMD_SLM_TW_OBA_AUX metdata name
			static constexpr const char* GMD_SLM_TW_OBA_AUX        = "slm-tw-oba-aux";
			/// @brief GMD_SLM_TW_OBA_DIR metdata name
			static constexpr const char* GMD_SLM_TW_OBA_DIR        = "slm-tw-oba-dir";
			/// @brief GMD_SLM_TW_OBA_FREQ metdata name
			static constexpr const char* GMD_SLM_TW_OBA_FREQ       = "slm-tw-oba-freq";
			/// @brief GMD_SLM_TW_OBA_OFFSET metdata name
			static constexpr const char* GMD_SLM_TW_OBA_OFFSET     = "slm-tw-oba-offset";
			/// @brief GMD_SLM_TW_OBA_WAVEFORM metdata name
			static constexpr const char* GMD_SLM_TW_OBA_WAVEFORM   = "slm-tw-oba-waveform";
			/// @brief GMD_SLM_TW_SEP_AMP metdata name
			static constexpr const char* GMD_SLM_TW_SEP_AMP        = "slm-tw-sep-amp";
			/// @brief GMD_SLM_TW_SEP_AUX metdata name
			static constexpr const char* GMD_SLM_TW_SEP_AUX        = "slm-tw-sep-aux";
			/// @brief GMD_SLM_TW_SEP_DIR metdata name
			static constexpr const char* GMD_SLM_TW_SEP_DIR        = "slm-tw-sep-dir";
			/// @brief GMD_SLM_TW_SEP_FREQ metdata name
			static constexpr const char* GMD_SLM_TW_SEP_FREQ       = "slm-tw-sep-freq";
			/// @brief GMD_SLM_TW_SEP_OFFSET metdata name
			static constexpr const char* GMD_SLM_TW_SEP_OFFSET     = "slm-tw-sep-offset";
			/// @brief GMD_SLM_TW_SEP_WAVEFORM metdata name
			static constexpr const char* GMD_SLM_TW_SEP_WAVEFORM   = "slm-tw-sep-waveform";
			/// @brief GMD_SLM_TW_WASTE_AMP metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_AMP      = "slm-tw-waste-amp";
			/// @brief GMD_SLM_TW_WASTE_AUX metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_AUX      = "slm-tw-waste-aux";
			/// @brief GMD_SLM_TW_WASTE_DIR metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_DIR      = "slm-tw-waste-dir";
			/// @brief GMD_SLM_TW_WASTE_FREQ metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_FREQ     = "slm-tw-waste-freq";
			/// @brief GMD_SLM_TW_WASTE_OFFSET metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_OFFSET   = "slm-tw-waste-offset";
			/// @brief GMD_SLM_TW_WASTE_WAVEFORM metdata name
			static constexpr const char* GMD_SLM_TW_WASTE_WAVEFORM = "slm-tw-waste-waveform";
		};
	};

	/// @class MBISDK::MBINumeric
	/// @brief Numeric constants for math and conversions
	/// @author Greg Van Aken
	namespace MBINumeric
	{
		/// @brief NANOSECONDS_PER_SECOND numeric constant
		static constexpr const double NANOSECONDS_PER_SECOND = 1e9; ///< Number of nanoseconds in a second.
		/// @brief MICROSECONDS_PER_SECOND numeric constant
		static constexpr const double MICROSECONDS_PER_SECOND = 1e6; ///< Number of microseconds in a second.
		/// @brief MILLISECONDS_PER_SECOND numeric constant
		static constexpr const double MILLISECONDS_PER_SECOND = 1e3; ///< Number of milliseconds in a second.
	};

	/// @class MBISDK::MBIFileAcces
	/// @brief The ways in which files can be accessed / initialized
	/// @author Greg Van Aken
	namespace MBIFileAccess
	{
		/// @brief READONLY file access constant
		static constexpr const char* READONLY = "r";
		/// @brief WRITE file access constant
		static constexpr const char* WRITE = "w";
		/// @brief APPEND file access constant
		static constexpr const char* APPEND = "a";
	};

}
