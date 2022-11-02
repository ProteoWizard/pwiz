command_args <- commandArgs(trailingOnly = FALSE)
script_args <- commandArgs(trailingOnly = TRUE)
# print(command_args)
file.arg.name <- "--file="

if (length(script_args) > 0 && length(grep(file.arg.name, command_args)) > 0) {
  script_file <- sub(file.arg.name, "", command_args[grep(file.arg.name, command_args)])
  script_dir <- dirname(script_file)
  if (script_dir != "") {
    script_dir <- paste(script_dir, "/", sep = "")
  }
  
  working_dir <- script_args[1]
  print(working_dir)
} else {
  errorCondition("Cannot find analysis folder")
}
setwd(working_dir)
file.create("I_WORKED.txt")