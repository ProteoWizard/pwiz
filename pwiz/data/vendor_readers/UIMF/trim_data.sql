update global_parameters set numframes = 100;
update frame_parameters set scans = 10;
delete from frame_parameters where framenum > 100;
delete from frame_scans where framenum > 100 OR scannum >= 10;
vacuum