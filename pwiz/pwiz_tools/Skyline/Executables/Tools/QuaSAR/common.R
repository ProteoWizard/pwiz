# The Broad Institute
# SOFTWARE COPYRIGHT NOTICE AGREEMENT
# This software and its documentation are copyright (2003-2006) by the
# Broad Institute/Massachusetts Institute of Technology. All rights are
# reserved.

# This software is supplied without any warranty or guaranteed support
# whatsoever. Neither the Broad Institute nor MIT can be responsible for its
# use, misuse, or functionality.
 
DEBUG <<- FALSE
options("warn"=-1)

info <- function(...) {
	if(DEBUG) {
		args <- list(...)
		for(i in 1:length(args)) {
			cat(paste(args[[i]], sep=''))
		}
		cat("\n", sep='')
	}
}

# extension e.g. '.gct'
check.extension <- function(file.name, extension) {
	ext <- regexpr(paste(extension,"$",sep=""), tolower(file.name))
	if(ext[[1]] == -1) {
		file.name <- paste(file.name, extension, sep="") 
	}
	return(file.name)
}

read.dataset <- function(file) {
	result <- regexpr(paste(".gct","$",sep=""), tolower(file))
	if(result[[1]] != -1)
		return(read.gct(file))
	result <- regexpr(paste(".res","$",sep=""), tolower(file))
	if(result[[1]] != -1)
		return(read.res(file))
	
	stop("Input is not a res or gct file.")	
}

wait.for.lsf.jobs <- function(jobs, interval, maxSleep, del=T) {
	while(length(jobs) > 0) {
		newJobs <- NULL
		for(i in 1:length(jobs)) {
			job <- jobs[i]
			state <- lsf.job.status(job)
			if (is.null(state)) {
				scat("lsf: unknown error retrieving job state.\n")
			} else if (state == "NULL" || state == "ZOMBI" || state == "UNKWN" || 
				state == "ERROR") {
				scat("lsf: job is in an error state.\n")
			} else if (state == "EXIT") {
				scat("lsf: job did not complete successfully ... no results\n")
				
			} else if (state == "DONE") {
			
			} else {
				newJobs <- c(newJobs, job)
			}	
		}
		jobs <- newJobs
		Sys.sleep(interval)
		interval <- interval + 10
		if(interval > maxSleep) {
			maxSleep <- interval
		}
	}
	if(del) {
		unlink("Rlsf_*", recursive = T)
	}
}

wait.for.lsf.job <- function(job) {
	interval <- 10
	maxSleep <- 60 * 60
	while (lsf.job.status(job) != "DONE") {
	        Sys.sleep(interval)
			interval <- interval + 10
			if(interval > maxSleep) {
				maxSleep <- interval
			}
	}
}

zip.file <- function(path.to.zip.exe, outfile, pattern) {
	outfile <- check.extension(outfile, ".zip") 
	isWindows <- Sys.info()[["sysname"]]=="Windows"
	if(isWindows) {
		path.to.zip.exe <- sub("Program Files", "Progra~1", path.to.zip.exe)
		path.to.zip.exe <- sub("Documents and Settings", "Docume~1", path.to.zip.exe)
		last.char <- substr(path.to.zip.exe, nchar(path.to.zip.exe), nchar(path.to.zip.exe))
		if(last.char != "/" && last.char!="\\") {
			path.to.zip.exe <- paste(path.to.zip.exe, "/", sep='')
		}
		system(paste(path.to.zip.exe, "zip.exe -q -m ", outfile, " ", pattern, sep=''))
	} else {
		system(paste("zip -q -m ", outfile, " ", pattern, sep=''))
	}
}

get.array.names.cn <- function(infile) {
	data.matrix <- scan(infile, nlines=1, what = "", quote = "", quiet = T, sep="\t")
	return(data.matrix[-(1:3)])
}

