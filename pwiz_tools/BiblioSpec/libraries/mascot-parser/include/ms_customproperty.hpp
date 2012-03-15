/*
##############################################################################
# file: ms_customproperty.hpp                                                #
# 'msparser' toolkit                                                         #
# Represents a custom property with unknown structure. It is useful for new  # 
# properties or for properties with variable structure that can be explored  #
# at run-time                                                                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_customproperty.hpp      $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.6 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_CUSTOMPROPERTY_HPP
#define MS_CUSTOMPROPERTY_HPP

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

// for the sake of #include <string>
#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif
#include <string>
#include <vector>

#include "msparser_lim.hpp"

namespace matrix_science {

    //! The class is used as a base for property-containing classes, such as ms_mascotoptions.
    /*!
     *  This class functionality is designed to allow users store and retrieve
     *  specified and unspecified (custom) properties as well as comments. 
     *
     *  Not every configuration file can hold custom properties and/or
     *  comments.  Whether a configuration file can have custom properties or
     *  not depends on its format.  For example, almost all sections of the
     *  main configuration file <tt>mascot.dat</tt> can have unspecified
     *  properties and comments, whereas <tt>Databases</tt> section of the same
     *  file cannot have either of them. Another example of non-supported
     *  custom properties is <tt>unimod.xml</tt> whose structure and syntax is
     *  defined in a corrsponding xml-schema file and cannot be altered
     *  temporarily.
     *
     *  Normally, custom properties are used in the following cases:
     *
     *  <ul>
     *  <li>future releases of software can introduce additional properties
     *  that can still be retrieved using old msparser-library;</li>
     *  <li>when a client application needs to know exactly whether a certain
     *  property is explicitly specified in the configuration file or set to
     *  it's default value;</li>
     *  <li>additional comments can be added/accessed programmatically and then
     *  stored in the disc file;</li>
     *  <li>client application needs a raw text representation of a property
     *  instead of a set of parsed values.</li>
     *  </ul>
     *
     *  Note that functionality related to comments is not particularly
     *  advanced.  Comments are all treated uniformly like properties with
     *  empty names and can be retrieved only by number. New comments can be
     *  added only at the end of existing properties list using either
     *  #appendText("# comment line preceded by a hash-character") or
     *  #appendProperty("", "# comment line", ""). They can also be
     *  dropped/altered by number.
     *
     *  All properties in configuration files have three elements associated
     *  with them:
     *
     *  <ul>
     *  <li>Name - an empty case-insensitive string in case of comments.</li>
     *  <li>Value - raw text representation (comment line will normally start
     *  with # character).</li>
     *  <li>Delimiter - a string between name and value in the configuration
     *  file.  It can be a space character, comma, colon, tab or a combination
     *  of them.  Delimiting characters are usually stripped out of property
     *  name and value and stored separately.  When adding a new property
     *  without explicitly specified delimiter string a default one will be
     *  used.</li>
     *  </ul>
     *
     *  Not necessarily all these elements are used and stored in a file
     *  -- consult the documentation of the particular configuration file
     *  format.  An instance of the class rather serves as a container of
     *  properties only. It doesn't have any file reading/writing
     *  functionality.
     */
    class MS_MASCOTRESFILE_API ms_customproperty
    {
    public:
        //! Default constructor.
        ms_customproperty();

        //! Destructor.
        ~ms_customproperty();

        //! Removes all property entries and comments.
        void defaultValues();

        //! Copies all properties and comments from another instance.
        void copyFrom(const ms_customproperty* src);

        //! Returns a total number of property/comment entries.
        int getNumberOfProperties() const;

        //! Returns a property name for a given index.
        /*!
         *  \param index property number from 0 to (#getNumberOfProperties()-1).
         *  \return property name of an empty string for comments.
         */
        std::string getPropertyName(const int index) const;

        //! Changes name of the property with the given index.
        /*!
         *  \param index property number from 0 to (#getNumberOfProperties()-1).
         *  \param name new name to be given to the property.
         */
        void setPropertyName(const int index, const char* name);

        //! Searches the list for a property with the given name.
        /*!
         *  If no property found with the given name -1 will be returned. 
         *  There may be several property entries in the list with the same
         *  name.
         *
         *  \param name a property name to be found.
         *  \param startFrom a minimal property index to start search from.
         *  \return a property index or -1 if no property found.
         */
        int findProperty(const char* name, const int startFrom = 0) const;

        //! Searches the list for a property with the partially matching name.
        /*!
         *  If no property found with the given name part -1 will be returned. 
         *  There may be several property entries in the list whose names start
         *  with the given string.
         *
         *  \param nameBeginning first part of a property name to be found.
         *  \param startFrom a minimal property index to start search from.
         *  \return a property index or -1 if no property found.
         */
        int findPropertyBeginning(const char* nameBeginning, const int startFrom = 0) const;

        //! Retrieves property value by name.
        /*!
         *  Don't use this method for comments as they all have empty name.
         *  Also note that there might be several entries corresponding to the
         *  same name .  -- only the first value will be returned. If in doubt
         *  use #findProperty() and #getPropValStringByNumber() instead.
         */
        std::string getPropValStringByName(const char* name) const;

        //! Retrieves property raw text values by number.
        std::string getPropValStringByNumber(const int index) const;

        //! Returns a specific delimiter used for the property.
        std::string getDelimiterByNumber(const int index) const;

        //! Adds a new property with the given parameters.
        /*!
         *  \param name a property name to use.
         *  \param value a property value to use.
         *  \param delimiter a specific delimiter or an empty string for
         *  a default one to be used.
         *  \param bFirstPlace forces a new property to be put on top of the
         *  list.
         */
        void appendProperty
            ( const char* name
            , const char* value 
            , const char* delimiter
            , const bool bFirstPlace = false
            );

        //! Adds a new non-parsed property.
        /*!
         *  All property elements will be retrieved from the first parameter.
         *  Before calling this method, a specific default delimiter can be set
         *  using #setDefaultDelimiter().
         *
         *  \param  line    raw text representation of the property to be
         *  parsed.
         *  \param bFirstPlace forces a new property to be put on top of the
         *  list.
         */
        void appendText
            ( const char* line
            , const bool bFirstPlace = false
            );

        //! Changes a string value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  \param name a name of the property to find or add.
         *  \param value a new string value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValStringByName
            ( const char* name
            , const char* value
            , const bool bFirstPlace = false
            );

        //! Changes a single character value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  \param name a name of the property to find or add.
         *  \param value a new single character value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValCharByName
            ( const char* name
            , const char value
            , const bool bFirstPlace = false
            );

        //! Changes an integer value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  \param name a name of the property to find or add.
         *  \param value a new integer value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValIntByName
            ( const char* name
            , const int value
            , const bool bFirstPlace = false
            );

        //! Changes a long 64-bit integer value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  \param name a name of the property to find or add.
         *  \param value a new long 64-bit integer value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValInt64ByName
            ( const char* name
            , const INT64 value
            , const bool bFirstPlace = false
            );

        //! Changes a boolean value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  A new value will be converted into <b>1</b> (for <b>TRUE</b>) 
         *  or <b>0</b> (for <b>FALSE</b>) character.
         *
         *  \param name a name of the property to find or add.
         *  \param value a new boolean value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValBoolByName
            ( const char* name
            , const bool value
            , const bool bFirstPlace = false
            );

        //! Changes an floating point value of the first entry with the given name or creates a new property if it is not found.
        /*!
         *  \param name a name of the property to find or add.
         *  \param value a new floating point value for the property.
         *  \param bFirstPlace if not found a new property can be put on top of
         *  the list.
         */
        void setPropValFloatByName
            ( const char* name
            , const double value
            , const bool bFirstPlace = false
            );

        //! Changes an string value of an existing property with the given index.
        /*!
         *  \param index an index of an existing property.
         *  \param value a new string value for the property.
         */
        void setPropValStringByNumber
            ( const int index
            , const char* value
            );


        //! Return current default delimiter string used for parsing/storing properties.
        std::string getDefaultDelimiter() const;

        //! Allows to set a specific delimiter string to be used when no property-specific
        //! delimiter is supplied.
        void setDefaultDelimiter(const char* delim);

        //! Deletes all properties with the specified name.
        /*!
         *  \param name a property name to find a match and then delete.
         */
        void delProp(const char* name);

        //! Deletes all properties whose names start with the given string.
        /*!
         *  \param nameBeginning a first part of the property name to delete.
         */
        void delPropStart(const char* nameBeginning);

        //! Deletes all non-comment properties.
        void delNonEmpty();

        //! Deletes a single property with the specified number only.
        void delPropByNumber(const int index);

        //! Uncomments a line in the configuration file.
        bool uncommentProp(const int index, const char * delimeter = 0);

    private:
        std::vector< std::string > names_;
        std::vector< std::string > values_;
        std::vector< std::string > delimiters_;
        std::string                defaultDelimiter_;
    }; // class ms_customproperty
} // namespace matrix_science

#endif // MS_CUSTOMPROPERTY_HPP

/*------------------------------- End of File -------------------------------*/
