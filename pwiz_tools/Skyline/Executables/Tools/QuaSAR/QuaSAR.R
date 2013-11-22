# $Id: QuaSAR.R 124 2013-10-09 14:27:44Z manidr $
print ("$Id: QuaSAR.R 124 2013-10-09 14:27:44Z manidr $", quote=FALSE)

# The Broad Institute of MIT and Harvard
# SOFTWARE COPYRIGHT NOTICE AGREEMENT
# Authors: Rushdy Ahmad, Deepak Mani and D.R. Mani
# This software and its documentation are copyright (2009) by the
# Broad Institute. All rights are reserved.

# This software is supplied without any warranty or guaranteed support
# whatsoever. The Broad Institute cannot be responsible for its
# use, misuse, or functioglity.

## suppress warnings (else GenePattern thinks an error has occurred)
options (warn = -1)


##
## Calculations
##
# function to calculate lod-loq, CV, response etc. for specified peptides and transitions
calculate <-
  function (data.in,                                       # data, exported from skyline (format described below)
            conc.in,                                       # concentration file
            output.prefix=NULL,                            # if non-NULL, used as prefix for output files
            output.digits=2,                               # number of significant digits in LOD/Q output (NULL for R default)
            area.ratio.multiplier=1,                       # analyte/int.std. ratio is multiplied by this factor. Used as a place holder. IS concentration supplied by input file.
            blank.na.to.zero=FALSE,                        # set NA areas for blank sample to zero?
            beta=0.05,                                     # confidence level (see references)
            measurement.replicates=1,                      # number of measurements to be used to determine conc
                                                           # (used to determine sd of blank / low conc measurements)
            low.index=1,                                   # by default the lowest spike-in concentration is used for LOD calcs
                                                           # this can be changed to the nth spike-in concentration by setting this to n
            comparative.lod.loq=NULL,                      # if non-null, different methods for determining LOD/LOQ are applied and a
                                                           # comparative table generated; else only the standard method is used
            endogenous.level.ci=NULL,                      # confidence interval (on intercept) to determine if endogenous levels are non-zero
                                                           # (calculated only when non-NULL)
            lodloq.table=FALSE,                            # if TRUE then generate the LOD-LOQ table of results
            cv.table=FALSE,                                # if TRUE then generate the cv table
            run.audit=FALSE,                               # if TRUE then run AuDIT
            audit.row.limit=10000,                         # sets the row limit for AuDIT processing
            audit.cv.threshold=0.2,                        # CV threshold for AuDIT
            site.name="mySite",                            # name of site
            units = "fmol/ul",							               # units used for measurement
            append = FALSE,                                # if TRUE, output csv files are appended to existing files
            debug=FALSE                                    # set this to TRUE for writing out intermediate files/extra info
           )
{
  
  # Calculates LOD and LOQ for the peptides and transitions listed in the data.in.
  # Also plots calibration curves, runs AuDIT, and calculates CVs
  # Samples and the corresponding analyte spike-in concentrations are included in data.in or listed in conc.in
  #   conc.in has 4 columns: sample.name, sample, concentration, is.conc
  #                       sample.name should match that in the data.file; the corresponding sample id 
  #                       is contained in the sample column, along with concentration; blank sample should have
  #                       concentration == 0; sample id should be identical for all replicates, but should be different for
  #                       different concentrations

    
  write.out <- function (data, file, ...) {
    # writes output to csv file, but automatically appends when append==TRUE in calculate
    if ( append && file.exists (file) ) write.table (data, file=file, sep=',', col.names=FALSE, append=TRUE, ...)
    else write.csv (data, file=file, ...)
  }
    
    
    
  print ('Initializing ...', quote=FALSE)
  
  # STEP 1: process the input data and concentration files
  d <- data.in
  conc <- conc.in

  # STEP 2: create the transition.id label
  d.new <- data.frame()
  tmp <- apply (d,1,
                function (x) {
                  transition.id <- paste (x['precursor.charge'], x['transition.id'], x['product.charge'], sep='.')
                })
  # insert the new transition.id label into FragmentIon
  d[,'transition.id'] <- tmp

  if (debug) write.out (d, file = paste (output.prefix, '-calc1.csv', sep=''), row.names=FALSE)

  # Merge input data file and concentration file
  # If a concentraion map is given merge data sets otherwise just use skyline file
 
  if (conc.in != 'NULL') {
  	data <- merge (d, conc, by='sample.name')
  } else {
    data <- d
  }

  if (debug) write.out (data, file = paste (output.prefix, '-calcAfterMerge.csv', sep=''), row.names=FALSE)

  # STEP 3: convert relevant fields
  # to numeric
  for (col in c ('area', 'IS.area', 'concentration')) 
    data [, col] <- as.numeric (unlist (lapply (data[, col], toString)))
  # to string
  for (col in c('peptide', 'sample', 'replicate', 'transition.id'))
    data [, col] <- unlist (lapply (data[, col], toString))
  
  # STEP 4: convert transition.id to transition which are identical for every peptide
  tr.old <- data [, c('sample', 'replicate', 'peptide', 'transition.id')]
  tr.new <- NULL
  temp <- by (tr.old, tr.old [,'peptide'],
              function (x) {
                trs <- unique (x[,'transition.id'])
                trs.number <- unlist (lapply (x[,'transition.id'], function (v) { which (trs %in% v) }))
                tr.new <<- rbind (tr.new, cbind (x, transition=trs.number))
              })
  data <- merge (data, tr.new)

  if (debug) write.out (data, file = paste (output.prefix, '-calc2.csv', sep=''), row.names=FALSE)
  
  # STEP 5: Run AuDIT
  # Initialize results data.frame
  audit.result <- NULL
  if (run.audit) {

    print ('Running AuDIT ...', quote=FALSE)
    
    peptide.col <- 'peptide'

    # split into parts of smaller size based on audit.row.limit (approximate)
    n <- nrow (data)
    parts <- ceiling (n / audit.row.limit)
    peptides <- unique (data [, peptide.col])
    n.peptides <- length (peptides)
    peptides.in.part <- ceiling (n.peptides / parts)
                        
    pid <- Sys.getpid ()
    final.result <- NULL
    for (i in 1:parts) {
      # process each part by
      # (i) creating AuDIT acceptable input file
      # (ii) creating sepatate files for peptides with specified numbers of transitions
                        
      start <- peptides.in.part * (i-1) + 1
      end <- min (peptides.in.part * i, n.peptides)
      peptide.subset <- data[,peptide.col] %in% peptides [start : end]
      data.subset <- data [ peptide.subset, ]                     
      
      # count number of transitions (K) for each peptide
      n.trs <- aggregate (data.subset [,'transition.id'], list (data.subset [,'peptide']),
                          function (x) { length (unique (x)) })
      colnames (n.trs) <- c ('peptide', 'n.transitions')
      audit.data <- merge (data.subset, n.trs)
      
                        
      # run AuDIT for peptides with K > 2 transitions
      if ( any (audit.data [, 'n.transitions'] <= 2) ) warning ("Peptides with <= 2 transitions removed from data set")
      audit.data <- audit.data [ audit.data[,'n.transitions'] > 2, ]
      
      if(nrow(audit.data) == 0) 
        stop("Error: No peptides with 3 or more transitions found in the dataset. \nAuDIT requires that peptides have at least 3 transitions.")

      
      audit.required.columns=c('sample', 'replicate', 'peptide', 'transition.id', 'area', 'IS.area')
      tmp <- by (audit.data [, audit.required.columns], list (audit.data [, 'n.transitions']),
                 function (tr.data) {
                   file <- paste ('AuDITtemp.', pid, '.csv', sep='')
                   write.csv (tr.data, file, row.names=FALSE)
                   
                   if (debug) print (' Calling AuDIT', quote=FALSE)
                   
                   out <- run.AuDIT (file, output.prefix, required.columns=audit.required.columns,
                                     cv.threshold=audit.cv.threshold,
                                     required.columns.location=1:(length (audit.required.columns)), debug=debug)
                   final.result <<- rbind (final.result, out$result)
                   unlink (file)  # delete temp file
                 })
    }
                
                
    # write final output
    if (!is.null (output.prefix))
      write.out (final.result, paste (output.prefix, '-audit.csv', sep=''), row.names=FALSE)


   } # End AuDIT


  
  ## Create an intergrated transition that combines intensities from all the "good" transitions

  # combine data and audit.result
  if (!is.null (audit.result)) {
    integrated.input <- merge (data, audit.result, by=c('peptide','transition.id', 'sample'))
  } else {
    # if audit was not run, all transitions are marked "good"
    integrated.input <- cbind (data, status=rep ("good", nrow(data)))
  }
 

  integrated.table <- NULL
  filtered.integrated.input <- NULL
  transition.list.output <- NULL	

  temp <- by (integrated.input, integrated.input[,c('peptide','transition.id')],
              function (x) {
		            if (! any(x[,'status'] == 'bad', na.rm = TRUE)) {	
		              filtered.integrated.input <<- rbind (filtered.integrated.input, x)
	   	          }
              })


  temp <- by (filtered.integrated.input, filtered.integrated.input[,c('peptide','sample','replicate')],
              function (x) {
                # remove any bad transitions
                integrated.index <- x[,'status'] == 'good'
                good.transitions <- x[integrated.index,]
                
                transition.list <- paste(good.transitions[,'transition.id'], collapse=' + ')
                transition.list <- c(x[1,'peptide'], transition.list)
                if (is.null (transition.list.output) || (! x[1,'peptide'] %in% transition.list.output[,1]) )
                  # add the transition list for each peptide (only once)
                  transition.list.output <<- rbind(transition.list.output,transition.list)
                
                sum.transitions <- cbind (peptide = x[1,'peptide'],
                                          transition.id = 'Sum.tr',   
                                          x[1,c('replicate','sample','sample.name')],
                                          protein = toString(x[1,'protein']),
                                          area = ifelse (nrow (good.transitions) > 0, sum(good.transitions[,'area'], na.rm=TRUE), NA),
                                          IS.area = ifelse (nrow (good.transitions) > 0, sum(good.transitions[,'IS.area'], na.rm=TRUE), NA),
                                          RT = x[1,'RT'],
                                          precursor.charge = 0,
                                          product.charge = 0, 
                                          x[1,c('concentration', 'is.conc')], 
                                          transition = 0)
                
                integrated.table <<- rbind(integrated.table, sum.transitions)
              })
  
  data <- rbind (data,integrated.table)
  
  for (col in c ('area', 'IS.area', 'concentration', 'is.conc')) 
    data [, col] <- as.numeric (unlist (lapply (data[, col], toString)))
  
  colnames(transition.list.output) <- c("peptide", "transitions.summed")
  write.out (transition.list.output, paste (output.prefix, '-summed-transitions.csv', sep=''), row.names=F)


  
  # STEP 6: calculate area ratio and concentration
  data <- cbind (data, actual.conc=data[,'concentration'])
  area.ratio <- data [,'area'] / data [,'IS.area']
  concentration.estimate <- area.ratio * data [, 'is.conc']
  data <- cbind (data, area.ratio, concentration.estimate)

  # STEP 7: set up for plotting data
  write.out (data, paste (output.prefix, '-raw-data.csv', sep=''), row.names=FALSE)

  # STEP 8: if user has requested generation of LOD-LOQ tables
  if (lodloq.table) {

    # separate out different groups
    # ... but make sure that rows with concentration == NA are removed for LOD/LOQ calcs
    missing.rows <- is.na (data [,'concentration'])
    blank.rows <- data [,'concentration']==0
    blank <- data [ blank.rows & !missing.rows, ]
    if (blank.na.to.zero)
      blank [ is.na (blank[,'concentration.estimate']), 'concentration.estimate' ] <- 0    # set NAs to 0; ok for MRM data
    non.blank <- data [ !blank.rows & !missing.rows, ]

    # for debug only: save intermediate file
    #if (!is.null (output.prefix))
    #  write.table (non.blank, paste (output.prefix, '-non-blank.csv', sep=''), sep=',', row.names=FALSE)

    # determine low concentration sample
    # by default, low.index <- 1 unless otherwise specified
    low.concentrations <- sort (unique (non.blank [,'concentration']))
    low.conc <- data [ data [,'concentration']==low.concentrations[low.index], ]

    # calculate mean and sd for blank and low.conc samples
    # this is done here (instead of in the LOD/LOQ calcs) since the blanks and low.conc
    # samples may have different number of replicates (and hence have different df)
    calc.mean.sd.qt <- function (d) {
      d <- d [ !is.na (d) ]
      if ( length (d) > 1 ) {
        d.n <- length (d)
        df <- d.n - 1
        d.mean <- mean (d, na.rm=T)
        d.sd <- sd (d, na.rm=T) / sqrt (measurement.replicates)
        d.qt <- qt (1-beta, df)
      } else {
        d.n <- d.mean <- d.sd <- d.qt <- NA
      }
      
      return (list (mean=d.mean, sd=d.sd, qt=d.qt, n=d.n))
    }
    
    blank.calcs <- NULL
    temp <- by (blank, blank[,c('peptide', 'transition')], 
                function (x) {
                  calc.values <- calc.mean.sd.qt (x[,'concentration.estimate'])
                  calc.values.area <- calc.mean.sd.qt (x[,'area'])
                  calc.values.par <- calc.mean.sd.qt (x[,'area.ratio'])
                  blank.calcs <<- rbind (blank.calcs,
                                         c (peptide=toString (x[1,'peptide']), transition=toString (x[1,'transition']),
                                            calc.values, 
                                            mean.area=calc.values.area$mean, sd.area=calc.values.area$sd,
                                            mean.par=calc.values.par$mean, sd.par=calc.values.par$sd))
                })

    lowconc.calcs <- NULL
    temp <- by (low.conc, low.conc[,c('peptide', 'transition')], 
                function (x) {
                  calc.values <- calc.mean.sd.qt (x[,'concentration.estimate'])
                  calc.values.area <- calc.mean.sd.qt (x[,'area'])
                  calc.values.par <- calc.mean.sd.qt (x[,'area.ratio'])
                  lowconc.calcs <<- rbind (lowconc.calcs,
                                           c (peptide=toString (x[1,'peptide']), transition=toString (x[1,'transition']),
                                              calc.values, 
                                              mean.area=calc.values.area$mean, sd.area=calc.values.area$sd,
                                              mean.par=calc.values.par$mean, sd.par=calc.values.par$sd))
                })

    # Calculate the average IS area 
    internal.standard.calcs <- NULL
    is.data <- data [ data [,'concentration']!='NA', ]
    temp <- by (is.data, is.data[,c('peptide', 'transition')], 
                function (x) {
                  calc.values <- calc.mean.sd.qt (x[,'IS.area'])
                  internal.standard.calcs <<- rbind (internal.standard.calcs,
                                                     c (peptide=toString (x[1,'peptide']), transition=toString (x[1,'transition']),
                                                        calc.values))
                })


    ## LOD and LOQ Calculations
         
    # calculate LODs (standard)
    print ('Calculating LODs ...', quote=FALSE)
    data.linnet <-  merge (blank.calcs, lowconc.calcs, by=c ('peptide','transition'),
                           suffixes=c('.blank','.lowconc'))
    # append the IS area information 
    is.table <- internal.standard.calcs[, c('peptide', 'transition', 'mean')]
    colnames (is.table)[3] <- "mean.IS.area"
    data.linnet <-  merge (data.linnet, is.table)

    lod <- apply (data.linnet, MARGIN=1,
                  function (x) { x$mean.blank + (x$sd.blank * x$qt.blank) + (x$sd.lowconc * x$qt.lowconc) })
    lod.area <- apply (data.linnet, MARGIN=1,
                       function (x) { x$mean.area.blank + (x$sd.area.blank * x$qt.blank) + (x$sd.area.lowconc * x$qt.lowconc) })
    lod.par <- apply (data.linnet, MARGIN=1,
                      function (x) { x$mean.par.blank + (x$sd.par.blank * x$qt.blank) + (x$sd.par.lowconc * x$qt.lowconc) })
    loq <- 3 * lod
    loq.area <- 3 * lod.area
    loq.par <- 3 * lod.par
    lod.loq <- cbind (data.linnet, LOD=lod, LOD.area=lod.area, LOD.par=lod.par, 
                      LLOQ=loq, LLOQ.area=loq.area, LLOQ.par=loq.par)
    
    # convert to proper type -- otherwise causes problems later (including in write.table)
    lod.loq [,'peptide'] <- unlist (lapply (lod.loq[,'peptide'], toString))
    for (y in setdiff (colnames (lod.loq), 'peptide')) lod.loq [,y] <- as.numeric (lod.loq[,y])

    # retrieve and include the transition.id column to enable proper interpretation of results
    d.extra <- unique (data [ , c ('peptide', 'transition', 'transition.id', 'protein')])   
    lod.loq <- merge (d.extra, lod.loq)

    ## additional methods for LOD/LOQ (if requested)
    lod.loq.compare <- NULL
    if ( !is.null (comparative.lod.loq)) {
      
      ## LOD/LOQ based on blank sample only (Currie)
      print ('  blank only method', quote=FALSE)
      temp <- by (blank, blank[, c('peptide', 'transition')], 
                  function (x) {
                    stddev <- sd (x[,'concentration.estimate'], na.rm=T)
                    lod.loq.compare <<- rbind (lod.loq.compare, 
                                               c ('blank-only',
                                                  toString (x[1,'peptide']),
                                                  toString (x[1,'transition']),
                                                  toString (x[1,'transition.id']),
                                                  3 * stddev, 10 * stddev))
                  })
      colnames (lod.loq.compare) <- c ('method', 'peptide', 'transition', 'transition.id', 'LOD', 'LLOQ')
      
      ## maximum RSD method
      rsd.max <- 0.15   # 15%
      print ('  rsd limit method', quote=FALSE)
      temp <- by (non.blank, non.blank[,c('peptide', 'transition')],
                  function (x) {
                    rsd <- by (x[,'concentration.estimate'], list (x[,'sample']), 
                               function (y) { sd (y, na.rm=TRUE) / mean (y, na.rm=TRUE) })
                    actual.conc <- by (x[,'concentration'], list (x[,'sample']), mean, na.rm=TRUE)
                    tfit <- try (fit <- lm (log(rsd) ~ log (actual.conc)), silent=TRUE)
                    if (!inherits (tfit, "try-error")) {
                      # record regression results and LOD/LOQ
                      intercept <- fit$coefficients[1]
                      slope <- fit$coefficients[2]
                      loq <- exp ( (log (rsd.max) - intercept) / slope )
                      lod <- loq/3
                    } else {
                      print (paste ('   ', toString(x[1,'transition.id']), 'rsd regression fit failed'), quote=FALSE)
                      lod <- loq <- NA
                    }
                    
                    lod.loq.compare <<- rbind (lod.loq.compare,
                                               c (method='rsd-limit',
                                                  toString (x[1,'peptide']),
                                                  toString (x[1,'transition']),
                                                  toString (x[1,'transition.id']),
                                                  lod, loq))
                  })
      
      ## blank + low concentration sample (Linnet)
      # already calculated; get data from lod.loq table
      lod.loq.compare <- rbind (lod.loq.compare,
                                cbind (method=rep ('blank+low-conc', nrow (lod.loq)),
                                       lod.loq [, c('peptide', 'transition', 'transition.id')],
                                       LOD=unlist (lapply (lod.loq [,'LOD'], toString)),
                                       LLOQ=unlist (lapply (lod.loq [,'LLOQ'], toString)) ))
      
      
      
      if (!is.null (output.prefix))
        write.out (lod.loq.compare, paste (output.prefix, '-lod-loq-comparison.csv', sep=''), row.names=FALSE)
    }
    ## end of comparative.lod.loq 
    
    ## endogenous level calculations
    if (!is.null (endogenous.level.ci)) {
      print (' calculating endogenous levels (this may take several minutes)', quote=FALSE)
      endogenous.levels <- NULL
      temp <- by (non.blank, non.blank[, c('peptide','transition')],
                  function (x) {
                    x <- x [ is.finite (x[,'concentration.estimate']) & is.finite (x[,'concentration']), ]
                    tcis <- try ( {
                      bs.results <- boot (x, function (a, b) {lqs ( a[b,'concentration.estimate'] ~ a[b,'concentration'] )$coef[1]}, 1000)
                      cis <- boot.ci (bs.results, conf=endogenous.level.ci, type='basic')
                    }, silent=TRUE)
                    if (!inherits (tcis, "try-error")) {
                      intercept <- cis$t0
                      intercept.ci <- cis$basic[4:5]
                      endogenous.level <- intercept
                    } else {
                      endogenous.level <- intercept.ci <- NA
                    }
                    endogenous.levels <<- rbind (endogenous.levels, c (toString (x[1,'peptide']), toString (x[1,'transition']),
                                                                       endogenous.level, intercept.ci))
                  })
      colnames (endogenous.levels) <- c ('peptide', 'transition', 'endogenous.level', 'endogenous.ci.lower', 'endogenous.ci.upper')
      lod.loq <- merge (lod.loq, endogenous.levels)
    }
    ## end of endogenous level calculations       
    
    # OUTPUT: write out LOD/LOQ results
    if (!is.null (output.prefix)){
      write.out (lod.loq, paste (output.prefix, '-lod-loq-raw.csv', sep=''), row.names=FALSE)
    }

    # calculate final LOD/LOQ:
    # this is the lowest of LOD/LOQ for all the transitions
    lod.loq.final <- NULL
    temp <- by (lod.loq, list (lod.loq [,'peptide']), 
                function (x) {
                  find.min <- function (column) {
                    data <- as.numeric (unlist (lapply (x[, column], toString)))
                    
                    # if no data exists, give up!
                    no.row <- c (toString (x[1,'peptide']), rep (NA, (ncol (x) - 1)))
                    if (length(data)==0) return (list (value=NA, row=no.row))
                    
                    val <- min (data, na.rm=TRUE)
                    if ( !is.finite (val) ) return (list (value=NA, row=no.row))
                    row <- unlist (lapply (x [ which (val==data), ], toString))
                    return ( list (value=val, row=row) )
                  }
                  
                  if (!is.null (endogenous.level.ci)) {
                    endogenous.level.final <- apply (x, 1,
                                                     function (z) {
                                                       ifelse ( as.numeric (z['endogenous.ci.lower']) < 0 &&
                                                               as.numeric (z['endogenous.ci.upper']) > 0,
                                                               0, as.numeric (z['endogenous.level']))
                                                     })
                    x[,'endogenous.level'] <- endogenous.level.final
                  }
                  lod <- find.min ('LOD')
                  
                  lod.loq.final <<- rbind (lod.loq.final, rbind(lod$row))
                })
    lod.loq.final <- data.frame (lod.loq.final)
                    
      
    # write out final LOD/LOQ table with units in column headers
    keep.cols <-  c ('peptide', 'transition.id', 'LOD', 'LLOQ')   # keep only the required columns
    if (!is.null (endogenous.level.ci)) keep.cols <- c (keep.cols, 'endogenous.level')
    lod.loq.final <- lod.loq.final [, keep.cols]
    names(lod.loq.final)[names(lod.loq.final)=='LOD'] <- paste('LOD (', units, ')', sep = "")
    names(lod.loq.final)[names(lod.loq.final)=='LLOQ'] <- paste('LLOQ (', units, ')', sep = "")
    if (!is.null (output.prefix))
      write.out (lod.loq.final, paste (output.prefix, '-lod-loq-final.csv', sep=''), row.names=FALSE)
  }

  # STEP 9: generate the CV table
  if (cv.table) {

    print    ("Calculating CVs ...", quote=FALSE)
    
    # First retrieve columns from data: peptide, transition.id, sample, area.ratio=concentration.estimate
    cv.required.columns <- c('peptide', 'transition.id', 'sample', 'concentration', 'is.conc', 'concentration.estimate', 'IS.area')
    cv.d <- data[, cv.required.columns]

    # CV calculating function
    cv <- function (x) {
      # CV = (std. dev. / mean)*100 
      value <- sd (x, na.rm=TRUE) / mean (x, na.rm=TRUE)
      value <- value*100
      value <- formatC(value, digits=4,format="fg")
      return (value)
    }

    # mean calculating function
    the.mean <- function (x) {
      value <- mean (x, na.rm=TRUE)
      value <- ifelse (is.infinite(value), NA, formatC(value, digits=4,format="fg"))
      return (value)
    }

    # aggregate by melt and cast functions, calling the cv function in cast to calculate cv for given id
    meltcv.d <- melt(cv.d, id=(c("peptide","sample","transition.id","concentration", "is.conc")))
    castcv.d <- cast(meltcv.d,peptide+sample+transition.id+concentration+is.conc~variable, cv)
    castcv.d <- castcv.d[,c('peptide', 'transition.id', 'sample', 'concentration', 'is.conc', 'concentration.estimate')]
    # set last column name to % cv
    colnames (castcv.d) [ncol (castcv.d) ] <- 'cv'
    
    #melt and cast to calculate mean concetration and mean IS area
    castmean.d <- cast(meltcv.d,peptide+sample+transition.id+concentration+is.conc~variable, the.mean)
    colnames (castmean.d) [ncol (castmean.d) - 1 ] <- 'mean.concentration'
    colnames (castmean.d) [ncol (castmean.d) ] <- 'mean.IS.area'
    castcv.d <- merge(castcv.d, castmean.d)
    
    #find out how many replicates were summed to calculate cv, mean concentration, mean IS area
    castnrepcv.d <- cast(meltcv.d,peptide+sample+transition.id+concentration+is.conc~variable, length)
    colnames (castnrepcv.d) [ncol (castnrepcv.d) ] <- 'replicates.summed'
    castcv.d <- cbind(castcv.d, castnrepcv.d['replicates.summed'])
   
          
    # write the CV table output file with units in the column headers
    names(castcv.d)[names(castcv.d)=='concentration'] <- paste('concentration (', units, ')', sep = "")
    names(castcv.d)[names(castcv.d)=='is.conc'] <- paste('is.conc (', units, ')', sep = "")
    names(castcv.d)[names(castcv.d)=='mean.concentration'] <- paste('mean.concentration (', units, ')', sep = "")
    write.out (castcv.d, file = paste (output.prefix, '-cvtable.csv', sep=''), row.names=FALSE)
    names(castcv.d)[names(castcv.d)==paste('concentration (', units, ')', sep = "")] <- 'concentration'
    names(castcv.d)[names(castcv.d)==paste('is.conc (', units, ')', sep = "")] <- 'is.conc'
    names(castcv.d)[names(castcv.d)==paste('mean.concentration (', units, ')', sep = "")] <- 'mean.concentration'


    # calculate final CV results file
    # this is the lowest CV value for a given sample/peptide pair if LOD/LOQ was not calculated
    # if the LOD/LOQ is available, the best transition (from *lod-loq-final.csv) is used instead
    cv.raw <- castcv.d
    cv.final <- NULL
    if ( exists ("lod.loq.final") ) {
      cv.final <- merge (lod.loq.final, cv.raw, by=c ('peptide', 'transition.id'))
    } else {
      temp <- by (cv.raw, cv.raw [, c('sample','peptide')], 
                  function (x) {
                    if (debug) {
                      print ( paste ("  ", toString (x[1,'peptide'])), quote=FALSE )
                      print ( paste ("  ", toString (x[1,'sample'])), quote=FALSE )
                    }

                    find.min <- function (column) {
                      data <- as.numeric (unlist (lapply (x[, column], toString)))
                                  
                      # if no data exists, give up!
                      no.row <- c (toString (x[1,'peptide']), rep (NA, (ncol (x) - 1)))
                      if (length(data)==0) return (list (value=NA, row=no.row))
                      
                      val <- min (data, na.rm=TRUE)
                      if ( !is.finite (val) ) return (list (value=NA, row=no.row))
                      row <- unlist (lapply (x [ which (val==data), ], toString))
                      return ( list (value=val, row=row) )
                    }
                                
                    sample.cv <- find.min ('cv')
                    
                    cv.final <<- rbind (cv.final, rbind(sample.cv$row))
                  })
    }
    
    cv.final <- data.frame (cv.final)
    # write out final sample analysis results stable with units in the column headers
    keep.cols <-  c ('sample', 'peptide', 'transition.id', 'concentration', 'is.conc', 'cv', 'mean.concentration', 'mean.IS.area', 'replicates.summed' )   
    # keep only the required columns
    cv.final <- cv.final [, keep.cols]
    names(cv.final)[names(cv.final)=='concentration'] <- paste('concentration (', units, ')', sep = "")
    names(cv.final)[names(cv.final)=='is.conc'] <- paste('is.conc (', units, ')', sep = "")
    names(cv.final)[names(cv.final)=='mean.concentration'] <- paste('mean.concentration (', units, ')', sep = "")
    if (!is.null (output.prefix))
      write.out (cv.final, paste (output.prefix, '-cvtable-final.csv', sep=''), row.names=FALSE)
  
  } # END CV
  
    
} # END calculate



