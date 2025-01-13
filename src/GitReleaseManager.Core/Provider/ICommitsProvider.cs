namespace GitReleaseManager.Core.Provider
{
    using System.Threading.Tasks;

    public interface ICommitsProvider
    {
        bool SupportsCommits { get; }

        Task<int> GetCommitsCountAsync(string owner, string repository, string baseCommit, string headCommit);

        string GetCommitsUrl(string owner, string repository, string head, string baseCommit = null);
    }
}