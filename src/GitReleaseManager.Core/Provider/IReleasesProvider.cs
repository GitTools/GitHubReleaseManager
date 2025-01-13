using System.Collections.Generic;
using System.Threading.Tasks;
using GitReleaseManager.Core.Model;

namespace GitReleaseManager.Core.Provider
{
    public interface IReleasesProvider
    {
        bool SupportReleases { get; }

        Task<Release> CreateReleaseAsync(string owner, string repository, Release release);

        Task DeleteReleaseAsync(string owner, string repository, Release release);

        Task<Release> GetReleaseAsync(string owner, string repository, string tagName);

        Task<IEnumerable<Release>> GetReleasesAsync(string owner, string repository, bool skipPrereleases);

        Task PublishReleaseAsync(string owner, string repository, string tagName, Release release);

        Task UpdateReleaseAsync(string owner, string repository, Release release);
    }
}