report_proteinlocators = '<?xml version="1.0"?>
<views>
  <view name="ProteinNames" rowsource="pwiz.Skyline.Model.Databinding.Entities.Protein" sublist="Results!*">
    <column name="Name" />
  </view>
</views>'

tool_folder <- commandArgs(trailingOnly = TRUE)[1];
cat ("Tool folder ", tool_folder);
connectionname <- commandArgs(trailingOnly = TRUE)[2];
program_path <- file.path(tool_folder, "ToolServiceCmd.exe")
csv_output <- system2(program_path, c("GetReport", "--connectionname", connectionname), input=report_proteinlocators, stdout=TRUE);
df <- read.csv(text=csv_output, stringsAsFactors = FALSE);

cat("There are ", nrow(df), " proteins");