##
## Plotting functions
##

plot.results <- function (output.prefix=NULL,                            # if non-NULL, used as prefix for output files
                          area.ratio.multiplier=1,                       # analyte/int.std. ratio is multiplied by this factor. Used as a place holder. IS concentration supplied by input file.
                          comparative.lod.loq=NULL,                      # if non-null, different methods for determining LOD/LOQ are applied and a
                                                                         # comparative table generated; else only the standard method is used
                          lodloq.table=FALSE,                            # if TRUE then generate the LOD-LOQ table of results
                          cv.table=FALSE,                                # if TRUE then generate the cv table
                          run.audit=FALSE,                               # if TRUE then run AuDIT
                          generate.cal.curves=FALSE,                     # if TRUE then generate calibration plots
                          use.peak.area=FALSE,                           # if TRUE then generate peak area plots in both linear and log-log plots; the IS Area is only shown on log-log plots
                          use.par=FALSE,                                 # if TRUE then the analysis is run with peak area ratios (instead of concentration);
                                                                         # in other words the area.multiplier.ratio=1 and corrections aren't applied
                          max.transitions.plot=3,                        # maximum number of transitions ot plot for calcurves                       
                          audit.cv.threshold=0.2,                        # CV threshold for AuDIT
                          site.name="mySite",                            # name of site
                          units = "fmol/ul",  						               # units used for measurement
                          max.linear.scale=NULL,                         # maximum of linear scale; if NULL, actual maximum is used
                          max.log.scale=NULL,                            # maximum of log scale; if NULL, actual maximum is used
                          plot.title="myPlotTitle",                      # the title of all plots
                          individual.plots=FALSE,                        # if TRUE, each peptide plot is put in a separate png file (under plots dir)
                          debug=FALSE                                    # set this to TRUE for writing out intermediate files/extra info
                         )
{
  # all plots are created from this function
  # this separation of calculation and plotting enables multi-pass processing of calculations
  # followed by a single plotting routine
  
  if (lodloq.table) { 
    # read in LOD/LOQ results
    # will be passed to the plotter to display LOD values on calibration plot
    lodtable <- read.csv (paste (output.prefix, '-lod-loq-raw.csv', sep=''), check.names=FALSE)
    lod.loq.final <- read.csv (paste (output.prefix, '-lod-loq-final.csv', sep=''), check.names=FALSE)
    names(lod.loq.final)[names(lod.loq.final)== paste('LOD (', units, ')', sep = "")] <- 'LOD'
    names(lod.loq.final)[names(lod.loq.final)== paste('LLOQ (', units, ')', sep = "")] <- 'LLOQ'              
    
    #
    # Plot LOD boxplots using ggplot2
    #
    today <- Sys.Date()
    titlename = paste (plot.title, today, sep='   ')
    plotheader = "Distribution of LOD values\n(Outliers not shown)"
    titlefile = paste (titlename, plotheader, sep="\n")
    
    lod.loq.final [, "LOD"] <- as.numeric (unlist (lapply (lod.loq.final[, "LOD"], toString)))
    
    LOD <- lod.loq.final[,'LOD']
    LOD <- LOD [ is.finite (LOD)]
    bx <- boxplot.stats (LOD)
    y.lim <- signif (bx$stats[5] * 1.05, digits=1)   # extreme of upper whisker + a smudge
    
    lod.df <- data.frame (LOD)
    colnames(lod.df) <- "values"
    lod.df2 <-  data.frame(lod.df, LOD=factor("LOD"))
    lod.df2 [, "values"] <- as.numeric (unlist (lapply (lod.df2[, "values"], toString)))
    
    # generate boxplot
    # the boxplot should not show outliers (to avoid having the y axis unnecessarily distorted),
    # but should be based on all data points (including outliers)
    # to accomplish this, the plot is created with all data points, and then coord_cartesian
    # zooms in to the main area of the boxplot; scale_y_continuous re-draws y-axis after zooming
    # with this approach, calculating and showing the mean can be problematic (since mean is
    # calculated with outliers included) -- hence mean is not shown
    
    ylab.lod=paste("Concentration (", units, ")", sep ="")
    
    if (use.peak.area) ylab.lod <- "Peak Area"
    if (use.par) ylab.lod <- "Peak Area Ratio"
    
    p1 <- qplot (LOD, values, data = lod.df2, geom="boxplot", fill="LOD", 
                 ylab=ylab.lod, xlab="LOD", main=titlefile) + xlab("") +
      opts (legend.position="none") + opts(plot.title = theme_text (colour = "black", face="bold", size = 7, vjust=2)) +
      coord_cartesian (ylim=c(0,y.lim)) +
      scale_y_continuous (breaks = grid.pretty (range = c(0, y.lim))) 
    
    # to save the ggplot to a PDF file
    ggsave (file=paste (output.prefix, '-lodboxplot.pdf', sep=''), width=2.5, height=5)
  }
  
  
  if (cv.table) {
    #
    # CV Plots
    #
    cv.final <- read.csv (paste (output.prefix, '-cvtable-final.csv', sep=''), check.names=FALSE)
    names(cv.final)[names(cv.final)==paste('concentration (', units, ')', sep = "")] <- 'concentration'
    names(cv.final)[names(cv.final)==paste('is.conc (', units, ')', sep = "")] <- 'is.conc'
    names(cv.final)[names(cv.final)==paste('mean.concentration (', units, ')', sep = "")] <- 'mean.concentration'
    
    # Plot cv vs. concentraiton boxplot
    today <- Sys.Date()
    titlename = paste(plot.title, today, sep='   ')
    plotheader = "Overall Reproducibility"
    titlefile = paste(titlename, plotheader, sep="\n")
    
    cv.final [,'cv'] <- as.numeric (unlist (lapply (cv.final[,'cv'],toString)))
    cv.final [,'mean.concentration'] <- as.numeric (unlist (lapply (cv.final[,'mean.concentration'],toString)))
    cv.final [,'IS.area'] <- as.numeric (unlist (lapply (cv.final[,'mean.IS.area'],toString)))
    
    # factor concentration for ggplot boxplot
    cv.final.plot <- cv.final [ is.finite (cv.final [,'concentration']), ]
    cv.final.plot[,'concentration'] <- factor(cv.final.plot$concentration)
    
    p2 <- qplot (concentration, cv, data=cv.final.plot, geom="boxplot", fill="concentration", 
                 ylab="Coefficient of Variation (%)", 
                 xlab=paste("Theoretical Concentration (", units, ")", sep =""),
                 main=titlefile) + stat_summary(fun.y=mean, geom="point", shape=15, size=2, color="red") + opts(legend.position="none") 
    
    ggsave(file=paste (output.prefix, '-cvplot.pdf', sep=''))
    
    ## if there are any samples (not in cal curve), create some useful plot
  }
  
  
  if (generate.cal.curves || use.peak.area) {
    #
    # Response curves
    #
    

    # read in data
    plotRawData <- read.csv (paste (output.prefix, '-raw-data.csv', sep=''), check.names=FALSE)
    
    # read in audit results
    # audit.result will be used to display bad transitions in calibration plots  
    audit.result <- NULL
    if (run.audit) audit.result <- read.csv (paste (output.prefix, '-audit.csv', sep=''), check.names=FALSE)
    
    # Add Summed Transitions and their cv's to the AuDIT table
    #  if the summed transition has cv > audit.cv.threshold, mark it "bad"
    if (cv.table) {
      summed.cv <- cv.final[cv.final$transition.id=="Sum.tr" & !is.na(cv.final$cv),]
      
      status <- unlist (lapply (summed.cv[,'cv'], function (x) { ifelse (as.numeric (toString(x)) > audit.cv.threshold, 'bad', 'good') }))
      summed.cv.audit <- cbind (summed.cv[,c('peptide','sample','transition.id')],
                                pvalue.final = rep (NA,nrow(summed.cv)),
                                status = rep (NA,nrow(summed.cv)),
                                cv = as.numeric (unlist (lapply (summed.cv[,'cv'],toString))),
                                cv.status = status,
                                final.call = status)
      
      audit.result <- rbind(audit.result,summed.cv.audit)
    }
    
    
    # Curve generation (calibration / peak area plots)
    generate.curves <- function (extra.prefix='', use.peak.area=FALSE, ...) {
      # generic function to be used for both concentration and peak area curves
      
      # Eliminating all blank (i.e., data points with concentration set to zero)
      # and non-numeric (samples not part of cal curve) data points
      data <- plotRawData [ is.finite (plotRawData[,'concentration']) & plotRawData[,'concentration'] != 0, ]
      
      # assemble data set
      data.final <- cbind (site = rep (site.name, nrow (data)), data)
      
      ## A. generate the PDF file with all calibration plots: order peptides by alphabetical order
      PARtable.f <- NULL
      if (individual.plots) {
        plots.dir <- paste (output.prefix, '-response-curves', extra.prefix, sep='')
        dir.create (plots.dir)
      } else {
        pdf (file = paste (output.prefix, '-response-curves', extra.prefix, '.pdf', sep=''), width = 8, height = 8, pointsize=8)
        par (oma=c(2,2,4,2)) # setup outer margin to insert main title and center multi-plot window
        par (mfrow = c(2,2))
      }
      
      peplodtable <- NULL
      tmp <- by (data.final, data.final [, c('peptide', 'site')],
                 function (x) {
                   protein <- x[1,'protein']
                   peptide <- x[1,'peptide']
                   if (lodloq.table) peplodtable <- lodtable [(lodtable[,'peptide']==unique(x[,'peptide'])),]
                   if (individual.plots) {
                     jpeg (file=paste (plots.dir, '/', peptide, '.jpeg', sep=''), width=8, height=8, units='in', res=300, pointsize=9, quality=90)
                     par (oma=c(2,2,4,2)) # setup outer margin to insert main title and center multi-plot window
                     par (mfrow=c(2,2))                 
                   }
                   PARtable <- plot.calibration.curve (x, peplodtable, audittable=audit.result, 
                                                       paste (protein, '\nPeptide:', peptide), 
                                                       n.transitions=max.transitions.plot,
                                                       full.range=c(0, max.linear.scale), 
                                                       log.full.range=c(0,max.log.scale), 
                                                       use.peak.area=use.peak.area,
                                                       use.par=use.par, output.prefix=output.prefix,
                                                       spike.level=area.ratio.multiplier,
                                                       thetitle=plot.title, units=units, ...)
                   if (individual.plots) dev.off()                 
                   PARtable.f <<- rbind (PARtable.f, PARtable)                 
                 })
     
      # write the corresponding csv file
      write.csv (PARtable.f,file = paste (output.prefix, '-response-curves', extra.prefix, '.csv', sep=''), row.names=FALSE)
      if (!individual.plots) dev.off()
      
      ## B. generate the PDF file with all calibration plots: order peptides by protein groups
      pdf (file = paste (output.prefix, '-by_protein_response-curves', extra.prefix, '.pdf', sep=''), width=8, height=8, pointsize=8)
      par (oma=c(2,2,4,2)) # setup outer margin to insert main title and center multi-plot window
      par (mfrow = c(2,2))
      peplodtable <- NULL
      tmp <- by (data.final, data.final [, c('peptide', 'protein')],
                 function (x) {
                   protein <- x[1,'protein']
                   page.break (protein)
                   if (lodloq.table) peplodtable <- lodtable [(lodtable[,'peptide']==unique(x[,'peptide'])),]
                   PARtable <- plot.calibration.curve (x, peplodtable, audittable=audit.result, 
                                                       paste (protein, '\nPeptide:', x[1,'peptide']), 
                                                       n.transitions=max.transitions.plot,
                                                       full.range=c(0, max.linear.scale), 
                                                       log.full.range=c(0,max.log.scale), 
                                                       use.peak.area=use.peak.area,
                                                       use.par=use.par, output.prefix=output.prefix,
                                                       spike.level=area.ratio.multiplier,
                                                       thetitle=plot.title, units=units, ...)
                 })
      dev.off()
    } # END generate.curves
    
    # generate the calibration plots
    if (generate.cal.curves) {
      print ('Generating response curves ...', quote=FALSE)
      # set up ranges for plotting data
      max.value <-  max ( plotRawData[,'concentration'], plotRawData[,'actual.conc'], na.rm=TRUE )
      if (is.null (max.linear.scale)) max.linear.scale <- max.value
      if (is.null (max.log.scale)) max.log.scale <- max.value
      # plot
      generate.curves ()
    }
    
    # generate the peak area plots
    if (use.peak.area) {
      print ('Generating peak area plots ...', quote=FALSE)
      # use default ranges and plot
      generate.curves (extra.prefix='-peakarea', use.peak.area=TRUE)
    }
  }
  
}




