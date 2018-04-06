#
# $Id$
#
# Licensed under the Apache License, Version 2.0 (the "License"); 
# you may not use this file except in compliance with the License. 
# You may obtain a copy of the License at 
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software 
# distributed under the License is distributed on an "AS IS" BASIS, 
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
# See the License for the specific language governing permissions and 
# limitations under the License.
#
# The Original Code is the IDPicker project.
#
# The Initial Developer of the Original Code is Yaoyi Chen.
#
# Copyright 2014 Vanderbilt University
#
# Contributor(s): Jay Holman
#

userLibPath=paste(Sys.getenv("LOCALAPPDATA"),"idpQuantify",sep="\\")
dir.create(userLibPath)
print (userLibPath)
.libPaths(userLibPath)
if (!require(missMDA)) {install.packages('missMDA', repos="http://cran.us.r-project.org"); require(missMDA)}
if (!require(MASS)) {install.packages('MASS', repos="http://cran.us.r-project.org"); require(MASS)}

cmd_args = commandArgs();
#args 7,7+n ..... name of samples
setwd(cmd_args[6])
print (cmd_args)
data=read.table("peptideScanTime.tsv", header=TRUE,sep="\t")
mztolLower=cmd_args[length(cmd_args)-1]
mztolLower=as.numeric(substr(mztolLower, 1, nchar(mztolLower)-3))*10^(-6)

mztolUpper=cmd_args[length(cmd_args)]
mztolUpper=as.numeric(substr(mztolUpper, 1, nchar(mztolUpper)-3))*10^(-6)
timetol=0.1

##if only one sample
if (length(data)==5){
newdata=data
colnames(newdata)[2]="scantime"
out=paste(cmd_args[7],"-peptideScantimeRegression.tsv",sep="")
write.table(newdata,file=out, quote = FALSE,sep="\t",row.names =F)
}


##if more than two samples
if(length(data)>6){
#mark 1 when data !=0 
data=replace(data,data==0,NA)

library(missMDA)

res.comp<-imputePCA(data[2:(length(data)-3)],ncp=1)
##res.pca<-PCA(res.comp$completeObs)
reg_data<-data.frame(res.comp$completeObs)


for(i in 2:(length(data)-3)){
out=paste(cmd_args[(i+5)],"-peptideScantimeRegression.tsv",sep="")
##only output the missing data the existing peptides.
mark=data.matrix(data[i])
newdata=data.frame(distinctMatch=data[1][is.na(mark)],scantime=reg_data[i-1][is.na(mark)],charge=data[(length(data)-2)][is.na(mark)],precursorMZ=data[(length(data)-1)][is.na(mark)],peptide=data[(length(data))][is.na(mark)])
##
write.table(newdata,file=out, quote = FALSE,sep="\t",row.names =F)
}
#write.table(data.frame(data[1],reg_data,data[(length(data)-2):length(data)]),file="peptideScantimeRegression_full.tsv", quote = FALSE,sep="\t",row.names =F)

}


#if only two samples
if (length(data)==6){
pep_inter=subset(data,scantime1!=0&scantime2!=0)
only1=subset(data,scantime2==0)
only2=subset(data,scantime1==0)


##with scantime1 na
m1=lm(scantime1~scantime2,data=pep_inter)
##these are peptides absent from rep 1 , predicted for rep1
data1=data.frame(distinctMatch=only2$distinctMatch,scantime=round(predict(m1,only2,interval="none"),digits=4),charge=only2[4],precursorMZ=only2[5],peptide=only2[6])

#with scantime2 na
m2=lm(scantime2~scantime1,data=pep_inter)
#these are peptides absent from rep 2 , predicted for rep2
data2=data.frame(distinctMatch=only1$distinctMatch, scantime=round(predict(m2,only1,interval="none"),digits=4),charge=only1[4],precursorMZ=only1[5],peptide=only1[6])

out1=paste(cmd_args[7],"-peptideScantimeRegression.tsv",sep="")
out2=paste(cmd_args[8],"-peptideScantimeRegression.tsv",sep="")
write.table(data1,file=out1,quote = FALSE,row.names=FALSE,sep="\t")
write.table(data2,file=out2,quote = FALSE,row.names=FALSE,sep="\t")
data1_1=data.frame(distinctMatch=only2$distinctMatch,scantime1=round(predict(m1,only2,interval="none"),digits=4),scantime2=only2$scantime2,charge=only2[4],precursorMZ=only2[5],peptide=only2[6])
data2_1=data.frame(distinctMatch=only1$distinctMatch, scantime1=only1$scantime1,scantime2=round(predict(m2,only1,interval="none"),digits=4),charge=only1[4],precursorMZ=only1[5],peptide=only1[6])
data_t=rbind(pep_inter,data1_1,data2_1)


#write.table(data_t,file="peptideScantimeRegression_full.tsv", quote = FALSE,sep="\t",row.names =F)

}





