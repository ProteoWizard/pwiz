runDSS<-function() {

options(warn=-1)

cat("\n\n ================================================================")
cat("\n ** Loading the required statistical software packages in R ..... \n \n")


# load the library
library(MSstats)

## save sessionInfo as .txt file
session<-sessionInfo()
sink("sessionInfo.txt")
print(session)
sink()

# Input data
arguments<-commandArgs(trailingOnly=TRUE);

### test argument
#cat("arguments--> ")
#cat(arguments)
#cat("\n")

### put argument
#arguments<-c("address", "TRUE", "1", "1", "0.80", "0.05", "1.25", "1.75")

cat("\n\n =======================================")
cat("\n ** Reading MSstat reports..... \n")

raw<-read.csv(arguments[1])

# remove the rows for iRT peptides
raw<-raw[is.na(raw$StandardType) | raw$StandardType!="iRT",]

# get standard protein name from StandardType column
standardproname<-NULL
if(sum(unique(raw$StandardType) %in% "Normalization")!=0){
	standardproname<-as.character(unique(raw[raw$StandardType=="Normalization","ProteinName"]))
}

# change column name as Intensity
colnames(raw)[colnames(raw)=="Area"]<-"Intensity"
raw$Intensity<-as.character(raw$Intensity)
raw$Intensity<-as.numeric(raw$Intensity)

## impute zero to NA
raw[!is.na(raw$Intensity)&raw$Intensity==0,"Intensity"]<-NA

## check result grid missing or not
countna<-apply(raw,2, function(x) sum(is.na(x) | x==""))
naname<-names(countna[countna!=0])
naname<-naname[-which(naname %in% c("StandardType","Intensity"))]

if(length(naname)!=0){
	stop(message(paste("Some ",paste(naname,collapse=", ")," have no value. Please check \"Result Grid\" in View.",sep="")))
}


#=====================
# Function: dataProcess
# pre-processing data: quality control of MS runs

cat("\n\n =======================================")
cat("\n ** Data Processing for analysis..... \n")

optionnormalize<-arguments[2]

## first check name of global standard
#if(optionnormalize==3 & is.null(standardproname)){
#	stop(message("Please assign the global standards peptides for normalization using standard proteins."))
#}

## input is character??
if(optionnormalize==0){ inputnormalize<-FALSE }
if(optionnormalize==1){ inputnormalize<-"constant" }
if(optionnormalize==2){ inputnormalize<-"quantile" }
if(optionnormalize==3){ inputnormalize<-"globalStandards" }
if(optionnormalize!=0 & optionnormalize!=1 & optionnormalize!=2 & optionnormalize!=3){ inputnormalize<-FALSE }

quantData<-try(dataProcess(raw, normalization=inputnormalize, nameStandards=standardproname))

if(class(quantData)!="try-error"){
	write.csv(quantData,file="dataProcessedData.csv")
	cat("\n Saved dataProcessedData.csv \n")
}
#else{
#	stop(message("\n Error : Can't process the data. \n"))
#}

#=====================
# Function: designSampleSize
# calulate number of biological replicates per group for your next experiment

cat("\n\n =======================================")
cat("\n ** Calculating sample size..... \n")

## input for options
# num of sample?
# cat("\n Number of Biological Replicates :  ")
inputnsample<-arguments[3]
#inputnsample<-readline()
if(inputnsample=="TRUE") inputnsample<-TRUE
if(inputnsample!="TRUE") inputnsample<-as.numeric(inputnsample)
if(inputnsample=="") inputnsample<-TRUE

# num of peptide?
# cat("\n Number of Peptides per protein :  ")
inputnpep<-arguments[4]
#inputnpep<-readline()
if(inputnpep=="TRUE") inputnpep<-TRUE
if(inputnpep!="TRUE") inputnpep<-as.numeric(inputnpep)
if(inputnpep=="") inputnpep<-TRUE

# num of peptide?
# cat("\n Number of Transitions per peptides :  ")
inputntran<-arguments[5]
#inputntran<-readline()
if(inputntran=="TRUE") inputntran<-TRUE
if(inputntran!="TRUE") inputntran<-as.numeric(inputntran)
if(inputntran=="") inputntran<-TRUE

# power?
# cat("\n power : ")
inputpower<-arguments[6]
#inputpower<-readline()
if(inputpower=="TRUE") inputpower<-TRUE
if(inputpower!="TRUE") inputpower<-as.numeric(inputpower)
if(inputpower=="") inputpower<-TRUE

# FDR?
# cat("\n FDR : (Default is 0.05)")
inputfdr<-arguments[7]
#inputfdr<-readline()
if(inputfdr=="TRUE") inputfdr<-0.05
inputfdr<-as.numeric(inputfdr)

# desiredFC?
# cat("\n lower desired Fold Change : (Default is 1.25) ")
inputlower<-arguments[8]
#inputlower<-readline()
if(inputlower=="TRUE") inputlower<-1.25
inputlower<-as.numeric(inputlower)

#cat("\n upper desired Fold Change : (Default is 1.75)")
inputupper<-arguments[9]
#inputupper<-readline()
if(inputupper=="TRUE") inputupper<-1.75
inputupper<-as.numeric(inputupper)


result.sample<-try(designSampleSize(data=quantData,numSample=inputnsample,numPep=inputnpep,numTran=inputntran,desiredFC=c(inputlower,inputupper),FDR=inputfdr,power=inputpower))
#result.sample<-designSampleSize(data=quantData,numSample=TRUE,numPep=2,numTran=3,desiredFC=c(1.25,1.75),FDR=0.05,power=0.8)

if(class(result.sample)!="try-error"){
	write.csv(result.sample,"SampleSizeCalculation.csv")
	cat("\n Saved the Sample Size Calculation. \n")
}
#else{
#	stop(message("\n Error : Can't analyze. \n"))
#}

#=====================
# Function: designSampleSizePlots
# visualization for sample size calculation

if(class(result.sample)!="try-error"){
	pdf("SampleSizePlot.pdf")
	designSampleSizePlots(data=result.sample)
	dev.off()
	cat("\n Saved SampleSizePlot.pdf \n \n")
}

}



temp<-try(runDSS())


if(class(temp)!="try-error"){
	cat("\n Finished.")
}else{
	cat("\n Can't finish analysis.")
}


#=====================
#tryCatch({runDSS()}, 
#	error=function(err){
#		temp<-grep("dataProcess", err)
#		if(length(temp)==1){
#			print("Error : Can't process the data.")
#		}
#
#		temp<-grep("designSampleSize", err)
#		if(length(temp)==1){
#			print("Error : Can't calculate the sample size.")
#		}
#
#		temp<-grep("designSampleSizePlots", err)
#		if(length(temp)==1){
#			print("Error : Can't generate plots.")
#		}
#	}, finally = {
#		cat("\n Finished.")
#	}
#)