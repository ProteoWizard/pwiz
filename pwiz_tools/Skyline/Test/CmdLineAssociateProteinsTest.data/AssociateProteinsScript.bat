set SKYLINE_CMD="%1"
REM Associate proteins using "TwoProteins.fasta" and save settings
%SKYLINE_CMD% --in=AssociateProteinsTest.sky --associate-proteins-fasta=TwoProteins.fasta --save-settings --out=Document1.sky
REM Associate proteins without specifying fasta file and it should use "TwoProteins.fasta"
%SKYLINE_CMD% --in=AssociateProteinsTest.sky --associate-proteins-group-proteins --out=Document2.sky
REM Associate proteins using "ThreeProteins.fasta" and do not save settings
%SKYLINE_CMD% --in=AssociateProteinsTest.sky --associate-proteins-group-proteins --associate-proteins-fasta=ThreeProteins.fasta --out=Document3.sky
REM Associate proteins without specifying fasta file and it should still use "TwoProteins.fasta"
%SKYLINE_CMD% --in=AssociateProteinsTest.sky --associate-proteins-group-proteins --out=Document4.sky
