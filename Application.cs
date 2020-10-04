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
                arguments = new string[0];

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
            List<string> InfoFileList = new List<string>();
            string MainInfoFile = string.Empty;
            string NewVersionNumber = string.Empty;

            foreach (string ProjectFile in ProjectFileList)
            {
                List<string> SourceFileList;
                string InfoFile;
                ParseProjectFile(ProjectFile, out SourceFileList, out InfoFile);

                if (InfoFile.Length > 0)
                {
                    InfoFileList.Add(InfoFile);

                    if (mainProjectFile == ProjectFile)
                        MainInfoFile = InfoFile;

                    DateTime InfoFileWriteTimeUtc = File.GetLastWriteTimeUtc(InfoFile);
                    DateTime LastSourceFileWriteTimeUtc = DateTime.MinValue;
                    string MostRecentFile = string.Empty;
                    foreach (string SourceFile in SourceFileList)
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
                        IncrementVersionNumber(InfoFile, ProductVersionTag, true, ref NewVersionNumber);

                        if (isVerbose)
                            Console.WriteLine("Project \"" + ProjectFile + "\" updated to " + NewVersionNumber + ", most recent file: \"" + MostRecentFile + "\"");

                        if (MainInfoFile == InfoFile) // Don't update the main project twice.
                            MainInfoFile = string.Empty;

                        UpdateNuget(Path.GetDirectoryName(ProjectFile), NewVersionNumber);
                    }
                }
            }

            if (IsInfoFileUpdated)
            {
                if (MainInfoFile.Length > 0)
                {
                    NewVersionNumber = string.Empty;
                    IncrementVersionNumber(MainInfoFile, ProductVersionTag, true, ref NewVersionNumber);

                    if (isVerbose)
                        Console.WriteLine("Main Project \"" + mainProjectFile + "\" updated to " + NewVersionNumber);
                }

                NewVersionNumber = string.Empty;
                foreach (string InfoFile in InfoFileList)
                    IncrementVersionNumber(InfoFile, AssemblyVersionTag, false, ref NewVersionNumber);

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

        private static void ParseProjectFile(string projectFile, out List<string> sourceFileList, out string infoFile)
        {
            List<string> Result = new List<string>();
            infoFile = string.Empty;
            string ProjectFolder = Path.GetDirectoryName(projectFile);

            try
            {
                using (FileStream fs = new FileStream(projectFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            Line = Line.Trim();

                            string SourceFile;
                            if (!ParseIncludeLine(Line, out SourceFile) && !ParseDependentUponLine(Line, out SourceFile))
                                continue;

                            bool IsInfoFile = SourceFile == @"Properties\AssemblyInfo.cs";

                            SourceFile = Path.Combine(ProjectFolder, SourceFile);
                            Result.Add(SourceFile);

                            if (IsInfoFile)
                                infoFile = SourceFile;
                        }
                    }
                }

                if (infoFile.Length > 0 && Result.Contains(infoFile))
                    Result.Remove(infoFile);

                sourceFileList = Result;
            }
            catch
            {
                sourceFileList = new List<string>();
            }
        }

        private static void IncrementVersionNumber(string infoFile, string tag, bool changeFileTime, ref string newVersionNumber)
        {
            List<string> FileContent;
            DateTime FileWriteTimeUtc;
            int VersionLineIndex;

            ReadVersionFile(infoFile, tag, out FileContent, out FileWriteTimeUtc, out VersionLineIndex);
            string VersionLine = FileContent[VersionLineIndex];
            VersionLine = GetLineWithIncrementedVersion(VersionLine, tag, VersionEnd, ref newVersionNumber);

            FileContent[VersionLineIndex] = VersionLine;

            if (changeFileTime)
                FileWriteTimeUtc = DateTime.UtcNow;

            WriteVersionFile(infoFile, FileContent, FileWriteTimeUtc);
        }

        private const string ProductVersionTag = "[assembly: AssemblyFileVersion(\"";
        private const string AssemblyVersionTag = "[assembly: AssemblyVersion(\"";
        private const string VersionEnd = "\")]";

        private static bool IsVersionLine(string line, string tag, string lineEnd)
        {
            return line.StartsWith(tag, StringComparison.InvariantCulture) && line.EndsWith(lineEnd, StringComparison.InvariantCulture);
        }

        private static string GetLineWithIncrementedVersion(string line, string tag, string lineEnd, ref string versionNumber)
        {
            string ModifiedVersionString;

            if (versionNumber == null)
            {
                string VersionString = line.Substring(tag.Length, line.Length - tag.Length - lineEnd.Length);
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

            return $"{tag}{ModifiedVersionString}{lineEnd}";
        }

        private static void ReadVersionFile(string infoFile, string tag, out List<string> fileContent, out DateTime fileWriteTimeUtc, out int versionLineIndex)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryReadVersionFile(infoFile, tag, out fileContent, out fileWriteTimeUtc, out versionLineIndex) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryReadVersionFile(string infoFile, string tag, out List<string> fileContent, out DateTime fileWriteTimeUtc, out int versionLineIndex)
        {
            versionLineIndex = -1;

            try
            {
                using (FileStream fs = new FileStream(infoFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        fileContent = new List<string>();

                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            if (IsVersionLine(Line, tag, VersionEnd))
                                versionLineIndex = fileContent.Count;

                            fileContent.Add(Line);
                        }
                    }
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
                using (FileStream fs = new FileStream(infoFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        foreach (string Line in fileContent)
                            sw.WriteLine(Line);
                    }
                }

                File.SetLastWriteTimeUtc(infoFile, fileWriteTimeUtc);
                return true;
            }
            catch
            {
                return false;
            }
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
