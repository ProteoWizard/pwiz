/*
##############################################################################
# file: ms_masses.hpp                                                        #
# 'msparser' toolkit                                                         #
# Encapsulates "masses"-file that defines atom and residue masses            #
# about several consecutive erros                                            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_masses.hpp              $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.20 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASSES_HPP
#define MS_MASSES_HPP

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


namespace matrix_science {

    class ms_umod_configfile; // forward declaration
    class ms_quant_component; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Reads and parses the <tt>masses</tt> file with residue and atom masses.
    /*!
     *  After reading a file and before using the object, check its validity by
     *  calling #isValid() and retrieve error descriptions with
     *  #getLastErrorString() if not valid.
     *
     *  A class instance can be used even if the configuration file has been
     *  read with problems or not read at all.  Default values will be given to
     *  all members in that case.
    */
    class MS_MASCOTRESFILE_API ms_masses: public ms_errors
    {
    public:
        //! Default constructor.
        ms_masses();

        //! Copying constructor.
        ms_masses(const ms_masses& src);

        //! Copying constructor.
        ms_masses(const ms_umod_configfile& src);

        //! Constructor for prepared isotope-substituted version of masses.
        ms_masses(const ms_umod_configfile& src, const ms_quant_component& quantComp);

        //! Reads the file right after the construction.
        ms_masses(const char* filename, const matrix_science::ms_connection_settings * cs = 0);

        //! Destructor.
        ~ms_masses();

        //! Use this member to re-initialise internal members.
        void defaultValues();

        //! Copies masses from another instance.
        void copyFrom(const ms_masses* right);

        //! Copies masses from a Unimod file.
        void copyFrom(const ms_umod_configfile *right);

        //! Change residue masses according to the list of fixed modifications.
        ms_modvector applyFixedMods(const ms_modvector *mods, ms_errs *err);

        //! Update masses of amino-acids with isotope substitution.
        void applyIsotopes(const ms_umod_configfile *umodFile, const ms_quant_component *quantComp);

        //! Return true if other ms_masses is the same as this one.
        bool isSame(const ms_masses& other, const MASS_TYPE massType) const;

#ifndef SWIG
        //! C++ style assignment operator.
        ms_masses& operator=(const ms_masses& right);
#endif
        //! Returns a file name set before or by default.
        std::string getFileName() const;

        //! Set a custom file name instead of <tt>../config/masses</tt>.
        void setFileName(const char* name);

        //! Sets the sessionID and proxy server for use with an HTTP transfer.
        /*!
         * This value would normally be passed in the constructor.
         *
         * \param cs is the new connection settings.
         */
        void setConnectionSettings(const matrix_science::ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        /*! See also the constructor documentation and setConnectionSettings().
         *
         * \return The current connection settings.
         */
        matrix_science::ms_connection_settings getConnectionSettings() const;

        //! Reads the configuration file, parses it and fill the mass arrays.
        void read_file();

        //! Saves the information back to the file.
        void save_file();

        //! Returns a mass for a given amino acid.
        double getResidueMass(const matrix_science::MASS_TYPE massType, const char residue) const;

        //! Changes mass values for a single residue.
        void setResidueMasses(const char residue, const double massMono, const double massAve);

        //! Returns N-term modification delta if there is a correspondent fixed modification applied.
        double getNtermDelta(const matrix_science::MASS_TYPE massType) const;

        //! Returns N-terminal group mass (i.e. fixed modification delta or H-mass).
        double getNterminalMass(const matrix_science::MASS_TYPE massType) const;

        //! Returns N-term neutral loss value if there is a correspondent fixed modification applied.
        double getNtermNeutralLoss(const matrix_science::MASS_TYPE massType) const;

        //! Set N-term modification.
        void setNtermModification(const double monoDelta, 
                                  const double aveDelta, 
                                  const double monoNeutralLoss = 0.0,
                                  const double aveNeutralLoss = 0.0);

        //! Returns C-term modification delta if there is a correspondent fixed modification applied.
        double getCtermDelta(const matrix_science::MASS_TYPE massType) const;

        //! Returns C-terminal group mass (i.e. fixed modification delta or OH-mass).
        double getCterminalMass(const matrix_science::MASS_TYPE massType) const;

        //! Returns C-term neutral loss value if there is a correspondent fixed modification applied.
        double getCtermNeutralLoss(const matrix_science::MASS_TYPE massType) const;

        //! Set C-term modification.
        void setCtermModification(const double monoDelta, 
                                  const double aveDelta, 
                                  const double monoNeutralLoss = 0.0,
                                  const double aveNeutralLoss = 0.0);

        //! Returns a residue mass with neutral loss after applying fixed modifications.
        double getFragResidueMass(const matrix_science::MASS_TYPE massType, const char residue) const;

        //! Returns <b>TRUE</b> for residues modified by fixed modifications.
        bool isResidueModified(const char residue) const;

        //! Applies a single residue modification.
        void setResidueModification(const double monoDelta, 
                                    const double aveDelta, 
                                    const double monoNeutralLoss,
                                    const double aveNeutralLoss,
                                    const char *residues);

        //! Returns a hydrogen (H) atom mass for a given mass type (MONO or AVE).
        double getHydrogenMass(const matrix_science::MASS_TYPE massType) const;

        //! Changes hydrogen mass values.
        void setHydrogenMass(const double massMono, const double massAve);

        //! Returns a carbon(C) atom mass for a given mass type (MONO or AVE).
        double getCarbonMass(const matrix_science::MASS_TYPE massType) const;

        //! Changes carbon mass values.
        void setCarbonMass(const double massMono, const double massAve);

        //! Returns a nitrogen (N) atom mass for a given mass type (MONO or AVE).
        double getNitrogenMass(const matrix_science::MASS_TYPE  massType) const;

        //! Changes nitrogen mass values.
        void setNitrogenMass(const double massMono, const double massAve);

        //! Returns a oxygen (O) atom mass for a given mass type (MONO or AVE).
        double getOxygenMass(const matrix_science::MASS_TYPE  massType) const;

        //! Changes oxygen mass values.
        void setOxygenMass(const double massMono, const double massAve);

        //! Returns an electron mass.
        double getElectronMass() const;

        //! Changes oxygen mass values.
        void setElectronMass(const double mass);

        // used internally
        void setStorage(double* res_mono, 
                        double* res_ave, 
                        double* hydrogen, 
                        double* carbon, 
                        double* nitrogen, 
                        double* oxygen, 
                        double* electron);

        // used internally
        void defaultMasses(double* res_mono, 
                           double* res_ave, 
                           double* hydrogen, 
                           double* carbon, 
                           double* nitrogen, 
                           double* oxygen, 
                           double* electron);

    private:

        bool getTwoMasses(char * line, 
                          char *lineForError, 
                          double *pMono, 
                          double *pAve, 
                          const double dMin, 
                          const double dMax);

        void setCustomProperty();

    private:
        // final destinations - they are assigned either 
        // 1) supplied array pointers 
        // 2) the internal storages
        double* res_mono;
        double* res_ave;

        // mass combined with neutral loss for residue-specific mods
        bool res_mod[26];
        double* frag_res_mono;
        double* frag_res_ave;

        // masses of terminal groups for terminus mods
        double ntermDelta[2];
        double ctermDelta[2];

        // neutral losses on terminal groups for terminus mods
        double neutralLossNterm[2];
        double neutralLossCterm[2];

        double* hydrogen;
        double* carbon;
        double* nitrogen;
        double* oxygen;

        double* electron;

        // internal storages in case they are not supplied with a proper constructor
        double _res_mono[26];
        double _res_ave[26];
        double _frag_res_mono[26];
        double _frag_res_ave[26];

        double _hydrogen[2];
        double _carbon[2];
        double _nitrogen[2];
        double _oxygen[2];

        double _electron;

        std::string _fn;
        ms_customproperty _custprop;
        ms_connection_settings cs_;
    }; // class ms_masses
    /** @} */ // end of config_group
}

#endif // MS_MASSES_HPP

