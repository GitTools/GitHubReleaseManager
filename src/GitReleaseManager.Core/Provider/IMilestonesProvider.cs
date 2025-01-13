namespace GitReleaseManager.Core.Provider
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Model;

    public interface IMilestonesProvider
    {
        bool SupportMilestones { get; }

        Task<Milestone> GetMilestoneAsync(string owner, string repository, string milestoneTitle, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        Task<IEnumerable<Milestone>> GetMilestonesAsync(string owner, string repository, ItemStateFilter itemStateFilter = ItemStateFilter.All);

        Task SetMilestoneStateAsync(string owner, string repository, Milestone milestone, ItemState itemState);

        string GetMilestoneQueryString();
    }
}
