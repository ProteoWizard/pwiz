# Produces Shewhart Control Charts and Pareto Analysis for Evaulating Proteomic Experiments
# for Rv2.13 or later must be installed along with the qcc library from R and Skyline
# (https://skyline.gs.washington.edu/labkey/project/home/software/Skyline/begin.view)


options (echo = FALSE)
debug <- FALSE

# Retention Time Reproducibility, Peak Asymmetry (a+b/2a), fwhm, and Peak Areas are monitored for 2-9 selected peptides and at least 3 QC replicates



##
## Command line processing and other functions to support GenePattern
##

parse.cmdline <- function () {

  # set up for command line processing (if needed)
  # arguments are specified positionally (since there are no optional arguments) and ...
  arguments <- commandArgs(trailingOnly=TRUE)
  if ( length (arguments) != 6)
    # expected arguments not present -- error
    stop ("USAGE: R --slave --no-save --args '<number> <highres.ms> <save.meta> <mma.value>' < QCplotsRgui2.R\n") #<libdir>
    
  for (z in 1:6) {
    arg <- arguments [z]
    # remove leading and trailing blanks
    arg <- gsub ("^ *", "", arg)
    arg <- gsub (" *$", "", arg)
    # remove any embedded quotation marks
    arg <- gsub ("['\'\"]", "", arg)
    if (z==1) filename <<- arg
    if (z==2) Q1 <<- as.numeric (arg)
    if (z==3) Q6 <<- as.numeric (arg)
    if (z==4) Q4 <<- as.numeric (arg)
    if (z==5) Q8 <<- as.numeric (arg)  
    if (z==6) savePath <<- arg
        
  }


windows(15,15)
##filename<-commandArgs(trailingOnly = TRUE)[1];

mydata=read.table(filename,sep=",",header=TRUE,na.strings=c("","0","0"),fill=TRUE); #converts empty spaces to NA
mydata=read.table(filename,sep=",",header=TRUE,na.strings=c("#N/A","0","0"),fill=TRUE); #converts empty spaces to NA

PA=mydata[,3]                              # takes third column of skyline files PAs
RT=mydata[,4]          # takes 4 column RTs
pep=mydata[,2]           # takes 2 peptide names
QC=mydata[,1]          # takes QC replicate names
fwhm=mydata[,5]
minstart=mydata[,6]       #  takes start integration values for all peptides and runs
maxend=mydata[,7]         #  End integration values for all peptides
MA=mydata[,8]


#M_abs_dev=function(x)        #  Defines function to take the median absolute deviation center around the mean
#mad(x,center=mean(x))


L=length(unique(QC))        # Determines number of QC replicates RAN
L2=length(unique(pep))        # Determines Number of Peptides used in QC run
dim(PA)=c(L,L2)         # reshapes PA a peptide per column
dim(RT)=c(L,L2)         # reshapes RT a peptide per column
dim(pep)=c(L,L2)          # reshapes pep names per column
dim(fwhm)=c(L,L2)
dim(minstart)=c(L,L2)
dim(maxend)=c(L,L2)
dim(MA)=c(L,L2)
      
#library(rpanel)         #Error check for NA values
#options(warning.length=8170)      #max value for warning is 8170 characters
#rows_NA <- data.frame(mydata[unique(unlist(lapply(mydata, function(x) which (is.na(x)))))<- 0,1:2]) #find rows that contain NA values
#temp_var1<-capture.output(rows_NA)
#temp_var2<-paste(temp_var1, "\n", sep="")
#if(any(is.na(mydata))){rp.messagebox("NA values exist", title="ERROR MESSAGE"); stop("ERROR MESSAGE: NA values exist for the following replicates (up to 8170 chars):\n",(dQuote(temp_var2)))} # prints row number, replicate, and peptide

     

if((is.array(PA[1:Q1,]))==FALSE){   # single peptide
  PApm=mean(PA[1:Q1,])
  PAsd=sd(PA[1:Q1,])
} else {      
  PApm=colMeans(PA[1:Q1,])
  PAsd=apply(PA[1:Q1,],2,sd)
}
PApm.all=colMeans(PA)       # Calculates means and SD of Peak areas per peptide
paline=rep(PApm,each=L)
dim(paline)=c(L,L2)   
PAsd.all=apply(PA,2,sd)


if((is.array(RT[1:Q1,]))==FALSE){
  RTm=mean(RT[1:Q1,])
  RTsd=sd(RT[1:Q1,])
} else {      
  RTm=colMeans(RT[1:Q1,])
  RTsd=apply(RT[1:Q1,],2,sd)    
}             # Calculates means and SD of RT per peptide
RTm.all=colMeans(RT)
RTline=rep(RTm,each=L)
dim(RTline)=c(L,L2)
RTsd.all=apply(RT,2,sd)


if((is.array(fwhm[1:Q1,]))==FALSE){
  fwhmM=mean(fwhm[1:Q1,])
  fwhmsd=sd(fwhm[1:Q1,])
} else {      
  fwhmM=colMeans(fwhm[1:Q1,])
  fwhmsd=apply(fwhm[1:Q1,],2,sd)
}             # Calculates means and SD of FWHM per peptide
fwhmM.all=colMeans(fwhm)
fwhmline=rep(fwhmM,each=L)
dim(fwhmline)=c(L,L2)
fwhmsd.all=apply(fwhm,2,sd)



A1=RT-minstart          # Calculates means and SD of Peak symmetry
B1=maxend-RT
Psym=(A1+B1)/(2*A1)

if((is.array(Psym[1:Q1,]))==FALSE){
  PsymM=mean(Psym[1:Q1,])
  Psymsd=sd(Psym[1:Q1,])
} else {      
  PsymM=colMeans(Psym[1:Q1,],)
  Psymsd=apply(Psym[1:Q1,],2,sd)  
}

PsymM.all=colMeans(Psym)
PsymMline=rep(PsymM,each=L)
dim(PsymMline)=c(L,L2)
Psymsd.all=apply(Psym,2,sd)

pepCL1p=PApm+PAsd         #calculates +1 SD from PA mean
pepCL2p=PApm+2*PAsd       #+2 SD from PA mean
pepCL3p=PApm+3*PAsd
pepCL1m=PApm-PAsd         #-1 SD from PA mean
pepCL2m=PApm-2*PAsd       #-2 SD from PA mean
pepCL3m=PApm-3*PAsd

RTCL1p=RTm+RTsd         #same as above but for RT
RTCL2p=RTm+2*RTsd
RTCL3p=RTm+3*RTsd
RTCL1m=RTm-RTsd
RTCL2m=RTm-2*RTsd
RTCL3m=RTm-3*RTsd

fwhmCL1p=fwhmM+fwhmsd       #same as above but for FWHM
fwhmCL2p=fwhmM+2*fwhmsd
fwhmCL3p=fwhmM+3*fwhmsd
fwhmCL1m=fwhmM-fwhmsd
fwhmCL2m=fwhmM-2*fwhmsd
fwhmCL3m=fwhmM-3*fwhmsd

PsymCL1p=PsymM+Psymsd       #same as above but for Peak asymetry
PsymCL2p=PsymM+2*Psymsd
PsymCL3p=PsymM+3*Psymsd
PsymCL1m=PsymM-Psymsd
PsymCL2m=PsymM-2*Psymsd
PsymCL3m=PsymM-3*Psymsd

RT1p=rep(RTCL1p,each=L)       #making the confidence lines for RT
dim(RT1p)=c(L,L2)
RT2p=rep(RTCL2p,each=L)
dim(RT2p)=c(L,L2)
RT1m=rep(RTCL1m,each=L)
dim(RT1m)=c(L,L2)
RT2m=rep(RTCL2m,each=L)
dim(RT2m)=c(L,L2)
RT3p=rep(RTCL3p,each=L)
dim(RT3p)=c(L,L2)
RT3m=rep(RTCL3m,each=L)
dim(RT3m)=c(L,L2)

pep1p=rep(pepCL1p,each=L)     #making the confidence lines for peak areas
dim(pep1p)=c(L,L2)
pep2p=rep(pepCL2p,each=L)
dim(pep2p)=c(L,L2)
pep1m=rep(pepCL1m,each=L)
dim(pep1m)=c(L,L2)
pep2m=rep(pepCL2m,each=L)
dim(pep2m)=c(L,L2)
pep3p=rep(pepCL3p,each=L)
dim(pep3p)=c(L,L2)
pep3m=rep(pepCL3m,each=L)
dim(pep3m)=c(L,L2)

fwhm1p=rep(fwhmCL1p,each=L)     #making the confidence lines for fwhm
dim(fwhm1p)=c(L,L2)
fwhm2p=rep(fwhmCL2p,each=L)
dim(fwhm2p)=c(L,L2)
fwhm1m=rep(fwhmCL1m,each=L)
dim(fwhm1m)=c(L,L2)
fwhm2m=rep(fwhmCL2m,each=L)
dim(fwhm2m)=c(L,L2)
fwhm3p=rep(fwhmCL3p,each=L)
dim(fwhm3p)=c(L,L2)
fwhm3m=rep(fwhmCL3m,each=L)
dim(fwhm3m)=c(L,L2)



Psym1p=rep(PsymCL1p, each=L)      #making the confidence lines for Peak Asymetry
dim(Psym1p)=c(L,L2)
Psym2p=rep(PsymCL2p, each=L)
dim(Psym2p)=c(L,L2)
Psym1m=rep(PsymCL1m, each=L)
dim(Psym1m)=c(L,L2)
Psym2m=rep(PsymCL2m, each=L)
dim(Psym2m)=c(L,L2)
Psym3p=rep(PsymCL3p,each=L)
dim(Psym3p)=c(L,L2)
Psym3m=rep(PsymCL3m,each=L)
dim(Psym3m)=c(L,L2)

par(mfrow=c(4,L2))
QCnum=1:L
a=rep(0,L2)         #for making the loop Number of Peptides Used
b=rep(0,L2)
c=rep(0,L2)
d=rep(0,L2)
E=rep(0,L2)
F=rep(0,L2)
G=rep(0,L2)
H=rep(0,L2)
M=rep(0,L2)

for (i in 1:L2) {         #calculates PA RSDs
RSDpa=(PAsd.all[i]/PApm.all[i])*100
a[i]=RSDpa
}

for (j in 1:L2) {                   #calculates RT RSDS
RSDrt=(RTsd.all[j]/RTm.all[j])*100
b[j]=RSDrt
}

for (k in 1:L2) {
RSDfwhm=(fwhmsd.all[k]/fwhmM.all[k])*100
c[k]=RSDfwhm
}

for (n in 1:L2) {
RSDpsym=(Psymsd.all[n]/PsymM.all[k])*100
d[n]=RSDpsym
}



if(L2>=9){            # value set to 9 for screens that can display 8 adjacent plots
# working_directory <- getwd()
pdf(savePath)
#rp.messagebox("Check QC-plot.pdf file for charts in working directory: ", (dQuote(working_directory)))
}

for (i in 1:L2) {       #plots peak areas and CI lines

plot(QCnum,PA[,i],type='o',ylab="peak area",pch=22,lty=2,ylim=c(min(PA[,i]),max(PA[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(a[i],2))))   
title(ylab="peak area")
lines(QCnum,pep1m[,i],col="green")
lines(QCnum,pep1p[,i],col="green")
lines(QCnum,pep2m[,i],col="brown")
lines(QCnum,pep2p[,i],col="brown")
lines(QCnum,pep3p[,i],lwd=2.0,col="red")
lines(QCnum,pep3m[,i],lwd=2.0,col="red")
lines(QCnum,paline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(a[i],2))),bty="n")
}

