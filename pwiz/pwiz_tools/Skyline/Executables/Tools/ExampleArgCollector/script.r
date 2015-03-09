##R Script Template : Skyline Tool Store
# Created by Yuval Boss, Department of Genome Sciences at UW.

options (echo = FALSE)
debug <- FALSE

##
## Command line processing for Argument Collector
##

parse.cmdline <- function () {
	
  # set up for command line processing (if needed)
  # arguments are specified positionally (since there are no optional arguments) and ...
  arguments <- commandArgs(trailingOnly=TRUE)
  if ( length (arguments) != 4)
	# expected arguments not present -- error
    stop ("USAGE: R --slave --no-save --args '<checkbox><textbox><combobox>'") 
  for (i in 1:4) {
    arg <- arguments [i]
    # remove leading and trailing blanks
    arg <- gsub ("^ *", "", arg)
    arg <- gsub (" *$", "", arg)
    # remove any embedded quotation marks
    arg <- gsub ("['\'\"]", "", arg)
	#report file is brought in as an argument, this is specified in TestArgsCollector.properties
    if (i==1) reportfile <<- arg
    if (i==2) checkbox <<- as.numeric (arg)
    if (i==3) textbox <<- toString(arg)
    if (i==4) combobox <<- as.numeric (arg)        
  }
   
   #All data processing done below
  
	if(checkbox == 0)
		cat("Check Box was not checked.")
	if(checkbox == 1)
		cat("Check Box was checked.")
		
	cat("\rText Box Content: ", textbox,"\r")
	
	cboxstring <<- " was selected from the Combo Box."
	if(combobox == 0)
	cat("Option 1", cboxstring)
	if(combobox == 1)
	cat("Option 2", cboxstring)
	if(combobox == 2)
	cat("Option 3", cboxstring)
	if(combobox == 3)
	cat("Option 4", cboxstring)	
}

tryCatch({parse.cmdline()}, 
         finally = {
           cat("\rFinished!")
         })