# function to plot the transitions
plot.transition <- function (data.tr, bad.tran.data, rug.shift=0, low.conc.value=0, par.conc="I",
                             info.columns = c ('site', 'protein', 'peptide', 'transition.id'),
                             use.peak.area=FALSE, plot.IS.Area=FALSE, col.tr=1, calc.fit=TRUE,
                             lty.fit=1, lwd.fit=1, add=FALSE, plot.points=TRUE, outputPrefix="myAnalysis", thetitle="myPageTitle", ...) {
  
  target.col <- ifelse (use.peak.area, "area", "concentration.estimate")
  keep <- is.finite (data.tr[,target.col])    # NB: both 'area' and 'concentration.estimate' should be NA (or otherwise)
  
  tc <- data.tr[keep,'actual.conc']
  tm <- data.tr[keep, target.col]
  
  tm.is.area <- data.tr[keep,'IS.area']
  
  if (nrow(bad.tran.data)>0){
    bad.keep <- is.finite(bad.tran.data[,target.col])
    bad.tc <- bad.tran.data[bad.keep,'actual.conc']
    bad.tm <- bad.tran.data[bad.keep, target.col]
  }
  
  
  if (plot.points) {
    # plot points
    if (!add) { 
      
      # plot all points
      plot (tc,tm, col=col.tr, axes=FALSE, frame=TRUE, ...)
      
      # replot the "bad" transition with "black" color
      if (nrow(bad.tran.data)>0) {
        # bad transitions (based on AuDIT) present
        rugdelta <- bad.tc * 0.1 * rug.shift   # 5% shift for each transition
        rug (bad.tc+rugdelta, ticksize=0.05, lwd=2, col=col.tr)
      }
      
      # take control of axis labels to avoid scientific notation in log plots
      axis (1, at=axTicks(1), labels=formatC (axTicks(1), format='fg'))
      axis (2, at=axTicks(2), labels=formatC (axTicks(2), format='fg'))
      
      if (use.peak.area & plot.IS.Area) {  
        # plot the IS.area only in log-log scale
        points (tc, tm.is.area, col="grey", ...)
      }
      
    } else {
      
      # plot all points
      points(tc,tm, col=col.tr, ...)
      
      # replot the "bad" transition with "black" color
      if (nrow(bad.tran.data)>0) {
        # bad transitions (based on AuDIT) present
        rugdelta <- bad.tc * 0.1 * rug.shift   # 5% shift for each transition
        rug (bad.tc+rugdelta, ticksize=0.05, lwd=2, col=col.tr)
      }
      
      if (use.peak.area & plot.IS.Area) { 
        # plot the IS.area only in log-log scale
        points(tc,tm.is.area, col="grey", ...)
      }
    }
  }
  
  # print the calibration plot filename and date on top of each multi-page window
  titlename = paste("Title", thetitle, sep=' : ')
  if (use.peak.area){
    titlename = paste(titlename, "(Peak Area Plots)", sep="   ")
    filename = paste(outputPrefix, '-response-curves-peakareaplots.pdf', sep='')
  } else {
    titlename = paste(titlename, "(Concentration Plots)", sep="   ")
    filename = paste(outputPrefix, '-response-curves', sep='')
  }
  title_file = paste(titlename, filename, sep="\n")
  today <- Sys.Date()
  title_file_date = paste(title_file, today, sep="   ")
  title (main=title_file_date, outer=T)
  
  
  # determine weighted robust regression line (if requested)
  fit.details <- NULL
  if (calc.fit) {
    w <- 1/(tc)^2
    tfit <- try (rlm (tm ~ tc, weights=w, method="MM", maxit=1000), silent=TRUE)
    if (!inherits (tfit, "try-error")) {
      # plot regression line
      abline(tfit, lty=lty.fit, lwd=lwd.fit, col=col.tr)
      
      # calculate R^2
      r2 <- (cor(tm,tc))^2 
      
      # record regression fit and PAR for conc point specified by par.conc (I, by default)
      MTran <- data.tr[(data.tr[,'sample']==par.conc),]
      MTran <- MTran[is.finite(MTran[,'concentration.estimate']),]
      PARforM <- mean(MTran[,'area.ratio'], na.rm=TRUE)
      #Get the slope, slope standard error, y-itercept, y-itercept standard error, coefficent of variation
      fit.details <- c (unlist (lapply (data.tr[1,info.columns], toString)),
                        tfit$coefficients[2], summary(tfit)$coefficients[2,2], tfit$coefficients[1], 
                        summary(tfit)$coefficients[1,2], r2, length (tm))
    } else {
      print (paste ('   ', toString(data.tr[1,'transition.id']), 'regression fit failed'), quote=FALSE)
    }
  }
  
  invisible (fit.details)
  
}
# END function plot.transition

