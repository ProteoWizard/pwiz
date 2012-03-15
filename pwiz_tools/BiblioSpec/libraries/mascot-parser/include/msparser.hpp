/*
##############################################################################
# File: msparser.hpp                                                         #
# Mascot Parser toolkit                                                      #
# Master include file                                                        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Source: /vol/cvsroot/parser/inc/msparser.hpp,v $
#    $Author: davidc $ 
#      $Date: 2009-07-08 14:55:57 $ 
#  $Revision: 1.21 $
##############################################################################
*/

//////////////////////////////////////////////////////////////////////////////
//                      Configuration files functionality                   //
//////////////////////////////////////////////////////////////////////////////

// Some necessary constants
#include "msparser_lim.hpp"

// General-purpose functionality or base classes
#include "ms_errors.hpp"                // generic error-handling mechanism
#include "ms_customproperty.hpp"        // custom property functionality
#include "ms_fileutilities.hpp"         // file-handling utilities
#include "ms_connection_settings.hpp"   // proxy server and sessionID for config files
#include "ms_tinycdb.hpp"               // cdb index files

// 'mascot.dat' file
#include "ms_computeraddress.hpp"       // auxiliary definition classes
#include "ms_mascotfiles.hpp"           // location of other configuration files
#include "ms_clusterparams.hpp"         // 'Cluster'-section
#include "ms_unigeneoptions.hpp"
#include "ms_cronoptions.hpp"           // 'Cron'-section
#include "ms_databaseoptions.hpp"       // 'Databases'-section
#include "ms_parserule.hpp"             // 'Parse'-section
#include "ms_processoroptions.hpp"      // 'Processor'-section
#include "ms_taxonomyrules.hpp"         // 'Taxonomy_XXX'-section
#include "ms_wwwoptions.hpp"            // 'WWW'-section
#include "ms_mascotoptions.hpp"         // 'Options'-section
#include "ms_datfile.hpp"               // main class that represents the whole file

// 'enzymes' file
#include "ms_enzyme.hpp"

// 'fragmentation_rules' file
#include "ms_fragmentationrules.hpp"

// 'mascot.license' file
#include "ms_license.hpp"

#include "ms_umod_configfile.hpp"

// 'mod_file' and 'substitutions' files
#include "ms_modfile.hpp"

// 'masses file'
#include "ms_masses.hpp"

// CPU configuration retreival
#include "ms_processors.hpp"

// 'taxonomy' file
#include "ms_taxonomyfile.hpp"

#include "ms_fragment.hpp"
#include "ms_fragmentvector.hpp"

// authentication
#include "ms_security_task.hpp"
#include "ms_security_tasks.hpp"
#include "ms_security_user.hpp"
#include "ms_security_group.hpp"
#include "ms_security_options.hpp"
#include "ms_security.hpp"
#include "ms_security_session.hpp"

// quantitation.xml programming interface
#include "ms_xml_typeinfo.hpp"
#include "ms_xml_schema.hpp"
#include "ms_quant_moverz.hpp"
#include "ms_quant_correction.hpp"
#include "ms_quant_isotope.hpp"
#include "ms_quant_component.hpp"
#include "ms_quant_composition.hpp"
#include "ms_quant_parameters.hpp"
#include "ms_quant_integration.hpp"
#include "ms_quant_neutralloss.hpp"
#include "ms_quant_specificity.hpp"
#include "ms_quant_pepneutralloss.hpp"
#include "ms_quant_localdef.hpp"
#include "ms_quant_unmodified.hpp"
#include "ms_quant_modgroup.hpp"
#include "ms_quant_quality.hpp"
#include "ms_quant_outliers.hpp"
#include "ms_quant_normalisation.hpp"
#include "ms_quant_precursor.hpp"
#include "ms_quant_average.hpp"
#include "ms_quant_multiplex.hpp"
#include "ms_quant_reporter.hpp"
#include "ms_quant_replicate.hpp"
#include "ms_quant_protocol.hpp"
#include "ms_quant_numerator.hpp"
#include "ms_quant_ratio.hpp"
#include "ms_quant_method.hpp"
#include "ms_quant_configfile.hpp"
#include "ms_quant_satellite.hpp"
#include "ms_quant_normalisation_peptide.hpp"
#include "ms_quant_normalisation_protein.hpp"

// unimod.xml programming interface
#include "ms_umod_element.hpp"
#include "ms_umod_modification.hpp"
#include "ms_umod_aminoacid.hpp"
#include "ms_umod_modbrick.hpp"
#include "ms_umod_specificity.hpp"
#include "ms_umod_composition.hpp"
#include "ms_umod_neutralloss.hpp"
#include "ms_umod_elemref.hpp"
#include "ms_umod_xref.hpp"

//////////////////////////////////////////////////////////////////////////////
//                      Mascot result files functionality                   //
//////////////////////////////////////////////////////////////////////////////

#include "ms_mascotrespeptide.hpp"
#include "ms_mascotresfile.hpp"
#include "ms_mascotresprotein.hpp"
#include "ms_mascotresults.hpp"
#include "ms_mascotresproteinsum.hpp"
#include "ms_mascotrespeptidesum.hpp"
#include "ms_inputquery.hpp"
#include "ms_mascotresparams.hpp"
#include "ms_mascotresunigene.hpp"

#include "ms_aahelper.hpp"
#include "ms_zip.hpp"
#include "ms_shapiro_wilk.hpp"
#include "ms_obofile.hpp"