for (i in 1:L2) {           #plots RT and CI lines               

plot(QCnum,RT[,i],type='o',ylab="RT (min)",pch=22,lty=2,ylim=c(min(RT[,i]),max(RT[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(b[i],2))))   
lines(QCnum,RT1m[,i],col="green")
lines(QCnum,RT1p[,i],col="green")
lines(QCnum,RT2p[,i],col="brown")
lines(QCnum,RT2m[,i],col="brown")
lines(QCnum,RT3p[,i],lwd=2.0,col="red")
lines(QCnum,RT3m[,i],lwd=2.0,col="red")
lines(QCnum,RTline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(b[i],2))),bty="n")
}

for (i in 1:L2) {       #plots fwhm and CI lines

plot(QCnum,fwhm[,i],type='o',ylab="fwhm (min)",pch=22,lty=2,ylim=c(min(fwhm[,i]),max(fwhm[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(c[i],2))))   
lines(QCnum,fwhm1m[,i],col="green")
lines(QCnum,fwhm1p[,i],col="green")
lines(QCnum,fwhm2p[,i],col="brown")
lines(QCnum,fwhm2m[,i],col="brown")
lines(QCnum,fwhm3p[,i],lwd=2.0,col="red")
lines(QCnum,fwhm3m[,i],lwd=2.0,col="red")
lines(QCnum,fwhmline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(c[i],2))),bty="n")

}

