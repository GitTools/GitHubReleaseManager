//-----------------------------------------------------------------------
// <copyright file="ReleaseNotesBuilder.cs" company="GitTools Contributors">
//     Copyright (c) 2015 - Present - GitTools Contributors
// </copyright>
//-----------------------------------------------------------------------

namespace GitReleaseManager.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Configuration;
    using GitReleaseManager.Core.Extensions;
    using GitReleaseManager.Core.Model;
    using Serilog;

    public class ReleaseNotesBuilder
    {
        private readonly IVcsService _vcsService;
        private readonly ILogger _logger;
        private readonly string _user;
        private readonly string _repository;
        private readonly string _milestoneTitle;
        private readonly Config _configuration;
        private ReadOnlyCollection<Milestone> _milestones;
        private Milestone _targetMilestone;

        public ReleaseNotesBuilder(IVcsService vcsService, ILogger logger, string user, string repository, string milestoneTitle, Config configuration)
        {
            _vcsService = vcsService;
            _logger = logger;
            _user = user;
            _repository = repository;
            _milestoneTitle = milestoneTitle;
            _configuration = configuration;
        }

        public async Task<string> BuildReleaseNotes()
        {
            _logger.Verbose("Building release notes...");
            await LoadMilestones().ConfigureAwait(false);
            GetTargetMilestone();

            var issues = await GetIssues(_targetMilestone).ConfigureAwait(false);
            var stringBuilder = new StringBuilder();
            var previousMilestone = GetPreviousMilestone();
            var numberOfCommits = await _vcsService.GetNumberOfCommitsBetween(previousMilestone, _targetMilestone, _user, _repository).ConfigureAwait(false);

            if (issues.Count == 0)
            {
                var logMessage = string.Format("No closed issues have been found for milestone {0}, or all assigned issues are meant to be excluded from release notes, aborting creation of release.", _milestoneTitle);
                throw new InvalidOperationException(logMessage);
            }

            if (issues.Count > 0)
            {
                var issuesText = string.Format(issues.Count == 1 ? "{0} issue" : "{0} issues", issues.Count);

                if (numberOfCommits > 0)
                {
                    var commitsLink = _vcsService.GetCommitsLink(_user, _repository, _targetMilestone, previousMilestone);
                    var commitsText = string.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);

                    stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}) which resulted in [{2}]({3}) being closed.", commitsText, commitsLink, issuesText, _targetMilestone.HtmlUrl + "?closed=1");
                }
                else
                {
                    stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}) closed.", issuesText, _targetMilestone.HtmlUrl + "?closed=1");
                }
            }
            else if (numberOfCommits > 0)
            {
                var commitsLink = _vcsService.GetCommitsLink(_user, _repository, _targetMilestone, previousMilestone);
                var commitsText = string.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);
                stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}).", commitsText, commitsLink);
            }

            stringBuilder.AppendLine();

            stringBuilder.AppendLine(_targetMilestone.Description);
            stringBuilder.AppendLine();

            AddIssues(stringBuilder, issues);

            if (_configuration.Create.IncludeFooter)
            {
                AddFooter(stringBuilder);
            }

            _logger.Verbose("Finished building release notes");

            return stringBuilder.ToString();
        }

        private void Append(IEnumerable<Issue> issues, string label, StringBuilder stringBuilder)
        {
            var features = issues.Where(x => x.Labels.Any(l => l.Name.ToUpperInvariant() == label.ToUpperInvariant())).ToList();

            if (features.Count > 0)
            {
                var singular = GetLabel(label, alias => alias.Header) ?? label;
                var plural = GetLabel(label, alias => alias.Plural) ?? label + "s";
                stringBuilder.AppendFormat("__{0}__\r\n\r\n", features.Count == 1 ? singular : plural);

                foreach (var issue in features)
                {
                    stringBuilder.AppendFormat("- [__#{0}__]({1}) {2}\r\n", issue.Number, issue.HtmlUrl, issue.Title);
                }

                stringBuilder.AppendLine();
            }
        }

        private string GetLabel(string label, Func<LabelAlias, string> func)
        {
            var alias = _configuration.LabelAliases.FirstOrDefault(x => x.Name.Equals(label, StringComparison.OrdinalIgnoreCase));
            return alias != null ? func(alias) : null;
        }

        private bool CheckForValidLabels(Issue issue)
        {
            var includedIssuesCount = 0;
            var excludedIssuesCount = 0;

            foreach (var issueLabel in issue.Labels)
            {
                includedIssuesCount += _configuration.IssueLabelsInclude.Count(issueToInclude => issueLabel.Name.ToUpperInvariant() == issueToInclude.ToUpperInvariant());

                excludedIssuesCount += _configuration.IssueLabelsExclude.Count(issueToExclude => issueLabel.Name.ToUpperInvariant() == issueToExclude.ToUpperInvariant());
            }

            if (includedIssuesCount + excludedIssuesCount != 1)
            {
                var allIssueLabels = _configuration.IssueLabelsInclude.Union(_configuration.IssueLabelsExclude).ToList();
                var allIssuesExceptLast = allIssueLabels.Take(allIssueLabels.Count - 1);
                var lastLabel = allIssueLabels.Last();

                var allIssuesExceptLastString = string.Join(", ", allIssuesExceptLast);

                var message = string.Format(CultureInfo.InvariantCulture, "Bad Issue {0} expected to find a single label with either {1} or {2}.", issue.HtmlUrl, allIssuesExceptLastString, lastLabel);
                throw new InvalidOperationException(message);
            }

            if (includedIssuesCount > 0)
            {
                return true;
            }

            return false;
        }

        private void AddIssues(StringBuilder stringBuilder, List<Issue> issues)
        {
            foreach (var issueLabel in _configuration.IssueLabelsInclude)
            {
                Append(issues, issueLabel, stringBuilder);
            }
        }

        private Milestone GetPreviousMilestone()
        {
            var currentVersion = _targetMilestone.Version;
            return _milestones
                .OrderByDescending(m => m.Version)
                .Distinct()
                .SkipWhile(x => x.Version >= currentVersion)
                .FirstOrDefault();
        }

        private void AddFooter(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "### {0}", _configuration.Create.FooterHeading));

            var footerContent = _configuration.Create.FooterContent;

            if (_configuration.Create.FooterIncludesMilestone
                && !string.IsNullOrEmpty(_configuration.Create.MilestoneReplaceText))
            {
                var replaceValues = new Dictionary<string, object>
                    {
                        { _configuration.Create.MilestoneReplaceText.Trim('{', '}'), _milestoneTitle },
                    };
                footerContent = footerContent.ReplaceTemplate(replaceValues);
            }

            stringBuilder.Append(footerContent);
            stringBuilder.AppendLine();
        }

        private async Task LoadMilestones()
        {
            _milestones = await _vcsService.GetReadOnlyMilestonesAsync(_user, _repository).ConfigureAwait(false);
        }

        private async Task<List<Issue>> GetIssues(Milestone milestone)
        {
            var issues = await _vcsService.GetIssuesAsync(milestone).ConfigureAwait(false);

            var hasIncludedIssues = false;

            foreach (var issue in issues)
            {
                if (CheckForValidLabels(issue))
                {
                    hasIncludedIssues = true;
                }
            }

            // If there are no issues assigned to the milestone that have a label that is part
            // of the labels to include array, then that is essentially the same as having no
            // closed issues assigned to the milestone.  In this scenario, we want to raise an
            // error, so return an emtpy issues list.
            if (!hasIncludedIssues)
            {
                return new List<Issue>();
            }

            return issues;
        }

        private void GetTargetMilestone()
        {
            _targetMilestone = _milestones.FirstOrDefault(x => x.Title == _milestoneTitle);

            if (_targetMilestone == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not find milestone for '{0}'.", _milestoneTitle));
            }
        }
    }
}