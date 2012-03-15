/*
##############################################################################
# file: ms_security_task.hpp                                                 #
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
#   $Revision: 1.12 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_sec_task_INCLUDED_)
#define ms_sec_task_INCLUDED_

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

// Includes from the standard template library
#include <string>
#include <set>
#include <time.h>
#include <vector>

namespace matrix_science {

/** @addtogroup security_group
 *  @{
 **/

    //! Each group has permission to do one or more tasks. This class defines an individual task.
    /*! 
     * There is currently no programmatic interface for saving task
     * information, since this should normally only be done by developers. 
     *
     * Tasks are loaded from the file <tt>config/security_tasks.xml</tt>.
     *
     * Parameters, for example, the maximum number of queries
     * for a group, or the databases that can be used by a group
     * are not saved in <tt>config/security_tasks.xml</tt>, but are saved 
     * as part of the group information. However, the <b>type</b> of the
     * parameter is saved in <tt>config/security_tasks.xml</tt>.
     */
    class MS_MASCOTRESFILE_API ms_security_task
    {
    public:
        //! Definitions for parameter type.
        /*!
         * See \ref DynLangEnums.
         *
         * Each task has one or more parameters associated with it. For
         * example, <i>"Access to administration pages"</i> would have
         * a boolean value.  Other tasks such as <i>"Maximum execution
         * time"</i> need a single number to determine the limit.
        */
        enum paramType
        {
            TYPE_BOOL           = 0x0000, //!< The default - A single boolean parameter that is always 'true'.
            TYPE_EQ_LONG        = 0x0001, //!< Single extra long integer parameter. The 'test' value must equal the stored value for this task to be permitted.
            TYPE_LTE_LONG       = 0x0002, //!< Single extra long integer parameter. The 'test' value must be less than or equal to the stored value for this task to be permitted.
            TYPE_GTE_LONG       = 0x0003, //!< Single extra long integer parameter. The 'test' value must be greater than or equal to the stored value for this task to be permitted.
            TYPE_LONG_ARRAY     = 0x0004, //!< Array of extra long integer parameters. The test value must equal one of these values for the task to be permitted.
            TYPE_EQ_DOUBLE      = 0x0005, //!< Single extra floating point parameter. The 'test' value must equal the stored value for this task to be permitted.
            TYPE_LTE_DOUBLE     = 0x0006, //!< Single extra floating point parameter. The 'test' value must be less than or equal to the stored value for this task to be permitted.
            TYPE_GTE_DOUBLE     = 0x0007, //!< Single extra floating point parameter. The 'test' value must be greater than or equal to the stored value for this task to be permitted.
            TYPE_DOUBLE_ARRAY   = 0x0008, //!< Array of extra floating point parameters. The test value must equal one of these values for the task to be permitted.
            TYPE_EQ_STRING      = 0x0009, //!< Single extra string parameter. The 'test' value must equal the stored value for this task to be permitted.
            TYPE_STRING_ARRAY   = 0x000A, //!< Array of extra string parameters. The test value must equal one of these values for the task to be permitted.
            TYPE_USERS_ARRAY    = 0x000B, //!< Array of user IDs, similar to TYPE_LONG_ARRAY. When this type of task is assigned to a group, it is automatically filled with all user IDs in the group.
            TYPE_ALL_USERS_ARRAY= 0x000C  //!< Array of user IDs, similar to TYPE_LONG_ARRAY. When this type of task is assigned to a group, it is automatically filled with the user IDs of all users that are in any group that the user belongs to.
        };

        //! Create a new empty object - not particularly useful.
	    ms_security_task();

        //! Destructor of the class.
	    ~ms_security_task();

        // Copying constructor for c++ programs - don't document
        ms_security_task(const ms_security_task& src);

#ifndef SWIG
        // Assignment operator for c++ programs - don't document
        ms_security_task& operator=(const ms_security_task& right);
#endif
        //! Returns the unique ID for the task.
	    int getID() const;

        //! Sets the unique ID for the task.
	    void setID(const int id);

        //! Returns the 'constant definition name' for the task.
        std::string getConstantName() const;

        void setConstantName(const std::string value);

        //! Returns any notes applied to the task. 
	    std::string getNotes() const;

        void setNotes(const std::string value);

        //! Returns a single line description for the task. 
	    std::string getDescription() const;

        void setDescription(const std::string value);

        //! Returns the 'type' of the parameter for a task.
        paramType getType() const;

        void setType(const paramType value);

        //! Clears all parameters.
        void clearParams();

        //! Set all parameters, regardless of type.
        void setParams(const std::string value);

        //! Add a long parameter to the task.
        bool addLongParam(const long value);

        //! Add a floating point parameter to the task.
        bool addDoubleParam(const double value);

        //! Add a string parameter to the task.
        bool addStringParam(const std::string value);

        //! Get the list of long parameters for the task.
        std::vector<long> getLongParams() const;

        //! Get the list of long parameters for the task.
        std::vector<double> getDoubleParams() const;

        //! Get the list of string parameters for the task.
        std::vector<std::string> getStringParams() const;

        //! Return all the parameters as a comma separated string.
        std::string getAllParamsAsString() const;

        //! See if the task is permitted.
        bool isPermitted() const;

        //! See if the value is in the long integer parameter list.
        bool isPermitted_long(const long value) const;

        //! See if the value is in the floating point parameter list.
        bool isPermitted_double(const double value) const;

        //! See if the value is in the string parameter list.
        bool isPermitted_string(const std::string value) const;

    private:
	    int id_;
	    std::string description_;
	    std::string notes_;
        std::string constantName_;
        paramType type_;
        std::set<long>           longParams_;
        std::set<double>         doubleParams_;
        std::set<std::string>    stringParams_;

        void copyFrom(const ms_security_task* src);
    };
/** @} */ // end of security_group
}

#endif // !defined(ms_sec_task_INCLUDED_)
