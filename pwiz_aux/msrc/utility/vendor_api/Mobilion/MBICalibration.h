// MBICalibration.h - Mass / CCS calibration information to associate with data
/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2021 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Greg Van Aken                                           *
 * 0.0.0.0
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

#pragma once
#ifndef MBI_DLLCPP
#ifdef SWIG_WIN
#define MBI_DLLCPP
#else
#ifdef MBI_EXPORTS
#define MBI_DLLCPP __declspec(dllexport)
#else
#define MBI_DLLCPP __declspec(dllimport)
#endif
#endif
#endif

#include <vector>
#include <string>
#include <map>

namespace MBISDK
{
class MBIFile;
class Frame;

extern "C"
{
	/*! @class MBISDK::TofCalibration
	*   @brief A calibration object that holds coefficients to convert to/from ToF values from/to mass-to-charge values.
	*/

	// Constants for Newton method
	const int ATTEMPT_MAX = (int)(1e6);
	const double TOLERANCE = 1e-8;

	/*
	* CalPolynomial is a polynomial function class. Can be used for Mass calibration and CCS calibration
	* The first coefficient has the highest order and the last coefficient has the lowest order (constant)
	*/
	class MBI_DLLCPP CalPolynomial
	{
	public:
		/// @brief Initialize a polynomial object with coefficients
		CalPolynomial(std::vector<double> coefficients);

		/// @brief Compute the y from x
		double YFromX(double x);

		/// @brief compute the y' at x
		double DerivativeAtX(double x);

		/// @brief degree of polynomial
		int GetDegree();

	private:
		std::vector<double> _coefficients; // y = a0*x^3 + a1*x^2 + a2*x + a3
	};

	/*
	* Bisection Search is a numerical method looking for X satisfing Y for a monotonic function y = f(x)
	* It is not as faster as Newton method and it will never fail for a montonic function
	*/
	class MBI_DLLCPP CCSBisectionSearch
	{
	public:
		/// @brief Initialize a BisectionSearch object with polynomial, target, and epsilon
		CCSBisectionSearch(CalPolynomial* CCSpolymonial);

		/// @brief Start to search a point close enough to the target
		double Search();

		/// @brief Change epsilon 
		void NewEpsilion(double epsilon);

		/// @brief set search Target
		void NewTarget(double target);

		/// @brief get epsilon
		double getEpsilon();


	private:
		CalPolynomial* _CCSpolynomial;
		double _target = 0.0;
		double _epsilon = 0.1;
		double _lowEnd = 0.0;
		double _highEnd = 100.0;
	};

	/* 
	* Newton Method is a numerical method looking for a root of a real - valued function
	* Xn+1 = Xn - f(Xn) / f'(Xn)
	* f(X) is the real-valued function and f'(X) is the derivate function
	* Newton Method need a starting point X0 and then X1 is calculated from X0, X2 is calculated from X1, ...
	* If the newton method is converging, Xn+1 is closer to the root than Xn, otherwise the method fails.
	* Finally, if (Xn+1 - Xn) is smaller than a predefined value, the iteration stops and return Xn+1 as the root.
	*/
	class MBI_DLLCPP NewtonMethod
	{
	public:
		/// @brief Initialize a NewtonMethod object with polynomial
		NewtonMethod(CalPolynomial* polynomial);

		/// @brief Calculate Y from X with Newton method
		double RootStartFromX(double startingX);

		/// @brief if Newton method Success
		bool isSuccess();

	private:
		CalPolynomial* _polynomial;
		bool _success = false;
	};
	
	class MBI_DLLCPP TofCalibration
	{
	public:
		/// @brief Initialize a calibration object
		TofCalibration();

		/// @brief Initialize a calibration object from json representation.
		TofCalibration(double sampleRate, const char* jsonString);

		/// @brief Initialize a calibration object from coefficients.
		TofCalibration(double sampleRate, double slope, double intercept, std::string strStatus, int nFailureStatus);

		/// @brief Initialize a calibration object from coefficients with mass correction.
		TofCalibration(double sampleRate, double slope, double intercept, std::vector<double> residualFit, std::string strStatus, int nFailureStatus);

		/// @brief Compute mass error from polynomial residual fit.
		double TofError(double uSecTOF);

		/// @brief Convert tof bin index to time-of-flight
		double IndexToMicroseconds(int64_t index);

		/// @brief Convert time-of-flight to m/z using this calibration.
		double MicrosecondsToMz(double uSec);

		/// @brief Convert tof bin index to m/z using this calibration.
		double IndexToMz(int64_t index);

		/// @brief Convert time-of-flight to tof bin index.
		size_t MicrosecondsToIndex(double uSec);

		/// @brief Convert m/z to time-of-flight using this calibration.
		double MzToMicroseconds(double mz);

		/// @brief get Agilent polynomial from Mobilion polynomial
		std::vector<double> TotalPolynomial();

		/// @brief Convert m/z to tof bin index using this calibration.
		size_t MzToIndex(double mz);

		/// @brief Retrieve the slope of the calibration.
		double Slope();

		/// @brief Retrieve the intercept of the calibration.
		double Intercept();

		/// @brief Retrieve the residual fit coefficients.
		std::vector<double> ResidualFit();

		/// @brief return status of failure
		int getFailureStatus();

		/// @brief return status of StatusText
		std::string getStatusText();

		double slope; ///< The slope of the traditional calibration.
		double intercept; ///< The intercept of the traditional calibration.

	private:

		int failure_status;
		std::string status_text;
		double sample_rate; ///< The digiter's sampling rate in samples/second.
		std::vector<double> residual_fit; ///< The terms of the polynomial fit of the mass residuals.
		double bin_width; ///< The TOF dimension bin width.
		bool mass_corrected; ///< Is this calibration mass-corrected?
		std::string json_string; ///< The json representation of the calibration.
	};

	/// @brief The types of calibrations that can be applied
	/// @author Greg Van Aken

	/// @brief Calibration type enumeration
	enum class eCalibrationType
	{
		/// @brief Polynomial calibration type
		POLYNOMIAL,
		/// @brief Unknown calibration type
		UNKNOWN
	};

	/// @class MBISDK::CcsCalibration
	/// @brief A calibration object that holds coefficients to convert to/from AT to CCS
	class MBI_DLLCPP CcsCalibration
	{
	public:
		/// @brief Initialize a calibration object from json representation.
		CcsCalibration(double sampleRate, const char* jsonString);

		/// @brief Initialize a calibration object from coefficients.
		CcsCalibration(double sampleRate, eCalibrationType type, std::vector<double> coefficients);

		//CcsCalibration(Frame* parent);

		/// @brief Get the type of calibration
		eCalibrationType Type();

		/// @brief Get the coefficients
		std::vector<double> Coefficients();

		/// @brief Default constructor.
		CcsCalibration() {};

	private:
		double sample_rate = 0.0; ///< The digiter's sampling rate in samples/second.
		eCalibrationType type = eCalibrationType::UNKNOWN; ///< The type of calibration.
		std::vector<double> coefficients; ///< The set of coefficients for this calibration.
		std::string json_string; ///< The json representation of the calibration.
	};

	/*! @class MBISDK::GlobalCcsCalibration
	*   @brief A calibration file that holds coefficients to convert to/from AT to CCS
	*/
	class MBI_DLLCPP GlobalCcsCalibration
	{
	public:
		/// @brief Initialize a calibration object from json representation.
		GlobalCcsCalibration(const char* jsonString);

		/// @brief Initialize a calibration object from coefficients.
		GlobalCcsCalibration(eCalibrationType type,
			std::vector<double> coefficients,
			std::vector<double> ccsCoefficients = std::vector<double>(),
			std::vector<double> atCoefficients = std::vector<double>(),
			std::vector<double> mzCoefficients = std::vector<double>());

		/// @brief Default constructor
		GlobalCcsCalibration();

		/// @brief Get the type of calibration
		eCalibrationType Type();

		/// @brief Get the coefficients
		std::vector<double> Coefficients();

		/// @brief Get the CSS coefficients
		std::vector<double> CSSCoefficients();

		/// @brief Get the MZ coefficients
		std::vector<double> MZCoefficients();

		/// @brief Get the AT coefficients
		std::vector<double> ATCoefficients();
	private:
		eCalibrationType type; ///< The type of calibration.
		std::vector<double> coefficients; ///< The set of coefficients for this calibration.
		std::string json_string; ///< The json representation of the calibration.
		std::vector<double> ccs_coefficients; ///< The json representation of the CSS calibration.
		std::vector<double> at_coefficients; ///< The json representation of the AT calibration.
		std::vector<double> mz_coefficients; ///< The json representation of the MZ calibration.
	};

	/// @class MBISDK::EyeOnCcsCalibration
	/// @brief A calibration file that holds coefficients to convert from AT to CCS
	class MBI_DLLCPP EyeOnCcsCalibration
	{
	public:

		/// @brief Initialize a calibration object with just a file pointer.
		EyeOnCcsCalibration(MBISDK::MBIFile* input_mbi_file_ptr);

		/// @brief Initialize a calibration object from json representation.
		EyeOnCcsCalibration(std::string strCCSData, MBISDK::MBIFile* input_mbi_file_ptr);

		/// @brief list of throwable errors
		enum {
			/// @brief bad file pointer throwable error
			BAD_FILE_POINTER = 0,
			/// @brief bad frame index throwable error
			BAD_FRAME_INDEX = 1,
			/// @brief bad mass value throwable error
			BAD_MASS_VALUE = 2,
			/// @brief bad calc value throwable error
			BAD_CALC_VALUE = 3
		};

		/// @brief Parse CCS Cal string
		void ParseCCSCal(std::string strCCSData);

		/// @brief Get CCS Minimum value
		double GetCCSMinimum();

		/// @brief Get CCS Maximum value
		double GetCCSMaximum();

		/// @brief Get Degree value
		int GetDegree();

		/// @brief Get AT Surfing value, measured in milliseconds
		double GetAtSurf();

		/// @brief Get CCS Coefficient for a given nIndex
		std::vector<double> GetCCSCoefficients();

		/// @brief Get CCS Coefficient for a given nIndex
		void SetCCSCoefficients(const std::vector<double> vec_input);

		/// @brief Calculate CCS value for given AT
		double ArrivalTimeToCCS(double scan_arrival_time, double ion_mass);

		/// @brief Calculate CCS value for set of AT
		std::vector<std::tuple<int64_t, double, double>> ArrivalTimeToCCS(std::vector<std::tuple<int64_t, double, double>> input_arrival_list);

		/// @brief Calculate CCS value for the AT of a single frame of intensities
		std::vector<std::tuple<int64_t, double, double>> ArrivalTimeToCCS(int frame_index);

		/// @brief Calculate Arrival Time for given CCS
		double CCSToArrivalTime(double ccs_angstroms_squared, double ion_mass);

		///  @brief Return the details during an error to assist developers
		std::string GetErrorOutput();

		///  @brief Return the value of the gas mass for the experiment
		double ComputeGasMass(std::string gas_string);

		///  @brief Calculate adjusted CCS value based on arrival time
		double AdjustCCSValue(double unadjusted_ccs, double dbArrivalTime, double Mz_ion);

		double InvertAdjustCCSValue(double ccs_value, double Mz_ion);

		///  @brief Return the value of the gas mass for the experiment
		double ComputeGasMass();

		///  @brief Generate a message to throw an error
		std::string GenerateThrownErrorMessage(int error_type, int frame_index = 0, double arrival_time = 0.0);

		///  @brief evaluate pointer to ensure it is not null
		bool IsValid(void* ptr);

		///  @brief override the gas mass value
		void SetGasMass(double gas_mass_input);

		///  @brief decide which of two roots is better
		double ChooseGoodRoot(double root_1, double root_2, double ion_mass);

		///  @brief CCS_ERROR_HEADER error text
		static constexpr const char* CCS_ERROR_HEADER = "Cannot process EyeOn CCS Calibration information.  ";
		///  @brief CCS_MISSING_DATA error text
		static constexpr const char* CCS_MISSING_DATA = "The global metadata field cal-ccs is not present in this file: ";
		///  @brief CCS_INVALID_DATA error text
		static constexpr const char* CCS_INVALID_DATA = "The global metadata field cal-ccs is not a valid JSON structure in this file: ";
		///  @brief CCS_MISSING_CCS_COEFFICIENTS error text
		static constexpr const char* CCS_MISSING_CCS_COEFFICIENTS = "The ccs_coefficients entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_MISSING_MIN_CCS error text
		static constexpr const char* CCS_MISSING_MIN_CCS = "The ccs_min entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_MISSING_MAX_CCS error text
		static constexpr const char* CCS_MISSING_MAX_CCS = "The ccs_max entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_MISSING_CCS_DEGREE error text
		static constexpr const char* CCS_MISSING_CCS_DEGREE = "The degree entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_MISSING_CCS_AT_SURF error text
		static constexpr const char* CCS_MISSING_CCS_AT_SURF = "The at_surf entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_DEGREE_COEFFICIENTS_MISMATCH error text
		static constexpr const char* CCS_DEGREE_COEFFICIENTS_MISMATCH = "The degree entry for the polynomial should align with the count of ccs coefficients, and it does not in this file: ";
		///  @brief CCS_BAD_FRAME_INDEX_HEADER error text
		static constexpr const char* CCS_BAD_FRAME_INDEX_HEADER = "The frame index supplied, ";
		///  @brief CCS_BAD_FRAME_INDEX_FOOTER error text
		static constexpr const char* CCS_BAD_FRAME_INDEX_FOOTER = ", must be at least 1 and no greater than the number of frames in this file: ";
		///  @brief CCS_BAD_FILE_POINTER error text
		static constexpr const char* CCS_BAD_FILE_POINTER = "The file pointer supplied is not valid for this file: ";
		///  @brief CCS_MISSING_CCS_GAS_TYPE error text
		static constexpr const char* CCS_MISSING_CCS_GAS_TYPE = "The mass flow gas type entry for CCS Calibration data is not present in this file: ";
		///  @brief CCS_ARRIVAL_TIME_HEADER error text
		static constexpr const char* CCS_ARRIVAL_TIME_HEADER = "The arrival time chosen, ";
		///  @brief CCS_BAD_MASS_VALUE_PART_2 error text
		static constexpr const char* CCS_BAD_MASS_VALUE_PART_2 = " yields a mass that is invalid for CCS Calibration data in this file: ";
		///  @brief CCS_BAD_ADJUSTMENT_PART_2 error text
		static constexpr const char* CCS_BAD_ADJUSTMENT_PART_2 = " yields a calculation that would result in a divide by zero for CCS Calibration data in this file: ";


	private:
		double ccs_minimum; // minimum value for valid CCS
		double ccs_maximum; // maximum value for valid ccs
		int degree;         // numeric degree of polynomial
		double at_surfing;  // earliest valid AT value for a given calibration, in milliseconds
		double mass_gas_medium; //mass of gas for a specific calibration
		std::vector<double> ccs_coefficients; // list of coefficients, as read in from MBI file with first coefficient applying to addend with the highest order (usually AX^3)
		MBISDK::MBIFile* mbi_file_ptr;
		MBISDK::TofCalibration tof_cal;
	};
}
}