# funtion to plot the calibration curves
plot.calibration.curve <- function (data, peplodtable, audittable, info, par.conc="I",
                                    info.columns = c ('site', 'protein', 'peptide', 'transition.id'),
                                    # columns containing identifying info for table
                                    n.transitions = 3,                 # max number of transitions to plot
                                    full.range = c (0,150),            # x & y range for full scale plots
                                    use.peak.area=FALSE,               # if TRUE then plot peak area instead of concentrations
                                    log.full.range = c (0,100),        # x & y range for log-log plots
                                    zoom.range = c (0,1),              # x & y from for zoom plots (when log.plots=FALSE)
                                    tran.pch = c(18,8,17,15,16,13),    # plotting characters for transitions
                                    # transition colors:
                                    tran.col = c(brewer.pal(3, 'Set1'),        #  3 bright + 3 pastel colors
                                                 brewer.pal(3, 'Pastel2')),
                                    use.par=FALSE,                     # if TRUE then the analysis is run with peak area ratios
                                    output.prefix=NULL,
                                    spike.level = 1,
                                    thetitle="myPlotTitle",
                                    units="fmol/ul")
{
  # plot calibration curve for given peptide in data
  # a separate calibration curve + regression line is plotted for each transition
  # high abundance transitions have brighter colors (first half of tran.col)
  # lower abundance transitions have pastel colors (second half of tran.col)
  # the legend lists transitions by size
  # the overall regression for all transitions combined is indicated, along with the diagonal
  
  tr.quality.fn <- function (x) {
    # generic function for calculating transition quality
    # (for sorting and eventually selecting a subset of transitions)
    
    x.is <- x[,'IS.area']
    retVal<- mean (x.is, na.rm=TRUE)
    
    if (x[1,'transition.id'] == 'Sum.tr'){
      tran.col <<- c(brewer.pal(7, 'RdPu')[7],tran.col)
      tran.pch <<- c(4,tran.pch)
      plotSumTr <<- TRUE
      retVal <- Inf 
    }
    
    actual.n.transitions <<- actual.n.transitions + 1  
    return (retVal) 
  }
  
  
  
  plot.all.transitions <- function (plot.type) {
    fit.table <- NULL
    
    # plot all transitions, for requested plot type (full, zoom, log)
    for (j in 1:length(tran.ordered))  {
      tran.index <- (pep.data[,'transition.id']==tran.ordered[j])
      tran.data <- pep.data[tran.index,]
      
      # rug shift to plot AuDIT interferences
      # shift the rug around the respective concentration
      rugshift <- j - ceiling (n.transitions/2)
      
      if (plot.type=="full") {
        # set up parameters for full range plots
        log <- ""
        plot.fit <- TRUE
        xrange <- yrange <- full.range
        if (use.peak.area) {xrange <- yrange <- NULL}
        if (use.par) yrange <- NULL
        title <- 'Response curves for'
        plotISarea <- FALSE
      }
      
      if (plot.type=="zoom") {
        # set up parameters for zoom plots
        log <- ""
        plot.fit <- TRUE
        plotISarea <- FALSE
        
        # zoom plots: set y-axis scale to show all points in zoom range (but leave out outliers)
        if (use.peak.area) y.col <- 'area'
        else y.col <- 'concentration.estimate'
        xrange <- yrange <- zoom.range
        y.values <- pep.data[pep.data[,'actual.conc']<=zoom.range[2], y.col]
        ylim <- boxplot.stats(y.values)$stats[5]
        if (is.finite (ylim) && ylim > yrange[2])
          yrange[2] <- ylim * 1.2
        title <- 'Response curves (zoom)'
if (use.par && is.finite(ylim)) yrange[2] <- ylim        
      }
      
      if (plot.type=="log") {
        plotISarea <- TRUE
        # setup parameters for log plots
        if (use.peak.area || use.par) {xrange <- yrange <- NULL}
        else {
          yrange <- zoom.range <- log.full.range
          values <- unlist (tran.data [, c ('actual.conc', 'concentration.estimate')])
          yrange[1] <- zoom.range[1] <- 0.9 * min ( values [ is.finite (values) & values > 0 ] )  # set lower lim to be non-zero
          xrange <- zoom.range
        }
        log <- "xy"
        plot.fit <- FALSE
        title <- 'Data points for'
        
        # select the "bad" transitions (only displayed on log/data plots)
        if (nrow(bad.pep.list)>0){
          bad.tran.index <- (bad.pep.list[,'transition.id']==tran.ordered[j])
          bad.tran.data <- bad.pep.list[bad.tran.index,]
        }
      }
            
      
      if (j == 1) {
        if (use.peak.area){            
          fit.details <-
            plot.transition (tran.data, bad.tran.data, rug.shift=rugshift,
                             par.conc=par.conc, 
                             info.columns=info.columns,
                             col.tr=tran.col[j], type="p",
                             calc.fit=plot.fit,
                             log=log,
                             pch=tran.pch[j], 
                             use.peak.area=use.peak.area,
                             xlim=xrange, ylim=yrange, 
                             main=paste(title, info), 
                             plot.IS.Area=plotISarea, 
                             xlab=paste("Theoretical Concentration (", units, ")", sep =""), 
                             ylab=ylabel, 
                             outputPrefix=output.prefix, 
                             thetitle=thetitle, low.conc.value)
        } else {
          fit.details <-
            plot.transition (tran.data, bad.tran.data, rug.shift=rugshift,
                             par.conc=par.conc, 
                             info.columns=info.columns,
                             col.tr=tran.col[j], type="p",
                             calc.fit=plot.fit, 
                             log=log,
                             use.peak.area=use.peak.area,
                             xlim=xrange, ylim=yrange, 
                             pch=tran.pch[j],
                             main=paste(title, info), 
                             plot.IS.Area=plotISarea,
                             xlab=paste("Theoretical Concentration (", units, ")", sep =""), 
                             ylab=ylabel, 
                             outputPrefix=output.prefix, 
                             thetitle=thetitle, 
                             low.conc.value)
        }
      } else {
        fit.details <-
          plot.transition (tran.data, bad.tran.data, rug.shift=rugshift,
                           par.conc=par.conc, 
                           info.columns=info.columns, 
                           use.peak.area=use.peak.area,
                           add=TRUE, col.tr=tran.col[j], 
                           plot.IS.Area=plotISarea,
                           type="p", pch=tran.pch[j],
                           calc.fit=plot.fit,
                           outputPrefix=output.prefix, 
                           thetitle=thetitle, 
                           low.conc.value)
        
      }
      if (!is.null (fit.details) && plot.type=="full") fit.table <- rbind (fit.table, fit.details)
    }
    
    
    legend ("topleft", inset=0.05, tran.ordered.leg, col=tran.col.leg,
            pch=tran.pch.leg, cex=0.7, bty='n')
    
    # Draw diagonal line. If using peak area ratio or peak area then do not draw line
    if (!use.par & !use.peak.area) abline(0,1,lty=1,lwd=0.5,col='black')
    
    invisible (fit.table)    
  }  
  
  
  
  print (paste ('  ', strsplit (info, split='\\\n')[[1]][2]), quote=FALSE)
  
  pep.data <- data     # input data is for a specific peptide
  PARtable <- NULL
  actual.n.transitions <- 0
  plotSumTr <- FALSE
  
  # get the lowest concentration point
  low.index <- 1
  low.concentrations <- sort (unique (pep.data [,'concentration']))
  low.conc.value <- low.concentrations[low.index]
  
  # check if audit results have been generated (to mark bad transitions)
  bad.pep.list <- data.frame()     # default: no bad transitions
  if (!is.null(audittable)){
    # merge audit and peptide data
    audit.peptide.table <- merge (audittable, pep.data)
    
    if (nrow (audit.peptide.table) > 0 ) {
      # now merge the LOQ data (if present)
      if (!is.null (peplodtable)) {
        merged.au.pep.loq <- merge (audit.peptide.table, peplodtable[,c('peptide','transition.id','LLOQ')])
        merged.au.pep.loq [,'LLOQ'] <- min (merged.au.pep.loq [,'LLOQ'], na.rm=TRUE)  # lowest LOQ is LOQ of peptide
      } else {
        # lod/loq tables not present; set LOQ=0
        merged.au.pep.loq <- cbind (audit.peptide.table, LLOQ=rep (0, nrow(audit.peptide.table)))
      }
      
      # select the list of AuDIT determined "bad" peptides from the list
      # rule for selecting "bad" peptides: final.call = "bad" and the particular peptide/transition has to be above the calculated LOQ for the peptide/transition
      # the "bad" peptide/transition will be displayed in black color on the log-log calibration plot 
      bad.pep.list <- merged.au.pep.loq[(merged.au.pep.loq$final.call=="bad" &
                                           merged.au.pep.loq$concentration>merged.au.pep.loq$LLOQ),]
    }
  } 
  
  # Extract the levels of the transitions and order transitions by IS abundance
  pep.data[,'transition.id'] <- unlist(lapply(pep.data[,'transition.id'],toString))
  #tran.quality <- tapply (pep.data[,c('IS.area','transition.id')], list(pep.data[,'transition.id']), tr.quality.fn)
  tran.quality <- by (pep.data, pep.data[,'transition.id'], tr.quality.fn)
  
  # calculate actual number of plottable transitions
  if (plotSumTr) actual.n.transitions <- actual.n.transitions - 1
  if (actual.n.transitions < n.transitions) n.transitions <- actual.n.transitions	
  if (plotSumTr) n.transitions <- n.transitions + 1
  
  index1 <- order (tran.quality, decreasing = TRUE) [1:n.transitions]
  tran.ordered <- names(tran.quality)[index1]
  if (length(tran.ordered) > 1) tran.ordered.leg <- mixedsort(tran.ordered)   # legend has transitions listed in alphanumeric order
  else tran.ordered.leg <- tran.ordered
  
  
  # Add the LOD and average IS area values to each transition. 
  # These will be displayed in both linear and log plots.
  tran.temp <- tran.ordered.leg
  legend.info.table <- data.frame()  	
  for(j in 1:length(tran.ordered.leg))  {
    for(k in 1:length(peplodtable[,'transition.id']))  {
      if (peplodtable[k,'transition.id'] == tran.temp[j]){
        lodvalue<- peplodtable [k,'LOD']
        lodvalue<-sprintf ("%.2f", lodvalue)
        is.area<- peplodtable [k,'mean.IS.area']
        is.area<-sprintf("%.1E", is.area)
        row <- cbind (tran.id=tran.temp[j], lod=lodvalue, isarea=is.area)    
        legend.info.table <- rbind (legend.info.table, row)      
      }
    } 
  }
  
  temp.tran.ordered.leg <- matrix(ncol=1,nrow=(length(tran.ordered.leg)+1))
  for (j in 1:length(tran.ordered.leg))  {
    temp.tran.ordered.leg[j+1] <- tran.ordered.leg[j]
  }
  tran.ordered.leg <- temp.tran.ordered.leg
  
  # Display spike-in level with no characters in legend for spike-in value
  spike.index <- length(tran.ordered.leg)+1
  tran.ordered.leg[spike.index] <- paste ("Spike level", spike.level, sep=' : ') 
  
  
  index2 <- mixedorder(tran.ordered)
  tran.col.leg <- tran.col[index2]              # ensure legend colors and plotting characters match
  tran.pch.leg <- tran.pch[index2]              # plotting color/character for transitions
  
  temp.tran.col.leg <- matrix(ncol=1,nrow=(length(tran.col.leg)+1))
  temp.tran.pch.leg <- matrix(ncol=1,nrow=(length(tran.pch.leg)+1))
  
  for (j in 1:length(tran.col.leg))  temp.tran.col.leg[j+1] <-  tran.col.leg[j]
  tran.col.leg <- temp.tran.col.leg
  
  for (j in 1:length(tran.pch.leg)) temp.tran.pch.leg[j+1] <-  tran.pch.leg[j]
  tran.pch.leg <- temp.tran.pch.leg
  tran.col.leg[spike.index] <- "white"  
  
  # adjust y-axis label based according to user choice 
  if (use.par) ylabel <- "Peak Area Ratio"
  else if (use.peak.area) ylabel <- "Peak Area"
  else ylabel <- paste("Measured Conc (", units, ")", sep ="")
  
  # initialize
  bad.tran.data <- data.frame()
  legend.info <- data.frame()
  
  
  ##
  ## (linear) full range plots
  ##
  fit.peptide <- plot.all.transitions (plot.type='full')
  PARtable<- rbind (PARtable, fit.peptide)
  
  ##
  ## zoom plots 
  ##
  plot.all.transitions(plot.type='zoom')
  
  
  ##
  ## log plots
  ##
  plot.all.transitions(plot.type='log')
  
  
  ##
  ## info table (printed as a separate plot)
  ##
  for (j in 1:length (tran.ordered)) {
    temp.legend.info <- cbind(tran.id = tran.ordered[j], tran.color = tran.col[j], tran.shape = tran.pch[j])
    legend.info <- rbind(legend.info, temp.legend.info)
  }
  
  legend.info <- merge(legend.info.table, legend.info, by = "tran.id")
  legend.colors <- matrix (nrow=nrow(legend.info), ncol=3)
  for (j in 1:nrow(legend.info)){
    row.colors <- rep (toString(legend.info[j,"tran.color"]), 3)
    legend.colors[j,] <- row.colors
    
  }
  legend.final <- cbind (legend.info[,1:3])
  colnames (legend.final) <- c ('Transition', 'LOD', 'IS Area')
  textplot(legend.final, valign="center", halign = "center", show.rownames = FALSE, col.data = legend.colors, cex=2)
  
  
  
  # return table of slope/intercept for regression
  if (!is.null (PARtable)) 
    colnames (PARtable) <- c(info.columns, "slope", "slope stderr", "y-intercept", "y-intercept stderr", "rsquare", "N")
  invisible (PARtable)
}
# END function plot.calibration.curve



