using System.Collections.Generic;
using System.Threading.Tasks;
using GitReleaseManager.Core.Model;

namespace GitReleaseManager.Core.Provider
{
    public interface IVcsProvider : IAssetsProvider, ICommitsProvider, IIssuesProvider, IMilestonesProvider, IRateLimitProvider, IReleasesProvider
    {
    }
}