for (i in 1:L2) {       #plots Peak Symmetry and CI lines

plot(QCnum,Psym[,i],type='o',ylab="Peak Symmetry",pch=22,lty=2,ylim=c(min(Psym[,i]),max(Psym[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(d[i],2))))   
lines(QCnum,Psym1p[,i],col="green")
lines(QCnum,Psym1m[,i],col="green")
lines(QCnum,Psym2p[,i],col="brown")
lines(QCnum,Psym2m[,i],col="brown")
lines(QCnum,Psym3m[,i],lwd=2.0,col="red")
lines(QCnum,Psym3p[,i],lwd=2.0,col="red")
lines(QCnum,PsymMline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(c[i],2))),bty="n")

}

if(L2>=9){
dev.off(2)}


if (Q4==1) 
{
  pdf(savePath)
    for (i in 1:L2) {
plot(QCnum,PA[,i],type='o',ylab="peak area",pch=22,lty=2,ylim=c(min(PA[,i]),max(PA[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(a[i],2))))   
title(ylab="peak area")
lines(QCnum,pep1m[,i],col="green")
lines(QCnum,pep1p[,i],col="green")
lines(QCnum,pep2m[,i],col="brown")
lines(QCnum,pep2p[,i],col="brown")
lines(QCnum,pep3p[,i],lwd=2.0,col="red")
lines(QCnum,pep3m[,i],lwd=2.0,col="red")
lines(QCnum,paline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(a[i],2))),bty="n")
}
  for (i in 1:L2) {
plot(QCnum,RT[,i],type='o',ylab="RT (min)",pch=22,lty=2,ylim=c(min(RT[,i]),max(RT[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(b[i],2))))   
lines(QCnum,RT1m[,i],col="green")
lines(QCnum,RT1p[,i],col="green")
lines(QCnum,RT2p[,i],col="brown")
lines(QCnum,RT2m[,i],col="brown")
lines(QCnum,RT3p[,i],lwd=2.0,col="red")
lines(QCnum,RT3m[,i],lwd=2.0,col="red")
lines(QCnum,RTline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(b[i],2))),bty="n")
}
  for (i in 1:L2) {
plot(QCnum,fwhm[,i],type='o',ylab="fwhm (min)",pch=22,lty=2,ylim=c(min(fwhm[,i]),max(fwhm[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(c[i],2))))   
lines(QCnum,fwhm1m[,i],col="green")
lines(QCnum,fwhm1p[,i],col="green")
lines(QCnum,fwhm2p[,i],col="brown")
lines(QCnum,fwhm2m[,i],col="brown")
lines(QCnum,fwhm3p[,i],lwd=2.0,col="red")
lines(QCnum,fwhm3m[,i],lwd=2.0,col="red")
lines(QCnum,fwhmline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(c[i],2))),bty="n")
}
  for (i in 1:L2) {

plot(QCnum,Psym[,i],type='o',ylab="Peak Symmetry",pch=22,lty=2,ylim=c(min(Psym[,i]),max(Psym[,i])))
title(c(paste(main=pep[L*i], "\n", "CV%=", round(d[i],2))))   
lines(QCnum,Psym1p[,i],col="green")
lines(QCnum,Psym1m[,i],col="green")
lines(QCnum,Psym2p[,i],col="brown")
lines(QCnum,Psym2m[,i],col="brown")
lines(QCnum,Psym3m[,i],lwd=2.0,col="red")
lines(QCnum,Psym3p[,i],lwd=2.0,col="red")
lines(QCnum,PsymMline[,i],col="blue")
#legend("topright", legend = c(paste("CV=",round(c[i],2))),bty="n")
}
  dev.off()
    
}