# get column indices of array.names in array.list.file
get.column.indices <- function(array.names, array.list.file) {
	iter.cols <- NULL
	array.list <- scan(array.list.file, what = "", quote = "", sep="\t", quiet = T)
	for(a in array.list) {
		idx <- which(array.names==a, arr.ind=T)
		if(length(idx) > 0) {
			iter.cols <- c(iter.cols, idx)
		}
	}	
	if(is.null(iter.cols)) {
		exit("No arrays in array list file found in cn file")
	}
	return(iter.cols)
}

# column.indices list of data column indices to read
read.cn <- function(file, column.indices=NULL) {

   # SNP	Chromosome	PhysicalPosition	sample1  sample2
   array.names <- get.array.names.cn(file)
	num.columns <- length(array.names)
	dataColClasses <- vector("list", num.columns+3)
	dataColClasses[1] <- "character"
	dataColClasses[2] <- "character"
	dataColClasses[3] <- "integer"
	
   if(is.null(column.indices)) {
	   for(i in 4:length(dataColClasses)) {
		   dataColClasses[i] <- "numeric"
	   }
	} else {
	   for(i in 1:length(column.indices)) {
		   dataColClasses[column.indices[i]+3] <- "numeric"
	   }
	}

	data.matrix<-read.table(file=file, row.names=NULL, header=T, quote="", comment.char="", sep="\t", as.is=T, colClasses=dataColClasses, fill=F)
	
	
	chrom <- data.matrix[,2]
	chrom[chrom=="X"]<-"23"
	chrom[chrom=="Y"]<-"24"
	chrom <- as.integer(chrom)
	
	maploc <- data.matrix[,3]
	maploc <- as.integer(maploc)
	
	genomdat <- as.matrix(data.matrix[,-(1:3)])

	return(list(chrom=chrom, genomdat=genomdat, maploc=maploc, array.names=array.names))
}


read.gct <- function(file) {
	if (is.character(file)) 
        if (file == "") 
            file <- stdin()
        else {
            file <- file(file, "r")
            on.exit(close(file))
        }
	if (!inherits(file, "connection")) 
        stop("argument `file' must be a character string or connection")
		  
   # line 1 version
	version <- readLines(file, n=1) 
	
	# line 2 dimensions
	dimensions <- scan(file, what=list("integer", "integer"), nmax=1, quiet=TRUE)   
	rows <- dimensions[[1]]
	columns <- dimensions[[2]]
	# line 3 Name\tDescription\tSample names...
	column.names <- read.table(file, header=FALSE, quote='', nrows=1, sep="\t", fill=FALSE, comment.char='') 
	column.names <-column.names[3:length(column.names)]

	
	if(length(column.names)!=columns) {
		stop(paste("Number of sample names", length(column.names), "not equal to the number of columns", columns, "."))	
	}
	
	colClasses <- c(rep(c("character"), 2), rep(c("double"), columns))
	
	x <- read.table(file, header=FALSE, quote="", row.names=NULL, comment.char="", sep="\t", colClasses=colClasses, fill=FALSE)
	row.descriptions <- as.character(x[,2]) 
	data <- as.matrix(x[seq(from=3, to=dim(x)[2], by=1)])
	
	column.names <- column.names[!is.na(column.names)]
	
	colnames(data) <- column.names
	row.names(data) <- x[,1]
	return(list(row.descriptions=row.descriptions, data=data))
}

