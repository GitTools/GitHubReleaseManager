namespace GitReleaseManager.Core.Provider
{
    using System.Threading.Tasks;
    using GitReleaseManager.Core.Model;

    public interface IAssetsProvider
    {
        bool SupportsAssets { get; }

        Task DeleteAssetAsync(string owner, string repository, ReleaseAsset asset);

        Task UploadAssetAsync(Release release, ReleaseAssetUpload releaseAssetUpload);
    }
}
