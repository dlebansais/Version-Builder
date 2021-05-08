namespace VersionBuilder
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a .NET Core project.
    /// </summary>
    public class ProjectInfoDotNetCore : ProjectInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectInfoDotNetCore"/> class.
        /// </summary>
        /// <param name="sourceFileList">Tthe list of source files.</param>
        /// <param name="infoFile">The file with version information.</param>
        public ProjectInfoDotNetCore(List<string> sourceFileList, string infoFile)
        {
            SourceFileList = sourceFileList;
            InfoFile = infoFile;
        }

        /// <summary>
        /// Gets the list of source files.
        /// </summary>
        public override List<string> SourceFileList { get; }

        /// <summary>
        /// Gets the file with version information.
        /// </summary>
        public override string InfoFile { get; }

        /// <summary>
        /// Gets the tag that starts the product version.
        /// </summary>
        public override VersionTag ProductVersionTag { get; } = new VersionTag("<FileVersion>", "</FileVersion>");

        /// <summary>
        /// Gets the tag that starts the assembly version.
        /// </summary>
        public override VersionTag AssemblyVersionTag { get; } = new VersionTag("<AssemblyVersion>", "</AssemblyVersion>");
    }
}