##
## Main function for Automated Detection of Inaccurate and Imprecise Transitions in MRM Mass Spectrometry
##
run.AuDIT <-
  function (data.file,                                        # file with data (format described below)
            output.prefix=NULL,                               # if non-NULL, used as prefix for output files
            required.columns=c('sample', 'replicate', 'peptide', 'transition.id', 'area', 'IS.area'),
            required.columns.location=1:6,                    # column numbers for required.columns in data.file
            interference.threshold1=1e-5,                     # pvalue above which IS and/or analyte are considered interference free
            interference.threshold2=1e-5,                     # pvalue below which IS and/or analyte definitely have interference
            cv.threshold=0.2,                                 # cv threshold for combined pvalue + cv based interference decision 
            notation=list (good='good', bad='bad', ugly='?'), # notation for transition labeling
            all.pairs=FALSE,                                  # if TRUE use all possible combinations of transitions
            debug=FALSE                                       # if TRUE, extra intermediate results files will be written out
           )
{
  
  # Determine if transitions listed in the data.file have any interference.
  # Expects input in the form of a pre-assembled data table (with required.columns present in the order shown).
  # Samples and the corresponding replicates (incl. diff. concentrations) should be listed such that:
  #   sample is the actual sample id (excluding replicate notation);
  #   replicate indicates replicate number for sample.
  # (sample is usually derived from the SampleName column in Skyline, MultiQuant, etc.)
  # For a given peptide and transition, all replicates must have the same sample name
  # Note that sample must be different for different concentrations of the same sample (if any).
  # Sample replicates would generally have the sample id followed by a suffix in sample.name, with this information
  #  explicitly specified in the (sample, replicate) columns
  # Transitions are indicated in transition.id, and must be unique for each peptide. They may be indicated as 1,2,3
  #  for each peptide (as in MultiQuant) or may be derived from the fragment and charge (y7.2, y5.2, etc.) as for
  #  data from Skyline, etc. While different peptides may have the same transition.id, these must be unique for a
  #  given peptide.


  if (debug) print (' ... Running AuDIT', quote=FALSE)

  # check if the file is a csv file by looking for a comma
  line <- scan(data.file, what="character", nlines=1, quiet=TRUE)
  if (length (grep(",", line)) == 0) {
    stop("Data file must be comma separated.")
  }

  data <- read.csv (data.file,
                    stringsAsFactors=FALSE, na.strings=c('NA','N/A','#N/A'))  # read so that strings are retained,
                                                                              # and numerical columns are identified
  check.columns (data, required.columns)

  if (ncol(data) < max(required.columns.location)) {
    missing.cols <- required.columns.location [required.columns.location > ncol(data)]
    missing.cols <- paste (missing.cols, collapse=", ")
    stop (paste ("Missing required columns", missing.cols, "in dataset"))
  }

  data <- data [, required.columns.location]
  colnames (data) <- required.columns
  data <- data.frame (data)

  # convert transition.id to transition which are identical for every peptide
  tr.old <- data [, c('sample', 'replicate', 'peptide', 'transition.id')]
  tr.new <- NULL
  temp <- by (tr.old, tr.old [,'peptide'],
              function (x) {
                trs <- unique (x[,'transition.id'])
                trs.number <- unlist (lapply (x[,'transition.id'], function (v) { which (trs %in% v) }))
                tr.new <<- rbind (tr.new, cbind (x, transition=trs.number))
              })
  data <- merge (data, tr.new)

  # generate list of combinations to use for calculating relative ratios
  # if all.pairs==TRUE, all possible pairs of transitions are created
  tr <- sort (unique (data [,'transition']))
  if (all.pairs) {
    library(gtools)
    tr.combo <- combinations ( length(tr), 2, tr )
  } else {
    tr.combo <- cbind (tr, c(tr[-1], tr[1]))
  }

  # calculate relative area ratios for transitions in each sample/replicate;
  # for transitions i,j in {1..n}, ratio r_ij = area(i) / area (j) for all pairs i,j;
  # if all.pairs==FALSE, ri = area(i) / area (i+1 mod n), (r1=tr1/tr2, r2=tr2/tr3, ..., rn=trn/tr1);
  # this is used to determine which transitions have interferences
  rel.ratios <- NULL
  temp <- by (data, data [, c('peptide', 'sample', 'replicate')],
              function (x) {
                ratios <- IS.ratios <- NULL
                for (r in 1:nrow(tr.combo)) {
                  ratio <- x [ x[,'transition']==tr.combo[r,1], 'area'] / x [ x[,'transition']==tr.combo[r,2], 'area']
                  is.ratio <- x [ x[,'transition']==tr.combo[r,1], 'IS.area'] / x [ x[,'transition']==tr.combo[r,2], 'IS.area']

                  if ( length (ratio) == 0 ) ratio <- NA
                  if ( length (is.ratio) == 0 ) is.ratio <- NA
                  
                  ratios <- c (ratios, ratio)
                  IS.ratios <- c (IS.ratios, is.ratio)
                }
                
                rel.ratios <<- rbind (rel.ratios, 
                                      c ( x[1,'peptide'], x[1,'sample'], x[1,'replicate'], ratios, IS.ratios ))
              })
       
  combos <- apply (tr.combo, 1, paste, collapse='.')
  ratio.colnames <- paste ('ratio', combos, sep='_')                   # transition ratios (analyte)
  ISratio.colnames <- paste ('IS.ratio', combos, sep='_')              # internal standard
  pvalue.colnames <- paste ('pvalue', combos, sep='_')
  colnames (rel.ratios) <- c ('peptide', 'sample', 'replicate',
                              ratio.colnames, ISratio.colnames)
                              
  data2 <- data.frame (rel.ratios)
             
  for (col in grep ('ratio', colnames (data2)))
    data2 [, col] <- as.numeric (unlist (lapply (data2 [, col], toString)))


  # use statistical tests to determine if the analyte and IS relative ratios indicate interference
  
  #
  # interference in each sample
  #
  # use t-test for determining (for each sample/conc) whether the analyte and IS ratios are similar
  t.result <- by.collapse (data2, data2 [,c ('peptide', 'sample')], 
                           function (x) {
                             pvals <- NULL
                             for (i in 1:length(combos)) {
                               test <- try ( t.test (x[,ratio.colnames[i]], x[,ISratio.colnames[i]]), TRUE )      # t-test
                               # test <- try ( var.test (x[,ratio.colnames[i]], x[,ISratio.colnames[i]]) )        # F-test
                               pval <- ifelse (!inherits (test, "try-error"), test$p.value , NA)
                               pvals <- c (pvals, pval)
                             }

                             return (pvals)
                           })

  t.result.data <- t.result[,3:ncol(t.result)]
  if (length (t.result.data [!is.na(t.result.data)]) == 0) {
    stop("Could not calculate any p values.")
  }
  
  colnames (t.result)[3:ncol(t.result)] <- pvalue.colnames


  # correct -test pvalues for multiple hypothesis testing (BH-FDR)
  t.pvalues <- t.result [, pvalue.colnames]
  t.corrected.pvalues <- pval2fdr ( unlist (t.pvalues), monotone=FALSE )
  t.result [, pvalue.colnames] <- matrix (t.corrected.pvalues, ncol=ncol (t.pvalues))

  
  # for each transition, combine p-values for the ratios that contain this transition
  #  (e.g., tr1 is used in ratio r1=tr1/tr2 and r3=tr3/tr1; hence combine p-values for r1 and r3 for transition 1,
  #         tr2 is used in ratio r2=tr2/tr3 and r1=tr1/tr2; combine p-values for r2 and r1,
  #         tr3 is used in ratio r3=tr3/tr1 and r2=tr2/tr3; combine p-values for r3 and r2,
  #   etc.)
  # different ratios use the same peak areas, and hence the pvalues for the ratios are DEPENDENT;
  # combination of dependent p-values is accomplished using the orginal method in:
  #  Brown (1975), A method fro combining non-independent one-sided tests of significance, Biometrics, 31:987-992
  # with modifications/improvements noted in:
  #  Kost & McDermott (2002), Combining dependent p-values, Statistics & Probablility Letters, 60:183-190
  #
  combined.p <- data.frame (matrix (rep (1, nrow(t.result) * length (tr)), ncol=length(tr)))
  colnames (combined.p) <- tr


  cov.term <- function (cov.table) {
    total.cov <- 0
    for (i in 2:nrow (cov.table))
      for (j in 1:(i-1)) {
        rho <- cov.table [i,j]
        cov.term <- 3.279 * rho + 0.711 * rho^2           # Kost & McDermott, pg. 188
        total.cov <- total.cov + cov.term
      }
    return (total.cov)
  }
  
  k <- ifelse (all.pairs, length(tr)-1, 2)   # number of tests = number of p-values
  for (i.tr in tr) {
    for (r in 1:nrow(t.result)) {
      ratio.table <- NULL
      i.combos <- which (apply (tr.combo, 1, function (x) { i.tr %in% x }))
      subdata <- data2 [ data2 [,'peptide']==t.result[r,'peptide'] & data2[,'sample']==t.result[r,'sample'], ]
      if ( nrow(subdata) == 0 ) {
        combined.p [r, i.tr] <- NA
        next
      }
      for (i.c in i.combos){
        # ratio.ic <- subdata [, ratio.colnames[i.c]] - subdata [, ISratio.colnames[i.c]]
        ratio.ic <- c (subdata [, ratio.colnames[i.c]], subdata [, ISratio.colnames[i.c]])
        ratio.table <- cbind (ratio.table, ratio.ic)
      }
      colnames (ratio.table) <- combos [i.combos]
      ratio.table [ is.na (ratio.table) ] <- 0
      
      cov.tr.r <- cor (ratio.table)
      cov.tr.r [ is.na (cov.tr.r) ] <- 0

      E.chisq <- 2*k                                      # Brown, pg. 989, 991
      Var.chisq <- 4*k + 2*cov.term (cov.tr.r)
                           
      f.dep <- 2 * E.chisq^2 / Var.chisq
      c.dep <- Var.chisq / (2 * E.chisq)

      chisq.combined <- sum (-2 * log ( t.result [r, pvalue.colnames [i.combos]] ))
      pvalue.combined <- 1 - pchisq (chisq.combined / c.dep, df=f.dep)

      combined.p [r, i.tr] <- pvalue.combined
    }
  }

  # annotate p-values to indication which transition are good, bad and ugly
  ok <- data.frame (matrix (rep (0, nrow(t.result) * length (tr)), ncol=length(tr)))
  ok [ combined.p > interference.threshold1 ] <- notation$good
  ok [ combined.p < interference.threshold2 ] <- notation$bad
  ok [ is.na (combined.p) ] <- NA
  ok [ ok==0 ] <- notation$ugly
  colnames (ok) <- paste ('transition', tr, sep='.')

  sample.result <- cbind (t.result, combined.p, ok)

  ## convert sample.result table into "long" form
  trs <- unlist (lapply (unique (data[,'transition']), toString))
  trs.status <- paste ('transition', trs, sep='.')
  res.samp <- sample.result [ , c('peptide', 'sample', trs, trs.status)]
  res.sample.level.long <- reshape (res.samp, direction='long', varying=list (trs, trs.status))
  # replace 'time' varible to reflect the correct transition
  res.sample.level.long [ ,'time'] <- trs [ res.sample.level.long [ ,'time'] ]
  colnames (res.sample.level.long) <- c('peptide', 'sample', 'transition', 'pvalue.final', 'status', 'id')

  ## retrieve and include the transition.id column to enable proper interpretation of results
  d.extra <- unique (data [ , c ('peptide', 'sample', 'transition', 'transition.id')])
  d.result <- merge (d.extra, res.sample.level.long)
  
  ## calculate cv for peak area ratio
  data.cv <- by.collapse (data [,'area'] / data [,'IS.area'],
                          data [, c ('peptide', 'sample', 'transition')], cv)
  colnames (data.cv) [ ncol(data.cv) ] <- 'cv'
  d.result <- merge (d.result, data.cv)
  cv.status <- ifelse (d.result [,'cv'] < cv.threshold, notation$good, notation$bad)
  final.call <- ifelse ((d.result[,'status']==notation$ugly & cv.status==notation$bad) |
                        d.result [,'status']==notation$bad | cv.status==notation$bad,
                        notation$bad, notation$good)    # ? are treated as bad
  d.result <- cbind (d.result, cv.status, final.call)

  # eliminate the internally created 'transition' column, and the 'id' column introduced by reshape
  d.final <- d.result [ , setdiff (colnames (d.result), c ('transition', 'id')) ]
                        
  if (!is.null (output.prefix)) {
    # save intermediate and result file
    if (debug) {
      write.table (data2, paste (output.prefix, '-intermediate1.csv', sep=''), sep=',', row.names=FALSE)
      write.table (sample.result, paste (output.prefix, '-intermediate2.csv', sep=''), sep=',', row.names=FALSE)
      write.table (d.result, paste (output.prefix, '-intermediate3.csv', sep=''), sep=',', row.names=FALSE)
    }
    #write.table (d.final, paste (output.prefix, '.csv', sep=''), sep=',', row.names=FALSE)
  }

  invisible ( list (data=data2, sample.level=sample.result, result=d.final) )
}
# END function run.AuDIT

