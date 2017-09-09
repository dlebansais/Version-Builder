# Version-Builder
This program enumerates files in a solution for a given project. If any file has been updated, it increases the project's version number, and the solution's product number if the updated file is the newest for all projects.

# How to use it
VersionBuilder.exe is a console program.

Use: `VersionBuilder <solution folder> <project folder>`

`<solution folder>` is the folder where the .sln file is found.

`<project folder>` is a sub-folder of `<solution folder>` with a .csproj project file.

In the project folder, Properties/AssemblyInfo.cs contains the following lines (actual version numbers will of course differ) :

* [assembly: AssemblyVersion("1.3.0.1602")]
* [assembly: AssemblyFileVersion("1.3.0.6527")]

If the project version number has changed, VersionBuilder.exe will increment the number in the AssemblyVersion tag.

If the solution version number has changed, VersionBuilder.exe will increment the number in the AssemblyFileVersion tag (this is not a typo). This number appears as the "Product Version" when looking at detailed properties of a program.