read.res <- function(filename)
{
  # read line 1: sample names
  headings <- read.table( filename, header=FALSE, nrows=1, sep="\t", fill=FALSE, comment.char='')
  # delete the NA entries for the tab-tab columns
  headings <- headings[!is.na(headings)]
  colNames <- headings[3:length(headings)]
   
  # read line 2: sample descriptions
  descriptions <- scan(filename, skip=1, nlines=1, sep="\t", fill=F, blank.lines.skip=F, quiet=T, what="character")
  
  # delete the NA entries for the tab-tab columns
 
  if(length(descriptions) > 0) {
  	descriptions <- descriptions[seq(from = 2, to = length(descriptions), by=2)]
  }
  # handle optionally missing number of lines (not used, but need to decide whether to ignore before actual data)  
  numLines <- as.list(read.table(filename, header=FALSE, skip=2, nrows=1, sep="\t", fill=FALSE, comment.char=''))
  numLines <- numLines[!is.na(numLines)] # remove NA entries
  skip <- (3 - ifelse(length(numLines) == 1, 0, 1)) # skip 3 lines if line number is present, 2 otherwise

  columns <- length(headings) - 2 # substract 2 for gene description and name 
  colClasses <- c(c("character", "character"), rep(c("double", "character"), columns))
 
  
  x <- .my.read.table(filename, header=FALSE, sep="\t", comment.char="", skip=skip, colClasses=colClasses, row.names=NULL, quote=NULL, fill=FALSE)
  
  data <- as.matrix(x[c(seq(from=3,length=(dim(x)[2]-3)/2, by=2))])
  calls <- as.matrix(x[c(seq(from=4,length=(dim(x)[2]-3)/2, by=2))])
  
  row.names <- x[,2]
  row.names(data) <- row.names
  row.names(calls) <- row.names
  row.descriptions <- as.character(x[, 1])
  colnames(data) <- colNames
  colnames(calls) <- colNames
  return(list(row.descriptions=row.descriptions, column.descriptions=descriptions, data=data, calls=calls))
}

# like read.table, but doesn't check to make sure all rows have same number of columns
.my.read.table <- function (file, header = FALSE, sep = "", quote = "\"'", dec = ".", row.names, col.names, as.is = FALSE, na.strings = "NA", colClasses, nrows = -1, skip = 0, check.names = TRUE, fill = !blank.lines.skip, strip.white = FALSE, blank.lines.skip = TRUE, comment.char = "") 
{
	if (is.character(file)) {
		file <- file(file, "r")
		on.exit(close(file))
	}
	if (!inherits(file, "connection")) 
		stop("argument `file' must be a character string or connection")
	if (!isOpen(file)) {
		open(file, "r")
		on.exit(close(file))
	}
	if (skip > 0) 
		readLines(file, skip)
		
	first <- readLines(file, n=1) 
	pushBack(first, file)
	temp <- strsplit(first, "\t") 
	cols <- as.integer(length(temp[[1]])) # number of columns
	 
	if (missing(col.names)) 
        col.names <- paste("V", 1:cols, sep = "")
	
	what <- rep(list(""), cols)
	names(what) <- col.names
	colClasses[colClasses %in% c("real", "double")] <- "numeric"
	known <- colClasses %in% c("logical", "integer", "numeric", "complex", "character")
	what[known] <- sapply(colClasses[known], do.call, list(0))
    
	data <- scan(file = file, what = what, sep = sep, quote = quote, dec = dec, nmax = nrows, skip = 0, na.strings = na.strings, quiet = TRUE, fill = fill, strip.white = strip.white, blank.lines.skip = blank.lines.skip, multi.line = FALSE, comment.char = comment.char)
	nlines <- length(data[[1]])
	if (cols != length(data)) {
		warning(paste("cols =", cols, " != length(data) =", length(data)))
		cols <- length(data)
	}
	if (is.logical(as.is)) {
        as.is <- rep(as.is, length = cols)
	}
	else if (is.numeric(as.is)) {
	  if (any(as.is < 1 | as.is > cols)) 
			stop("invalid numeric as.is expression")
	  i <- rep(FALSE, cols)
	  i[as.is] <- TRUE
	  as.is <- i
	}
	else if (is.character(as.is)) {
	  i <- match(as.is, col.names, 0)
	  if (any(i <= 0)) 
			warning("not all columns named in as.is exist")
	  i <- i[i > 0]
	  as.is <- rep(FALSE, cols)
	  as.is[i] <- TRUE
	}
	else if (length(as.is) != cols) 
		stop(paste("as.is has the wrong length", length(as.is), 
			"!= cols =", cols))
	if (missing(row.names)) {
		if (rlabp) {
			row.names <- data[[1]]
			data <- data[-1]
	  }
	  else row.names <- as.character(seq(len = nlines))
	}
	else if (is.null(row.names)) {
		row.names <- as.character(seq(len = nlines))
	}
	else if (is.character(row.names)) {
		if (length(row.names) == 1) {
			rowvar <- (1:cols)[match(col.names, row.names, 0) == 
				 1]
			row.names <- data[[rowvar]]
			data <- data[-rowvar]
	  }
	}
	else if (is.numeric(row.names) && length(row.names) == 1) {
	  rlabp <- row.names
	  row.names <- data[[rlabp]]
	  data <- data[-rlabp]
	}
	else stop("invalid row.names specification")
	class(data) <- "data.frame"
	row.names(data) <- row.names
	data
}

