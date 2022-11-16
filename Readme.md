## Overview

AutoMate is a development tool. It scans a series of files and/or directories and executes a batch file if any of
the scanned items are updated.

This allows an immediate triggering of build tools when a source file has been modified.

This program is undergoing a lot of changes. Anyone is welcome to use it, 
but at this point in time I would not recommend using it for a production product;
I would recommend only using this for development projects by users who are comfortable
updating their scripts as changes to this tool occur, and are comfrotable using software that is not fully tested.

## Installation

dotnet tool install --global Eir.AutoMate

To update

dotnet tool uninstall --global Eir.AutoMate

dotnet tool install --global Eir.AutoMate

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

```json
{
    "watchs": [
        {
            -- Watch 1
        },
        {
            -- Watch 2
        },
    "clearScreenTime": 30
    ]
}
```

clearScreenTime is in seconds. If a watch is triggered this many seconds after the last watch was 
triggered, the console screen will be cleared of all previous output.

Each watch has the following structure

```json
{
    "name": "{name}",
	"filter" : "{filter}",
    "watchPaths": ["{watch path 1}, {watch path 2}]",
    "workingDir": "{working dir}",
	"cmdPath": "{command path}",
	"cmdArgs": "{command arguments}"
}
```

| name | meaning |
| ---- | -------------- |
| {name} | Name of the watch |
| {filter} | Optional file filter to specify what files to monitor for changes. Defaults to "*.*". |
| {watch paths} | Array of directory to monitor for modified files.  |
| {working dir} | Sets the working dir used when a command is executed. |
| {cmdPath} | Command to execute whan modified files are detected. |
| {cmdArgs} | Optional command arguments for {cmdPath}. Defaults to "". |


## Environment Variables

the command paths, arguments and working direcories may contain 
windows environment variables ('%{name}%') which will be expanded when run.

The following special environment variables will be set by AutoMate when running:

| Variable Name | Variable Value |
| ------------- | -------------- |
| ApplicationData | Environment.SpecialFolder.ApplicationData |
| LocalApplicationData | Environment.SpecialFolder.LocalApplicationData |
| MyDocuments | Environment.SpecialFolder.MyDocuments |
| ProgramFiles | Environment.SpecialFolder.ProgramFiles |
| ProgramFilesX86 | Environment.SpecialFolder.ProgramFilesX86 |
| Programs | Environment.SpecialFolder.Programs |
| System | Environment.SpecialFolder.System |
| SystemX86 | Environment.SpecialFolder.SystemX86 |
| UserProfile | Environment.SpecialFolder.UserProfile |
| Windows | Environment.SpecialFolder.Windows |
