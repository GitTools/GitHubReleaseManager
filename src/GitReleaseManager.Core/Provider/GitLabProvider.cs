namespace GitReleaseManager.Core.Provider
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using NGitLab;
    using NGitLab.Models;
    using Serilog;
    using ApiException = GitReleaseManager.Core.Exceptions.ApiException;
    using Issue = GitReleaseManager.Core.Model.Issue;
    using IssueComment = GitReleaseManager.Core.Model.IssueComment;
    using ItemState = GitReleaseManager.Core.Model.ItemState;
    using ItemStateFilter = GitReleaseManager.Core.Model.ItemStateFilter;
    using Label = GitReleaseManager.Core.Model.Label;
    using Milestone = GitReleaseManager.Core.Model.Milestone;
    using NotFoundException = GitReleaseManager.Core.Exceptions.NotFoundException;
    using RateLimit = GitReleaseManager.Core.Model.RateLimit;
    using Release = GitReleaseManager.Core.Model.Release;
    using ReleaseAsset = GitReleaseManager.Core.Model.ReleaseAsset;
    using ReleaseAssetUpload = GitReleaseManager.Core.Model.ReleaseAssetUpload;

    public class GitLabProvider : IVcsProvider
    {
        private const string NOT_FOUND_MESSGAE = "NotFound";
        private const string COMMITS_FORMAT_STRING = "https://gitlab.com/{0}/{1}/-/commits/{2}";
        private const string COMPARE_FORMAT_STRING = "https://gitlab.com/{0}/{1}/-/compare/{2}...{3}";
        private const string MILESTONES_FORMAT_STRING = "https://gitlab.com/{0}/{1}/-/milestones/{2}#tab-issues";
        private const string RELEASES_FORMAT_STRING = "https://gitlab.com/{0}/{1}/-/releases/{2}";

        private readonly IGitLabClient _gitLabClient;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        private long? _projectId;

        public GitLabProvider(IGitLabClient gitLabClient, IMapper mapper, ILogger logger)
        {
            _gitLabClient = gitLabClient;
            _mapper = mapper;
            _logger = logger;
        }

        public Task DeleteAssetAsync(string owner, string repository, ReleaseAsset asset)
        {
            // TODO: This is a discussion here:
            // https://github.com/ubisoft/NGitLab/discussions/511
            // about what is necessary to implement the required functionality in NGitLab
            throw new NotImplementedException();
        }

        public Task UploadAssetAsync(Release release, ReleaseAssetUpload releaseAssetUpload)
        {
            return ExecuteAsync(() =>
            {
                _logger.Warning("Uploading of assets is not currently supported when targeting GitLab.");
                return Task.CompletedTask;
            });
        }

        public Task<int> GetCommitsCountAsync(string owner, string repository, string @base, string head)
        {
            return ExecuteAsync(() =>
            {
                // TODO: This is waiting on a PR being merged...
                // https://github.com/ubisoft/NGitLab/pull/444
                // Once it is, we might be able to implement what is necessary here.
                return Task.FromResult(0);
            });
        }

        public string GetCommitsUrl(string owner, string repository, string head, string @base = null)
        {
            Ensure.IsNotNullOrWhiteSpace(owner, nameof(owner));
            Ensure.IsNotNullOrWhiteSpace(repository, nameof(repository));
            Ensure.IsNotNullOrWhiteSpace(head, nameof(head));

            return string.IsNullOrWhiteSpace(@base)
                ? string.Format(CultureInfo.InvariantCulture, COMMITS_FORMAT_STRING, owner, repository, head)
                : string.Format(CultureInfo.InvariantCulture, COMPARE_FORMAT_STRING, owner, repository, @base, head);
        }

        public Task CreateIssueCommentAsync(string owner, string repository, Issue issue, string comment)
        {
            return ExecuteAsync(() =>
            {
                var projectId = GetGitLabProjectId(owner, repository);

                if (issue.IsPullRequest)
                {
                    var mergeRequestClient = _gitLabClient.GetMergeRequest(projectId);
                    var commentsClient = mergeRequestClient.Comments(issue.PublicNumber);
                    var mergeRequestComment = new MergeRequestCommentCreate
                    {
                        Body = comment,
                    };

                    commentsClient.Add(mergeRequestComment);
                }
                else
                {
                    var issueNotesClient = _gitLabClient.GetProjectIssueNoteClient(projectId);
                    var issueComment = new ProjectIssueNoteCreate
                    {
                        IssueId = issue.PublicNumber,
                        Body = comment,
                    };

                    issueNotesClient.Create(issueComment);
                }

                return Task.CompletedTask;
            });
        }

        public Task<IEnumerable<Issue>> GetIssuesAsync(string owner, string repository, Milestone milestone, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            return ExecuteAsync(() =>
            {
                var issuesClient = _gitLabClient.Issues;

                var query = new IssueQuery();
                query.Milestone = milestone.Title;

                if (itemStateFilter == ItemStateFilter.Open)
                {
                    query.State = IssueState.opened;
                }
                else if (itemStateFilter == ItemStateFilter.Closed)
                {
                    query.State = IssueState.closed;
                }

                var projectId = GetGitLabProjectId(owner, repository);

                var issues = issuesClient.GetAsync(projectId, query);

                var mergeRequestsClient = _gitLabClient.GetMergeRequest(projectId);

                var mergeRequestQuery = new MergeRequestQuery();
                mergeRequestQuery.Milestone = milestone.Title;

                if (itemStateFilter == ItemStateFilter.Open)
                {
                    mergeRequestQuery.State = MergeRequestState.opened;
                }
                else if (itemStateFilter == ItemStateFilter.Closed)
                {
                    mergeRequestQuery.State = MergeRequestState.merged;
                }

                var mergeRequests = mergeRequestsClient.Get(mergeRequestQuery);

                var issuesAndMergeRequests = new List<Issue>();
                issuesAndMergeRequests.AddRange(_mapper.Map<IEnumerable<Issue>>(issues));
                issuesAndMergeRequests.AddRange(_mapper.Map<IEnumerable<Issue>>(mergeRequests));

                return Task.FromResult(issuesAndMergeRequests.AsEnumerable());
            });
        }

        public Task<IEnumerable<IssueComment>> GetIssueCommentsAsync(string owner, string repository, Issue issue)
        {
            return ExecuteAsync(() =>
            {
                IEnumerable<IssueComment> issueComments = Enumerable.Empty<IssueComment>();
                var projectId = GetGitLabProjectId(owner, repository);

                if (issue.IsPullRequest)
                {
                    var mergeRequestClient = _gitLabClient.GetMergeRequest(projectId);
                    var commentsClient = mergeRequestClient.Comments(issue.PublicNumber);
                    var comments = commentsClient.All;
                    issueComments = _mapper.Map<IEnumerable<IssueComment>>(comments);
                }
                else
                {
                    var issueNotesClient = _gitLabClient.GetProjectIssueNoteClient(projectId);
                    var comments = issueNotesClient.ForIssue(issue.PublicNumber);
                    issueComments = _mapper.Map<IEnumerable<IssueComment>>(comments);
                }

                return Task.FromResult(issueComments);
            });
        }

        public Task CreateLabelAsync(string owner, string repository, Label label)
        {
            // The label functionality in GitLab already provides more than
            // what is possible in GRM. As such, the decision was taken to not
            // implement the creation of labels for the GitLab provider.
            throw new NotImplementedException();
        }

        public Task DeleteLabelAsync(string owner, string repository, Label label)
        {
            // The label functionality in GitLab already provides more than
            // what is possible in GRM. As such, the decision was taken to not
            // implement the deletion of labels for the GitLab provider.
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Label>> GetLabelsAsync(string owner, string repository)
        {
            // The label functionality in GitLab already provides more than
            // what is possible in GRM. As such, the decision was taken to not
            // implement the fetching of labels for the GitLab provider.
            throw new NotImplementedException();
        }

        public Task<Milestone> GetMilestoneAsync(string owner, string repository, string milestoneTitle, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            return ExecuteAsync(async () =>
            {
                var milestones = await GetMilestonesAsync(owner, repository, itemStateFilter).ConfigureAwait(false);
                var foundMilestone = milestones.FirstOrDefault(m => m.Title == milestoneTitle);

                if (foundMilestone is null)
                {
                    throw new NotFoundException(NOT_FOUND_MESSGAE);
                }

                return foundMilestone;
            });
        }

        public Task<IEnumerable<Milestone>> GetMilestonesAsync(string owner, string repository, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            return ExecuteAsync(() =>
            {
                var query = new MilestoneQuery();

                if (itemStateFilter == ItemStateFilter.Open)
                {
                    query.State = MilestoneState.active;
                }
                else if (itemStateFilter == ItemStateFilter.Closed)
                {
                    query.State = MilestoneState.closed;
                }

                var mileStoneClient = _gitLabClient.GetMilestone(GetGitLabProjectId(owner, repository));

                var milestones = mileStoneClient.Get(query);
                var mappedMilestones = _mapper.Map<IEnumerable<Milestone>>(milestones);

                foreach (var mappedMilestone in mappedMilestones)
                {
                    mappedMilestone.HtmlUrl = string.Format(CultureInfo.InvariantCulture, MILESTONES_FORMAT_STRING, owner, repository, mappedMilestone.PublicNumber);
                }

                return Task.FromResult(mappedMilestones);
            });
        }

        public Task SetMilestoneStateAsync(string owner, string repository, Milestone milestone, ItemState itemState)
        {
            return ExecuteAsync(() =>
            {
                var mileStoneClient = _gitLabClient.GetMilestone(GetGitLabProjectId(owner, repository));

                if (itemState == ItemState.Open)
                {
                    mileStoneClient.Activate(milestone.InternalNumber);
                }
                else if (itemState == ItemState.Closed)
                {
                    if (milestone.DueOn.HasValue)
                    {
                        mileStoneClient.Update(milestone.InternalNumber, new MilestoneUpdate { DueDate = milestone.DueOn.Value.ToString("o", CultureInfo.InvariantCulture) });
                    }

                    mileStoneClient.Close(milestone.InternalNumber);
                }

                return Task.CompletedTask;
            });
        }

        public Task<Release> CreateReleaseAsync(string owner, string repository, Release release)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                var newRelease = _mapper.Map<ReleaseCreate>(release);
                var nGitLabRelease = releaseClient.Create(newRelease);
                var mappedRelease = _mapper.Map<Release>(nGitLabRelease);

                if (mappedRelease != null)
                {
                    mappedRelease.HtmlUrl = string.Format(CultureInfo.InvariantCulture, RELEASES_FORMAT_STRING, owner, repository, release.TagName);
                }

                return Task.FromResult(mappedRelease);
            });
        }

        public Task DeleteReleaseAsync(string owner, string repository, Release release)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                releaseClient.Delete(release.TagName);
                return Task.CompletedTask;
            });
        }

        public Task<Release> GetReleaseAsync(string owner, string repository, string tagName)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                var releases = releaseClient.GetAsync();

                var release = releases.FirstOrDefault(r => r.TagName == tagName);
                var mappedRelease = _mapper.Map<Release>(release);

                if (mappedRelease != null)
                {
                    mappedRelease.HtmlUrl = string.Format(CultureInfo.InvariantCulture, RELEASES_FORMAT_STRING, owner, repository, tagName);
                }

                return Task.FromResult(mappedRelease);
            });
        }

        public Task<IEnumerable<Release>> GetReleasesAsync(string owner, string repository, bool skipPrereleases)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                var releases = _mapper.Map<IEnumerable<Release>>(releaseClient.GetAsync());

                return Task.FromResult(releases);
            });
        }

        public Task PublishReleaseAsync(string owner, string repository, string tagName, Release release)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                var update = new ReleaseUpdate
                {
                    ReleasedAt = DateTime.UtcNow,
                    TagName = tagName,
                };

                releaseClient.Update(update);
                return Task.CompletedTask;
            });
        }

        public Task UpdateReleaseAsync(string owner, string repository, Release release)
        {
            return ExecuteAsync(() =>
            {
                var releaseClient = _gitLabClient.GetReleases(GetGitLabProjectId(owner, repository));

                var update = new ReleaseUpdate
                {
                    Description = release.Body,
                    ReleasedAt = release.Draft ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow,
                    Name = release.Name,
                    TagName = release.TagName,
                    Milestones = new string[] { release.TagName },
                };

                releaseClient.Update(update);
                return Task.CompletedTask;
            });
        }

        public RateLimit GetRateLimit()
        {
            // TODO: There doesn't currently seem to be a way to get the remaining
            // rate limit for GitLab using the library we are using, so for now,
            // let's just hard code it.
            return new RateLimit { Limit = 600, Remaining = 100 };
        }

        public string GetMilestoneQueryString()
        {
            return "state=closed";
        }

        public string GetIssueType(Issue issue)
        {
            return issue.IsPullRequest ? "Merge Request" : "Issue";
        }

        private long GetGitLabProjectId(string owner, string repository)
        {
            if (_projectId.HasValue)
            {
                return _projectId.Value;
            }

            var projectName = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", owner, repository);
            var project = _gitLabClient.Projects[projectName];
            _projectId = project.Id;

            return _projectId.Value;
        }

        private static async Task ExecuteAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                throw new ApiException(ae.InnerException.Message, ae.InnerException);
            }
            catch (Exception ex) when (!(ex is NotFoundException))
            {
                throw new ApiException(ex.Message, ex);
            }
        }

        private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                throw new ApiException(ae.InnerException.Message, ae.InnerException);
            }
            catch (Exception ex) when (!(ex is NotFoundException))
            {
                throw new ApiException(ex.Message, ex);
            }
        }
    }
}