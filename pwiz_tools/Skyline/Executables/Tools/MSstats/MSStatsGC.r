runComparison <- function() {

	options(warn = -1)

	cat("\n\n ================================================================")
	cat("\n ** Loading the required statistical software packages in R ..... \n \n")

	## load the library
	library(MSstats)

	## save sessionInfo as .txt file
	session <- sessionInfo()
	sink("sessionInfo.txt")
	print(session)
	sink()

	## Input data
	arguments <- commandArgs(trailingOnly=TRUE);
	#cat(arguments)

	cat("\n\n =======================================")
	cat("\n ** Reading the data for MSstats..... \n")

	raw <- read.csv(arguments[1])

	colnames(raw)[colnames(raw) == 'Detection.Q.Value'] <- 'DetectionQValue'
	if(!is.element(c('DetectionQValue'), colnames(raw)) || all(is.na(as.numeric(as.character(raw$DetectionQValue))))) {
	    filter.qvalue <- FALSE
	} else {
	    filter.qvalue <- TRUE
	}
	
	## remove the rows for iRT peptides
	raw <- SkylinetoMSstatsFormat(raw,
	                              removeProtein_with1Feature = TRUE,
	                              filter_with_Qvalue = filter.qvalue)
	
	## get standard protein name from StandardType column
	standardpepname <- ""
	if(sum(unique(raw$StandardType) %in% c("Normalization", "Global Standard")) !=0 ){
	    standardpepname <- as.character(unique(raw[raw$StandardType %in% c("Normalization", "Global Standard"), "PeptideSequence"]))
	}

	## check result grid missing or not
	countna<-apply(raw, 2, function(x) sum(is.na(x) | x == ""))
	naname<-names(countna[countna != 0])
	naname<-naname[-which(naname %in% c("StandardType", "Intensity", "Truncated",
	                                    "FragmentIon", "ProductCharge"))]

	if(length(naname) != 0){
		stop(message(paste("Some ", paste(naname, collapse=", "), " have no value. Please check \"Result Grid\" in View. \n", sep="")))
	}
	
	## -----------------------------------------------
	## Function: dataProcess
	## pre-processing data: quality control of MS runs
	## -----------------------------------------------

	cat("\n\n =======================================")
	cat("\n ** Data Processing for analysis..... \n")

	## get length of command line argument array
	len <- length(arguments)

	optionnormalize <- arguments[3]

	## input is character??
	if (optionnormalize == 0) { 
		inputnormalize <- FALSE 
	}
	if (optionnormalize == 1) { 
		inputnormalize <- "equalizeMedians" 
	}
	if (optionnormalize == 2) { 
		inputnormalize <- "quantile" 
	}
	if (optionnormalize == 3) { 
		inputnormalize <- "globalStandards" 
	}
	if (optionnormalize != 0 & optionnormalize != 1 & optionnormalize != 2 & optionnormalize != 3){ 
		inputnormalize <- FALSE
	}

	## missing peaks cbox
	inputmissingpeaks <- arguments[4]


	## feature selection input
	
	optionfeatureselection <- arguments[5]
    lastFixedArgument <- 6
	
	if (optionfeatureselection == "TRUE") { 
		input_feature_selection <- "highQuality" 
	} else { 
		input_feature_selection <- "all" 
	}

	## Nick, here for new option for 'allow...'
	## remove proteins with interference cbox
	
	inputremoveproteins <- arguments[6]
		

	quantData <- try(dataProcess(raw, 
	                             normalization=inputnormalize, 
	                             nameStandards=standardpepname, 
	                             fillIncompleteRows=(inputmissingpeaks=="TRUE"), 
	                             featureSubset=input_feature_selection, 
	                             remove_noninformative_feature_outlier=(inputremoveproteins=="TRUE"), 
	                             summaryMethod = "TMP", 
	                             censoredInt="0"))

	if (class(quantData) != "try-error") {

		allfiles <- list.files()
		num <- 0
		filenaming <- "dataProcessedData"
		finalfile <- "dataProcessedData.csv"

		while (is.element(finalfile, allfiles)) {
			num <- num + 1
			finalfile <- paste(paste(filenaming, num, sep="-"), ".csv", sep="")
		}

		write.csv(quantData$ProcessedData, file=finalfile)
		cat("\n Saved dataProcessedData.csv \n")
	} else {
		stop(message("\n Error : Can't process the data. \n"))
	}

	## --------------------------------------------------------------------
	## Function: groupComparison
	## generate testing results of protein inferences across concentrations
	## --------------------------------------------------------------------

	## first, number of comparisons
	numcomparison <- 1

	## then, what comparisons?
	alllevel <- levels(quantData$ProcessedData$GROUP_ORIGINAL)

	comparison <- NULL

	for (k in 1:numcomparison) {

		temp <- NULL

		for (i in 1:length(alllevel)) {
			inputtemp <- arguments[i + lastFixedArgument]
			temp <- c(temp, as.numeric(inputtemp))
		}
		
		comparison <- rbind(comparison, matrix(temp, nrow=1))

		#inputrowname <- arguments[2]
		
		namepos <- alllevel[temp > 0]
		
		if(length(namepos) != 1){
			
			namepos <- paste(temp[temp >0][1], "(", paste(namepos, collapse = "+"), ")", sep="")
		
		}
		
		
		nameneg <- alllevel[temp < 0]
		
		if(length(namepos) != 1){
			
			nameneg <- paste(temp[temp >0][1], "(", paste(nameneg, collapse = "+"), ")", sep="")
		
		}

		
		inputrowname <- paste(namepos, nameneg, sep='-')

		row.names(comparison)[k] <- inputrowname 
	}


	## then testing with inputs from users
	cat("\n\n ============================")
	cat("\n ** Starting comparison... \n \n")

	## here is the issues : labeled, and interference need to be binary, not character
	resultComparison <- try(groupComparison(contrast.matrix=comparison, data=quantData))

	if (class(resultComparison) != "try-error") {

		allfiles <- list.files()
		num <- 0
		filenaming <- "TestingResult"
		finalfile <- "TestingResult.csv"

		while (is.element(finalfile,allfiles)) {
			num <- num + 1
			finalfile <- paste(paste(filenaming, num, sep="-"), ".csv", sep="")
		}

		write.csv(resultComparison$ComparisonResult, file=finalfile)
		cat("\n Saved the testing result. \n")
	} else {
		cat("\n Error : Can't compare the groups. \n")
	}

	## ---------------------------------
	## Function: groupComparisonPlots
	## visualization for testing results
	## ---------------------------------

	if (class(resultComparison) != "try-error") {

		cat("\n\n =======================================")
		cat("\n ** Visualizing testing results..... \n")

		## Visualization 1: Volcano plot
		## default setup: FDR cutoff = 0.05; fold change cutoff = NA
		groupComparisonPlots(data=resultComparison$ComparisonResult, type="VolcanoPlot", address="")
		cat("\n Saved VolcanoPlot.pdf \n")

		## Visualization 2: Heatmap (required more than one comparisons)
 		if (length(unique(resultComparison$ComparisonResult$Label)) > 1) {
   			groupComparisonPlots(data=resultComparison$ComparisonResult, type="Heatmap", address="")
   			cat("\n Saved Heatmap.pdf \n")
 		} else {
   			cat("\n No Heatmap. Need more than 1 comparison for Heatmap. \n")
 		}

		## Visualization 3: Comparison plot
		groupComparisonPlots(data=resultComparison$ComparisonResult, type="ComparisonPlot", address="")
		cat("\n Saved ComparisonPlot.pdf \n \n")
	} ## draw only for comparison working
}

temp <- try(runComparison())

if (class(temp) != "try-error") {
	cat("\n Finished.")
} else {
	cat("\n Can't finish analysis.")
}


