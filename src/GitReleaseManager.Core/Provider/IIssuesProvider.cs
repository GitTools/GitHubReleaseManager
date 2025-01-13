namespace GitReleaseManager.Core.Provider
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Model;

    public interface IIssuesProvider
    {
        bool SupportIssues { get; }
        bool SupportIssueComments { get; }

        Task CreateIssueCommentAsync(string owner, string repository, Issue issue, string comment);

        Task<IEnumerable<IssueComment>> GetIssueCommentsAsync(string owner, string repository, Issue issue);

        Task<IEnumerable<Issue>> GetIssuesAsync(string owner, string repository, Milestone milstone, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        string GetIssueType(Issue issue);
    }
}