/*
##############################################################################
# file: ms_quant_integration.hpp                                             #
# 'msparser' toolkit                                                         #
# Encapsulates integration-element from "quantitation.xml"-file              #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_integration.hpp,v $
 * @(#)$Revision: 1.12 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_INTEGRATION_HPP
#define MS_QUANT_INTEGRATION_HPP

#ifdef _WIN32
#pragma warning(disable:4251)   // Don't want all classes to be exported
#pragma warning(disable:4786)   // Debug symbols too long
#   ifndef _MATRIX_USE_STATIC_LIB
#       ifdef MS_MASCOTRESFILE_EXPORTS
#           define MS_MASCOTRESFILE_API __declspec(dllexport)
#       else
#           define MS_MASCOTRESFILE_API __declspec(dllimport)
#       endif
#   else
#       define MS_MASCOTRESFILE_API
#   endif
#else
#   define MS_MASCOTRESFILE_API
#endif

#include <string>
#include <vector>

// forward declarations
namespace msparser_internal {
    class ms_quant_xmlloader;
    class ms_quant_integration_impl;
}


namespace matrix_science {

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single <tt>integration</tt> element in <tt>quantitation.xml</tt>.
    /*!
     * Specifies the method and parameters to be used to integrate precursor
     * over time.
     */
    class MS_MASCOTRESFILE_API ms_quant_integration: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_integration();

        //! Copying constructor.
        ms_quant_integration(const ms_quant_integration& src);

        //! Destructor.
        virtual ~ms_quant_integration();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_integration* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_integration& operator=(const ms_quant_integration& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c method attribute.
        bool haveMethod() const;

        //! Returns the value of the \c method attribute.
        std::string getMethod() const;

        //! Set a custom value for the \c method attribute.
        void setMethod(const char* value);

        //! Delete the \c method attribute.
        void dropMethod();

        //! Obtain a symbolic name for the \c method attribute schema type.
        std::string getMethodSchemaType() const;


        //! Indicates presence of the \c source attribute.
        bool haveSource() const;

        //! Returns the value of the \c source attribute.
        std::string getSource() const;

        //! Set a custom value for the \c source attribute.
        void setSource(const char* value);

        //! Delete the \c source attribute.
        void dropSource();

        //! Obtain a symbolic name for the \c source attribute schema type.
        std::string getSourceSchemaType() const;


        //! \deprecated Not in use.
        bool haveMassDelta() const;

        //! \deprecated Not in use.
        std::string getMassDelta() const;

        //! \deprecated Not in use.
        void setMassDelta(const char* value);

        //! \deprecated Not in use.
        void dropMassDelta();

        //! \deprecated Not in use.
        std::string getMassDeltaSchemaType() const;


        //! \deprecated Not in use.
        bool haveMassDeltaUnit() const;

        //! \deprecated Not in use.
        std::string getMassDeltaUnit() const;

        //! \deprecated Not in use.
        void setMassDeltaUnit(const char* value);

        //! \deprecated Not in use.
        void dropMassDeltaUnit();

        //! \deprecated Not in use.
        std::string getMassDeltaUnitSchemaType() const;


        //! Indicates presence of the \c elution_time_delta attribute.
        bool haveElutionTimeDelta() const;

        //! Returns the value of the \c elution_time_delta attribute.
        std::string getElutionTimeDelta() const;

        //! Set a custom value for the \c elution_time_delta attribute.
        void setElutionTimeDelta(const char* value);

        //! Delete the \c elution_time_delta attribute.
        void dropElutionTimeDelta();

        //! Obtain a symbolic name for the \c elution_time_delta attribute schema type.
        std::string getElutionTimeDeltaSchemaType() const;


        //! Indicates presence of the \c elution_time_delta_unit attribute.
        bool haveElutionTimeDeltaUnit() const;

        //! Returns the value of the \c elution_time_delta_unit attribute.
        std::string getElutionTimeDeltaUnit() const;

        //! Set a custom value for the \c elution_time_delta_unit attribute.
        void setElutionTimeDeltaUnit(const char* value);

        //! Delete the \c elution_time_delta_unit attribute.
        void dropElutionTimeDeltaUnit();

        //! Obtain a symbolic name for the \c elution_time_delta_unit attribute schema type.
        std::string getElutionTimeDeltaUnitSchemaType() const;


        //! Indicates presence of the \c elution_profile_correlation_threshold attribute.
        bool haveElutionProfileCorrelationThreshold() const;

        //! Returns the value of the \c elution_profile_correlation_threshold attribute.
        std::string getElutionProfileCorrelationThreshold() const;

        //! Set a custom value for the \c elution_profile_correlation_threshold attribute.
        void setElutionProfileCorrelationThreshold(const char* value);

        //! Delete the \c elution_profile_correlation_threshold attribute.
        void dropElutionProfileCorrelationThreshold();

        //! Obtain a symbolic name for the \c elution_profile_correlation_threshold attribute schema type.
        std::string getElutionProfileCorrelationThresholdSchemaType() const;


        //! \deprecated Do not use.
        bool haveProcessingOptions() const;

        //! \deprecated Do not use.
        std::string getProcessingOptions() const;

        //! \deprecated Do not use.
        void setProcessingOptions(const std::string value);

        //! \deprecated Do not use.
        void dropProcessingOptions();

        //! \deprecated Do not use.
        std::string getProcessingOptionsSchemaType() const;


        //! \deprecated Do not use.
        bool havePrecursorRange() const;

        //! \deprecated Do not use.
        std::string getPrecursorRange() const;

        //! \deprecated Do not use.
        void setPrecursorRange(const char* value);

        //! \deprecated Do not use.
        void dropPrecursorRange();

        //! \deprecated Do not use.
        std::string getPrecursorRangeSchemaType() const;


        //! Indicates presence of the \c matched_rho attribute.
        bool haveMatchedRho() const;

        //! Returns the value of the \c matched_rho attribute.
        std::string getMatchedRho() const;

        //! Set a custom value for the \c matched_rho attribute.
        void setMatchedRho(const char * value);

        //! Delete the \c matched_rho attribute.
        void dropMatchedRho();

        //! Obtain a symbolic name for the \c matched_rho attribute schema type.
        std::string getMatchedRhoSchemaType() const;


        //! Indicates presence of the \c xic_threshold attribute.
        bool haveXicThreshold() const;

        //! Returns the value of the \c xic_threshold attribute.
        std::string getXicThreshold() const;

        //! Set a custom value for the \c xic_threshold attribute.
        void setXicThreshold(const char * value);

        //! Delete the \c xic_threshold attribute.
        void dropXicThreshold();

        //! Obtain a symbolic name for the \c xic_threshold attribute schema type.
        std::string getXicThresholdSchemaType() const;


        //! Indicates presence of the \c xic_max_width attribute.
        bool haveXicMaxWidth() const;

        //! Returns the value of the \c xic_max_width attribute.
        int getXicMaxWidth() const;

        //! Set a custom value for the \c xic_max_width attribute.
        void setXicMaxWidth(int value);

        //! Delete the \c xic_max_width attribute.
        void dropXicMaxWidth();

        //! Obtain a symbolic name for the "xic_max_width" schema type.
        std::string getXicMaxWidthSchemaType() const;


        //! Indicates presence of the \c xic_smoothing attribute.
        bool haveXicSmoothing() const;

        //! Returns the value of the \c xic_smoothing attribute.
        int getXicSmoothing() const;

        //! Set a custom value for the \c xic_smoothing attribute.
        void setXicSmoothing(int value);

        //! Delete the \c xic_smoothing attribute.
        void dropXicSmoothing();

        //! Obtain a symbolic name for the \c xic_smoothing attribute schema type.
        std::string getXicSmoothingSchemaType() const;


        //! Indicates presence of the \c all_charge_states attribute.
        bool haveAllChargeStates() const;

        //! Returns the value of the \c all_charge_states attribute.
        bool isAllChargeStates() const;

        //! Set a custom value for the \c all_charge_states attribute.
        void setAllChargeStates(const bool value);

        //! Delete the \c all_charge_states attribute.
        void dropAllChargeStates();

        //! Obtain a symbolic name for the \c all_charge_states attribute schema type.
        std::string getAllChargeStatesSchemaType() const;


        //! Indicates presence of the \c simple_ratio attribute.
        bool haveSimpleRatio() const;

        //! Returns the value of the \c simple_ratio attribute.
        bool isSimpleRatio() const;

        //! Set a custom value for the \c simple_ratio attribute.
        void setSimpleRatio(const bool value);

        //! Delete the \c simple_ratio attribute.
        void dropSimpleRatio();

        //! Obtain a symbolic name for the \c simple_ratio attribute schema type.
        std::string getSimpleRatioSchemaType() const;


        //! Indicates presence of the \c all_charge_states_threshold attribute.
        bool haveAllChargeStatesThreshold() const;

        //! Returns the value of the \c all_charge_states_threshold attribute.
        std::string getAllChargeStatesThreshold() const;

        //! Set a custom value for the \c all_charge_states_threshold attribute.
        void setAllChargeStatesThreshold(const char * value);

        //! Delete the \c all_charge_states_threshold attribute.
        void dropAllChargeStatesThreshold();

        //! Obtain a symbolic name for the \c all_charge_states_threshold attribute schema type.
        std::string getAllChargeStatesThresholdSchemaType() const;


        //! Indicates presence of the \c allow_elution_shift attribute.
        bool haveAllowElutionShift() const;

        //! Returns the value of the \c allow_elution_shift attribute.
        bool isAllowElutionShift() const;

        //! Set a custom value for the \c allow_elution_shift attribute.
        void setAllowElutionShift(const bool value);

        //! Delete the \c allow_elution_shift attribute.
        void dropAllowElutionShift();

        //! Obtain a symbolic name for the \c allow_elution_shift attribute schema type.
        std::string getAllowElutionShiftSchemaType() const;

    private:
        msparser_internal::ms_quant_integration_impl *m_pImpl;
    }; // class ms_quant_integration

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_INTEGRATION_HPP

/*------------------------------- End of File -------------------------------*/

