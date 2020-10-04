namespace VersionBuilder
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a project.
    /// </summary>
    public class ProjectInfoNone : ProjectInfo
    {
        /// <summary>
        /// Gets the neutral project element.
        /// </summary>
        public static ProjectInfoNone Singleton { get; } = new ProjectInfoNone();

        /// <summary>
        /// Gets the list of source files.
        /// </summary>
        public override List<string> SourceFileList { get; } = new List<string>();

        /// <summary>
        /// Gets the file with version information.
        /// </summary>
        public override string InfoFile { get; } = string.Empty;

        /// <summary>
        /// Gets the tag that starts the product version.
        /// </summary>
        public override VersionTag ProductVersionTag { get; } = new VersionTag(string.Empty, string.Empty);

        /// <summary>
        /// Gets the tag that starts the assembly version.
        /// </summary>
        public override VersionTag AssemblyVersionTag { get; } = new VersionTag(string.Empty, string.Empty);
    }
}
