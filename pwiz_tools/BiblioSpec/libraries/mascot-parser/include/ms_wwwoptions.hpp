/*
##############################################################################
# file: ms_wwwoptions.hpp                                                    #
# 'msparser' toolkit                                                         #
# Represents "WWW" section of "mascot.dat" file                              #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_wwwoptions.hpp          $ #
#     $Author: villek $ #
#       $Date: 2011-04-12 09:56:17 $ #
#   $Revision: 1.12 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_WWWOPTIONS_HPP
#define MS_WWWOPTIONS_HPP

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


namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! All supported types of entries in the <tt>WWW</tt> section.
    /*!
     * See \ref DynLangEnums.
     */
    enum WWW_TYPE
    {
        WWW_SEQ = 0,    //!< Sequence string source.
        WWW_REP = 1     //!< Full text report source.
    };

    //! Represent a single entry in the <tt>WWW</tt> section of <tt>mascot.dat</tt>.
    /*!
     * Instances of this class can be stored in in ms_wwwoptions to represent
     * the whole section. Consult the Mascot manual if you are unsure how to
     * configure an entry. It defines an information source where CGI scripts
     * look for the information needed to compile a results report.
     */
    class MS_MASCOTRESFILE_API ms_wwwentry
    {
        friend class ms_datfile;
        friend class ms_wwwoptions;

    public:
        //! Default constructor.
        ms_wwwentry();

        //! Copying constructor.
        ms_wwwentry(const ms_wwwentry& src);
        
        //! Destructor.
        ~ms_wwwentry();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_wwwentry* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_wwwentry& operator=(const ms_wwwentry& right);
#endif
        //! Returns the name of the database the entry refers to.
        /*!
         *  Note that database name is case sensitive.
         *
         *  The name should be one of those specified in the <tt>Databases</tt>
         *  section.  Every name can be associated with more than one entry in
         *  the <tt>WWW</tt> section as different entry types are supported. In
         *  order to retrieve the entry type use #getType().
         *
         *  By default this is an empty string.
         */
        std::string getName() const;

        //! Set the database name for the entry.
        /*!
         *  See #getName() for more information.
         */
        void setName(const char* value);

        //! Returns the name of the database the entry refers to.
        /*!
         *  Several entry types are supported. For the complete list of types,
         *  see the enumeration values in WWW_TYPE.
         *
         *  There is no default value for this parameter.
         */
        WWW_TYPE getType() const;

        //! Change the entry type.
        /*!
         *  See #getType() for more information.
         */
        void setType(const WWW_TYPE value);

        //! Returns a parse rule number for the entry that corresponds to one of the parse rules specified in the <tt>PARSE</tt> section of <tt>mascot.dat</tt>.
        /*!
         *  The index of a rule in the <tt>PARSE</tt> section that can be used
         *  to extract the information required.
         *
         *  Default is <b>-1</b>, which is an invalid rule number.
         */
        int getParseRule() const;

        //! Change the parse rule number for the entry.
        /*!
         *  See #getParseRule() for more information.
         */
        void setParseRule(const int value);

        //! Returns a host name used to retrieve reports from.
        /*!
         *  For <tt>ms-getseq.exe</tt> or a similar local executable, this
         *  attribute should contain localhost.  The word <b>localhost</b> is
         *  used to determine whether the application is a command line
         *  executable or a CGI application.  If you want to specify a CGI
         *  application on the local server, just specify the hostname in some
         *  other way, for example <b>127.0.0.1</b>).
         *
         *  For a remote source, or a local source that will be queried as 
         *  a CGI application, this should be the hostname. 
         *
         *  Default is <b>localhost</b>.
         *
         *  \sa #getPortNumber()
         */
        std::string getHostName() const;

        //! Change the host name parameter.
        /*!
         *  See #getHostName() for more information.
         */
        void setHostName(const char* value);

        //! Returns a port number for the host used as the information source.
        /*!
         *  In most cases it should be left as <b>80</b>, which is the default
         *  value.
         *
         *  \sa #getHostName()
         */
        int getPortNumber() const;

        //! Change the port number.
        /*!
         *  See #getPortNumber() for more information.
         */
        void setPortNumber(const int value);

        //! Returns a path to the executable and parameters.
        /*!
         *  A string containing the path to the executable and parameters, 
         *  some of which are variables. 
         *
         *  By default this is empty.
         */
        std::string getPath() const;

        //! Change the path.
        /*!
         *  See #getPath() for more information.
         */
        void setPath(const char* value);

    private:
        std::string name_;
        WWW_TYPE type_;
        int parseRuleNum_;
        std::string hostName_;
        int portNumber_;
        std::string path_;

        std::string getStringName() const;
        std::string getStringValue() const;
    }; // class MS_MASCOTRESFILE_API ms_wwwentry

    //! Represents the whole <tt>WWW</tt> section.
    /*!
     *  The <tt>WWW</tt> section defines where CGI scripts look for the
     *  information needed to compile a results report.  At least one entry is
     *  required for each database, to define the source from which the
     *  sequence string of a database entry can be obtained. A second line can
     *  optionally define the source from which the full text report of an
     *  entry can be obtained.  The syntax is very similar in both cases,
     *  independent of whether the information originates locally or on
     *  a remote system.
     *
     *  An instance of this class holds a list of entries (see ms_wwwentry)
     *  that can be obtained and dealt with individually. By default the list
     *  is empty.
     *
     *  For detailed information on any of the options please consult the Mascot
     *  manual.
     *
     *  Also get yourselves acquainted with the base class ms_customproperty. 
     *  It facilitates the following tasks:
     *
     *  <ul>
     *  <li>Retrieving an unsupported property.</li>
     *  <li>Retrieving a raw/text/XML property representation.</li>
     *  <li>Checking for existence of a certain property rather than 
     *  dealing with its default value.</li>
     *  <li>Accessing commented lines in a section.</li>
     *  </ul>
     *
     *  More functionality is described in the documentation for
     *  ms_customproperty.
     */
    class MS_MASCOTRESFILE_API ms_wwwoptions: public ms_customproperty
    {
        friend class ms_datfile;
    public:

        //! Default constructor.
        ms_wwwoptions();

        //! Copying constructor.
        ms_wwwoptions(const ms_wwwoptions& src);

        //! Destructor.
        ~ms_wwwoptions();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_wwwoptions* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_wwwoptions& operator=(const ms_wwwoptions& right);
#endif
        //! Check whether the section has been actually read from the file.
        /*!
         *  By default the <tt>WWW</tt> section is unavailable until it has 
         *  been set to a different state.
         */
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. whether it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Returns the total number of entries in the section.
        /*!
         *  Note that there might be several entries associated with each
         *  database.  Therefore, total number of entries differs with the
         *  number of databases in <tt>Databases</tt> section. 
         *
         *  The entries are not ordered and are represented as is. Use specific
         *  functions such as #getSeqEntryByName() and #getRepEntryByName() to
         *  retrieve the entry by name, or #getEntry() by the entry index.
         */
        int getNumberOfEntries() const;

        //! Returns an entry by its index.
        /*!
         *  See #getNumberOfEntries() for more information.
         *
         *  \param index The entry to return, between 0 and #getNumberOfEntries()-1.
         */
        const ms_wwwentry* getEntry(const int index) const;

        //! Searches the entries list for the given name and type of <tt>SEQ</tt>.
        /*!
         *  If an entry is not found, a null value is returned.
         *  Note that database names are case sensitive.
         */
        const ms_wwwentry* getSeqEntryByName(const char* dbName) const;
        
        //! Searches the entries list for the given name and type of <tt>REP</tt>.
        /*!
         *  If an entry is not found, a null value is returned.
         *  Note that database names are case sensitive.
         */
        const ms_wwwentry* getRepEntryByName(const char* dbName) const;

        //! Deletes all entries from the list.
        void clearEntries();
        
        //! Append a new entry which is a copy of the only parameter.
        void appendEntry(const ms_wwwentry* item);

        //! Update the entry at the given index.
        /*!
         * \param index The index of the entry to update, between 0 and
         * #getNumberOfEntries()-1.
         * \param item The data to update with. Member fields of \a item will 
         * copied to the underlying object.
         */
        void setEntry(const int index, const ms_wwwentry* item);

        //! Make the entry at the given index unavailable.
        /*! This is achieved by deleting the corresponding line from the
         * mascot.dat file. This does not affect the indices of other 
         * entries in this instance of the object. In other words, dropping
         * an entry will reset the value at that index to a default, and
         * the gap is plugged the next time the file is loaded.
         *
         * \param index The index of the entry to delete, between 0 and
         * #getNumberOfEntries()-1.
         */
        void dropEntry(const int index);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool sectionAvailable_;
        std::vector< ms_wwwentry* > entries_;
    }; // class ms_wwwoptions
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_WWWOPTIONS_HPP
