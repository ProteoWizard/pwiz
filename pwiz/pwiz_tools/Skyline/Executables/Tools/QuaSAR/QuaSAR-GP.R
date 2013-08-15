# $Id: QuaSAR-GP.R 107 2012-06-25 18:37:49Z rahmad $
print ("$Id: QuaSAR-GP.R 107 2012-06-25 18:37:49Z rahmad $", quote=FALSE)

# The Broad Institute of MIT and Harvard
# SOFTWARE COPYRIGHT NOTICE AGREEMENT
# This software and its documentation are copyright (2009) by the
# Broad Institute. All rights are reserved.

# This software is supplied without any warranty or guaranteed support
# whatsoever. The Broad Institute cannot be responsible for its
# use, misuse, or functionality.

## suppress warnings (else GenePattern thinks an error has occurred)
options (warn = -1)
debug = FALSE

##
## Command line processing and other functions to support GenePattern
##

parse.cmdline <- function () {
  # set up for command line processing (if needed)
  # arguments are specified positionally (since there are no optional arguments) and ...
  arguments <- commandArgs(trailingOnly=TRUE)
  if ( length (arguments) != 23)
    # expected arguments not present -- error
    stop ("USAGE: R --slave --no-save --args '<quasar.r> <common.r> <skyline.file> <concentration.file> <title> <analyte> <standard.present> <standard> <units>\n
                  <generate.cv.table> <generate.calcurves> <number.transitions.plot> <generate.lodloq.table> <generate.peak.area.plots> <use.par> <max.calcurve.linear.scale>\n
                  <max.calcurve.log.scale> <perform.audit> <audit.cv.threshold> <output.prefix>' < QuaSAR-GP.r\n") #<libdir>
    
  for (i in 1:23) {
    arg <- arguments [i]
    # remove leading and trailing blanks
    arg <- gsub ("^ *", "", arg)
    arg <- gsub (" *$", "", arg)
    # remove any embedded quotation marks
    arg <- gsub ("['\'\"]", "", arg)
    # if (i==1) libdir <<- arg
	if (i==1) quasarScript <<- arg
	if (i==2) commonScript <<- arg
    if (i==3) skylineFile <<- arg
    if (i==4) concentrationFile <<- arg
    if (i==5) site <<- arg
    if (i==6) analyteName <<- arg
    if (i==7) internalStandard <<- as.numeric (arg)
    if (i==8) standardName <<- arg
    if (i==9) unitsLabel <<- arg
    if (i==10) generateCVTable <<- as.numeric (arg)
    if (i==11) generateCalcurves <<- as.numeric (arg)
    if (i==12) nTransitionsPlot <<- as.numeric (arg)
    if (i==13) generateLODLOQTable <<- as.numeric (arg)
    if (i==14) generateLODLOQcomparison <<- as.numeric (arg)
    if (i==15) generatePeakAreaPlots <<- as.numeric (arg)
    if (i==16) usePAR <<- as.numeric (arg)
    if (i==17) maxLinearScale <<- as.numeric (arg)
    if (i==18) maxLogScale <<- as.numeric (arg)
    if (i==19) performAudit <<- as.numeric (arg)
    if (i==20) CVThreshold <<- as.numeric (arg)
    if (i==21) performEndogenouscalc <<- as.numeric (arg)
    if (i==22) endogenousCI <<- as.numeric (arg)
    if (i==23) outputPrefix <<- arg
  }
  # if (libdir == "NULL") libdir <- NULL

  # # for use in GenePattern
  # if (! is.null (libdir) ) {
    # source paste (libdir, "QuaSAR.R", sep=''))
    # source paste (libdir, "common.R", sep=''))
    # if (libdir!='') {
      # setLibPath(libdir)
      # #install.required.packages(libdir)
    # }
  # }  

  source(quasarScript)
  source(commonScript)
  
  # load libraries
  library(RColorBrewer)
  library(gtools)
  library(MASS)
  library(reshape)
  library(lattice)
  library(ggplot2)
  library(boot)
  library(grid)

  # write summary of experiment to a summary file
  filename <- paste (outputPrefix, '-parameters-summary.csv', sep='')

  # get the input CSV file names
  file.name <- unlist(strsplit(skylineFile, "/"))
  input.file.name <- file.name[length(file.name)]
  file.name <- unlist(strsplit(concentrationFile, "/"))
  conc.file.name <- file.name[length(file.name)]
  
  # when no standard turn off calbriation curves and turn on peak area plots
  if (!internalStandard) {
	generateCalcurves = 0
	generatePeakAreaPlots = 1
  }
	
  print ('Processing arguments ...', quote=FALSE)
	
  params <- c("Input Data File", "Input Concentration File", "Experiment Title", 
               "Analyte Name", "Internal Standard", "Internal Standard Name", "Units", "Perform AuDIT", "Generate CV Table", "Generate Calibration Curves", "Max Number of Transitions to Plot",
               "Generate LOD and  LOQ Tables", "Generate LOD and LOQ Comparison", "Generate Peak Area Plots", "Use PAR for analysis", "Liner Scale Maximum", "Log Scale Maximum",
               "AuDIT: CV threshold", "Perform Endogenous Calculations", "Endogenous Confidence Level")

  if (internalStandard) {istandard <- "yes"} else {istandard <-"no"}
  if (performAudit) {audit <- "yes"} else {audit <- "no"}
  if (generateCVTable) {cvtable <- "yes"} else {cvtable <- "no"}
  if (generateCalcurves) {calcurves <- "yes"} else {calcurves <- "no"}
  if (generateLODLOQTable) {lodloqtable <- "yes"} else {lodloqtable <- "no"}
  if (generateLODLOQcomparison) {lodloqcompare <- "yes"} else {lodloqcompare <- "no"}
  if (generatePeakAreaPlots) {peakareaplots <- "yes"} else {peakareaplots <- "no"}
  if (usePAR) {dopar <- "yes"} else {dopar <- "no"}
  if (performEndogenouscalc) {endocalc <- "yes"} else {endocalc <- "no"}

  params.values <- c(input.file.name, conc.file.name, site, 
                     analyteName, istandard, standardName, unitsLabel, audit, cvtable, calcurves, nTransitionsPlot,
                     lodloqtable, lodloqcompare, peakareaplots, dopar, maxLinearScale, maxLogScale,
                     CVThreshold, endocalc, endogenousCI)

  paramdata <- data.frame(parameters=params, values=params.values)
  write.table(paramdata, filename, sep=",",row.names=F)

  # library for displaying minor tick marks on a plot
  # example: minor.tick(nx=2, ny=2, tick.ratio=0.5)
  # library(Hmisc)

  # get user specified analyte and internal standard names. 
  # this has to match the column header names in the input CSV file
  light.label <- make.names(analyteName)
  heavy.label <- make.names(standardName)

  # check format of input data file and preprocess
  # currently only processing Skyline formatted data files
  concentrationFile <- preprocess.concfile (concentrationFile, is.present=internalStandard)
  data <- preprocess.datafile (skylineFile, skyline.export=TRUE, light.label=light.label, heavy.label=heavy.label, conc.present=concentrationFile, is.present=internalStandard)
  

  # determine if the internal standard concentration is the same for all analytes
  if(concentrationFile == 'NULL'){
  	unique.is.conc <- unique(data[,"is.conc"])
  }else{
  	unique.is.conc <- unique(concentrationFile[,"is.conc"])
  }
  if (length(unique.is.conc) == 1) {
	heavySpikeConc = unique.is.conc[1]
  } else {
	heavySpikeConc = "variable"
  }

  if (debug) write.csv (concentrationFile, file = paste (outputPrefix, '-concentraionFile.csv', sep=''), row.names=FALSE)

  #determine if LOD/LOQ comparison is 0 or 1, if 0 set to NULL
  if (!generateLODLOQcomparison) {
    comparative.lod.loq <- NULL
  } else {
	comparative.lod.loq <- generateLODLOQcomparison
  }


  #determine if Endogenous Calc is 0 or 1, if 1 set to CI
  if (performEndogenouscalc) {
    endogenous.level.ci <- endogenousCI
  } else {
	endogenous.level.ci <- NULL
  }

  # if peak are ratio, PAR, is used for analysis then set area.ratio.multiplier=1, i.e. heavySpikeConc = 1
  # also for this case no corrections will be applied within the calculate function
  # this will override the user supplied value if peak area ratio is selected
  if (usePAR) {
    heavySpikeConc = 1
  }


  # if calibration curves or peak area plots are generated then LOD/LOQ has to be calculated
  # the LOD values are displayed in these plots, so force generateLODLOQTable = TRUE
  # if AuDIT is run then the bad transitions are plotted on the calibration curve plot
  if (generateCalcurves | generatePeakAreaPlots) generateLODLOQTable = TRUE

  # Call function that generates the data used for lod-loq tables and calibration plots 
  d <- calculate (data, concentrationFile,
                  output.prefix=outputPrefix, 
                  area.ratio.multiplier=heavySpikeConc,
				  comparative.lod.loq=comparative.lod.loq,
    			  endogenous.level.ci=endogenous.level.ci,
                  lodloq.table=generateLODLOQTable, 
                  cv.table=generateCVTable, 
                  run.audit=performAudit, 
                  gen.peak.area.plots=generatePeakAreaPlots,
                  generate.cal.curves=generateCalcurves,
                  use.par=usePAR,
                  max.transitions.plot=nTransitionsPlot,
                  audit.cv.threshold=CVThreshold,
                  audit.row.limit=10000,
                  site.name=site,
                  units=unitsLabel, 
                  max.linear.scale=maxLinearScale, 
                  max.log.scale=maxLogScale,
                  plot.title=site) 
}

install.required.packages <- function (libdir) {
  # install all required packages
  if (!is.package.installed (libdir, "gtools")) {
    install.package (libdir, "gtools_2.6.1.zip", "gtools_2.6.1.tgz","gtools_2.6.1.tar.gz")
  }
  
  if (!is.package.installed (libdir, "RColorBrewer")) {
    install.package (libdir, "RColorBrewer_1.0-5.zip", "RColorBrewer_1.0-5.tgz","RColorBrewer_1.0-5.tar.gz")
  }
  
  if (!is.package.installed (libdir, "MASS")) {
    install.package (libdir, "MASS_7.3-14.zip", "MASS_7.3-14.tgz","MASS_7.3-14.tar.gz")
  }

  if (!is.package.installed (libdir, "boot")) {
    install.package (libdir, "boot_1.3-5.zip", "boot_1.3-5.tgz","boot_1.3-5.tar.gz")
  }
}

tryCatch({parse.cmdline()}, 
finally = {
cat("Finished!")
})

# END
