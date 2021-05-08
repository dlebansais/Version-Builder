namespace VersionBuilder
{
    /// <summary>
    /// Represents a couple of tags around a version number.
    /// </summary>
    public class VersionTag
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VersionTag"/> class.
        /// </summary>
        /// <param name="tagStart">The tag that starts the version.</param>
        /// <param name="tagEnd">The tag that ends the version.</param>
        public VersionTag(string tagStart, string tagEnd)
        {
            TagStart = tagStart;
            TagEnd = tagEnd;
        }

        /// <summary>
        /// Gets the tag that starts the version.
        /// </summary>
        public string TagStart { get; }

        /// <summary>
        /// Gets the tag that ends the version.
        /// </summary>
        public string TagEnd { get; }
    }
}
