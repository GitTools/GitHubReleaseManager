using System.Collections.Generic;
using System.ComponentModel;
using YamlDotNet.Serialization;

namespace GitReleaseManager.Core.Configuration
{
    public class Config
    {
        internal const string ISSUE_COMMENT_FORMAT = @":tada: This issue has been resolved in version {milestone} :tada:

The release is available on:

- [GitHub release](https://github.com/{owner}/{repository}/releases/tag/{milestone})

Your **[GitReleaseManager](https://github.com/GitTools/GitReleaseManager)** bot :package: :rocket:";

        public Config()
        {
            Create = new CreateConfig
            {
                IncludeFooter = false,
                FooterHeading = string.Empty,
                FooterContent = string.Empty,
                FooterIncludesMilestone = false,
                MilestoneReplaceText = string.Empty,
                IncludeShaSection = false,
                ShaSectionHeading = "SHA256 Hashes of the release artifacts",
                ShaSectionLineFormat = "- `{1}\t{0}`",
                AllowUpdateToPublishedRelease = false,
            };

            Export = new ExportConfig
            {
                IncludeCreatedDateInTitle = false,
                CreatedDateStringFormat = string.Empty,
                PerformRegexRemoval = false,
                RegexText = string.Empty,
                IsMultilineRegex = false,
            };

            Close = new CloseConfig
            {
                IssueComments = false,
                IssueCommentFormat = ISSUE_COMMENT_FORMAT,
                SetDueDate = false, // by default, do not set the due date to match previous behavior
            };

            DefaultBranch = "master";

            Labels = new List<LabelConfig>
            {
                new LabelConfig
                {
                    Name = "Breaking Change",
                    Description = "Functionality breaking changes",
                    Color = "b60205",
                },

                new LabelConfig
                {
                    Name = "Bug",
                    Description = "Something isn't working",
                    Color = "ee0701",
                },

                new LabelConfig
                {
                    Name = "Build",
                    Description = "Build pipeline",
                    Color = "009800",
                },

                new LabelConfig
                {
                    Name = "Documentation",
                    Description = "Improvements or additions to documentation",
                    Color = "d4c5f9",
                },

                new LabelConfig
                {
                    Name = "Feature",
                    Description = "Request for a new feature",
                    Color = "84b6eb",
                },

                new LabelConfig
                {
                    Name = "Good First Issue",
                    Description = "Good for newcomers",
                    Color = "7057ff",
                },

                new LabelConfig
                {
                    Name = "Help Wanted",
                    Description = "Extra attention is needed",
                    Color = "33aa3f",
                },

                new LabelConfig
                {
                    Name = "Improvement",
                    Description = "Improvement of an existing feature",
                    Color = "207de5",
                },

                new LabelConfig
                {
                    Name = "Question",
                    Description = "Further information is requested",
                    Color = "cc317c",
                },
            };

            IssueLabelsInclude = new List<string>
            {
                "Breaking Change",
                "Bug",
                "Documentation",
                "Feature",
                "Good First Issue",
                "Help Wanted",
                "Improvement",
                "Question",
            };

            IssueLabelsExclude = new List<string>
            {
                "Build",
            };

            LabelAliases = new List<LabelAlias>();

            TemplatesDirectory = ".templates";
        }

        [Description("The directory where templates are located")]
        [YamlMember(Alias = "templates-dir")]
        public string TemplatesDirectory { get; set; }

        [Description("Configuration values used when creating new releases")]
        [YamlMember(Alias = "create")]
        public CreateConfig Create { get; private set; }

        [Description("Configuration values used when exporting release notes")]
        [YamlMember(Alias = "export")]
        public ExportConfig Export { get; private set; }

        [Description("Configuration values used when closing a milestone")]
        [YamlMember(Alias = "close")]
        public CloseConfig Close { get; private set; }

        [Description("The name of the default branch.")]
        [YamlMember(Alias = "default-branch")]
        public string DefaultBranch { get; private set; }

        [Description("Configuration values used when creating labels")]
        [YamlMember(Alias = "labels")]
        public IList<LabelConfig> Labels { get; private set; }

        [Description("The labels that will be used to include issues in release notes.")]
        [YamlMember(Alias = "issue-labels-include")]
        public IList<string> IssueLabelsInclude { get; private set; }

        [Description("The labels that will NOT be used when including issues in release notes.")]
        [YamlMember(Alias = "issue-labels-exclude")]
        public IList<string> IssueLabelsExclude { get; private set; }

        [Description("Overrides default pluralization and header names for specific labels.")]
        [YamlMember(Alias = "issue-labels-alias")]
        public IList<LabelAlias> LabelAliases { get; private set; }
    }
}