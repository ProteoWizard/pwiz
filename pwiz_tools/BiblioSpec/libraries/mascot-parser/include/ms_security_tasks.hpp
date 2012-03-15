/*
##############################################################################
# file: ms_security_tasks.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates Mascot security as used in authentication                     #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.27 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_security_tasks_INCLUDED_)
#define ms_security_tasks_INCLUDED_

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

// Includes from the standard template library
#include <string>
#include <map>
#include <time.h>
#include <vector>

namespace matrix_science {

/** @addtogroup security_group
 *  @{
 **/

    //! Each group has permission to do one or more tasks. This class defines a collection of tasks.
    /*! 
     * There is currently no programmatic interface for saving task
     * information, since this should normally only be done by developers. 
     *
     * Tasks are loaded from the file <tt>security_tasks.xml</tt>.
     *
     * Parameters, for example, the maximum number of queries for a group, or
     * the databases that can be used by a group are not saved in 
     * <tt>security_tasks.xml</tt>, but are saved as part of the group file.
     * However, the <b>type</b> of the parameter is saved in 
     * <tt>security_tasks.xml</tt>.
     */
    class MS_MASCOTRESFILE_API ms_security_tasks: public ms_errors
    {
    public:
        //! Definitions for TASKID
        /*!
         * See \ref DynLangEnums.
         *
         * Each task has one or more parameters associated with it. For
         * example, <i>"Access to administration pages"</i> would have
         * a boolean value.  Other tasks such as <i>"Maximum execution
         * time"</i> need a single number to determine the limit. 
         * <tt>security_tasks.xml</tt> has further information about each task.
        */
        enum TASKID
        {
            SECTASK_ALLOWPMF                =  1, //!< SEARCH: Allow PMF searches.
            SECTASK_ALLOWMSMS               =  2, //!< SEARCH: Allow MS-MS (and SQ) searches.
            SECTASK_ALLOWMSMSNOENZYME       =  3, //!< SEARCH: Allow no-enzyme MSMS searche.
            SECTASK_ALLOWPMFNOENZYME        =  4, //!< SEARCH: Allow no-enzyme PMF searches
            SECTASK_MAXCONCURRENTSEARCHES   =  5, //!< SEARCH: Maximum number of concurrent searches per user.
            SECTASK_MAXJOBPRIORITY          =  6, //!< SEARCH: Maximum mascot search job priority.
            SECTASK_MAXQUERIES              =  7, //!< SEARCH: Maximum number of queries per search.
            SECTASK_MAXEXECUTIONTIME        =  8, //!< SEARCH: Maximum execution time.
            SECTASK_ALLFASTA                =  9, //!< SEARCH: Allow all fasta databases to be searched.
            SECTASK_NAMEDFASTA              = 10, //!< SEARCH: Special fasta databases that override the default setting.
            SECTASK_MAXVARMODS              = 11, //!< SEARCH: Maximum number of variable modifications allowed for a standard search.
            SECTASK_MAXETVARMODS            = 12, //!< SEARCH: Maximum number of variable modifications allowed in an error tolerant search.

            SECTASK_SEESEARCHINGROUP        = 13, //!< VIEW: See search results from other people in your own group.
            SECTASK_SEEALLSEARCHESWITHUSERID= 14, //!< VIEW: See all search results with a USERID.
            SECTASK_SEEOLDSEARCHES          = 15, //!< VIEW: See search results without USERID field.
            SECTASK_USEMSREVIEWEXE          = 16, //!< VIEW: Allow user to view the search log.

            SECTASK_VIEWCONFIGUSINGMSSTATUS = 17, //!< GENERAL: View config files using \c ms-status.
            SECTASK_MODIFYOWNPROFILE        = 18, //!< GENERAL: Allow user to modify their own profile.

            SECTASK_DAEMONCLIENT            = 19, //!< CLIENT: Mascot Daemon is allowed to submit searches.
            SECTASK_DISTILLERCLIENT         = 20, //!< CLIENT: Mascot Distiller is allowed to submit searches.
            SECTASK_ALLOWSPOOFOTHERUSER     = 22, //!< CLIENT: For Mascot Daemon, allow spoofing of another user.
            SECTASK_INTEGRASYSTEMACCOUNT    = 23, //!< CLIENT: Integra system account.
            SECTASK_BIOTOOLSBATCH           = 24, //!< CLIENT: Bruker BioTools batch searches.
            SECTASK_SPOOFNAMEDGROUPSONLY    = 25, //!< CLIENT: For Mascot Daemon, restrict spoofing to the named group(s).

            SECTASK_ADMINPAGES              = 30, //!< ADMIN: Use the security administration utility.
            SECTASK_ACCESSDBSETUP           = 31, //!< ADMIN: Access to the Database Maintenance application.
            SECTASK_USEMSSTATUSEXE          = 32, //!< ADMIN: Allow use of Database Status application.
            SECTASK_MSSTATUSEXECLUSTER      = 33, //!< ADMIN: Allow user to see cluster pages in <tt>ms-status.exe</tt>.
            SECTASK_MSSTATUSEXERETRYDB      = 34, //!< ADMIN: Allow user to retry a failed database using <tt>ms-status.exe</tt>.
            SECTASK_KILLTASKINGROUP         = 35, //!< ADMIN: Kill / pause / change priority of searches from own group.
            SECTASK_KILLALLTASK             = 36, //!< ADMIN: Kill / pause / change priority of searches from other groups.
            SECTASK_VIEWCONFIG              = 37, //!< ADMIN: Access to the configuration editor to view configuration files.
            SECTASK_EDITCONFIG              = 38, //!< ADMIN: Access to the configuration editor to edit configuration files.

            SECTASK_MAXETACCESSIONS         = 50, //!< SEARCH: Maximum number of accessions allowed in an old style error tolerant search.
            SECTASK_MAXNOENZQUERIES         = 51, //!< SEARCH: Maximum number of queries for a no enzyme search. If not specified, SECTASK_MAXQUERIES will be used.
            SECTASK_MAXFASTAFILES           = 52, //!< SEARCH: Maximum number of fasta files in a single search.
            SECTASK_DENYQUANT               = 53, //!< SEARCH: Prevent the use of quantitation methods in a search.

            SECTASK_SEESEARCHINANYGROUP     = 60  //!< VIEW: See search results from any user in any group you belong to.
        };

        //! Create an empty list of tasks.
        ms_security_tasks();

        //! Destructor of the class.
        ~ms_security_tasks();

        // Copying constructor for c++ programs - don't document
        ms_security_tasks(const ms_security_tasks& src);

        //! Load the list of tasks from file.
        ms_security_tasks(const char * filename);

#ifndef SWIG
        // Assignment operator for c++ programs - don't document
        ms_security_tasks& operator=(const ms_security_tasks& right);
#endif
        //! Load definitions either from <tt>security_tasks.xml</tt> or from a group file.
        bool loadFromFile(const char * filename);

        //! Save definitions either to the <tt>security_tasks.xml</tt> file or to a group file.
        bool saveToFile(const char * filename);

        //! Add a task to the list.
        bool addTask(const ms_security_task & task);

        //! Remove a task from the list.
        bool removeTask(const int taskID);

        //! Remove all tasks from the list.
        void removeAllTasks();

        //! See if the task is permitted.
        bool isPermitted(const int taskID) const;

        //! See if the value is in the long integer parameter list.
        bool isPermitted_long(const int taskID, const long value) const;

        //! See if the value is in the floating point parameter list.
        bool isPermitted_double(const int taskID, const double value) const;

        //! See if the value is in the string parameter list.
        bool isPermitted_string(const int taskID, const std::string value) const;

        //! See if a particular fasta file can be accessed.
        bool isFastaPermitted(const std::string database) const;

        //! Returns the number of tasks in the collection.
        int getNumberOfTasks() const;

        //! Returns a particular task.
        ms_security_task getTask(const int num) const;

        //! Returns a particular task.
        ms_security_task getTaskFromID(const int taskID) const;

        //! Updates a task in the list with new parameters.
        bool updateTask(const ms_security_task & task);

        ms_security_tasks & operator+=(const ms_security_tasks & a);

        //! Returns differences between the two task lists as text.
        std::string getDiffsAsText(const ms_security_tasks & old);

        // Don't document
        void updateAllUsersTasks(const std::set<int> & allUsers);

    private:
        typedef std::map<int, ms_security_task> tasksSet;
        tasksSet    tasks_;

        void copyFrom(const ms_security_tasks* src);
    };
/** @} */ // end of security_group

}

#endif // !defined(ms_security_tasks_INCLUDED_)
