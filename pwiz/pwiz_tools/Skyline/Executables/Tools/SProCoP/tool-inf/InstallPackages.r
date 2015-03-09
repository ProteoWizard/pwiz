#Install QuaSAR Packages
a<-installed.packages()
packages<-a[,1]

if (!is.element("qcc",packages)){
install.packages("qcc" , repos='http://cran.us.r-project.org')
}

