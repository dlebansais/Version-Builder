using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace VersionBuilder
{
    public class VersionBuilder
    {
        public static void Main(string[] Args)
        {
            if (Args.Length >= 1)
            {
                string SolutionFile = Args[0];
                bool IsVerbose = (Args.Length >= 2 && Args[1] == "-v");

                if (IsVerbose)
                    Echo("Checking \"" + SolutionFile + "\"");

                CheckSolutionVersion(SolutionFile, IsVerbose);
            }
            else
            {
                string ExeName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
                Console.WriteLine(ExeName + " enumerates files in a solution for a given project. If any file has been updated, increases the project's version number, and the solution's product number if the updated file is the newest for all projects.");
                Console.WriteLine("Use: " + ExeName + " <solution file> [-v for Verbose mode]");
            }
        }

        private static void Echo(string s)
        {
            Console.WriteLine(s);
        }

        private static void CheckSolutionVersion(string SolutionFile, bool IsVerbose)
        {
            List<string> ProjectFileList;
            ParseSolutionFile(SolutionFile, out ProjectFileList);

            bool IsInfoFileUpdated = false;
            List<string> InfoFileList = new List<string>();
            foreach (string ProjectFile in ProjectFileList)
            {
                List<string> SourceFileList;
                string InfoFile;
                ParseProjectFile(ProjectFile, out SourceFileList, out InfoFile);

                if (InfoFile != null)
                {
                    InfoFileList.Add(InfoFile);

                    DateTime InfoFileWriteTimeUtc = File.GetLastWriteTimeUtc(InfoFile);
                    DateTime LastSourceFileWriteTimeUtc = DateTime.MinValue;
                    string MostRecentFile = null;
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
                        string NewVersionNumber = null;
                        IncrementVersionNumber(InfoFile, ProductVersionTag, true, ref NewVersionNumber);

                        if (IsVerbose)
                            Console.WriteLine("Project \"" + ProjectFile + "\" updated to " + NewVersionNumber + ", most recent file: \"" + MostRecentFile + "\"");
                    }
                }
            }

            if (IsInfoFileUpdated)
            {
                string NewVersionNumber = null;
                foreach (string InfoFile in InfoFileList)
                    IncrementVersionNumber(InfoFile, AssemblyVersionTag, false, ref NewVersionNumber);

                if (IsVerbose && NewVersionNumber != null)
                    Console.WriteLine("Solution updated to " + NewVersionNumber);
            }
        }

        private static void ParseSolutionFile(string SolutionFile, out List<string> ProjectFileList)
        {
            List<string> Result = new List<string>();
            string SolutionFolder = Path.GetDirectoryName(SolutionFile);

            try
            {
                using (FileStream fs = new FileStream(SolutionFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            Line = Line.Trim();
                            if (!Line.StartsWith("Project("))
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

                ProjectFileList = Result;
            }
            catch
            {
                ProjectFileList = new List<string>();
            }
        }

        private static bool ParseIncludeLine(string Line, out string SourceFile)
        {
            SourceFile = null;

            if (!Line.StartsWith("<Compile"))
                return false;

            string[] KeyValue = Line.Substring(8).Split('=');
            if (KeyValue.Length != 2 || KeyValue[0].Trim() != "Include")
                return false;

            SourceFile = KeyValue[1].Trim();
            if (SourceFile.EndsWith("/>"))
                SourceFile = SourceFile.Substring(0, SourceFile.Length - 2).Trim();
            else if (SourceFile.EndsWith(">"))
                SourceFile = SourceFile.Substring(0, SourceFile.Length - 1).Trim();
            if (SourceFile.Length < 2 || SourceFile[0] != '"' || SourceFile[SourceFile.Length - 1] != '"')
                return false;

            SourceFile = SourceFile.Substring(1, SourceFile.Length - 2);

            return true;
        }

        private static bool ParseDependentUponLine(string Line, out string SourceFile)
        {
            SourceFile = null;

            string StartPattern = "<DependentUpon>";
            string EndPattern = "</DependentUpon>";
            if (!Line.StartsWith(StartPattern) || !Line.EndsWith(EndPattern))
                return false;

            SourceFile = Line.Substring(StartPattern.Length, Line.Length - StartPattern.Length - EndPattern.Length);

            return true;
        }

        private static void ParseProjectFile(string ProjectFile, out List<string> SourceFileList, out string InfoFile)
        {
            List<string> Result = new List<string>();
            InfoFile = null;
            string ProjectFolder = Path.GetDirectoryName(ProjectFile);

            try
            {
                using (FileStream fs = new FileStream(ProjectFile, FileMode.Open, FileAccess.Read, FileShare.Read))
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

                            bool IsInfoFile = (SourceFile == @"Properties\AssemblyInfo.cs");

                            SourceFile = Path.Combine(ProjectFolder, SourceFile);
                            Result.Add(SourceFile);

                            if (IsInfoFile)
                                InfoFile = SourceFile;
                        }
                    }
                }

                if (InfoFile != null && Result.Contains(InfoFile))
                    Result.Remove(InfoFile);

                SourceFileList = Result;
            }
            catch
            {
                SourceFileList = new List<string>();
            }
        }

        private static void IncrementVersionNumber(string InfoFile, string Tag, bool ChangeFileTime, ref string NewVersionNumber)
        {
            List<string> FileContent;
            DateTime FileWriteTimeUtc;
            int VersionLineIndex;

            ReadVersionFile(InfoFile, Tag, out FileContent, out FileWriteTimeUtc, out VersionLineIndex);
            string VersionLine = FileContent[VersionLineIndex];
            VersionLine = GetLineWithIncrementedVersion(VersionLine, Tag, VersionEnd, ref NewVersionNumber);

            FileContent[VersionLineIndex] = VersionLine;

            if (ChangeFileTime)
                FileWriteTimeUtc = DateTime.UtcNow;

            WriteVersionFile(InfoFile, FileContent, FileWriteTimeUtc);
        }

        private static readonly string ProductVersionTag = "[assembly: AssemblyFileVersion(\"";
        private static readonly string AssemblyVersionTag = "[assembly: AssemblyVersion(\"";
        private static readonly string VersionEnd = "\")]";

        private static bool IsVersionLine(string Line, string Tag, string LineEnd)
        {
            return Line.StartsWith(Tag) && Line.EndsWith(LineEnd);
        }

        private static string GetLineWithIncrementedVersion(string Line, string Tag, string LineEnd, ref string VersionNumber)
        {
            string ModifiedVersionString;

            if (VersionNumber == null)
            {
                string VersionString = Line.Substring(Tag.Length, Line.Length - Tag.Length - LineEnd.Length);
                string[] VersionParts = VersionString.Split('.');

                ModifiedVersionString = VersionString;
                if (VersionParts.Length > 2)
                {
                    int BuildNumber;
                    if (int.TryParse(VersionParts[VersionParts.Length - 1], out BuildNumber))
                    {
                        int NewBuildNumber = BuildNumber + 1;
                        VersionParts[VersionParts.Length - 1] = NewBuildNumber.ToString();

                        ModifiedVersionString = "";
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

                VersionNumber = ModifiedVersionString;
            }
            else
                ModifiedVersionString = VersionNumber;

            return Tag + ModifiedVersionString + LineEnd;
        }

        private static void ReadVersionFile(string InfoFile, string Tag, out List<string> FileContent, out DateTime FileWriteTimeUtc, out int VersionLineIndex)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryReadVersionFile(InfoFile, Tag, out FileContent, out FileWriteTimeUtc, out VersionLineIndex) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryReadVersionFile(string InfoFile, string Tag, out List<string> FileContent, out DateTime FileWriteTimeUtc, out int VersionLineIndex)
        {
            VersionLineIndex = -1;

            try
            {
                using (FileStream fs = new FileStream(InfoFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        FileContent = new List<string>();

                        for (;;)
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            if (IsVersionLine(Line, Tag, VersionEnd))
                                VersionLineIndex = FileContent.Count;

                            FileContent.Add(Line);
                        }
                    }
                }

                FileWriteTimeUtc = File.GetLastWriteTimeUtc(InfoFile);

                return true;
            }
            catch
            {
                FileContent = null;
                FileWriteTimeUtc = DateTime.MinValue;
                return false;
            }
        }

        private static void WriteVersionFile(string InfoFile, List<string> FileContent, DateTime FileWriteTimeUtc)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryWriteVersionFile(InfoFile, FileContent, FileWriteTimeUtc) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryWriteVersionFile(string InfoFile, List<string> FileContent, DateTime FileWriteTimeUtc)
        {
            try
            {
                using (FileStream fs = new FileStream(InfoFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        foreach (string Line in FileContent)
                            sw.WriteLine(Line);
                    }
                }

                File.SetLastWriteTimeUtc(InfoFile, FileWriteTimeUtc);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
