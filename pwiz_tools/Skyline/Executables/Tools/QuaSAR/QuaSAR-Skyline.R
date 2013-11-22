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
debug <- FALSE

row.limit <- 10000
peptide.col <- 'peptide'


##
## Command line processing and other functions to support GenePattern
##

parse.cmdline <- function () {
  # set up for command line processing (if needed)
  # arguments are specified positionally (since there are no optional arguments) and ...
  arguments <- commandArgs(trailingOnly=TRUE)
  if ( length (arguments) != 24)
    # expected arguments not present -- error
    stop ("USAGE: R --slave --no-save --args '<quasar.r> <common.r> <skyline.file> <concentration.file> <title> <analyte> <standard.present> <standard> <units>\n
                  <generate.cv.table> <generate.calcurves> <number.transitions.plot> <generate.lodloq.table> <generate.peak.area.plots> <use.par> <max.calcurve.linear.scale>\n
                  <max.calcurve.log.scale> <perform.audit> <audit.cv.threshold> <output.prefix> <create.individual.plots>' < QuaSAR-Skyline.r\n") #<libdir>
    
  for (i in 1:24) {
    arg <- arguments [i]
    # remove leading and trailing blanks
    arg <- gsub ("^ *", "", arg)
    arg <- gsub (" *$", "", arg)
    # remove any embedded quotation marks
    arg <- gsub ("['\'\"]", "", arg)
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
    if (i==24) createIndividualPlots <<- as.numeric (arg)
  }

  source(quasarScript)
  source(commonScript)
  

  # create a new directory for all the result tables and plots
  new.dir <- paste (outputPrefix, '-results', sep='')
  if (file.exists (new.dir)) {
    # directory exists -- create a variant name (using a brute force approach!)
    i <- 0
    unique.new.dir <- new.dir
    while (file.exists (unique.new.dir)) {
      i <- i+1
      unique.new.dir <- paste (new.dir, '_', i, sep='')
    }
    new.dir <- unique.new.dir
  }
  dir.create (new.dir)
  outputPrefix <- file.path (new.dir, outputPrefix)
                    

  # load libraries
  library(RColorBrewer)
  library(grid)
  library(gtools)
  library(MASS)
  library(reshape)
  library(lattice)
  library(ggplot2)
  library(boot)
  library(gplots)

  # write summary of experiment to a summary file
  filename <- paste (outputPrefix, '-parameters-summary.csv', sep='')

  # get the input CSV file names
  file.name <- unlist(strsplit(skylineFile, "/"))
  input.file.name <- file.name[length(file.name)]
  file.name <- unlist(strsplit(concentrationFile, "/"))
  conc.file.name <- file.name[length(file.name)]
  
  # when no standard turn off calibration curves and AuDIT; turn on peak area plots
  if (!internalStandard) {
    generateCalcurves = 0
    generatePeakAreaPlots = 1
    performAudit <- 0
  }
	
  print ('Processing arguments ...', quote=FALSE)
  params <- c ("Input Data File", "Input Concentration File", "Experiment Title", 
               "Analyte Name", "Internal Standard", "Internal Standard Name", "Units", "Perform AuDIT", "Generate CV Table", 
               "Generate Response Curves", "Create Individual (jpg) Plots", "Max Number of Transitions to Plot",
               "Generate LOD and  LLOQ Tables", "Generate LOD and LLOQ Comparison", "Generate Peak Area Plots", "Use PAR for analysis", 
               "Liner Scale Maximum", "Log Scale Maximum",
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
  if (createIndividualPlots) {jpgplots <- "yes"} else {jpgplots <- "no"}

  params.values <- c (input.file.name, conc.file.name, site, 
                      analyteName, istandard, standardName, unitsLabel, audit, cvtable, calcurves, jpgplots, nTransitionsPlot,
                      lodloqtable, lodloqcompare, peakareaplots, dopar, maxLinearScale, maxLogScale,
                      CVThreshold, endocalc, endogenousCI)
  
  paramdata <- data.frame (parameters=params, values=params.values)
  write.table (paramdata, filename, sep=",", row.names=FALSE)
  # add other notes to parameter file
  cat ('\n"NOTES:"\n', file=filename, append=TRUE)
  cat ('"  Any rows with TRUE in (any) light do not use, heavy do not use,"\n', file=filename, append=TRUE)
  cat ('"    light do no use 1, and heavy do not use 1 columns are excluded"\n', file=filename, append=TRUE)
  if (generateCalcurves || generatePeakAreaPlots) {
    cat ('" Response curves fitted using robust, weighted regression"\n', file=filename, append=TRUE)
    cat ('" Plot legends list transitions by increasing fragment size"\n', file=filename, append=TRUE)
    cat ('" Transitions colored by IS intensity: red (max), green, blue (min)"\n', file=filename, append=TRUE)
  }
  if (usePAR)
    cat ('" Using PAR--all calculations use PAR (instead of concentration)"\n', file=filename, append=TRUE)
  if (generateLODLOQTable) {
    cat ('" LOD plots include only the quantifying (lowest LOD) transitions"\n', file=filename, append=TRUE)
    cat ('" LOD/LLOQ in peak area and PAR are calculated assuming an ideal response"\n', file=filename, append=TRUE)
    cat ('"    [i.e., slope=1, intercept=0]"\n', file=filename, append=TRUE)
  }
  if (generateCVTable) 
    cat ('" CV plots include only the best transitions (lowest LOD or CV)"\n', file=filename, append=TRUE)
  if (performAudit)
    cat ('" Interference detection by AuDIT is informational only. All transitions are used for calculations"\n', file=filename, append=TRUE)
  cat ('" Reference: Details about most of the calculations performed by QuaSAR can be found in:"\n', file=filename, append=TRUE)
  cat ('"   Mani, D. R., Abbatiello, S. E., and Carr, S. A. (2012)"\n', file=filename, append=TRUE)
  cat ('"   Statistical characterization of multiple-reaction monitoring"\n', file=filename, append=TRUE)
  cat ('"   mass spectrometry (MRM-MS) assays for quantitative proteomics."\n', file=filename, append=TRUE)
  cat ('"   BMC Bioinformatics 13, S9"\n', file=filename, append=TRUE)
  
  

  # get user specified analyte and internal standard names. 
  # this has to match the column header names in the input CSV file
  light.label <- make.names(analyteName)
  heavy.label <- make.names(standardName)

  # check format of input data file and preprocess
  # currently only processing Skyline formatted data files
  concentrationFile <- preprocess.concfile (concentrationFile, is.present=internalStandard)
  data <- preprocess.datafile (skylineFile, skyline.export=TRUE, light.label=light.label, heavy.label=heavy.label, 
                               conc.present=concentrationFile, is.present=internalStandard, use.par=usePAR)
  

  # determine if the internal standard concentration is the same for all analytes
  if(concentrationFile == 'NULL') unique.is.conc <- unique(data[,"is.conc"])
  else unique.is.conc <- unique(concentrationFile[,"is.conc"])

  if (length(unique.is.conc) == 1) heavySpikeConc = unique.is.conc[1]
  else heavySpikeConc = "variable"

  if (debug) write.csv (concentrationFile, file = paste (outputPrefix, '-concentraionFile.csv', sep=''), row.names=FALSE)

  # determine if LOD/LOQ comparison is 0 or 1, if 0 set to NULL
  if (!generateLODLOQcomparison) comparative.lod.loq <- NULL
  else comparative.lod.loq <- generateLODLOQcomparison
  
  
  # determine if Endogenous Calc is 0 or 1, if 1 set to CI
  if (performEndogenouscalc) endogenous.level.ci <- endogenousCI
  else endogenous.level.ci <- NULL

  # if peak are ratio, PAR, is used for analysis then set area.ratio.multiplier=1, i.e. heavySpikeConc = 1
  # also for this case no corrections will be applied within the calculate function
  # this will override the user supplied value if peak area ratio is selected
  if (usePAR) heavySpikeConc = 1


  # if calibration curves or peak area plots are generated then LOD/LOQ has to be calculated
  # the LOD values are displayed in these plots, so force generateLODLOQTable = TRUE
  # if AuDIT is run then the bad transitions are plotted on the calibration curve plot
  if (generateCalcurves | generatePeakAreaPlots) generateLODLOQTable = TRUE

  
  # split data into parts of smaller size based on row.limit (approximate)
  data <- data [ order (data[,peptide.col]), ]   # sort by peptide
  n <- nrow (data)
  parts <- ceiling (n / row.limit)
  peptides <- unique (data [, peptide.col])
  n.peptides <- length (peptides)
  peptides.in.part <- ceiling (n.peptides / parts)
  
  for (i in 1:parts) {
    # process each part by splitting the dataset appropriately
    
    start <- peptides.in.part * (i-1) + 1
    end <- min (peptides.in.part * i, n.peptides)
    peptide.subset <- data[,peptide.col] %in% peptides [start : end]
    data.subset <- data [ peptide.subset, ]                     
    
    print ( paste ('>>> Processing part', i, 'of', parts), quote=FALSE )
    
    # Call function that generates the data used for lod-loq tables and calibration plots 
    d <- calculate (data.subset, concentrationFile,
                    output.prefix=outputPrefix, 
                    area.ratio.multiplier=heavySpikeConc,
                    comparative.lod.loq=comparative.lod.loq,
                    endogenous.level.ci=endogenous.level.ci,
                    lodloq.table=generateLODLOQTable, 
                    cv.table=generateCVTable, 
                    run.audit=performAudit, 
                    audit.cv.threshold=CVThreshold,
                    audit.row.limit=row.limit,
                    site.name=site,
                    units=unitsLabel, 
                    append=ifelse (i==1, FALSE, TRUE)) 
  }
    
    
  # create plots
  print ('Generating plots ...', quote=FALSE)
  plot.results (output.prefix=outputPrefix, 
                comparative.lod.loq=comparative.lod.loq,
                area.ratio.multiplier=heavySpikeConc,
                lodloq.table=generateLODLOQTable, 
                cv.table=generateCVTable, 
                run.audit=performAudit, 
                use.peak.area=generatePeakAreaPlots,
                generate.cal.curves=generateCalcurves,
                use.par=usePAR,
                max.transitions.plot=nTransitionsPlot,
                audit.cv.threshold=CVThreshold,
                site.name=site,
                units=unitsLabel, 
                max.linear.scale=maxLinearScale, 
                max.log.scale=maxLogScale,
                plot.title=site,
                individual.plots=createIndividualPlots) 
  
}

tryCatch({parse.cmdline()}, 
         finally = {
           cat("Finished!")
         })

# END
