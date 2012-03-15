/*
##############################################################################
# file: ms_quant_satellite.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates satellite-element from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2009 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_satellite.hpp,v $
 * @(#)$Revision: 1.5 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_SATELLITE_HPP
#define MS_QUANT_SATELLITE_HPP

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
}

namespace matrix_science {

    class ms_quant_modgroup; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single \c satellite element in <tt>quantitation.xml</tt>.
    /*!
     * Modification group that defines satellite peaks to be summed into the
     * heaviest component to correct for Arg-Pro conversion of SILAC label.
     */
    class MS_MASCOTRESFILE_API ms_quant_satellite: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_satellite();

        //! Copying constructor.
        ms_quant_satellite(const ms_quant_satellite& src);

        //! Destructor.
        virtual ~ms_quant_satellite();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_satellite* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_satellite& operator=(const ms_quant_satellite& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Check for presence of the \c modifications element.
        bool haveModifications() const;

        //! Returns the value of the \c modifications element.
        const ms_quant_modgroup* getModifications() const;

        //! Supply a custom content for \c modifications element.
        void setModifications(const ms_quant_modgroup* moverz);

        //! Delete the \c modifications element.
        void dropModifications();

        //! Obtain a symbolic name for the \c modifications element schema type.
        std::string getModificationsSchemaType() const;

    private:

        ms_quant_modgroup *_pModifications;
        bool _modifications_set;
    }; // class ms_quant_satellite

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_SATELLITE_HPP

/*------------------------------- End of File -------------------------------*/

