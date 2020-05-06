## Overview

AutoMate is a development tool. It scans a series of files and/or directories and executes a batch file if any of
the scanned items are updated.

This allows an immediate triggering of build tools when a source file has been modified.

## Command Line Options

AutoMate is controlled from the commadn line. The command line options are below.

-n {name} Creates a new watch node and sets its name to {name}. All commands until the next -n will apply to this
watch node.

-f {filter} Sets the FileSystemWatcher.filter property. This tells the system
what files monitor for changes.

-d {dir} sets the working dir used when a command is executed in response to a file change.

-w {dir} sets the directory to watch for modifed files in. 

-c {path} path to command to execute when fiel changed are detected. Usually a batch file.

-a {string} optional command line arguments to pass into -c command.

## Example Usage

AutoMate is a .net core console program. Typically a batch file is created that starts AutoMare with the correct options.
An example of such a batch file is below.
This creates two watchers, 
a) one for the directory MFSH which executes MFISH.bat when changes in MFSH are detected and
b) one for the directory FSH which executes FISH.bat when changes in FSH are detected

Eir.AutoMate.exe -n MFSH -d . -w MFSH -c "MFish.bat" -n FSH -d . -w FSH -c "Fish.bat"

MFish.bat anr Fish.bat are standard  windows batch files.