for (i in 1:L2) {                          # Finding the number of NonConformers +/- 3s for PAs

NCpa=length(which(PA[,i]>pepCL3p[i]))
NCpa2=length(which(PA[,i]<pepCL3m[i]))
sum=NCpa+NCpa2
E[i]=sum

}


for (i in 1:L2) {        # Finding the number of NonConformers +/- 3s for RT

NCrt=length(which(RT[,i]>RTCL3p[i]))
NCrt2=length(which(RT[,i]<RTCL3m[i]))
sumrt=NCrt+NCrt2
F[i]=sumrt

}

for (i in 1:L2) {

NCfwhm=length(which(fwhm[,i]>fwhmCL3p[i]))  # Finding the number of NonConformers +/- 3s for fwhm
NCfwhm2=length(which(fwhm[,i]<fwhmCL3m[i]))
sumfwhm=NCfwhm+NCfwhm2
G[i]=sumfwhm

}

for (i in 1:L2) {

NCps=length(which(Psym[,i]>PsymCL3p[i]))    # Finding the number of NonConformers +/- 3s for Psym
NCps2=length(which(Psym[,i]<PsymCL3m[i]))
sumPS=NCps+NCps2
H[i]=sumPS

}

if (Q6==1)
{

for (i in 1:L2) {

MAhigh=length(which(MA[,i]> Q8))    # Finding the number of NonConformers +/- 3s for MMA User defined
MAlow=length(which(MA[,i]< -Q8))
MAtotal=MAhigh+MAlow
M[i]=MAtotal

}}


