runQC <- function() {

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
	#arguments<-c("C:/Users/Ijae/AppData/Local/Temp/MSstats_QC_MSstats_Input.csv","1")

	## test argument
	#cat("arguments--> ")
	#cat(arguments)
	#cat("\n")

	cat("\n\n =======================================")
	cat("\n ** Reading the data for MSstats..... \n")

	raw <- read.csv(arguments[1])

	## remove the rows for iRT peptides
	raw <- raw[is.na(raw$StandardType) | raw$StandardType != "iRT", ]

	## get standard protein name from StandardType column
	standardpepname <- ""
	if(sum(unique(raw$StandardType) %in% "Normalization") !=0 ){
		standardpepname <- as.character(unique(raw[raw$StandardType == "Normalization", "PeptideModifiedSequence"]))
	}

	## change column name as Intensity
	colnames(raw)[colnames(raw) == "Area"] <- "Intensity"
	raw$Intensity <- as.character(raw$Intensity)
	raw$Intensity <- as.numeric(raw$Intensity)

	## change column name 'FileName' as Run
	colnames(raw)[colnames(raw) == "FileName"] <- "Run"

	## check result grid missing or not
	countna<-apply(raw, 2, function(x) sum(is.na(x) | x == ""))
	naname<-names(countna[countna != 0])
	naname<-naname[-which(naname %in% c("StandardType", "Intensity", "Truncated"))]

	if(length(naname) != 0){
		stop(message(paste("Some ", paste(naname, collapse=", "), " have no value. Please check \"Result Grid\" in View. \n", sep="")))
	}


	## -----------------------------------------------
	## Function: dataProcess
	## pre-processing data: quality control of MS runs
	## -----------------------------------------------

	cat("\n\n =======================================")
	cat("\n ** Data Processing for analysis..... \n")

	optionnormalize <- arguments[2]

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
	
	inputmissingpeaks <- arguments[3]
	
		
	## feature selection input
	
	
	optionfeatureselection <- arguments[4]
	
	
	if (optionfeatureselection == "TRUE") { 
		input_feature_selection <- "highQuality" 
	} else { 
		input_feature_selection <- "all" 
	}

	## Nick, here for new option for 'allow...'
	## remove proteins with interference cbox
	
	inputremoveproteins <- arguments[5]


	quantData <- try(dataProcess(raw, normalization=inputnormalize, nameStandards=standardpepname,  fillIncompleteRows=(inputmissingpeaks=="TRUE"), featureSubset=input_feature_selection, remove_proteins_with_interference=(inputremoveproteins=="TRUE"), summaryMethod = "TMP", censoredInt="0", skylineReport=TRUE))

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

	## --------------------------
	## Function: dataProcessPlots
	## visualization 
	## --------------------------

	if (class(quantData) != "try-error") {
		cat("\n\n =======================================")
		cat("\n ** Generating dataProcess Plots..... \n \n")

		dataProcessPlots(data=quantData, type="ProfilePlot", address="", width=as.numeric(arguments[6]), height=as.numeric(arguments[7]))
		cat("\n Saved ProfilePlot.pdf \n \n")

		dataProcessPlots(data=quantData, type="QCPlot", address="", width=as.numeric(arguments[6]), height=as.numeric(arguments[7]))
		cat("\n Saved QCPlot.pdf \n \n")

		dataProcessPlots(data=quantData, type="ConditionPlot", address="")
		cat("\n Saved ConditionPlot.pdf \n ")
	}
}

temp <- try(runQC())


if (class(temp) != "try-error") {
	cat("\n Finished.")
} else {
	cat("\n Can't finish analysis.")
}

