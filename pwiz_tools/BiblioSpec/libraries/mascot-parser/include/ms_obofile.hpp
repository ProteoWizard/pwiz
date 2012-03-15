/*
##############################################################################
# File: ms_obofile.hpp                                                       #
# Mascot Parser toolkit                                                      #
# Utility functions for accessing ontology files in the .obo format          #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2008 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Source: /vol/cvsroot/parser/inc/ms_obofile.hpp,v $
#    $Author: villek $ 
#      $Date: 2010-09-06 16:18:57 $ 
#  $Revision: 1.3 $
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_OBOFILE_HPP
#define MS_OBOFILE_HPP

#ifdef _WIN32
#pragma warning(disable:4251)   // Don't want all classes to be exported
#pragma warning(disable:4786)   // Debug symbols too long
#pragma warning(disable:4503)   // decorated name length exceeded...
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
#include <map>

namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! The class provides simplified access to ontology files in the .obo format.
    /*!
     * The file format for the ontology files is described in
     * http://www.geneontology.org/GO.format.obo-1_2.shtml .
     *
     * This class provides limited read only access to OBO files. The aim of
     * the class is to allow scripts using Mascot Parser to easily look up an
     * ID for a given term in a local .obo file.
     */
    class MS_MASCOTRESFILE_API ms_obofile: public ms_errors
    {
    public:
        //! Default constructor - not generally useful.
        ms_obofile();

        //! Constructor which loads the obo file from a local file.
        ms_obofile(const char * oboFileName);

        //! Copying constructor.
        ms_obofile(const ms_obofile& right);

        //! Destructor.
        ~ms_obofile();

#ifndef SWIG
        //! Assignment operator.
        ms_obofile& operator=(const ms_obofile& right);
#endif
        //! Copies all data from another instance of the class.
        void copyFrom(const ms_obofile* right);

        //! Initialises the object.
        void defaultValues();

        //! Returns the vector of tag, value, comment strings for the specified ontology item.
        std::vector<std::string> getItemFromId(const char * id) const;

        //! Find the ontology term with an exact match to the passed tag/value pair.
        std::string findIDFromTagValue(const char * tag, const char * value) const;

        //! Retrieves the tag, value and any comments from a line in the obo file.
        void parseLine(const std::string str, std::string & tag, std::string & value, std::string & comment) const;

        //! Returns the number of entries .
        int getNumberOfEntries() const;


    private:
        void clearOntology();
        typedef std::vector<std::string> ontologyItem_t;
        typedef std::map<std::string, ontologyItem_t> ontology_t;
        ontology_t ontology_;

    }; // class ms_obofile
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_OBOFILE_HPP

/*------------------------------- End of File -------------------------------*/
