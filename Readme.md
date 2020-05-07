## Overview

AutoMate is a development tool. It scans a series of files and/or directories and executes a batch file if any of
the scanned items are updated.

This allows an immediate triggering of build tools when a source file has been modified.

## Command Line Option

AutoMate has only one optional command line option. This option is the name of the 
json configuration file that configures the watches.
If no name is passed on the command line, then the config file defaults to

AutoMate.json

## Options file

AutoMate is controlled from a user defined json file. 

The format of this file is json. The option data is an array of watches, each
watch defines on set of files to watch for changes to, and the commands to execute
when any changed are detected.

{<br/>
    "watchs": [<br/>
        {<br/>
            -- Watch 1<br/>
        },<br/>
        {<br/>
            -- Watch 2<br/>
        }<br/>
    ]<br/>
}<br/>

Each watch has the following structure

{<br/>
    "name": "{name}",<br/>
	"filter" : "{filter}",<br/>
    "watchPath": "{watch path}",<br/>
    "workingDir": "{working dir}",<br/>
	"cmdPath": "{command path}",<br/>
	"cmdArgs": "{command arguments}"<br/>
}<br/>

{name} Name of the watch

{filter} Optional file filter to specify what files to monitor for changes. Defaults to "*.*".

{watch path} Sets the directory to monitor for modified files. 

{working dir} Sets the working dir used when a command is executed.

{cmdPath} Command to execute whan modified files are detected.

{cmdArgs} Optional command arguments for {cmdPath}. Defaults to "".
