namespace VersionBuilder
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a project.
    /// </summary>
    public abstract class ProjectInfo
    {
        /// <summary>
        /// Gets the neutral project element.
        /// </summary>
        public static ProjectInfo None { get; } = ProjectInfoNone.Singleton;

        /// <summary>
        /// Gets the list of source files.
        /// </summary>
        public abstract List<string> SourceFileList { get; }

        /// <summary>
        /// Gets the file with version information.
        /// </summary>
        public abstract string InfoFile { get; }

        /// <summary>
        /// Gets tags that surround the product version.
        /// </summary>
        public abstract VersionTag ProductVersionTag { get; }

        /// <summary>
        /// Gets tags that surround the assembly version.
        /// </summary>
        public abstract VersionTag AssemblyVersionTag { get; }
    }
}
