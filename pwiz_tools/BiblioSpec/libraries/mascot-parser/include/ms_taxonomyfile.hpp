/*
##############################################################################
# file: ms_taxonomyfile.hpp                                                  #
# 'msparser' toolkit                                                         #
# Encapsulates "taxonomy"-file that available taxonomy choices               #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_taxonomyfile.hpp        $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.11 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_TAXONOMYFILE_HPP
#define MS_TAXONOMYFILE_HPP

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

    //! The class represents a single taxonomy choice entry in the <tt>taxonomy</tt> file.
    /*!
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
    class MS_MASCOTRESFILE_API ms_taxonomychoice: public ms_customproperty
    {
        friend class ms_taxonomyfile;
    public:

        //! Default constructor.
        ms_taxonomychoice();

        //! Copying constructor.
        ms_taxonomychoice(const ms_taxonomychoice& src);

        //! Destructor.
        ~ms_taxonomychoice();

        //! Re-initialises the object.
        void defaultValues();

        //! Copies modification configuration from another instance.
        void copyFrom(const ms_taxonomychoice* src);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_taxonomychoice& operator=(const ms_taxonomychoice& right);
#endif
        //! Returns the taxonomy choice title.
        std::string getTitle() const;

        //! Change the title for taxonomy choice.
        void setTitle(const char* value);

        //! Returns a total number of included taxonomy IDs.
        int getNumberOfIncludeTaxonomies() const;

        //! Traverse the included taxonomy IDs.
        int getIncludeTaxonomy(const int n) const;

        //! Deletes all taxonomy IDs from the include list.
        void clearIncludeTaxonomies();

        //! Adds one more taxonomy ID to the include list.
        void appendIncludeTaxonomy(const int id);

        //! Returns a total number of excluded taxonomy IDs.
        int getNumberOfExcludeTaxonomies() const;

        //! Traverse the excluded taxonomy IDs.
        int getExcludeTaxonomy(const int n) const;

        //! Deletes all taxonomy IDs from the exclude list.
        void clearExcludeTaxonomies();

        //! Adds one more taxonomy ID to the exclude list.
        void appendExcludeTaxonomy(const int id);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        void setCustomProperty();

    private:
        std::string title;

        std::vector< int > includeList;
        std::vector< int > excludeList;
    }; // ms_taxonomychoice

    class ms_filesource;

    //! Use this class in order to read in a taxonomy file.
    /*!
     *  The list of taxonomy choices in the search form is taken from the
     *  taxonomy file. The file consists of several entries. The first line of
     *  each entry must start with the <tt>Title:</tt> keyword, followed by
     *  a text string that is used to identify the species in forms and
     *  reports. The definition should be short and self-explanatory. 
     *
     *  To show the tree structure, indentation can be used. Unfortunately, it
     *  is not possible to use tabs or multiple spaces for indentation in an
     *  HTML form, so a full stop (period) and a space are used to indent the
     *  list. Internal spaces are significant, and there should never be two or
     *  more spaces together. 
     *
     *  This should be followed with a definition line starting with the
     *  <tt>Include:</tt> keyword, followed by one or more numbers separated
     *  with commas. With the supplied MSDB database, and supplied taxonomy
     *  files, these numbers should be the NCBI taxonomy ID. 
     *
     *  This should be followed with a definition line starting with the
     *  <tt>Exclude:</tt> keyword, followed by one or more numbers separated
     *  with commas. Any sequence with a taxonomy ID that passes the 
     *  'include' test may then be rejected by any entry in the exclude list.
     *
     *  Finally, each entry must end with a <tt>*</tt>.
     */
    class MS_MASCOTRESFILE_API ms_taxonomyfile: public ms_errors
    {
    public:
        //! Default constructor.
        ms_taxonomyfile();

        //! Copying constructor.
        ms_taxonomyfile(const ms_taxonomyfile& src);

        //! Immediate-action constructor that reads the given file on construction.
        ms_taxonomyfile(const char* filename, const matrix_science::ms_connection_settings * cs = 0); 

        //! Destructor.
        ~ms_taxonomyfile();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_taxonomyfile* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_taxonomyfile& operator=(const ms_taxonomyfile& right);
#endif
        //! Call this member to set a custom file name to read from a different location.
        void setFileName(const char* name);

        //! Returns a file name that is used to read the file.
        std::string getFileName() const;

        //! Sets the sessionID and proxy server for use with an http transfer.
        /*!
         * This value would normally be passed in the constructor.
         * \param cs Is the new connection settings.
         */
        void setConnectionSettings(const matrix_science::ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        /*! See also the constructor documentation and setConnectionSettings().
         * \return The current connection settings.
         */
        matrix_science::ms_connection_settings getConnectionSettings() const;

        //! Reads configuration information from the file.
        void read_file();

        //! Reads and parses an in-memory null-terminated buffer instead of a disk file.
        void read_buffer(const char* buffer);

        //! Stores modification information in the file.
        void save_file();

        //! Returns a number of taxonomy choices currently held in memory.
        int getNumberOfEntries() const;

        //! Deletes all taxonomy choices from the list.
        void clearEntries();

        //! Adds a new taxonomy choice at the end of the list.
        void appendEntry(const ms_taxonomychoice* item);

        //! Returns a taxonomy choice entry by its number.
        const ms_taxonomychoice * getEntryByNumber(const int index) const;

        //! Returns a taxonomy choice entry by its name or NULL in case of not found.
        ms_taxonomychoice * getEntryByName(const char* name);

    private:
        void read_internal(ms_filesource *pFSource);

        typedef std::vector<ms_taxonomychoice* > entries_vector;
        entries_vector  entries;

        std::string     filename_;
        std::vector< std::string > comments_;
        ms_connection_settings cs_;
    }; // ms_taxonomyfile
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_TAXONOMYFILE_HPP

/*------------------------------- End of File -------------------------------*/
