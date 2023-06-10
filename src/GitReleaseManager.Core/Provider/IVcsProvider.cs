using System.Collections.Generic;
using System.Threading.Tasks;
using GitReleaseManager.Core.Model;

namespace GitReleaseManager.Core.Provider
{
    public interface IVcsProvider
    {
        Task DeleteAssetAsync(string owner, string repository, int id);

        Task UploadAssetAsync(Release release, ReleaseAssetUpload releaseAssetUpload);

        Task<int> GetCommitsCount(string owner, string repository, string @base, string head);

        string GetCommitsUrl(string owner, string repository, string head, string @base = null);

        Task CreateIssueCommentAsync(string owner, string repository, int issueNumber, string comment);

        Task<IEnumerable<Issue>> GetIssuesAsync(string owner, string repository, int milestoneNumber, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        Task<IEnumerable<IssueComment>> GetIssueCommentsAsync(string owner, string repository, int issueNumber);

        Task CreateLabelAsync(string owner, string repository, Label label);
        
        Task UpdateLabelAsync(string owner, string repository, string existingLabelName,Label label);

        Task DeleteLabelAsync(string owner, string repository, string labelName);

        Task<IEnumerable<Label>> GetLabelsAsync(string owner, string repository);

        Task<Milestone> GetMilestoneAsync(string owner, string repository, string milestoneTitle, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        Task<IEnumerable<Milestone>> GetMilestonesAsync(string owner, string repository, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        Task SetMilestoneStateAsync(string owner, string repository, int milestoneNumber, ItemState itemState);

        Task<Release> CreateReleaseAsync(string owner, string repository, Release release);

        Task DeleteReleaseAsync(string owner, string repository, int id);

        Task<Release> GetReleaseAsync(string owner, string repository, string tagName);

        Task<IEnumerable<Release>> GetReleasesAsync(string owner, string repository, bool skipPrereleases);

        Task PublishReleaseAsync(string owner, string repository, string tagName, int releaseId);

        Task UpdateReleaseAsync(string owner, string repository, Release release);

        RateLimit GetRateLimit();
    }
}