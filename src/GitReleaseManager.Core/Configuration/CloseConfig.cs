using System.ComponentModel;
using GitReleaseManager.Core.Attributes;
using YamlDotNet.Serialization;

namespace GitReleaseManager.Core.Configuration
{
    /// <summary>
    /// Class for holding configuration values used during milestone closing. This class cannot be inherited.
    /// </summary>
    public sealed class CloseConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether adding issue comments is enabled or not.
        /// </summary>
        [Description("Whether to add comments to issues closed and with the published milestone release.")]
        [YamlMember(Alias = "use-issue-comments")]
        public bool IssueComments { get; set; }

        /// <summary>
        /// Gets or sets the issue comment format to use when commenting on issues.
        /// </summary>
        [Sample(":tada: This issue has been resolved in version {milestone} :tada:\n\nThe release is available on:\n\n- [NuGet package(@{milestone})](https://nuget.org/packages/{repository}/{milestone})\n- [GitHub release](https://github.com/{owner}/{repository}/releases/tag/{milestone})\n\nYour **[GitReleaseManager](https://github.com/GitTools/GitReleaseManager)** bot :package::rocket:")]
        [YamlMember(Alias = "issue-comment", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string IssueCommentFormat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the due date should be set when closing the milestone.
        /// </summary>
        [Description("Whether to set the due date when closing the milestone.")]
        [YamlMember(Alias = "set-due-date")]
        public bool SetDueDate { get; set; }
    }
}