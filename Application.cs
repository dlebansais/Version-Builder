namespace VersionBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Updates the version number in a project based on file timestamps changes.
    /// </summary>
    public static class Application
    {
        /// <summary>
        /// Entry point of the application.
        /// </summary>
        /// <param name="arguments">Command-line arguments.</param>
        public static void Main(string[] arguments)
        {
            if (arguments == null)
                arguments = Array.Empty<string>();

            if (!ParseCommandLineArguments(arguments, out bool IsVerbose, out string SolutionFile, out string MainProjectFile))
            {
                string ExeName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

                Echo($"{ExeName} enumerates files in a solution for a given project. If any file has been updated, increases the project's version number, and the solution's product number if the updated file is the newest for all projects.");
                Echo($"Use: {ExeName} <solution file> [main project file] [-v for Verbose mode]");
                Echo("If a main project file is provided, its version number is increased at the same time than the solution version.");
            }

            if (IsVerbose)
                Echo("Checking \"" + SolutionFile + "\"");

            CheckSolutionVersion(SolutionFile, MainProjectFile, IsVerbose);
        }

        private static bool ParseCommandLineArguments(string[] arguments, out bool isVerbose, out string solutionFile, out string mainProjectFile)
        {
            isVerbose = false;
            solutionFile = string.Empty;
            mainProjectFile = string.Empty;

            foreach (string Argument in arguments)
                if (Argument == "-v")
                    isVerbose = true;
                else if (solutionFile.Length == 0)
                    solutionFile = Argument;
                else if (mainProjectFile.Length == 0)
                    mainProjectFile = Argument;

            return solutionFile.Length > 0;
        }

        private static void Echo(string s)
        {
            Console.WriteLine(s);
        }

        private static void CheckSolutionVersion(string solutionFile, string mainProjectFile, bool isVerbose)
        {
            List<string> ProjectFileList;
            ParseSolutionFile(solutionFile, out ProjectFileList);

            bool IsInfoFileUpdated = false;
            string NewVersionNumber = string.Empty;
            List<ProjectInfo> ProjectList = new List<ProjectInfo>();
            int MainProjectIndex = -1;

            foreach (string ProjectFile in ProjectFileList)
            {
                if (ParseProjectFile(ProjectFile, out ProjectInfo Project))
                {
                    ProjectList.Add(Project);

                    if (mainProjectFile == ProjectFile)
                        MainProjectIndex = ProjectList.IndexOf(Project);

                    DateTime InfoFileWriteTimeUtc = File.GetLastWriteTimeUtc(Project.InfoFile);
                    DateTime LastSourceFileWriteTimeUtc = DateTime.MinValue;
                    string MostRecentFile = string.Empty;
                    foreach (string SourceFile in Project.SourceFileList)
                    {
                        DateTime SourceFileWriteTimeUtc = File.GetLastWriteTimeUtc(SourceFile);
                        if (LastSourceFileWriteTimeUtc < SourceFileWriteTimeUtc)
                        {
                            LastSourceFileWriteTimeUtc = SourceFileWriteTimeUtc;
                            MostRecentFile = SourceFile;
                        }
                    }

                    DateTime ProjectFileWriteTimeUtc = File.GetLastWriteTimeUtc(ProjectFile);
                    if (LastSourceFileWriteTimeUtc < ProjectFileWriteTimeUtc)
                    {
                        LastSourceFileWriteTimeUtc = ProjectFileWriteTimeUtc;
                        MostRecentFile = ProjectFile;
                    }

                    if (LastSourceFileWriteTimeUtc > InfoFileWriteTimeUtc)
                    {
                        IsInfoFileUpdated = true;
                        NewVersionNumber = string.Empty;
                        IncrementVersionNumber(Project, Project.ProductVersionTag, true, ref NewVersionNumber);

                        if (isVerbose)
                            Console.WriteLine("Project \"" + ProjectFile + "\" updated to " + NewVersionNumber + ", most recent file: \"" + MostRecentFile + "\"");

                        if (MainProjectIndex == ProjectList.IndexOf(Project)) // Don't update the main project twice.
                            MainProjectIndex = -1;

                        UpdateNuget(Path.GetDirectoryName(ProjectFile), NewVersionNumber);
                    }
                }
            }

            if (IsInfoFileUpdated)
            {
                if (MainProjectIndex >= 0)
                {
                    NewVersionNumber = string.Empty;
                    ProjectInfo MainProject = ProjectList[MainProjectIndex];
                    IncrementVersionNumber(MainProject, MainProject.ProductVersionTag, true, ref NewVersionNumber);

                    if (isVerbose)
                        Console.WriteLine("Main Project \"" + mainProjectFile + "\" updated to " + NewVersionNumber);
                }

                NewVersionNumber = string.Empty;
                foreach (ProjectInfo Project in ProjectList)
                    IncrementVersionNumber(Project, Project.AssemblyVersionTag, false, ref NewVersionNumber);

                if (isVerbose && NewVersionNumber.Length > 0)
                    Console.WriteLine("Solution updated to " + NewVersionNumber);
            }
        }

        private static void ParseSolutionFile(string solutionFile, out List<string> projectFileList)
        {
            List<string> Result = new List<string>();
            string SolutionFolder = Path.GetDirectoryName(solutionFile);

            try
            {
                using (FileStream fs = new FileStream(solutionFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            Line = Line.Trim();
                            if (!Line.StartsWith("Project(", StringComparison.InvariantCulture))
                                continue;

                            string[] KeyValue = Line.Split('=');
                            if (KeyValue.Length != 2)
                                continue;

                            string[] Fields = KeyValue[1].Split(',');
                            if (Fields.Length != 3)
                                continue;

                            string ProjectFile = Fields[1].Trim();
                            if (ProjectFile.Length < 2 || ProjectFile[0] != '"' || ProjectFile[ProjectFile.Length - 1] != '"')
                                continue;

                            ProjectFile = ProjectFile.Substring(1, ProjectFile.Length - 2);
                            ProjectFile = Path.Combine(SolutionFolder, ProjectFile);
                            Result.Add(ProjectFile);
                        }
                    }
                }

                projectFileList = Result;
            }
            catch
            {
                projectFileList = new List<string>();
            }
        }

        private static bool ParseIncludeLine(string line, out string sourceFile)
        {
            sourceFile = string.Empty;

            if (!line.StartsWith("<Compile", StringComparison.InvariantCulture))
                return false;

            string[] KeyValue = line.Substring(8).Split('=');
            if (KeyValue.Length != 2 || KeyValue[0].Trim() != "Include")
                return false;

            sourceFile = KeyValue[1].Trim();
            if (sourceFile.EndsWith("/>", StringComparison.InvariantCulture))
                sourceFile = sourceFile.Substring(0, sourceFile.Length - 2).Trim();
            else if (sourceFile.EndsWith(">", StringComparison.InvariantCulture))
                sourceFile = sourceFile.Substring(0, sourceFile.Length - 1).Trim();
            if (sourceFile.Length < 2 || sourceFile[0] != '"' || sourceFile[sourceFile.Length - 1] != '"')
                return false;

            sourceFile = sourceFile.Substring(1, sourceFile.Length - 2);

            return true;
        }

        private static bool ParseDependentUponLine(string line, out string sourceFile)
        {
            sourceFile = string.Empty;

            string StartPattern = "<DependentUpon>";
            string EndPattern = "</DependentUpon>";
            if (!line.StartsWith(StartPattern, StringComparison.InvariantCulture) || !line.EndsWith(EndPattern, StringComparison.InvariantCulture))
                return false;

            sourceFile = line.Substring(StartPattern.Length, line.Length - StartPattern.Length - EndPattern.Length);

            return true;
        }

        private static bool ParseProjectFile(string projectFile, out ProjectInfo project)
        {
            project = ProjectInfo.None;

            if (!File.Exists(projectFile))
                return false;

            string ProjectFolder = Path.GetDirectoryName(projectFile);

            try
            {
                using FileStream Stream = new FileStream(projectFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using StreamReader Reader = new StreamReader(Stream, Encoding.UTF8);

                string Line = Reader.ReadLine().Trim();

                if (Line.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk", StringComparison.InvariantCulture))
                    return ParseDotNetCoreProjectFile(ProjectFolder, projectFile, out project);
                else
                    return ParseDotNetFrameworkProjectFile(ProjectFolder, Reader, out project);
            }
            catch
            {
                return false;
            }
        }

        private static bool ParseDotNetCoreProjectFile(string projectFolder, string projectFile, out ProjectInfo project)
        {
            ParseDirectory(projectFolder, ".cs", new List<string>() { "bin", "obj" }, out List<string> SourceFileList);
            project = new ProjectInfoDotNetCore(SourceFileList, projectFile);
            return true;
        }

        private static void ParseDirectory(string directory, string expectedExtension, List<string> ignoredDirectoryList, out List<string> fileList)
        {
            fileList = new List<string>();

            string[] Files = Directory.GetFiles(directory);
            foreach (string File in Files)
            {
                string Extension = Path.GetExtension(File);
                bool IsDirectory = Directory.Exists(File);

                if (Extension == expectedExtension)
                    fileList.Add(File);
                else if (IsDirectory)
                {
                    string DirectoryName = Path.GetFileName(File);

                    if (!ignoredDirectoryList.Contains(DirectoryName))
                    {
                        ParseDirectory(File, expectedExtension, new List<string>(), out List<string> SubdirectoryFileList);
                        fileList.AddRange(SubdirectoryFileList);
                    }
                }
            }
        }

        private static bool ParseDotNetFrameworkProjectFile(string projectFolder, StreamReader reader, out ProjectInfo project)
        {
            string InfoFile = string.Empty;
            List<string> Result = new List<string>();

            for (;;)
            {
                string Line = reader.ReadLine();
                if (Line == null)
                    break;

                Line = Line.Trim();

                string SourceFile;
                if (!ParseIncludeLine(Line, out SourceFile) && !ParseDependentUponLine(Line, out SourceFile))
                    continue;

                bool IsInfoFile = SourceFile == @"Properties\AssemblyInfo.cs";

                SourceFile = Path.Combine(projectFolder, SourceFile);
                Result.Add(SourceFile);

                if (IsInfoFile)
                    InfoFile = SourceFile;
            }

            if (InfoFile.Length > 0 && Result.Contains(InfoFile))
                Result.Remove(InfoFile);

            project = new ProjectInfoDotNetFramework(Result, InfoFile);
            return true;
        }

        private static void IncrementVersionNumber(ProjectInfo project, VersionTag tag, bool changeFileTime, ref string newVersionNumber)
        {
            List<string> FileContent;
            DateTime FileWriteTimeUtc;
            int VersionLineIndex;

            ReadVersionFile(project.InfoFile, tag, out FileContent, out FileWriteTimeUtc, out VersionLineIndex, out int Tabulation);
            if (VersionLineIndex >= 0)
            {
                string VersionLine = FileContent[VersionLineIndex];
                VersionLine = GetLineWithIncrementedVersion(VersionLine, Tabulation, tag, ref newVersionNumber);

                FileContent[VersionLineIndex] = VersionLine;

                if (changeFileTime)
                    FileWriteTimeUtc = DateTime.UtcNow;

                WriteVersionFile(project.InfoFile, FileContent, FileWriteTimeUtc);
            }
        }

        private static bool IsVersionLine(string line, VersionTag tag)
        {
            return line.StartsWith(tag.TagStart, StringComparison.InvariantCulture) && line.EndsWith(tag.TagEnd, StringComparison.InvariantCulture);
        }

        private static string GetLineWithIncrementedVersion(string line, int tabulation, VersionTag tag, ref string versionNumber)
        {
            string ModifiedVersionString;

            if (versionNumber.Length == 0)
            {
                string VersionString = line.Substring(tabulation + tag.TagStart.Length, line.Length - tag.TagStart.Length - tag.TagEnd.Length - tabulation);
                string[] VersionParts = VersionString.Split('.');

                if (VersionParts.Length > 2)
                {
                    int BuildNumber;
                    if (int.TryParse(VersionParts[VersionParts.Length - 1], out BuildNumber))
                    {
                        int NewBuildNumber = BuildNumber + 1;
                        VersionParts[VersionParts.Length - 1] = NewBuildNumber.ToString(CultureInfo.InvariantCulture);

                        ModifiedVersionString = string.Empty;
                        foreach (string Part in VersionParts)
                        {
                            if (ModifiedVersionString.Length > 0)
                                ModifiedVersionString += ".";

                            ModifiedVersionString += Part;
                        }
                    }
                    else
                        ModifiedVersionString = VersionString;
                }
                else
                    ModifiedVersionString = VersionString;

                versionNumber = ModifiedVersionString;
            }
            else
                ModifiedVersionString = versionNumber;

            string Prefix = string.Empty;
            while (tabulation > 0)
            {
                Prefix += " ";
                tabulation--;
            }

            return $"{Prefix}{tag.TagStart}{ModifiedVersionString}{tag.TagEnd}";
        }

        private static void ReadVersionFile(string infoFile, VersionTag tag, out List<string> fileContent, out DateTime fileWriteTimeUtc, out int versionLineIndex, out int tabulation)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryReadVersionFile(infoFile, tag, out fileContent, out fileWriteTimeUtc, out versionLineIndex, out tabulation) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryReadVersionFile(string infoFile, VersionTag tag, out List<string> fileContent, out DateTime fileWriteTimeUtc, out int versionLineIndex, out int tabulation)
        {
            versionLineIndex = -1;
            tabulation = 0;

            try
            {
                using FileStream Stream = new FileStream(infoFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using StreamReader Reader = new StreamReader(Stream, Encoding.UTF8);

                fileContent = new List<string>();

                for (;;)
                {
                    string Line = Reader.ReadLine();
                    if (Line == null)
                        break;

                    if (IsVersionLine(Line.Trim(), tag))
                    {
                        versionLineIndex = fileContent.Count;
                        tabulation = 0;

                        while (tabulation < Line.Length && Line[tabulation] == ' ')
                            tabulation++;
                    }

                    fileContent.Add(Line);
                }

                fileWriteTimeUtc = File.GetLastWriteTimeUtc(infoFile);

                return true;
            }
            catch
            {
                fileContent = new List<string>();
                fileWriteTimeUtc = DateTime.MinValue;
                return false;
            }
        }

        private static void WriteVersionFile(string infoFile, List<string> fileContent, DateTime fileWriteTimeUtc)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryWriteVersionFile(infoFile, fileContent, fileWriteTimeUtc) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryWriteVersionFile(string infoFile, List<string> fileContent, DateTime fileWriteTimeUtc)
        {
            try
            {
                using FileStream Stream = new FileStream(infoFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using StreamWriter Writer = new StreamWriter(Stream, Encoding.UTF8);

                foreach (string Line in fileContent)
                    Writer.WriteLine(Line);
            }
            catch
            {
                return false;
            }

            File.SetLastWriteTimeUtc(infoFile, fileWriteTimeUtc);
            return true;
        }

        private static void UpdateNuget(string projectPath, string versionNumber)
        {
            string[] Files = Directory.GetFiles(projectPath, "*.nuspec");
            if (Files.Length > 0)
            {
                string NugetFileName = Files[0];

                string Content = string.Empty;

                using (FileStream fs = new FileStream(NugetFileName, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.ASCII))
                    {
                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            string VersionTagStart = "<version>";
                            int VersionTagStartIndex = Line.IndexOf(VersionTagStart, StringComparison.InvariantCulture);
                            if (VersionTagStartIndex >= 0)
                            {
                                string VersionTagEnd = "</version>";
                                int VersionTagEndIndex = Line.IndexOf(VersionTagEnd, VersionTagStartIndex, StringComparison.InvariantCulture);

                                if (VersionTagEndIndex > VersionTagStartIndex + VersionTagStart.Length)
                                {
                                    string OldVersion = Line.Substring(VersionTagStartIndex + VersionTagStart.Length, VersionTagEndIndex - (VersionTagStartIndex + VersionTagStart.Length));
                                    string NewLine = Line.Substring(0, VersionTagStartIndex + VersionTagStart.Length) + versionNumber + Line.Substring(VersionTagEndIndex);

                                    Line = NewLine;
                                }
                            }

                            Content += Line + "\r\n";
                        }
                    }
                }

                using (FileStream fs = new FileStream(NugetFileName, FileMode.Create, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.ASCII))
                    {
                        sw.Write(Content);
                    }
                }
            }
        }
    }
}