library(qcc)
dev.set(3)
windows(5,5)


if (Q6==1){
Exp=c(sum(E),sum(F),sum(G),sum(H),sum(M)) 
metrics=c("peak area", "RT", "fwhm", "Peak Symmetry", "MMA") 
names(Exp)=metrics 
PChart=pareto.chart(Exp,ylab="# of NonConformers ±3s") 
dev.set(2) 

} 

if (Q6==0){
Exp=c(sum(E),sum(F),sum(G),sum(H)) 
metrics=c("peak area", "RT", "fwhm", "Peak Symmetry") 
names(Exp)=metrics 
PChart=pareto.chart(Exp,ylab="# of NonConformers ±3s") 
dev.set(2) 
}


if (Q6==1)
{
windows(5,5)
library(ggplot2)

col_list = L*L2
MMA = MA
PeptideModifiedSequence = pep
dim(MMA) = c(col_list,1)
dim(PeptideModifiedSequence) = c(col_list,1)

plot.new()
df <- data.frame (Nums = rep((1:L), L2), MMA, PeptideModifiedSequence)
p <- ggplot(data = df, aes(factor(x=Nums), y=MMA))
p <- p + labs(title = "Mass Measurement Accuracy (MMA)") + xlab("QC Run Number") + ylab("MMA (ppm)") + geom_boxplot(fill = "grey80", colour = "#3366FF", outlier.size = NA)+ geom_jitter(aes(colour=(PeptideModifiedSequence)))
plot(p)
dev.set(2)

#MA=t(MA)
#boxplot(MA,main="Mass Measurement Accuracy (MMA)",pars=list(boxwex=0.5,staplewex=0.2,outwex=0.2), col="red",xlab="QC Run Num", ylab="MMA (ppm)")

}
}

tryCatch({parse.cmdline()}, 
         finally = {
           cat("Finished!")
         })





