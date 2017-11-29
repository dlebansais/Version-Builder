# Version-Builder
This program enumerates files in a solution for a given project. If any file has been updated, it increases the project's version number, and the solution's product number if the updated file is the newest for all projects.

## Download
The latest version for Windows 64-bits can be found [here](https://github.com/dlebansais/Version-Builder/releases)

# How to use it
VersionBuilder.exe is a console program.

Use: `VersionBuilder <solution file> <project file> [<version project name>]`

`<solution file>` is the .sln file for the solution to update.

`<project file>` is the .csproj project file.

`<version project name>` (optional) is the name of the version project in the solution.

In the project folder, Properties/AssemblyInfo.cs contains the following lines (actual version numbers will of course differ) :

* [assembly: AssemblyVersion("1.3.0.1602")]
* [assembly: AssemblyFileVersion("1.3.0.6527")]

If the project version number has changed, VersionBuilder.exe will increment the number in the AssemblyVersion tag.

If the solution version number has changed, VersionBuilder.exe will increment the number in the AssemblyFileVersion tag (this is not a typo). This number appears as the "Product Version" when looking at detailed properties of a program.

## Automatic Versioning

If you put a call to VersionBuilder in Pre-Build events, your project will be recompiled with a new version number every time a file has changed, and the version number will not be incremented if you just do a "build all".

## Version project (optional)

In the case of a solution with several projects, for instance one .exe and several assemblies, the reference project that contains the product version for the whole solution could be another project than the .exe. This has the advantage that the product version can be incremented when one of the assemblies is updated, but not the .exe itself. When displaying its version number, the .exe will reference this version project's assembly.

To use this (optional) feature, specify the name of the project -not the project file name- as the last parameter.

# Certification

This program is digitally signed with a [CAcert](https://www.cacert.org/) certificate.

