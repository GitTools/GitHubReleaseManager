namespace GitReleaseManager.Core.Provider
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Model;
    using Serilog;

    public class NullReleasesProvider : IVcsProvider
    {
        private readonly string _vcsProvider;
        private readonly ILogger _logger;

        public NullReleasesProvider(string vcsProvider, ILogger logger)
        {
            _vcsProvider = vcsProvider;
            _logger = logger;
        }

        public bool SupportsAssets => false;

        public bool SupportsCommits => false;

        public bool SupportIssues => false;

        public bool SupportIssueComments => false;

        public bool SupportMilestones => false;

        public bool SupportReleases => false;

        public Task CreateIssueCommentAsync(string owner, string repository, Issue issue, string comment)
        {
            _logger.Warning("The provider '{Provider}' do not support creating issue comments!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task CreateLabelAsync(string owner, string repository, Label label)
        {
            _logger.Warning("The provider '{Provider}' do not support creating labels!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task<Release> CreateReleaseAsync(string owner, string repository, Release release)
        {
            _logger.Warning("The provider '{Provider}' do not support creating releases!", _vcsProvider);
            return Task.FromResult<Release>(null);
        }

        public Task DeleteAssetAsync(string owner, string repository, ReleaseAsset asset)
        {
            _logger.Warning("The provider '{Provider}' do not support deleting assets!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task DeleteLabelAsync(string owner, string repository, Label label)
        {
            _logger.Warning("The provider '{Provider}' do not support deleting labels!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task DeleteReleaseAsync(string owner, string repository, Release release)
        {
            _logger.Warning("The provider '{Provider}' do not support releases labels!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task<int> GetCommitsCountAsync(string owner, string repository, string @base, string head)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring commits!", _vcsProvider);
            return Task.FromResult(0);
        }

        public string GetCommitsUrl(string owner, string repository, string head, string @base = null)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring commits!", _vcsProvider);
            return null;
        }

        public Task<IEnumerable<IssueComment>> GetIssueCommentsAsync(string owner, string repository, Issue issue)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring issue comments!", _vcsProvider);
            return Task.FromResult(Enumerable.Empty<IssueComment>());
        }

        public Task<IEnumerable<Issue>> GetIssuesAsync(string owner, string repository, Milestone milstone, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring issues!", _vcsProvider);
            return Task.FromResult(Enumerable.Empty<Issue>());
        }

        public string GetIssueType(Issue issue)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring issues!", _vcsProvider);
            return null;
        }

        public Task<IEnumerable<Label>> GetLabelsAsync(string owner, string repository)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring labels!", _vcsProvider);
            return Task.FromResult(Enumerable.Empty<Label>());
        }

        public Task<Milestone> GetMilestoneAsync(string owner, string repository, string milestoneTitle, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring milestones!", _vcsProvider);
            return Task.FromResult<Milestone>(null);
        }

        public string GetMilestoneQueryString()
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring milestones!", _vcsProvider);
            return null;
        }

        public Task<IEnumerable<Milestone>> GetMilestonesAsync(string owner, string repository, ItemStateFilter itemStateFilter = ItemStateFilter.All)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring milestones!", _vcsProvider);
            return Task.FromResult(Enumerable.Empty<Milestone>());
        }

        public RateLimit GetRateLimit()
        {
            return new RateLimit
            {
                Limit = int.MaxValue,
                Remaining = int.MaxValue,
            };
        }

        public Task<Release> GetReleaseAsync(string owner, string repository, string tagName)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring releases!", _vcsProvider);
            return Task.FromResult<Release>(null);
        }

        public Task<IEnumerable<Release>> GetReleasesAsync(string owner, string repository, bool skipPrereleases)
        {
            _logger.Warning("The provider '{Provider}' do not support acquiring releases!", _vcsProvider);
            return Task.FromResult(Enumerable.Empty<Release>());
        }

        public Task PublishReleaseAsync(string owner, string repository, string tagName, Release release)
        {
            _logger.Warning("The provider '{Provider}' do not support publishing releases!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task SetMilestoneStateAsync(string owner, string repository, Milestone milestone, ItemState itemState)
        {
            _logger.Warning("The provider '{Provider} do not support updating milestones!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task UpdateReleaseAsync(string owner, string repository, Release release)
        {
            _logger.Warning("The provider '{Provider}' do not support updating releases!", _vcsProvider);
            return Task.CompletedTask;
        }

        public Task UploadAssetAsync(Release release, ReleaseAssetUpload releaseAssetUpload)
        {
            _logger.Warning("The provider '{Provider}' do not support uploading assets!", _vcsProvider);
            return Task.CompletedTask;
        }
    }
}