write.res <-
#
# write a res structure as a file
#
function(res, filename, check.file.extension=TRUE)
{	
	calls <- res$calls
	if(is.null(calls)) {
		exit("No calls found")
	}
	# write the data
	if(!is.null(res$row.descriptions)) {
		if(length(res$row.descriptions) != NROW(res$data)) {
			exit("invalid length of row.descriptions")
		}
	} 
	
	if(check.file.extension) {
		filename <- check.extension(filename, ".res")
	}
	f <- file(filename, "w")
	on.exit(close(f))
	# write the labels
	cat("Description\tAccession\t", file=f, append=TRUE)
	cat(colnames(res$data), sep="\t\t", file=f, append=TRUE)
	cat("\n", file=f, append=TRUE)
	
	# write the descriptions
	if(!is.null(res$column.descriptions)) {
		cat("\t", file=f, append=TRUE)
		cat(res$column.descriptions, sep="\t\t", file=f, append=TRUE)
	} 
	cat("\n", file=f, append=TRUE)
	
	# write the number of rows
	cat(NROW(res$data), "\n", sep="", file=f, append=TRUE)
	
	row.descriptions <- res$row.descriptions
	if(is.null(row.descriptions)) {
	    row.descriptions <- ''
	}
	m <- cbind(row.descriptions, row.names(res$data))
	
	#s <- integer(0)
   	#s <- c(1, 2)
   	#cols <- NCOL(res$data)
   	#offset <- 2
	#for(i in 1:NCOL(res$data)) {
	#	s <- c(s, i + offset)
	#	s <- c(s, cols+i + offset)
	#}
	#m <- cbind(res$data, calls)
	
	# combine matrices
	for(i in 1:NCOL(res$data)) {
		m <- cbind(m, res$data[,i])
		m <- cbind(m, as.character(calls[,i]))
	}
	
	write.table(m, file=f, col.names=FALSE, row.names=FALSE, append=TRUE, quote=FALSE, sep="\t", eol="\n")
	return(filename)
}

read.cls <- function(file) {
	# returns a list containing the following components: 
	# labels the factor of class labels
	# names the names of the class labels if present
	
	if (is.character(file)) 
        if (file == "") 
            file <- stdin()
        else {
            file <- file(file, "r")
            on.exit(close(file))
        }
    if (!inherits(file, "connection")) 
        stop("argument `file' must be a character string or connection")
   
	line1 <- scan(file, nlines=1, what="character", quiet=TRUE)
	
	numberOfDataPoints <- as.integer(line1[1])
	numberOfClasses <- as.integer(line1[2])
	
	line2 <- scan(file, nlines=1, what="character", quiet=TRUE)
	
	classNames <- NULL
	if(line2[1] =='#') { # class names are given
		classNames <- as.vector(line2[2:length(line2)])
		line3 <- scan(file, what="character", nlines=1, quiet=TRUE)
	} else {
		line3 <- line2
	}
	
	if(is.null(classNames)) {
		labels <- as.factor(line3)
		classNames <- levels(labels)
	} else {
		labels <- factor(line3, labels=classNames)
	}
	if(numberOfDataPoints!=length(labels)) {
		stop("Incorrect number of data points") 	
	}
	r <- list(labels=labels,names=classNames)
	r
}

