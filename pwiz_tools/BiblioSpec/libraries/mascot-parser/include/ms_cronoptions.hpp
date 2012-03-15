/*
##############################################################################
# file: ms_cronoptions.hpp                                                   #
# 'msparser' toolkit                                                         #
# Represents parameters from cron-section of 'mascot.dat' configuration file #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_cronoptions.hpp         $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.8 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_CRONOPTIONS_HPP
#define MS_CRONOPTIONS_HPP

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

    //! Represents a single entry from the <tt>cron</tt> section of <tt>mascot.dat</tt>.
    /*!
     *  Instances of this class can be stored in ms_cronoptions.
     */
    class MS_MASCOTRESFILE_API ms_cronjob
    {
        friend class ms_datfile;
        friend class ms_cronoptions;

    public:
        //! Default constructor.
        ms_cronjob();

        //! Copying constructor.
        ms_cronjob(const ms_cronjob& src);

        //! Default destructor.
        ~ms_cronjob();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_cronjob* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_cronjob& operator=(const ms_cronjob& right);
#endif
        //! Returns <b>TRUE</b> for a valid minute number, <b>FALSE</b> otherwise.
        /*!
         *  A cron job entry in the configuration file specifies time to run
         *  a command.  Use this function to retrieve the 'minute' part of this
         *  specification.
         *
         *  \param min a minute number (0..59).
         *  \return By default <b>FALSE</b> is returned for all minutes.
         */
        bool isMinute(const int min) const;

        //! Set valid/illegal minutes.
        /*!
         *  \sa #isMinute()
         *
         *  \param min a minute number (0..59)
         *  \param value a new boolean flag for the given minute
         */
        void setMinute(const int min, const bool value);

        //! Returns <b>TRUE</b> for a valid hour number, <b>FALSE</b> otherwise.
        /*!
         *  A cron job entry in the configuration file specifies time to run
         *  a command.  Use this function to retrieve the 'hour' part of this
         *  specification.
         *
         *  \param hour an hour number (0..23).
         *  \return By default <b>FALSE</b> is returned for all hours.
         */
        bool isHour(const int hour) const;

        //! Set valid/illegal hours.
        /*!
         *  \sa #isHour()
         *
         *  \param hour an hour number (0..23).
         *  \param value a new boolean flag for the given hour.
         */
        void setHour(const int hour, const bool value);

        //! Returns <b>TRUE</b> for a valid day number, <b>FALSE</b> otherwise.
        /*!
         *  A cron job entry in the configuration file specifies time to run
         *  a command.  Use this function to retrieve the 'day' part of this
         *  specification.
         *
         *  \param day a day number (1..31).
         *  \return By default <b>FALSE</b> is returned for all days.
         */
        bool isDayOfMonth(const int day) const;

        //! Set valid/illegal days.
        /*!
         *  \sa #isDayOfMonth()
         *
         *  \param day an hour number (1..31).
         *  \param value a new boolean flag for the given day.
         */
        void setDayOfMonth(const int day, const bool value);

        //! Returns <b>TRUE</b> for a valid month number, <b>FALSE</b> otherwise.
        /*!
         *  A cron job entry in the configuration file specifies time to run
         *  a command.  Use this function to retrieve the 'month' part of this
         *  specification.
         *
         *  \param month a month number (1..12).
         *  \return By default <b>FALSE</b> is returned for all months.
         */
        bool isMonthOfYear(const int month) const;

        //! Set valid/illegal months.
        /*!
         *  \sa #isMonthOfYear()
         *
         *  \param month a month number (1..12).
         *  \param value a new boolean flag for the given month.
         */
        void setMonthOfYear(const int month, const bool value);

        //! Returns <b>TRUE</b> for a valid day of week number, <b>FALSE</b> otherwise.
        /*!
         *  A cron job entry in the configuration file specifies time to run
         *  a command.  Use this function to retrieve the 'day of week' part of
         *  this specification.
         *
         *  \param day a day of week number (0..6, 0=Sunday).
         *  \return By default <b>FALSE</b> is returned for all days.
         */
        bool isDayOfWeek(const int day) const;

        //! Set valid/illegal days.
        /*!
         *  \sa #isDayOfWeek()
         *
         *  \param day a day of week number (0..6, 0=Sunday).
         *  \param value a new boolean flag for the given day.
         */
        void setDayOfWeek(const int day, const bool value);

        //! Returns a string that is executed by the shell (command prompt) at the specified times.
        /*!
         *  By default this is empty.
         */
        std::string getCommandStr() const;

        //! Allows to change is a string that is executed by the shell (command prompt) at the specified times.
        void setCommandStr(const char* str);

        // used internally
        std::string getStringValue() const;

    private:

        bool  minute_[60];            /* index of 0-59 - Value of true means do it  */
        bool  hour_[24];              /* index of 0-23 - Value of true means do it  */
        bool  dayOfMonth_[32];        /* index of 1-31 - Value of true means do it  */
        bool  monthOfYear_[13];       /* index of 1-12 - Value of true means do it  */
        bool  dayOfWeek_[7];          /* index of  0-6 (0 = Sunday)                 */
        std::string szCommand_;

        void setCustomString();
        std::string customString_;
    }; // class ms_cronjob

    //! Contains parameters from the <tt>cron</tt> section of <tt>mascot.dat</tt>.
    /*!
     *  On Unix systems, <tt>cron</tt> can be used to automate routine
     *  procedures such as the overnight updating of sequence database files.
     *  Windows 2000 includes an equivalent utility called <tt>Scheduled
     *  Tasks</tt>.  Windows NT does not include a suitable utility so, as
     *  a convenience to Windows NT users, <tt>ms_monitor.exe</tt> can be
     *  configured to emulate the functionality of cron.
     *
     *  For detailed information on any of the cron-parameters please 
     *  consult Mascot manual.
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
    class MS_MASCOTRESFILE_API ms_cronoptions: public ms_customproperty
    {
        friend class ms_datfile;
    public:

        //! Default constructor.
        ms_cronoptions();

        //! Copying constructor.
        ms_cronoptions(const ms_cronoptions& src);

        //! Destructor.
        ~ms_cronoptions();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_cronoptions* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_cronoptions& operator=(const ms_cronoptions& right);
#endif
        //! Returns <b>TRUE</b> if the section was actually read from a file.
        bool isSectionAvailable() const;

        //! Changes availability of the section, meaning whether it should be stored in a file.
        void setSectionAvailable(const bool value);

        //! Returns <b>TRUE</b> if parameter <b>CronEnabled</b> is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         *  The first parameter in the <tt>cron</tt> section of
         *  <tt>mascot.dat</tt> is always <b>CronEnabled</b> parameter. The
         *  rest is a list of cron-jobs.  See #getNumberOfCronJobs() for more
         *  information about them.
         *
         *  <b>CronEnable</b> should be set to <b>1</b> to enable cron
         *  functionality, <b>0</b> to disable. By default it is disabled.
         */
        bool isCronEnabled() const;

        //! Change <b>CronEnabled</b>.
        /*!
         *  See #isCronEnabled() for more information.
         */
        void setCronEnabled(const bool value);

        //! Returns the total number of cron-jobs configured.
        /*!
         *  Every line (except the first one) in the <tt>cron</tt> section
         *  represents a cron-job. This function returns a number of such lines
         *  (except commented out).
         */
        int getNumberOfCronJobs() const;

        //! Returns an object describing a single cron-job by its number.
        /*!
         *  All cron-jobs are assigned consecutive ID numbers (0 based). Use
         *  them in order to retrieve a particular cron-job definition.
         *
         *  \param idx a number from 0 to (#getNumberOfCronJobs()-1)
         *  \return instance of ms_cronjob-class describing the corresponding entry.
         */
        const ms_cronjob* getCronJob(const int idx) const;

        //! Delete all cron-job entries.
        void clearCronJobs();

        //! Add a new job entry at the end of the list.
        void appendCronJob(const ms_cronjob* job);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool                        sectionAvailable_;
        bool                        cronEnabled_;
        std::vector< ms_cronjob* >  cronJobArray_;
    }; // ms_cronoptions
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_CRONOPTIONS_HPP

/*------------------------------- End of File -------------------------------*/