##
## Various Support Functions
##

##
check.columns <- function (data, reqd.cols) {
  # check that columns are exactly as required
  expected.col.names <- unlist (lapply (reqd.cols, tolower))
  col.names <- colnames(data)

  # remove any extra spaces
  col.names <- lapply (col.names, gsub, pattern=" ", replacement="")

  # make lower case
  col.names <- lapply (col.names, tolower)

  col.names <- unlist (col.names)
  if (!identical (col.names, expected.col.names)) {
    cat (paste ("Expected column: ", expected.col.names, " -- found ", col.names, ".\n", sep=''))
    stop (paste ("An error occurred which validating the input file format. \nPlease check that the columns in the input file have the correct names and are in the right order."))
  }
}

# function for page breaks in plots
# when starting the plot, call start.new.plot() function
# then invoke page.break with the appropriate attribute
# page breaks will be inserted when the attribute value changes
current.attr <- NULL
start.new.plot <- function () { current.attr <<- NULL }
page.break <- function (attr) {
  if (!is.null (current.attr) && (attr != current.attr)) {
    mfrow <- par ("mfrow")      # remember graphical parameters
    par (mfrow=mfrow)           # invoking this creates a page break (when needed)
  }
  current.attr <<- attr
}

##
cv <- function (x) {
  # CV = std. dev. / mean
  value <- try ( sd (x, na.rm=TRUE) / mean (x, na.rm=TRUE), TRUE )
  result <- ifelse ( !inherits (value, "try-error"), value, NA )
  return (result)
}

