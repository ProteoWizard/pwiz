if (!require(tcltk2)) {install.packages('tcltk2', repos="http://cran.us.r-project.org"); require(tcltk2)}
if (!require(reshape)) {install.packages('reshape', repos="http://cran.us.r-project.org"); require(reshape)}
if (!require(RSQLite)) {install.packages('RSQLite', repos="http://cran.us.r-project.org"); require(RSQLite)}
if (!require(stats)) {install.packages('stats', repos="http://cran.us.r-project.org"); require(stats)}

quasitel <- function(data, group1, group2, weight=NULL, rm.SID=TRUE, rm.zero=FALSE, minavgcount=NULL)
{
    # should pass these as arguments instead
    option.contrasts <- getOption('contrasts')
    options(contrasts=c('contr.SAS', 'contr.treatment'))

    # make a grouping factor that will be applicable to the subsetted data
    group <- character()
    if (is.character(group1)) {
        group1 <- which(colnames(data) %in% group1)
    }
    if (is.character(group2)) {
        group2 <- which(colnames(data) %in% group2)
    }
    grp <- union(group1, group2)
    group[group2] <- "group2"
    group[group1] <- "group1"
    group <- factor(group[grp])
    gpl <- split(1:length(group), group)

    # subset the data and groups
    data <- subset(data, select=grp)
    #group1 <- gpl$group1
    #group2 <- gpl$group2

    # filter based on minimum count
    if (!is.null(minavgcount)) {
        grp12count <- apply(data, 1, mean)
        data <- subset(data, grp12count >= minavgcount)
    }

    #grp1zero <- apply(subset(data, select=group1), 1, sum) == 0
    #grp2zero <- apply(subset(data, select=group2), 1, sum) == 0
    #if (rm.zero) {
    #    # only keep features that are not both zero
    #    data <- subset(data, !(grp1zero | grp2zero))
    #} else {
    #    # or add a single count
    #    data[grp1zero, group1[1]] <- 1
    #    data[grp2zero, group2[1]] <- 1
    #}

    # prepare the weight
    if (is.null(weight)) {
        offset <- NULL
        wei <- sapply(gpl, length)
    } else {
        weight <- weight[colnames(data)]
        offset <- log(weight)
        wei <- sapply(gpl, function(x) { sum(weight[x]) })
    }

    Nprotein <- nrow(data)
    result <- matrix(numeric(), nrow=Nprotein, ncol=11)

    for (i in 1:Nprotein) {
        count <- as.numeric(data[i,])

        # poisson p-value
        g1a <- glm(count ~ group, offset=offset, family=poisson)
        g1 <- glm(count ~ 1, offset=offset, family=poisson)
        anovaP <- data.frame(anova(g1, g1a, test="Chisq"))
        Pvalues <- ifelse(anovaP[2,4] < 0.1e-15, 1, anovaP[2,5])

        # quasi p-value
        gquasi1a <- glm(count ~ group, offset=offset, family=quasi(link=log, variance=mu))
        gquasi1 <- glm(count ~ 1, offset=offset, family=quasi(link=log, variance=mu))
        anovaPq <- data.frame(anova(gquasi1, gquasi1a, test="F"))
        Pvaluesq <- ifelse(anovaPq[2,4] < 0.1e-15, 1, anovaPq[2,6])

        lambda <- exp(rev(cumsum(as.numeric(g1a$coef))))
        totcot <- round(wei * lambda, 0)
        rateratio <- log2(lambda[1] / lambda[2])

        sdl <- sapply(gpl, function(x) { sd(count[x]) })
        meanl <- sapply(gpl, function(x) { mean(count[x]) })
        cvl <- mapply("/", sdl, meanl)

        result[i, ] <- c(   totcot,     # 2 items
                            lambda,     # 2 items
                            rateratio,
                            Pvalues,    NA,
                            Pvaluesq,   NA,
                            cvl)        # 2 items
    }
    # fdr adjustment
    for (j in c(6, 8)) {
        result[, j+1] <- p.adjust(result[, j], method="fdr")
    }
    rownames(result) <- rownames(data)
    colnames(result) <- c(  "count1",   "count2",
                            "rates1",   "rates2",
                            "2log(rate1/rate2)",
                            "poisson.p", "poisson.fdr",
                            "quasi.p",  "quasi.fdr",
                            "cv1",      "cv2")
    options(contrasts=option.contrasts)
    result
}

