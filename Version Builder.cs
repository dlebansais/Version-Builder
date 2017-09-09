using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace VersionBuilder
{
    public static class VersionBuilder
    {
        public static void Main(string[] Args)
        {
            if (Args.Length > 1)
            {
                string SolutionFolder = Path.GetDirectoryName(Args[0]);
                string ProjectFolder = Path.GetDirectoryName(Args[1]);
                string MainProjectName = (Args.Length > 2) ? Args[2] : "";

                List<string> SolutionFileList = new List<string>();
                List<string> SolutionInfoList = new List<string>();
                EnumerateFiles(null, SolutionFolder, SolutionFileList, SolutionInfoList);

                List<string> ProjectFileList = new List<string>();
                List<string> ProjectInfoList = new List<string>();
                EnumerateFiles(ProjectFolder, ProjectFolder, ProjectFileList, ProjectInfoList);

                if (SolutionInfoList.Count > 0)
                {
                    DateTime SolutionLatestTimeUtc = LatestOutOfDateFile(SolutionFileList, SolutionInfoList);
                    DateTime ProjectLatestTimeUtc = LatestOutOfDateFile(ProjectFileList, ProjectInfoList);

                    if (SolutionLatestTimeUtc != DateTime.MinValue || ProjectLatestTimeUtc != DateTime.MinValue)
                        IncrementVersionNumber(SolutionLatestTimeUtc, SolutionInfoList, ProjectLatestTimeUtc, ProjectInfoList, MainProjectName);
                }
            }
            else
            {
                string ExeName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
                Console.WriteLine(ExeName + " enumerates files in a solution for a given project. If any file has been updated, increases the project's version number, and the solution's product number if the updated file is the newest for all projects.");
                Console.WriteLine("Use: " + ExeName + " <solution folder> <project folder>");
            }
        }

        private static string CleanedUpFolderPath(string Folder)
        {
            string Result = Folder;
            while (Result.Length > 2 && Result[Result.Length - 1] == '\\')
                Result = Result.Substring(0, Result.Length - 1);

            return Folder;
        }

        private static void EnumerateFiles(string BaseFolder, string Folder, List<string> FileList, List<string> InfoList)
        {
            string[] Files = Directory.GetFiles(Folder);
            foreach (string File in Files)
            {
                if (IsProjectFile(File))
                {
                    BaseFolder = Folder;
                    FileList.Add(File);
                }

                else if (IsAssemblyInfoFile(BaseFolder, File))
                    InfoList.Add(File);

                else if (IsSourceFile(File))
                    FileList.Add(File);
            }

            string[] Subfolders = Directory.GetDirectories(Folder);
            foreach (string Subfolder in Subfolders)
                if (IsSourceFolder(BaseFolder, Subfolder))
                    EnumerateFiles(BaseFolder, Subfolder, FileList, InfoList);
        }

        private static bool IsSourceFolder(string BaseFolder, string Folder)
        {
            if (BaseFolder != null)
            {
                string BinFolder = Path.Combine(BaseFolder, "bin");
                string ObjFolder = Path.Combine(BaseFolder, "obj");

                if (Folder.StartsWith(BinFolder))
                    return false;

                else if (Folder.StartsWith(ObjFolder))
                    return false;

                else
                    return true;
            }

            else
                return true;
        }

        private static bool IsProjectFile(string File)
        {
            string FileExtension = Path.GetExtension(File);
            return FileExtension == ".csproj";
        }

        private static bool IsAssemblyInfoFile(string BaseFolder, string File)
        {
            if (BaseFolder != null)
            {
                string AssemblyInfoFile = Path.Combine(BaseFolder, "Properties", "AssemblyInfo.cs");

                if (File == AssemblyInfoFile)
                    return true;
            }

            return false;
        }

        private static bool IsSourceFile(string File)
        {
            string FileExtension = Path.GetExtension(File);
            return FileExtension != ".suo";
        }

        private static DateTime LatestOutOfDateFile(List<string> FileList, List<string> InfoList)
        {
            DateTime LastFileWriteTime = DateTime.MinValue;
            foreach (string SourceFile in FileList)
            {
                try
                {
                    DateTime LastWriteTime = File.GetLastWriteTimeUtc(SourceFile);
                    if (LastFileWriteTime < LastWriteTime)
                        LastFileWriteTime = LastWriteTime;
                }
                catch
                {
                }
            }

            DateTime LastInfoWriteTime = DateTime.MinValue;
            foreach (string InfoFile in InfoList)
            {
                try
                {
                    DateTime LastWriteTime = File.GetLastWriteTimeUtc(InfoFile);
                    if (LastInfoWriteTime < LastWriteTime)
                        LastInfoWriteTime = LastWriteTime;
                }
                catch
                {
                }
            }

            if (LastInfoWriteTime != DateTime.MinValue && LastInfoWriteTime < LastFileWriteTime)
                return LastFileWriteTime;
            else
                return DateTime.MinValue;
        }

        private static void IncrementVersionNumber(DateTime SolutionLatestTimeUtc, List<string> SolutionInfoList, DateTime ProjectLatestTimeUtc, List<string> ProjectInfoList, string MainProjectName)
        {
            string SolutionLine = null;

            foreach (string AssemblyInfoFile in SolutionInfoList)
                if (IsMainAssemblyInfoFile(MainProjectName, AssemblyInfoFile))
                    IncrementFileVersionNumber(SolutionLatestTimeUtc, SolutionInfoList, ProjectLatestTimeUtc, ProjectInfoList, MainProjectName, AssemblyInfoFile, ref SolutionLine);

            foreach (string AssemblyInfoFile in SolutionInfoList)
                if (!IsMainAssemblyInfoFile(MainProjectName, AssemblyInfoFile))
                    IncrementFileVersionNumber(SolutionLatestTimeUtc, SolutionInfoList, ProjectLatestTimeUtc, ProjectInfoList, MainProjectName, AssemblyInfoFile, ref SolutionLine);
        }

        private static bool IsMainAssemblyInfoFile(string MainProjectName, string AssemblyInfoFile)
        {
            return MainProjectName.Length > 0 && AssemblyInfoFile.EndsWith(Path.Combine(MainProjectName, "Properties", "AssemblyInfo.cs"));
        }

        private static void IncrementFileVersionNumber(DateTime SolutionLatestTimeUtc, List<string> SolutionInfoList, DateTime ProjectLatestTimeUtc, List<string> ProjectInfoList, string MainProjectName, string AssemblyInfoFile, ref string SolutionLine)
        {
            bool FileNeedsUpdate = false;

            List<string> FileContent;
            DateTime FileWriteTimeUtc;
            int ProductVersionLineIndex;
            int AssemblyVersionLineIndex;

            ReadVersionFile(AssemblyInfoFile, out FileContent, out FileWriteTimeUtc, out ProductVersionLineIndex, out AssemblyVersionLineIndex);

            if (SolutionLatestTimeUtc != DateTime.MinValue && ProductVersionLineIndex >= 0)
            {
                if (SolutionLine == null)
                {
                    string VersionLine = FileContent[ProductVersionLineIndex];
                    SolutionLine = GetLineWithIncrementedVersion(VersionLine, ProductVersionStart, VersionEnd);
                }

                FileContent[ProductVersionLineIndex] = SolutionLine;
                FileNeedsUpdate = true;
            }

            if (ProjectLatestTimeUtc != DateTime.MinValue && ProjectInfoList.Contains(AssemblyInfoFile) && AssemblyVersionLineIndex >= 0)
            {
                string VersionLine = FileContent[AssemblyVersionLineIndex];
                FileContent[AssemblyVersionLineIndex] = GetLineWithIncrementedVersion(VersionLine, AssemblyVersionStart, VersionEnd);

                FileNeedsUpdate = true;
                FileWriteTimeUtc = ProjectLatestTimeUtc;
            }

            if (FileNeedsUpdate)
                WriteVersionFile(AssemblyInfoFile, FileContent, FileWriteTimeUtc);
        }

        private static void ReadVersionFile(string AssemblyInfoFile, out List<string> FileContent, out DateTime FileWriteTimeUtc, out int ProductVersionLineIndex, out int AssemblyVersionLineIndex)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryReadVersionFile(AssemblyInfoFile, out FileContent, out FileWriteTimeUtc, out ProductVersionLineIndex, out AssemblyVersionLineIndex) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryReadVersionFile(string AssemblyInfoFile, out List<string> FileContent, out DateTime FileWriteTimeUtc, out int ProductVersionLineIndex, out int AssemblyVersionLineIndex)
        {
            ProductVersionLineIndex = -1;
            AssemblyVersionLineIndex = -1;

            try
            {
                using (FileStream fs = new FileStream(AssemblyInfoFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        FileContent = new List<string>();

                        for (; ; )
                        {
                            string Line = sr.ReadLine();
                            if (Line == null)
                                break;

                            if (IsVersionLine(Line, ProductVersionStart, VersionEnd))
                                ProductVersionLineIndex = FileContent.Count;

                            if (IsVersionLine(Line, AssemblyVersionStart, VersionEnd))
                                AssemblyVersionLineIndex = FileContent.Count;

                            FileContent.Add(Line);
                        }
                    }
                }

                FileWriteTimeUtc = File.GetLastWriteTimeUtc(AssemblyInfoFile);

                return true;
            }
            catch
            {
                FileContent = null;
                FileWriteTimeUtc = DateTime.MinValue;
                return false;
            }
        }

        private static void WriteVersionFile(string AssemblyInfoFile, List<string> FileContent, DateTime FileWriteTimeUtc)
        {
            int MaxTry = 5;
            int CurrentTry = 0;

            while (!TryWriteVersionFile(AssemblyInfoFile, FileContent, FileWriteTimeUtc) && CurrentTry++ < MaxTry)
                Thread.Sleep(500);
        }

        private static bool TryWriteVersionFile(string AssemblyInfoFile, List<string> FileContent, DateTime FileWriteTimeUtc)
        {
            try
            {
                using (FileStream fs = new FileStream(AssemblyInfoFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        foreach (string Line in FileContent)
                            sw.WriteLine(Line);
                    }
                }

                File.SetLastWriteTimeUtc(AssemblyInfoFile, FileWriteTimeUtc);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static readonly string ProductVersionStart = "[assembly: AssemblyFileVersion(\"";
        private static readonly string AssemblyVersionStart = "[assembly: AssemblyVersion(\"";
        private static readonly string VersionEnd = "\")]";

        private static bool IsVersionLine(string Line, string LineStart, string LineEnd)
        {
            return Line.StartsWith(LineStart) && Line.EndsWith(LineEnd);
        }

        private static string GetLineWithIncrementedVersion(string Line, string LineStart, string LineEnd)
        {
            string VersionString = Line.Substring(LineStart.Length, Line.Length - LineStart.Length - LineEnd.Length);
            string[] VersionParts = VersionString.Split('.');

            string ModifiedVersionString = VersionString;
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

            return LineStart + ModifiedVersionString + LineEnd;
        }
    }
}
