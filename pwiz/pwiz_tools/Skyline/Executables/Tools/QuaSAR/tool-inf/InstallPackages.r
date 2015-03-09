#Install QuaSAR Packages
a<-installed.packages()
packages<-a[,1]

if (!is.element("bitops",packages)){
install.packages("bitops" , repos='http://cran.us.r-project.org')
}

if (!is.element("reshape",packages)){
install.packages("reshape" , repos='http://cran.us.r-project.org')
}

if (!is.element("gtools",packages)){
install.packages("gtools" , repos='http://cran.us.r-project.org')
}

if (!is.element("boot",packages)){
install.packages("boot" , repos='http://cran.us.r-project.org')
}

if (!is.element("ggplot2",packages)){
install.packages("ggplot2" , repos='http://cran.us.r-project.org')
}

if (!is.element("gplots",packages)){
install.packages("gplots" , repos='http://cran.us.r-project.org')
}