# read spectral counts per group from the given idpDB file
datafile.readFilteredSpectraByGroup <- function(filepath)
{
    drv <- dbDriver("SQLite")
    con <- dbConnect(drv, filepath)
    res <- dbSendQuery(con, "SELECT ssg.Name, COUNT(DISTINCT Spectrum) FROM PeptideSpectrumMatch psm JOIN Spectrum s ON psm.Spectrum=s.Id JOIN SpectrumSource ss ON s.Source=ss.Id JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id GROUP BY ssg.Id ORDER BY ssg.Name")
    groupFilteredSpectra <- fetch(res, n=-1)
    dbClearResult(res)
    dbDisconnect(con)

    return(groupFilteredSpectra)
}

# read spectra per protein group by source group from the given idpDB file
datafile.readSpectraPerProteinGroupBySourceGroup <- function(filepath)
{
    drv <- dbDriver("SQLite")
    con <- dbConnect(drv, filepath)

    res <- dbSendQuery(con, "SELECT ProteinGroup, ssg.Name AS SourceGroup, COUNT(DISTINCT Spectrum) AS Spectra FROM PeptideSpectrumMatch psm JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide JOIN Protein pro ON pi.Protein=pro.Id LEFT JOIN ProteinMetadata pmd ON pro.Id=pmd.Id JOIN Spectrum s ON psm.Spectrum=s.Id JOIN SpectrumSource ss ON s.Source=ss.Id JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id GROUP BY pro.ProteinGroup, ssgl.Group_ ORDER BY ProteinGroup")
    proteinGroupSourceGroupCounts <- fetch(res, n=-1)
    dbClearResult(res)

    res <- dbSendQuery(con, "SELECT GROUP_CONCAT(DISTINCT Accession) as Proteins, pro.Cluster, IFNULL(pmd.Description, '') as Description FROM Protein pro LEFT JOIN ProteinMetadata pmd ON pro.Id=pmd.Id GROUP BY pro.ProteinGroup ORDER BY ProteinGroup")
    proteinGroupMetadata <- fetch(res, n=-1)
    dbClearResult(res)

    dbDisconnect(con)

    proteinGroupSourceGroupCounts <- cast(proteinGroupSourceGroupCounts, ProteinGroup ~ SourceGroup, value="Spectra")
    return(cbind(proteinGroupMetadata, proteinGroupSourceGroupCounts))
}

base <- NULL
done <- tclVar(0)

