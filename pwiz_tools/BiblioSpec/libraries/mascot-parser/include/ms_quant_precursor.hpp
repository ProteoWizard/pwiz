/*
##############################################################################
# file: ms_quant_precursor.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates precursor-element from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_precursor.hpp,v $
 * @(#)$Revision: 1.13 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_PRECURSOR_HPP
#define MS_QUANT_PRECURSOR_HPP

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

// forward declarations
namespace msparser_internal {
    class ms_quant_xmlloader;
    class ms_quant_precursor_impl;
    class ms_quant_configfile_impl;
    class ms_quant_protocol_impl;
}

namespace matrix_science {

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single \c precursor element in <tt>quantitation.xml</tt>.
    /*!
     * Serves as a possible sub-element of "protocol" element
     */
    class MS_MASCOTRESFILE_API ms_quant_precursor: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
        friend class msparser_internal::ms_quant_configfile_impl;
        friend class msparser_internal::ms_quant_protocol_impl;
    public:
        //! Default constructor.
        ms_quant_precursor();

        //! Copying constructor.
        ms_quant_precursor(const ms_quant_precursor& src);

        //! Destructor.
        virtual ~ms_quant_precursor();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_precursor* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_precursor& operator=(const ms_quant_precursor& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c allow_mass_time_match attribute.
        bool haveAllowMassTimeMatch() const;

        //! Returns the value of the \c allow_mass_time_match attribute.
        bool isAllowMassTimeMatch() const;

        //! Set a custom value for the \c allow_mass_time_match attribute.
        void setAllowMassTimeMatch(const bool value);

        //! Delete the \c allow_mass_time_match attribute.
        void dropAllowMassTimeMatch();

        //! Obtain a symbolic name for the \c allow_mass_time_match attribute schema type.
        std::string getAllowMassTimeMatchSchemaType() const;


        //! \deprecated Moved to ms_quant_integration.
        bool haveAllowElutionShift() const;

        //! \deprecated Moved to ms_quant_integration.
        bool isAllowElutionShift() const;

#ifdef DOXYGEN_SHOULD_SKIP_THIS
        //! \deprecated Moved to ms_quant_integration.
        /*!
         * This function is no longer available in Parser 2.3
         * See ms_quant_integration::setAllowElutionShift().
         */
        void setAllowElutionShift(const bool value);
#endif

        //! \deprecated Moved to ms_quant_integration.
        void dropAllowElutionShift();

        //! \deprecated Moved to ms_quant_integration.
        std::string getAllowElutionShiftSchemaType() const;


        //! \deprecated Moved to ms_quant_integration.
        bool haveAllChargeStates() const;

        //! \deprecated Moved to ms_quant_integration.
        bool isAllChargeStates() const;

#ifdef DOXYGEN_SHOULD_SKIP_THIS
        //! \deprecated Moved to ms_quant_integration.
        /*!
         * This function is no longer available in Parser 2.3
         * See ms_quant_integration::setAllChargeStates().
         */
        void setAllChargeStates(const bool value);
#endif
        //! \deprecated Moved to ms_quant_integration.
        void dropAllChargeStates();

        //! \deprecated Moved to ms_quant_integration.
        std::string getAllChargeStatesSchemaType() const;

    private:

        msparser_internal::ms_quant_precursor_impl * m_pImpl;
    }; // class ms_quant_precursor

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_PRECURSOR_HPP

/*------------------------------- End of File -------------------------------*/
