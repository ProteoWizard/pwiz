command_args <- commandArgs(trailingOnly = FALSE)
script_args <- commandArgs(trailingOnly = TRUE)
# print(command_args)
file.arg.name <- "--file="

if (length(script_args) > 0 && length(grep(file.arg.name, command_args)) > 0) {
  report_file <- script_args[1]
  report_dir <- dirname(report_file)
  if (report_dir != "") {
    report_dir <- paste(report_dir, "/", sep = "")
  }
  print(report_dir)
  working_dir <- dirname(report_file)#script_args[1]
  print(working_dir)
} else {
  errorCondition("Cannot find analysis folder")
}
setwd(working_dir)
file.create("I_WORKED.txt")