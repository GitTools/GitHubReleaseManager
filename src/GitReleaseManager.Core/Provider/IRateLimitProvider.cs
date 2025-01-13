namespace GitReleaseManager.Core.Provider
{
    using GitReleaseManager.Core.Model;

    public interface IRateLimitProvider
    {
        RateLimit GetRateLimit();
    }
}
