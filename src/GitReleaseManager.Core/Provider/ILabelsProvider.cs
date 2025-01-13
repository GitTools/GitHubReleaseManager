namespace GitReleaseManager.Core.Provider
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Model;

    public interface ILabelsProvider
    {
        bool SupportsLabels { get; }

        Task CreateLabelAsync(string owner, string repository, Label label);

        Task DeleteLabelAsync(string owner, string repository, Label label);

        Task<IEnumerable<Label>> GetLabelsAsync(string owner, string repository);
    }
}