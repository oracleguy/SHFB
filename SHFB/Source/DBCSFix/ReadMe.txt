﻿dbcsFix.exe
(C) Microsoft 2007

FUNCTION:
dbcsFix.exe attempts to work around limitations in the CHM compiler regarding character encodings and
representations. Specifically:

1. Replaces some characters with ASCII equivelents, as follows:
            /* substitution table:
             * Char name				utf8 (hex)		ascii
             * Non-breaking space		\xC2\xA0		"&nbsp;" (for all languages except Japanese)
             * Non-breaking hyphen		\xE2\x80\x91	"-"
             * En dash			    	\xE2\x80\x93	"-"
             * Left curly single quote	\xE2\x80\x98	"'"
             * Right curly single quote \xE2\x80\x99	"'"
             * Left curly double quote	\xE2\x80\x9C	"\""
             * Right curly double quote \xE2\x80\x9D	"\""
             * Horizontal ellipsis      U+2026          "..."
             */
After this step, no further work is done when LCID == 1033.

2. Replaces some characters with named entitites, as follows:
            /* substitution table:
             * Char name      			utf8 (hex)		named entity
             * Copyright	  			\xC2\xA0		&copy
             * Registered trademark 	\xC2\xAE		&reg
             * Em dash  	     		\xE2\x80\x94	&mdash;
             * Trademark			\xE2\x84\xA2		&trade;
             */

3. Replaces the default "CHARSET=UTF-8" setting in the HTML generated by ChmBuilder with "CHARSET=" + the
proper value for the specified LCID, as determined by the application's .config file.

4. Re-encodes all input HTML from their current encoding (UTF-8, as output by ChmBuilder) to the correct
encoding for the specified LCID.

USAGE:
dbcsFix.exe [-d=Directory] [-l=LCID]
-d is the directory containing CHM input files (e.g., HHP file). For example, 'C:\DocProject\Output\Chm'.
   Default is the current directory.
-l is the language code ID in decimal. For example, '1033'. Default is '1033' (for EN-US).

Usage is also available with -?

After processing the inputs with dbcsFix.exe, the call to the CHM compiler must be made when the system
locale is the same as the value set when calling this tool. This can be done either by changing your system
settings via the control panel, or by using the 3rd party utility SbAppLocale
(http://www.steelbytes.com/?mid=45). In the latter case, the call should be similar to:

SbAppLocale.exe $(LCID) "%PROGRAMFILES%\HTML Help Workshop\hhc.exe" Path\Project.hhp
