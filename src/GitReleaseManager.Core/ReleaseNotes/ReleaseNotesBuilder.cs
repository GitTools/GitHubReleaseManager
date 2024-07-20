using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GitReleaseManager.Core.Configuration;
using GitReleaseManager.Core.Exceptions;
using GitReleaseManager.Core.Helpers;
using GitReleaseManager.Core.Model;
using GitReleaseManager.Core.Provider;
using GitReleaseManager.Core.Templates;
using Serilog;

namespace GitReleaseManager.Core.ReleaseNotes
{
    public class ReleaseNotesBuilder : IReleaseNotesBuilder
    {
        private readonly IVcsProvider _vcsProvider;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly Config _configuration;
        private readonly TemplateFactory _templateFactory;
        private string _user;
        private string _repository;
        private string _milestoneTitle;
        private IEnumerable<Milestone> _milestones;
        private Milestone _targetMilestone;

        public ReleaseNotesBuilder(IVcsProvider vcsProvider, ILogger logger, IFileSystem fileSystem, Config configuration, TemplateFactory templateFactory)
        {
            _vcsProvider = vcsProvider;
            _logger = logger;
            _fileSystem = fileSystem;
            _configuration = configuration;
            _templateFactory = templateFactory;
        }

        public async Task<string> BuildReleaseNotesAsync(string user, string repository, string milestoneTitle, string template)
        {
            _user = user;
            _repository = repository;
            _milestoneTitle = milestoneTitle;

            _logger.Verbose("Building release notes...");
            await LoadMilestonesAsync().ConfigureAwait(false);
            GetTargetMilestone();

            var issues = await GetIssuesAsync(_targetMilestone).ConfigureAwait(false);
            var previousMilestone = GetPreviousMilestone();

            var @base = previousMilestone != null
                ? previousMilestone.Title
                : _configuration.DefaultBranch;

            var head = _targetMilestone.Title;

            _logger.Verbose("Getting commit count between base '{Base}' and head '{Head}'", @base, head);

            var numberOfCommits = await _vcsProvider.GetCommitsCountAsync(_user, _repository, @base, head).ConfigureAwait(false);

            if (issues.Count == 0)
            {
                var logMessage = string.Format(CultureInfo.CurrentCulture, "No closed issues have been found for milestone {0}, or all assigned issues are meant to be excluded from release notes, aborting release creation.", _milestoneTitle);
                throw new InvalidOperationException(logMessage);
            }

            var commitsLink = _vcsProvider.GetCommitsUrl(_user, _repository, _targetMilestone?.Title, previousMilestone?.Title);

            var issuesDict = GetIssuesDict(issues);
            var distinctValidIssues = issuesDict.SelectMany(kvp => kvp.Value).DistinctBy(i => i.PublicNumber);

            foreach (var issue in distinctValidIssues)
            {
                // Linked issues are only necessary for figuring out who contributed to a given issue.
                // Therefore, we only need to fetch linked issues if IncludeContributors is enabled.
                if (_configuration.Create.IncludeContributors)
                {
                    var linkedIssues = await _vcsProvider.GetLinkedIssuesAsync(_user, _repository, issue).ConfigureAwait(false);
                    issue.LinkedIssues = Array.AsReadOnly(linkedIssues ?? Array.Empty<Issue>());
                }
                else
                {
                    issue.LinkedIssues = Array.AsReadOnly(Array.Empty<Issue>());
                }
            }

            var contributors = _configuration.Create.IncludeContributors
                ? GetContributors(distinctValidIssues)
                : Array.Empty<User>();

            var milestoneQueryString = _vcsProvider.GetMilestoneQueryString();

            var templateModel = new
            {
                Issues = new
                {
                    issues.Count,
                    Items = issuesDict,
                },
                Contributors = new
                {
                    Count = contributors.Length,
                    Items = contributors,
                },
                Commits = new
                {
                    Count = numberOfCommits,
                    HtmlUrl = commitsLink,
                },
                Milestone = new
                {
                    Target = _targetMilestone,
                    Previous = previousMilestone,
                    QueryString = milestoneQueryString,
                },
                IssueLabels = issuesDict.Keys.ToList(),
            };

            var releaseNotes = await _templateFactory.RenderTemplateAsync(template, templateModel).ConfigureAwait(false);

            _logger.Verbose("Finished building release notes");

            return releaseNotes;
        }

