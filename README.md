# Version-Builder
This program enumerates files in a solution for a given project. If any file has been updated, it increases the project's version number, and the solution's product number if the updated file is the newest for all projects.

## Download
The latest version for Windows 64-bits can be found [here](https://github.com/dlebansais/Version-Builder/releases)

# How to use it
VersionBuilder.exe is a console program.

Use: `VersionBuilder <solution file> [main project file]`

`<solution file>` is the .sln file for the solution to update. `main project file` is an optional .csproj file to update at the time than the solution (see below).

In project folders, Properties/AssemblyInfo.cs contains the following lines (actual version numbers will of course differ) :

* [assembly: AssemblyVersion("1.3.0.1602")]
* [assembly: AssemblyFileVersion("1.3.0.6527")]

If the project version number has changed, VersionBuilder.exe will increment the number in the AssemblyVersion tag.

If the solution version number has changed, VersionBuilder.exe will increment the number in the AssemblyFileVersion tag (this is not a typo). This number appears as the "Product Version" when looking at detailed properties of a program.

## Automatic Versioning

If you put a call to VersionBuilder in Pre-Build events, your project will be recompiled with a new version number every time a file has changed, and the version number will not be incremented if you just do a "build all".

## Main Project

Sometimes, it is desirable to store a global version number of a group of assemblies. The optional `main project file` option serves this purpose. Specify a main project file in the Pre-Build event of each of the assemblies that belong to this group (the main project can be one of them), and that project will see its own version number incremented as well. 

# Certification

This program is digitally signed with a [CAcert](https://www.cacert.org/) certificate.