# writes a factor to a cls file
write.factor.to.cls <- function(factor, filename, check.file.extension=TRUE)
{
	if(check.file.extension) {
		filename <- check.extension(filename, ".cls")
	}
	file <- file(filename, "w")
	on.exit(close(file))
 	codes <- unclass(factor)
	cat(file=file, length(codes), length(levels(factor)), "1\n")

	levels <- levels(factor)
	
	cat(file=file, "# ")
	num.levels <- length(levels)

    if(num.levels-1 != 0)
	{
	    for(i in 1:(num.levels-1))
	    {
		    cat(file=file, levels[i])
		    cat(file=file, " ")
	    }
	}
	cat(file=file, levels[num.levels])
	cat(file=file, "\n")
	
	num.samples <- length(codes)
	if(num.samples-1 != 0)
	{
	    for(i in 1:(num.samples-1))
	    {
		    cat(file=file, codes[i]-1)
		    cat(file=file, " ")
	    }
	}

	cat(file=file, codes[num.samples]-1)
	return(filename) 
}


read.sample.info <- function(file) {
	return(read.table(file, header=T, colClasses = "character", sep="\t", comment.char="", quote="", strip.white=T))
}

write.cls <-
#
# writes a cls result to a file. A cls results is a list containing names and labels
function(cls, filename, check.file.extension=TRUE)
{
	if(check.file.extension) {
		filename <- check.extension(filename, ".cls")
	}
	file <- file(filename, "w")
	on.exit(close(file))
 
	cat(file=file, length(cls$labels), length(levels(cls$labels)), "1\n")

    # write cls names
	if(length(cls$names) > 0) {
		cat(file=file, "# ")
        i <- 1
		while(i < length(cls$names)) {
			cat(file=file, cls$names[i])
			cat(file=file, " ")

			i <- i+1
		}
		cat(file=file, cls$names[length(cls$names)])
		cat(file=file, "\n")
	}

   # write cls labels
	i <-1
	while(i < length(cls$labels)){
		cat(file=file, as.numeric(cls$labels[[i]])-1)
		cat(file=file, " ")

		i <- i+1
	}
	cat(file=file, as.numeric(cls$labels[[length(cls$labels)]])-1)

	return(filename)
}


read.clm <- function(input.file.name) {
	s <- read.table(input.file.name, colClasses = "character", sep="\t", comment.char="", quote="", strip.white=T) 
	columns <- ncol(s)
	scan.names <- s[,1]
	sample.names <- NULL # names to use in the gct file instead of the scan name
	if(columns > 1) {
		sample.names <- s[,2]
	}
	
	for(i in 1:length(scan.names)) { # check for duplicate scans
		scan.names[i] <- trim(scan.names[i])
		scan <- scan.names[i]
		l <- which(scan.names==scan)
		if(length(l) >= 2) {
			exit(paste("Duplicate scan name:", scan))
		}
	}
	
	f <- NULL
	if(columns > 2) {
		class.names <- s[, 3]
		f <- factor(class.names)
	}
	list("factor"=f, "scan.names"=scan.names , "sample.names"=sample.names)
}

write.gct <-
#
# save a GCT result to a file, ensuring the filename has the extension .gct
#
function(gct, filename, check.file.extension=TRUE)
{
	if(check.file.extension) {
		filename <- check.extension(filename, ".gct") 
	}
	if(is.null(gct$data)) {
		exit("No data given.")
	}
	if(is.null(row.names(gct$data))) {
		exit("No row names given.")
	}
	if(is.null(colnames(gct$data))) {
		exit("No column names given.")
	}
	
	rows <- dim(gct$data)[1]
	columns <- dim(gct$data)[2]
	
	if(rows!=length(row.names(gct$data))) {
		exit("Number of data rows (", rows, ") not equal to number of row names (", length(row.names(gct$data)), ").")
	}
	if(columns!=length(colnames(gct$data))) {
		exit("Number of data columns (", columns , " not equal to number of column names (", length(colnames(gct$data)), ").")
	}
		
	if(!is.null(gct$row.descriptions)) {
		if(length(gct$row.descriptions)!=rows) {
			exit("Number of row descriptions (", length(gct$row.descriptions), ") not equal to number of row names (", rows, ").")
		}
	}
	
	row.descriptions <- gct$row.descriptions
	if(is.null(row.descriptions)) {
	    row.descriptions <- ''
	}
	m <- cbind(row.names(gct$data), row.descriptions, gct$data)
	f <- file(filename, "w")
	on.exit(close(f))
	
	cat("#1.2", "\n", file=f, append=TRUE, sep="")
	cat(rows, "\t", columns, "\n", file=f, append=TRUE, sep="")
	cat("Name", "\t", file=f, append=TRUE, sep="")
	cat("Description", file=f, append=TRUE, sep="")
	names <- colnames(gct$data)
	
	for(j in 1:length(names)) {
		cat("\t", names[j], file=f, append=TRUE, sep="")
	}

	cat("\n", file=f, append=TRUE, sep="")
	write.table(m, file=f, append=TRUE, quote=FALSE, sep="\t", eol="\n", col.names=FALSE, row.names=FALSE)
	return(filename)
}

