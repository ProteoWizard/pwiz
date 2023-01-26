App: Hardklor
Purpose: Analyze mass spectra

Version History
------------------------------------------
1.0 - Apr 17 2007 - Stable release
1.01 - May 17 2007 - Recompiled with latest MSToolkit(1.1)
1.02 - Jun 15 2007 - Fixed monoisotopic mass bug
1.03 - Aug 29 2007 - Added sensitivity level parameter - much better low abundance peptide performance. Some changes made to Mercury

1.1 - Sep 29 2007 - Added mzXML file support and compressed .cms1 & .cms2 support. Several bugfixes, including better S/N processing.

1.11 - Jan 16 2008 - Fixed bugs in MSToolkit 64-bit file support. Modified default Hardklor settings.

1.20 - Feb 6 2008
  * Fixed user-defined scan window bug. 
  * Allows reading of centroid data (-c). No signal-to-noise processing is done for this option.
  * Can set a static S/N cutoff for entire scan. This optimizes Orbitrap and LTQ-FT data analysis. See documentation.
  * Allows substitution of a user-defined molecule for averagine (-mol). Note: still in testing stages.
  * Toggle isotope distribution area for output in place of base isotope peak instensity (-da).
  * Minor speed improvements.

1.22 - Apr 24 2008
  * Minor Bug Fixes
  * Upgraded MSToolkit software to 2.42

1.23 - Oct 24 2008
  * Improvements to spectrum dividing functions

1.24 - Dec 11 2008
  * Bugfixes for zoomscan and ultrazoomscan files

1.25 - Mar 3 2009
  * Additional support for ms2 file format
  * Bugfix to MSToolkit (forked from SVN)

1.26 - Apr 30 2009
  * Upgraded MSToolkit
  * Bugfix for rare S/N ratio calculation error
  * Potentially fixed rare, irregular, and odd 64-bit linux behavior

1.27 - Aug 13 2009
  * Filenames and their paths can have spaces if entire path and filename is contained in quotes

1.28 - Sep 2 2009
  * Writes results in XML format with the following parameter: -xml true

1.30 - Sep 13 2009
  * Bugfix to QuickCharge algorithm

1.31 - Oct 1 2009
  * Added new signal detection algorithm. It operates on the assumption valid signal peaks persist over multiple adjacent spectra.
    Detailed description of its usage can be found in the online documentation.