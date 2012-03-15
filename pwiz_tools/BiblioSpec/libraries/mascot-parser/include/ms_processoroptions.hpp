/*
##############################################################################
# file: ms_processoroptions.hpp                                              #
# 'msparser' toolkit                                                         #
# Represents parameters of "Processors" section of "mascot.dat"-file         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_processoroptions.hpp    $ #
#     $Author: villek $ #
#       $Date: 2010-09-10 10:22:35 $ #
#   $Revision: 1.9 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_PROCESSOROPTIONS_HPP
#define MS_PROCESSOROPTIONS_HPP

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

    //! Represent a single dabatase entry in the <tt>Processors</tt> section.
    class MS_MASCOTRESFILE_API ms_dbprocessors
    {
        friend class ms_datfile;
        friend class ms_processoroptions;
    public:
        //! Default constructor.
        ms_dbprocessors();

        //! Copying constructor.
        ms_dbprocessors(const ms_dbprocessors& src);

        //! Destructor.
        ~ms_dbprocessors();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_dbprocessors* right);

#ifndef SWIG
        //! Assinment operator for C++ client applications.
        ms_dbprocessors& operator=(const ms_dbprocessors& right);
#endif
        //! Return database name for the entry.
        std::string getName() const;

        //! Change the database name of the entry.
        void setName(const char * str);

        //! Returns a number of values (threads/processor IDs) specified for the database.
        int getNumberOfThreads() const;

        //! Deletes all threads (processor IDs) from the entry.
        void clearThreads();

        //! Returns a single processor ID by its index from <b>0</b> to (#getNumberOfThreads()-1).
        int getThreadProcessorID(const int threadIndex) const;

        //! Adds a new thread with a processor ID to the list.
        void appendThreadProcessorID(const int proccessorID);
    private:
        std::string         name_;
        std::vector<int>    threads_;

        std::string getStringValue() const;
    }; // class ms_dbprocessors

    //! An instance of this class represents all the parameters specified in the <b>Processors</b> section of <tt>mascot.dat</tt>.
    /*!
     *  An instance of this class is created and populated in ms_datfile.  It
     *  can also be created separately and initialized with default values.
     *  You can create an instance of the class or copy from another instance
     *  in order to pass it then as an parameters-containing object.  For
     *  detailed information on any of the options please consult Mascot
     *  manual.
     *
     *  Most of the parameters here can be accessed using a dabatase number,
     *  which corresponds to database indices in ms_databases.
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
    class MS_MASCOTRESFILE_API ms_processoroptions : public ms_customproperty
    {
        friend class ms_datfile;
    public:

        //! Default constructor.
        ms_processoroptions();

        //! Copying constructor.
        ms_processoroptions(const ms_processoroptions& src);

        //! Destructor.
        ~ms_processoroptions();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_processoroptions* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_processoroptions& operator=(const ms_processoroptions& right);
#endif
        //! Check whether the section has been actually read from the file.
        /*!
         *  By default the <tt>Processors</tt> section is unavailable until it
         *  has been set to a different state.
         */
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. whether it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Returns a number of processors specified in the <b>ProcessorSet</b> parameter.
        /*!
         *  The <b>ProcessorSet</b> line specifies the complete set of
         *  processors to be used.  The number in this list must be less than
         *  or equal to the number processors licensed, or the system will not
         *  run.
         *
         *  There can be up to 64 CPUs specified in the bit mask. Each bit can
         *  be set either to <b>1</b> or <b>0</b> for a listed CPU or not
         *  listed one respectively.  Following this parameter, the processors
         *  to be used for each database are specified in <tt>mascot.dat</tt>.
         *  These numbers must be a subset of the numbers in the
         *  <b>ProcessorSet</b>, and there must be the same number of values as
         *  the number of threads specified earlier in the database section. 
         *
         *  \sa ms_processors::getWhichForDB(), ms_processors::getDBValSetInMascotDat()
         *
         *  By default the list is empty.
         */
        int getNumberOfProcessors() const;

        //! Deletes all processors from the <b>ProcessorSet</b> list.
        void clearProcessors();

        //! Returns a single processor ID by its index.
        int getProcessor(const int index) const;

        //! Adds a new processor to the list for the <b>ProcessorSet</b> parameter.
        void appendProcessor(const int processorID);

        //! Returns number of individual database entries in the section.
        /*!
         *  Each of the lines corresponds to a certain database.  All CPU
         *  numbers are listed in the same database entry and correspond to
         *  threads.  A number of CPUs listed for the database must be the same
         *  as number of threads specified for the database in
         *  <tt>Databases</tt> section of <tt>mascot.dat</tt>. Also, all CPUs
         *  listed here must belong to the set of CPUs specified by
         *  <b>ProcessorSet</b> parameter.
         *
         *  By default the list of databases is empty.
         */
        int getNumberOfDatabases() const;

        //! Returns an individual database entry.
        const ms_dbprocessors* getDatabase(const int index) const;

        //! Deletes all database entries.
        void clearDatabases();

        //! Adds a new database to the list.
        void appendDatabase(const ms_dbprocessors* db);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool                            sectionAvailable_;
        std::vector<int>                processors_;
        std::vector<ms_dbprocessors*>   dbs_;
    }; // class ms_processoroptions
    /** @} */ // end of config_group
} // namespace matrix_science;

#endif // MS_PROCESSOROPTIONS_HPP

/*------------------------------- End of File -------------------------------*/