##
by.collapse <- function (...) {
  # an extension of the 'by' function to return a data frame
  # instead of a list/array that by usually does

  result <- by (...)
  dimensions <- expand.grid ( dimnames (result) )
  # NB: In expand.grid, the first dimension varies fastest;
  #     the same happens in the output of by, and hence the
  #     table of dimensions and results match
  
  # if the result of the applied function is a single value ... use cbind ... else use do.call (rbind, ...)
  result.length <- length (result[[1]])
  
  # not all possible levels of the indexing attributes may be present
  # fill NA for missing combination results (else, 'dimensions' will not match with 'data' below)
  for (i in 1:length(result)) 
    if ( is.null (result[[i]])) result[[i]] <- rep (NA, result.length)
 
  if (result.length==1) data <- cbind ( unlist (lapply (result, unlist)) )
  else data <- do.call (rbind, result)
  
  final.result <- cbind (dimensions, data)
  invisible (final.result)
}

# correction for multiple hypothesis testing
# converts a list of p-values to a list of fdr p-values
# courtesy of Stefano Monti

pval2fdr <- function( p, monotone=TRUE )
{
 p.ord <- order(p)
 p1 <- p[p.ord]
 fdr <- p1 * length(p) / cumineq(p1,p1)
 if (monotone)
   for ( i in (length(p)-1):1 ) {
     fdr.min <- min(fdr[i],fdr[i+1])
     if ( !is.na (fdr.min) ) fdr[i] <- fdr.min
   }
 fdr[fdr>1] <- 1
 fdr[rank(p)]
} 