        private Dictionary<string, List<Issue>> GetIssuesDict(List<Issue> issues)
        {
            var issueLabels = _configuration.IssueLabelsInclude;
            var excludedIssueLabels = _configuration.IssueLabelsExclude;

            var issuesByLabel = issues
                .Where(o => !o.Labels.Any(l => excludedIssueLabels.Any(eil => string.Equals(eil, l.Name, StringComparison.OrdinalIgnoreCase))))
                .SelectMany(o => o.Labels, (issue, label) => new { Label = label.Name, Issue = issue })
                .Where(o => issueLabels.Any(il => string.Equals(il, o.Label, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(o => o.Label, o => o.Issue)
                .OrderBy(o => o.Key)
                .ToDictionary(o => GetValidLabel(o.Key, o.Count()), o => o.OrderBy(issue => issue.PublicNumber).ToList());

            return issuesByLabel;
        }

        private static User[] GetContributors(IEnumerable<Issue> issues)
        {
            var contributors = issues.Select(i => i.User);
            var linkedContributors = issues.SelectMany(i => i.LinkedIssues).Select(i => i.User);

            var allContributors = contributors
                .Union(linkedContributors)
                .Where(u => u != null)
                .DistinctBy(u => u.Login)
                .ToArray();

            return allContributors;
        }

        private string GetValidLabel(string label, int issuesCount)
        {
            var alias = _configuration.LabelAliases.FirstOrDefault(x => x.Name.Equals(label, StringComparison.OrdinalIgnoreCase));
            var newLabel = label;

            if (alias != null)
            {
                newLabel = issuesCount == 1 ? alias.Header : alias.Plural;
            }
            else if (issuesCount > 1)
            {
                newLabel += "s";
            }

            return newLabel;
        }

        private (List<Issue> IssuesWithValidLabel, List<string> Errors) CheckIssuesForValidLabels(IEnumerable<Issue> issues)
        {
            var validIssues = new List<Issue>();
            var errors = new List<string>();

            foreach (var issue in issues)
            {
                var includedIssuesCount = 0;
                var isExcluded = false;

                foreach (var issueLabel in issue.Labels)
                {
                    includedIssuesCount += _configuration.IssueLabelsInclude.Count(issueToInclude => string.Equals(issueLabel.Name, issueToInclude, StringComparison.InvariantCultureIgnoreCase));

                    isExcluded = isExcluded || _configuration.IssueLabelsExclude.Any(issueToExclude => string.Equals(issueLabel.Name, issueToExclude, StringComparison.InvariantCultureIgnoreCase));
                }

                if (isExcluded)
                {
                    continue;
                }

                if (includedIssuesCount != 1)
                {
                    var allIssueLabels = _configuration.IssueLabelsInclude.Union(_configuration.IssueLabelsExclude).ToList();
                    var allIssuesExceptLast = allIssueLabels.Take(allIssueLabels.Count - 1);
                    var lastLabel = allIssueLabels.Last();

                    var allIssuesExceptLastString = string.Join(", ", allIssuesExceptLast);

                    var message = string.Format(CultureInfo.InvariantCulture, "Bad Issue {0} expected to find a single label with either {1} or {2}.", issue.HtmlUrl, allIssuesExceptLastString, lastLabel);
                    errors.Add(message);
                    continue;
                }

                if (includedIssuesCount > 0)
                {
                    validIssues.Add(issue);
                }
            }

            return (validIssues, errors);
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

        private async Task LoadMilestonesAsync()
        {
            _milestones = await _vcsProvider.GetMilestonesAsync(_user, _repository).ConfigureAwait(false);
        }

        private async Task<List<Issue>> GetIssuesAsync(Milestone milestone)
        {
            var allIssues = await _vcsProvider.GetIssuesAsync(_user, _repository, milestone, ItemStateFilter.Closed).ConfigureAwait(false);

            var result = CheckIssuesForValidLabels(allIssues);

            if (result.Errors.Count > 0)
            {
                throw new InvalidIssuesException(result.Errors);
            }

            // If there are no issues assigned to the milestone that have a label that is part
            // of the labels to include array, then that is essentially the same as having no
            // closed issues assigned to the milestone.  In this scenario, we want to raise an
            // error, so return an emtpy issues list.
            if (!result.IssuesWithValidLabel.Any())
            {
                return new List<Issue>();
            }

            return allIssues.ToList();
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