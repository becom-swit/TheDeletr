# Program to delete Files

This should be a Program to delete files from a given Folder, which are older then a given date. The Program should be a console application with Spectre.Console to help with the tui.

## Input Parameters

* The root folder -> Required
* The earliest date from which older files are deleted -d --deletion Date -> Required
* Dry run -dr --dryRun
* a List of comma or semicolon seperated strings of folders which are excluded for search. It should allow full qualified doc structures, single doc name, and parts of them name (marked with *folder, folder*, *folder*). -e --excluded
* a File containing the same folder to exclude. but also allow new line as seperator -ef --excludedFile
* Allow deletion without asking the user -y --yes
* Verbose -v --verbose

Show a help option

## Data Parsing

The program should start with the root folder and collect all files and add them to a candidates array if the last modified date is equal or older then the given date. it should recursevly look thru any folder which is in the given folder and collect the canidates in there too.
if a folder or file matches a item in the excluded list then skip the file or skip the whole folder from searching.

## List candidates

After the parsing, list out all candidates  to the console (if verbose is not set). new line after each candidates. When dry run flag is set, the program can end here

## Ask for permission

If the -y flag is not set, ask the user for permission to delete the candidates. if the answer is no, the program ends here

## Delete file

Show ca progress bar when deleteing the files. when finished show a report like x files deleted in xx seconds (or minutes and seconds if more the 60s mm:ss or hours and minutes and seconds if more the 60min hh:mm:ss)