# support function for pval2fdr
cumineq <- function( prm, obs, dir=1, debug=F )
{
 # INPUT:
 #  - prm    n-sized array
 #  - obs    n-sized array
 # WHAT:
 #  for each entry in obs, count how many entries in prm
 #  are <= (dir=1) or >= (dir=2) than that entry
 #
 p.ord <- order(if ( dir==1 ) prm else -prm)
 o.ord <- order(if ( dir==1 ) obs else -obs)
 o.rnk <- rank(if ( dir==1 ) obs else -obs)

 # sort entries
 #
 prm <- prm[p.ord]
 obs <- obs[o.ord]

 u.obs <- unique(obs)
 cup <- c(prm,u.obs)
 cup <- cup[order(if (dir==1) cup else -cup)]
 fp <- length(cup)+1-match(obs,rev(cup))-match(obs,u.obs)

 # return values sorted according to original order (o.rnk)
 #
 return ( if (debug)
            cbind( prm[o.rnk], obs[o.rnk], fp[o.rnk] )
          else
            fp[o.rnk] )
}

give.n <- function(x) { 
  # Return N
  return (c(y = mean(x), label = length(x))) 
}

# Check format of input data file and preprocess 
# Only acceptable format at this point is the predefined Skyline format 02/01/2012. 10 predefined column headers in no particular order. 
# Other formats: Multiquant and others to be defined
preprocess.datafile <- function (data.file, skyline.export=TRUE, light.label="light.Area", heavy.label="heavy.Area", 
                                 conc.present='NULL', is.present=TRUE, use.par=FALSE)
{

	conc.columns = NULL
	final.conc.columns = NULL
	
	if(conc.present == 'NULL'){
		conc.columns = c('SampleGroup', 'Concentration', 'IS.Spike')
		final.conc.columns = c('sample', 'concentration', 'is.conc')
	} 
	
  # check if the data file is a csv file by looking for a comma
  line <- scan(data.file, what="character", nlines=1, quiet=TRUE)
  if (length (grep(",", line)) == 0) {
    stop("The data file must be a comma separated CSV file.")
  }

  # read the data file
  d <- read.csv (data.file, na.strings=c('NA', '#N/A', 'N/A'))

  # As long as these 10 columns with these header names are in the input file, the module will work
  # Check that the correct columns are present in the data file and set to internal column names
	if (skyline.export) {
	  input.columns <- c('SampleName', 'PeptideSequence', 'ProteinName', 'FragmentIon', light.label, heavy.label, 
	                     'AverageMeasuredRetentionTime', 'ReplicateName', 'PrecursorCharge', 'ProductCharge',
	                     conc.columns)
	  
	  if (! all (input.columns %in% colnames (d)) ) {
	    stop (paste ("Missing required columns in dataset:\n",
	                 paste ( input.columns [ which (! input.columns %in% colnames (d)) ], collapse=',')))
	  }
	  
	  # when there is no standard set heavy area and IS.spike values to 1
	  if (!is.present) {
	    d[, heavy.label] <- NULL
	    d <- cbind (d, rep(1,nrow(d)))
	    colnames (d)[ncol(d)] <- heavy.label
	    d[,'IS.Spike'] <- NULL	
	    d <- cbind(d, IS.Spike = rep(1, nrow(d)))
	  }
    
    # when use.par is TRUE, ignore IS.spike values and set it to 1 -- all calcs then use PAR
    # (instead of concentration)
    if (use.par) {
      d[,'IS.Spike'] <- NULL	
      d <- cbind(d, IS.Spike = rep(1, nrow(d)))
    }
	  
	  # if "do not use" for light or heavy is used then remove rows with these set to TRUE
	  do.not.use.columns <- c('light.do.not.use', 'heavy.do.not.use')
	  do.not.use.columns.1 <- c('light.do.not.use.1', 'heavy.do.not.use.1')
	  if (all (do.not.use.columns %in% colnames (d)) ) {
	    dmod <- d [ (d[,'light.do.not.use']=="False" | d[,'light.do.not.use']=="FALSE") & (d[,'heavy.do.not.use']=="False" | d[,'heavy.do.not.use']=="FALSE"),]
	    if (nrow(dmod) > 1) d <- dmod
	  }
	
	  if (all (do.not.use.columns.1 %in% colnames (d)) ) {
	    dmod <- d [ (d[,'light.do.not.use.1']=="False" | d[,'light.do.not.use.1']=="FALSE") & (d[,'heavy.do.not.use.1']=="False" | d[,'heavy.do.not.use.1']=="FALSE"),]
	    if (nrow(dmod) > 1) d <- dmod
	  }
	  
	  # the columns are present, set column names to internal column names used throught out the module
	  # for Skyline format this are the required column
	  skyline.required.columns <- c('SampleName', 'PeptideModifiedSequence', 'ProteinName', 'FragmentIon', light.label, heavy.label, 
	                                'AverageMeasuredRetentionTime', 'ReplicateName', 'PrecursorCharge', 'ProductCharge',
	                                conc.columns)
	  d <- d [, skyline.required.columns]
	  internal.column.names <- c('sample.name', 'peptide', 'protein', 'transition.id', 'area', 'IS.area', 'RT', 'replicate', 'precursor.charge', 'product.charge', 
	                             final.conc.columns)
	  
	  # set the internal column names
	  colnames (d) <- internal.column.names
	}
	
	return(d)
} # END function preprocess.datafile


# Check format of the concentation file and preprocess data 
# Only acceptable format at this point is a predefined format consisting of four columns with these exact column names
#   1) "SampleName", 2) "SampleGroup", 3) "Concentration", and 4) "IS Spike"
preprocess.concfile <- function (conc.file, is.present = TRUE)
{
	
  if (conc.file != 'NULL'){
    # check if the concentration file is a csv file by looking for a comma
    line <- scan(conc.file, what="character", nlines=1, quiet=TRUE)
    if (length (grep(",", line)) == 0) {
      stop("The concentration file must be a comma separated CSV file.")
    }
    
    # read the concentration file
    d <- read.csv (conc.file, na.strings=c('NA', '#N/A', 'N/A'))
    
    # As long as columns with the specified column names are in the concentration input file, the module will work
    # Check that the correct columns are present in the data file and set to internal column names
    input.columns <- c('SampleName', 'SampleGroup', 'Concentration', 'IS.Spike')
    if (! all (input.columns %in% colnames (d)) ){
      stop (paste ("Missing required columns in concentration file:\n",
                   paste ( input.columns [ which (! input.columns %in% colnames (d)) ], collapse=',')))
    }
    
    # when there is no standard create IS Conc column with value 1
    if (!is.present) {
      d[,'IS.Spike'] <- NULL	
      d <- cbind(d, IS.Spike = rep(1, nrow(d)))
    }
    
    # set the internal column names
    d <- d [, input.columns]
    internal.conc.column.names=c('sample.name', 'sample', 'concentration', 'is.conc')
    colnames (d) <- internal.conc.column.names
    
    return(d)
  } else return ('NULL')
}
# END function preprocess.concfile

# END QuaSAR.R=======
