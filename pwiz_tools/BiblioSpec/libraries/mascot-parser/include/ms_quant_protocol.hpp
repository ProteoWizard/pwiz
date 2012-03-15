/*
##############################################################################
# file: ms_quant_protocol.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates protocol-element from "quantitation.xml"-file                 #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_protocol.hpp,v $
 * @(#)$Revision: 1.11 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_PROTOCOL_HPP
#define MS_QUANT_PROTOCOL_HPP

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

// forward declarations
namespace msparser_internal {
    class ms_quant_xmlloader;
    class ms_quant_protocol_impl;
    class ms_quant_method_impl;
}

namespace matrix_science {

     // forward declarations
    class ms_quant_parameters;
    class ms_quant_reporter;
    class ms_quant_precursor;
    class ms_quant_replicate;
    class ms_quant_average;
    class ms_quant_multiplex;
    class ms_xml_schema;

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a \c protocol element in <tt>quantitation.xml</tt>.
    /*!
     * 6 different protocols are currently supported, and an object of this
     * class is a container for exactly one of those protocols. The getType()
     * function returns a string which contains the protocol for the current
     * quantitation method.  Use the return value from it to determine
     * which of the functions to call to get further protocol details.
     */
    class MS_MASCOTRESFILE_API ms_quant_protocol: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;
        friend class msparser_internal::ms_quant_method_impl;
    public:
        //! Default constructor.
        ms_quant_protocol();

        //! Copying constructor.
        ms_quant_protocol(const ms_quant_protocol& src);

        //! Destructor.
        virtual ~ms_quant_protocol();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_protocol* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_protocol& operator=(const ms_quant_protocol& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Call this method first to determine which sub element to retrieve.
        std::string getType() const;

        //! Sets content of the \c protocol element to a null value and deletes all previously held information.
        void setNull();


        //! Returns the \c reporter element or a null value if not available.
        const ms_quant_reporter* getReporter() const;

        //! Override content with another \c reporter element.
        void setReporter(const ms_quant_reporter* reporter);

        //! Obtain a symbolic name for the \c reporter element schema type.
        std::string getReporterSchemaType() const;


        //! Returns the \c precursor element or a null value if not available.
        const ms_quant_precursor* getPrecursor() const;

        //! Override content with another \c precursor element.
        void setPrecursor(const ms_quant_precursor* precursor);

        //! Obtain a symbolic name for the \c precursor element schema type.
        std::string getPrecursorSchemaType() const;


        //! Returns the \c multiplex element or a null value if not available.
        const ms_quant_multiplex* getMultiplex() const;

        //! Override content with another \c multiplex element.
        void setMultiplex(const ms_quant_multiplex* multiplex);

        //! Obtain a symbolic name for the \c multiplex element schema type.
        std::string getMultiplexSchemaType() const;


        //! Returns the \c replicate element or a null value if not available.
        const ms_quant_replicate* getReplicate() const;

        //! Override content with another \c replicate element.
        void setReplicate(const ms_quant_replicate* replicate);

        //! Obtain a symbolic name for the \c replicate element schema type.
        std::string getReplicateSchemaType() const;


        //! Returns the \c average element or a null value if not available.
        const ms_quant_average* getAverage() const;

        //! Override content with another \c average element.
        void setAverage(const ms_quant_average* average);

        //! Obtain a symbolic name for the \c average element schema type.
        std::string getAverageSchemaType() const;

    private:

        msparser_internal::ms_quant_protocol_impl * m_pImpl;

    }; // class ms_quant_protocol

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_PROTOCOL_HPP

/*------------------------------- End of File -------------------------------*/
