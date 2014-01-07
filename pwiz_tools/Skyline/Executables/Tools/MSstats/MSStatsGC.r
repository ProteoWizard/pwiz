#### loading package
runComparison<-function() {

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
#cat(length(arguments))
#cat("\n")

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
naname<-naname[-which(naname %in% c("PrecursorCharge","FragmentIon","ProductCharge","StandardType","Intensity"))]

if(length(naname)!=0){
	stop(message(paste("Some ",paste(naname,collapse=", ")," have no value. Please check \"Result Grid\" in View.",sep="")))
}


#=====================
# Function: dataProcess
# pre-processing data: quality control of MS runs

cat("\n\n =======================================")
cat("\n ** Data Processing for analysis..... \n")


## get length of command line argument array
len<-length(arguments)

optionnormalize<-arguments[len-5]

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
# Function: groupComparison
# generate testing results of protein inferences across concentrations

## cat("\n\n =======================================")
## at("\n ** Let's compare Groups..... \n")


## input from groupComparison
# comparison matrix

## first, number of comparisons
numcomparison<-1

## then, what comparisons?

alllevel<-levels(quantData$GROUP_ORIGINAL)

comparison<-NULL

for(k in 1:numcomparison){

	temp<-NULL

	for(i in 1:length(alllevel)){
		inputtemp<-arguments[i+1]
		temp<-c(temp,as.numeric(inputtemp))
	}

	comparison<-rbind(comparison, matrix(temp,nrow=1))

	inputrowname<-arguments[1+length(alllevel)+1]

	row.names(comparison)[k]<-inputrowname 
}



## input for options
## cat("\n\n ** Enter your choices for model..... \n")

# labeled or label-free?
inputlabel<-arguments[len-4] ## count from the end, because length of group level can be different.


### about input, is it possible neither true nor false? 12022013 meena
####if(inputlabel=="" | inputlabel=="T") inputlabel<-"TRUE"  : 12022013 meena
####if(inputlabel=="F") inputlabel<-"FALSE" : 12022013 meena
if(inputlabel!="TRUE" & inputlabel!="FALSE"){
	## cat("\n Wrong input. will use the default=TRUE.\n")
	inputlabel<-"TRUE"
}

##Equal Variance Check Box
equalvariance<-arguments[len-3] ## count from the end, because length of group level can be different.

if(equalvariance!="TRUE" & equalvariance!="FALSE"){
	equalvariance<-"TRUE"
}



# scope of biological replicate?
inputbio<-arguments[len-2]

if(inputbio=="" | inputbio=="R") inputbio<-"restricted"
if(inputbio=="E") inputbio<-"expanded"
if(inputbio!="expanded" & inputbio!="restricted"){
	##  cat("\n Wrong input. will use the default=restricted. \n")
	inputbio<-"restricted" 
}


# scope of technical replicate?

inputtech<-arguments[len-1]

if(inputtech=="" | inputtech=="E") inputtech<-"expanded"
if(inputtech=="R") inputtech<-"restricted"
if(inputtech!="expanded" & inputtech!="restricted"){
	## cat("\n Wrong input. will use the default=expanded. \n")
	inputtech<-"expanded" 
}

# interference or not?
inputinfer<-arguments[10]

if(inputinfer=="" | inputinfer=="T") inputinfer<-"TRUE"
if(inputinfer=="F") inputinfer<-"FALSE"
if(inputinfer!="TRUE" & inputinfer!="FALSE"){
	## cat("\n Wrong input. will use the default=TRUE.\n")
	inputinfer<-"TRUE"
}


## then testing with inputs from users
cat("\n\n ============================")
cat("\n ** Starting comparison... \n \n")

## here is the issues : labeled, and interference need to be binary, not character
resultComparison<-try(groupComparison(contrast.matrix=comparison,data=quantData,labeled=inputlabel=="TRUE",scopeOfBioReplication=inputbio, scopeOfTechReplication=inputtech, interference=inputinfer=="TRUE", featureVar=equalvariance!="TRUE"))

if(class(resultComparison)!="try-error"){
	write.csv(resultComparison$ComparisonResult,"TestingResult.csv")
	cat("\n Saved the testing result. \n")
}
#else{
#	cat("\n Error : Can't compare the groups. \n")
#}

#=====================
# Function: groupComparisonPlots
# visualization for testing results

if(class(resultComparison)!="try-error"){

cat("\n\n =======================================")
cat("\n ** Visualizing testing results..... \n")


# Visualization 1: Volcano plot
# default setup: FDR cutoff = 0.05; fold change cutoff = NA
groupComparisonPlots(data=resultComparison$ComparisonResult,type="VolcanoPlot",address="")
cat("\n Saved VolcanoPlot.pdf \n")

# Visualization 2: Heatmap (required more than one comparisons)
 if(length(unique(resultComparison$ComparisonResult$Label))>1){
   groupComparisonPlots(data=resultComparison$ComparisonResult,type="Heatmap",address="")
   cat("\n Saved Heatmap.pdf \n")
 } else{
   cat("\n No Heatmap. Need more than 1 comparison for Heatmap. \n")
 }

# Visualization 3: Comparison plot
groupComparisonPlots(data=resultComparison$ComparisonResult,type="ComparisonPlot",address="")
cat("\n Saved ComparisonPlot.pdf \n \n")
} ## draw only for comparison working

}

temp<-try(runComparison())

if(class(temp)!="try-error"){
	cat("\n Finished.")
}else{
	cat("\n Can't finish analysis.")
}


#tryCatch({runComparison()}, 
#	error=function(err){
#		temp<-grep("dataProcess", err)
#		if(length(temp)==1){
#			print("Error : Can't process the data.")
#		}
#
#		temp<-grep("comparison", err)
#		if(length(temp)==1){
#			print("Error : Can't compare the groups.")
#		}
#
#		temp<-grep("groupComparisonPlots", err)
#		if(length(temp)==1){
#			print("Error : Can't generate plots with testing result.")
#		}
#	}, finally = {
#		cat("\n Finished.")
#	}
#)