quasitelgui <- function(inputfile = NULL) {
    require(tcltk) || stop("tcltk support is missing")
    require(tcltk2) || stop("tcltk2 support is missing")
    require(stats) || stop("stats package is missing")

    namelist <- tclVar()
    if (length(inputfile) > 0)
        datafilename <- tclVar(inputfile)
    else
        datafilename <- tclVar()
    dataset <- NULL
    offset <- NULL
    dirname <- getwd()
    statustext <- tclVar()
    minavgcount <- tclVar(1)

    # open file dialog wrapper
    getfile <- function(parent, dir=getwd(), title="Open a Dataset") {
        filters <- "{ {IDPicker 3 database} {.idpDB} }"
        tkgetOpenFile(parent=parent,
            title=title,
            filetypes=filters,
            initialdir=dir)
    }
    # save file dialog wrapper
    savefile <- function(parent, file="filename", dir=getwd()) {
        filters <- "
            { {Tab-separated}   {.tsv} }
            { {Comma-separated} {.csv} }
            { {Tab-delimited}   {.txt} }
        "
        tkgetSaveFile(parent=parent,
            title="Save Output",
            filetypes=filters,
            defaultextension=".tsv",
            initialdir=dir,
            initialfile=file)
    }

    # write file based on extension
    writeDelim <- function(x, filename, ...) {

        row.names <- FALSE
        col.names <- TRUE
        fileparts <- strsplit(filename, split=".", fixed=TRUE)[[1]]
        type <- fileparts[length(fileparts)]

        switch(type,
            csv=function(x){write.csv(x, filename, row.names=row.names, ...)},
            tsv=function(x){write.table(x, filename, sep="\t", row.names=row.names, col.names=col.names, ...)},
            txt=function(x){write.table(x, filename, sep="\t", row.names=row.names, col.names=col.names, ...)},
            function(x){write.csv(x, filename, row.names=row.names, ...)})(data.frame(x))
    }

    # explode the group selection into items
    explode <- function(curselection, subdata, superdata) {
        newselection <- c()
        for (i in curselection) {
            pattern <- paste("^", sub("/$", "/[^/]+", subdata[i]), "$", sep="")
            result <- grep(pattern, subdata)
            if (length(result) < 1){
                newselection <- c(newselection, i)
            } else {
                newselection <- c(newselection, result)
            }
        }
        which(superdata %in% subdata[unique(newselection)])
    }

    datafile.open <- function(filepath)
    {
        tclObj(datafilename) <- filepath
        dirname <<- tclvalue(tclfile.dir(tclObj(datafilename)))

        groupFilteredSpectra <- datafile.readFilteredSpectraByGroup(filepath)
        dataset <<- datafile.readSpectraPerProteinGroupBySourceGroup(filepath)
        tclObj(namelist) <- groupFilteredSpectra[,1]
        offset <- groupFilteredSpectra[,2]
    }

    # command to run when open dataset file button is clicked
    datafile.cmd <- function()
    {
        inputfile <- getfile(base, dirname)
        datafile.open(tclvalue(inputfile))
    }

    # command to run when ok button is clicked
    ok.cmd <- function() {
        i1 <- as.integer(tkcurselection(g1.list)) + 1
        i2 <- as.integer(tkcurselection(g2.list)) + 1
        #Set filter i1, i2 to include all items if a group is selected

        i1 <- explode(i1, as.character(tclObj(namelist)), colnames(dataset))
        i2 <- explode(i2, as.character(tclObj(namelist)), colnames(dataset))
        bth <- intersect(i1, i2)
        if (length(bth) == 0) {
            #tkmessageBox(parent=base, title="Group 1",message=paste(colnames(dataset)[i1], collapse=", "),type="ok")
            #tkmessageBox(parent=base, title="Group 2",message=paste(colnames(dataset)[i2], collapse=", "),type="ok")
            if (length(i1) < 3) {
                tkmessageBox(parent=base,
                  title="ERROR",
                  message="Group 1 must contain at least 3 items.",
                  type="ok",
                  icon="error")
            } else if (length(i2) < 3) {
                tkmessageBox(parent=base,
                  title="ERROR",
                  message= "Group 2 must contain at least 3 items.",
                  type="ok",
                  icon="error")
            } else {
                outputfile <- sub('.idpDB', '-quasitel.tsv', tclvalue(tclfile.tail(tclObj(datafilename))))
                outputfile <- tclvalue(savefile(base, outputfile, dirname))
                if (nchar(outputfile)) {
                    result <- quasitel(dataset, i1, i2, weight=offset, minavgcount=as.numeric(tclvalue(minavgcount)))
                    datatmp <- dataset[rownames(result),]
                    dataused1 <- subset(datatmp, select=i1)
                    dataused2 <- subset(datatmp, select=i2)
                    result <- cbind(subset(datatmp, select=c("Proteins", "ProteinGroup", "Cluster")), result, TotalCounts=rowSums(subset(result, select=c("count1","count2"))), dataused1, dataused2, subset(datatmp, select="Description"))
                    writeDelim(result, outputfile)
                    tkmessageBox(parent=base,
                        title="Done",
                        message="Work complete",
                        type="ok")
                }
            }
        } else {
            msg = paste("The following were selected in both 'Group 1' and 'Group 2':",
                        paste(as.character(tclObj(namelist))[bth], collapse="\n"), sep="\n\n")
            tkmessageBox(parent=base,
                title="ERROR: Duplicate selection",
                message=msg,
                type="ok",
                icon="error")
        }
    }

    base <- tktoplevel()
    tkwm.title(base, "QuasiTel")

    # Files
    file.frm <- tkframe(base, borderwidth=2)
    datafile.lbl <- tklabel(file.frm, text="Data")
    datafile.entry <- tkentry(file.frm,
        textvariable=datafilename,
        state="readonly")
    datafile.btn <- tkbutton(file.frm,
        text="Browse...",
        command=datafile.cmd)
    tkgrid(datafile.lbl, datafile.entry, datafile.btn)
    tkgrid.configure(datafile.lbl, sticky="e")
    tkgrid.configure(datafile.entry, sticky="ew", padx=1)
    tkgrid.columnconfigure(file.frm, 1, weight=1)
    tkgrid(file.frm)
    tkgrid.configure(file.frm, sticky="ew")

    # Main
    main.frm <- tkframe(base, borderwidth=2)
    g1.lbl <- tklabel(main.frm, text="Group 1")
    g2.lbl <- tklabel(main.frm, text="Group 2")
    tkgrid(g1.lbl, g2.lbl)

    ## group 1 selection
    g1.frm <- tkframe(main.frm, borderwidth=1)
    g1.scroll <- tkscrollbar(g1.frm,
        command=function(...) tkyview(g1.list, ...))
    g1.list <- tklistbox(g1.frm,
        listvariable=namelist,
        selectmode="extended",
        exportselection=0,
        yscrollcommand=function(...) tkset(g1.scroll, ...))
    tkgrid(g1.list, g1.scroll)
    tkgrid.configure(g1.list, sticky="news")
    tkgrid.configure(g1.scroll, sticky="ns")
    tkgrid.columnconfigure(g1.frm, 0, weight=1)
    tkgrid.rowconfigure(g1.frm, 0, weight=1)

    ## group 2 selection
    g2.frm <- tkframe(main.frm, borderwidth=1)
    g2.scroll <- tkscrollbar(g2.frm,
        command=function(...) tkyview(g2.list, ...))
    g2.list <- tklistbox(g2.frm,
        listvariable=namelist,
        selectmode="extended",
        exportselection=0,
        yscrollcommand=function(...) tkset(g2.scroll, ...))
    tkgrid(g2.list, g2.scroll)
    tkgrid.configure(g2.list, sticky="news")
    tkgrid.configure(g2.scroll, sticky="ns")
    tkgrid.columnconfigure(g2.frm, 0, weight=1)
    tkgrid.rowconfigure(g2.frm, 0, weight=1)

    tkgrid(g1.frm, g2.frm)
    tkgrid.configure(g1.frm, g2.frm, sticky="news")
    tkgrid.columnconfigure(main.frm, 0, weight=1)
    tkgrid.columnconfigure(main.frm, 1, weight=1)
    tkgrid.rowconfigure(main.frm, 1, weight=1)
    tkgrid(main.frm)
    tkgrid.configure(main.frm, sticky="news")
    tkgrid.rowconfigure(base, 1, weight=1)

    # Bottom
    bott.frm <- tkframe(base, borderwidth=2)
    filter.lbl <- tklabel(bott.frm, text="Minimum Average Count Across Groups")
    filter.entry <- tk2spinbox(bott.frm, values=c(seq(from=1, to=5, by=0.25), seq(from=0, to=0.75, by=0.25)), textvariable=minavgcount, wrap=TRUE)
    ok.btn <- tkbutton(bott.frm, text="Compute",
        command=function() {
            tkconfigure(ok.btn, state="disabled")
            tkconfigure(q.btn, state="disabled")
            tkconfigure(datafile.btn, state="disabled")
            try(ok.cmd())
            tkconfigure(ok.btn, state="normal")
            tkconfigure(q.btn, state="normal")
            tkconfigure(datafile.btn, state="normal")
        })
    q.btn <- tkbutton(bott.frm, text="Quit", command=function() tclvalue(done) <- 1)
    tkbind(base,"<Destroy>", function() tclvalue(done) <- 2)
    tkgrid(filter.lbl, columnspan=2)
    tkgrid(filter.entry)
    tkgrid(ok.btn, q.btn)
    tkgrid.configure(ok.btn, q.btn, padx=1)
    tkgrid(bott.frm)
    tkgrid.columnconfigure(base, 0, weight=1)

    tcl("wm", "attributes", base, topmost=TRUE)
    tcl("wm", "attributes", base, topmost=FALSE)
    tkfocus(base)

    if (length(inputfile) > 0) { datafile.open(inputfile) }
}

cmd.args <- commandArgs(trailingOnly=TRUE)

if (length(cmd.args) > 0) {
    quasitelgui(gsub("\\\\", "/", cmd.args[1]))
} else {
    quasitelgui()
}

tkwait.variable(done)
tkdestroy(base)