is.package.installed <- function(libdir, pkg) {
	f <- paste(libdir, pkg, sep='')
	return(file.exists(f) && file.info(f)[["isdir"]])
}


install.package <- function(dir, windows, mac, other) {
	isWindows <- Sys.info()[["sysname"]]=="Windows"
	isMac <- Sys.info()[["sysname"]]=="Darwin" 
	if(isWindows) {
		f <- paste(dir, windows, sep="")
		.install.windows(f)
	} else if(isMac) {
		f <- paste(dir, mac, sep="")
      .install.unix(f)
	} else { # install from source
		f <- paste(dir, other, sep="")
		.install.unix(f)
	}	
}

.install.windows <- function(pkg) {
  if(!is.na(file.info(pkg)['size'])) {
      if(DEBUG) {
         info("Installing windows package ", pkg)
      }
      suppressMessages(install.packages(pkg, .libPaths()[1], repos=NULL))
   }
}

.install.unix <- function(pkg) {
   if(!is.na(file.info(pkg)['size'])) {
      if(DEBUG) {
         info("Installing package ", pkg)
      }
       lib <- .libPaths()[1]
      # cmd <- paste(file.path(R.home(), "bin", "R"), "CMD INSTALL --with-package-versions")
       cmd <- paste(file.path(R.home(), "bin", "R"), "CMD INSTALL")
       cmd <- paste(cmd, "-l", lib)
       cmd <- paste(cmd, " '", pkg, "'", sep = "")
       status <- suppressMessages(system(cmd))
       if (status != 0) 
         cat("\tpackage installation failed\n")
    }
}

trim <- function(s) {
	sub(' +$', '', s, extended = TRUE) 
}

setLibPath <- function(libdir) {
	libPath <- libdir
	if(!file.exists(libdir)) {
		# remove trailing /
		libPath <- substr(libdir, 0, nchar(libdir)-1)
	}
	.libPaths(libPath)
}

yes.no.to.boolean <- function(s) {
	if(s=="yes") {
		return(TRUE)	
	}
	return(FALSE)
}

exit <- function(...) {
	args <- list(...)
	s <- paste(args)
	stop(s, call. = FALSE)
}

isWindows <- function() {
	Sys.info()[["sysname"]]=="Windows"
}


isMac <- function() {
	Sys.info()[["sysname"]]=="Darwin" 
}

unzip <- function(zip.filename, dest) {
	if(is.null(dest)) {
		dest = getwd()
	}
	if(isWindows()) {
		zip.unpack(zip.filename, dest=dest)
	} else {
      unzip <- getOption("unzip")
      cmd <- paste(unzip, " -q '", zip.filename, "' -d ", dest, sep='')
      system(cmd)
	}
}

get.arg <- function(key, args, default.value='') {
	if(is.null(args[key])) {
		return(default.value)
	}
	return(args[key])
}

parse.command.line <- function(args) {
	result <- list()
	for(i in 1:length(args)) {
		flag <- substring(args[[i]], 0, 2)
		value <- substring(args[[i]], 3, nchar(args[[i]]))
		if(flag=='') {
			next
		}	
		result[flag] <- value
	}
}



