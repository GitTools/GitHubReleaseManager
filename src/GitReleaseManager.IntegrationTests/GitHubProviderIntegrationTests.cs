using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using GitReleaseManager.Core;
using GitReleaseManager.Core.Provider;
using NUnit.Framework;
using Octokit;
using Shouldly;
using Issue = GitReleaseManager.Core.Model.Issue;
using Milestone = GitReleaseManager.Core.Model.Milestone;

namespace GitReleaseManager.IntegrationTests
{
    [TestFixture]
    [Explicit]
    public class GitHubProviderIntegrationTests
    {
        private const string OWNER = "GitTools";
        private const string REPOSITORY = "GitReleaseManager";
        private const bool SKIP_PRERELEASES = false;

        private GitHubProvider _gitHubProvider;
        private IGitHubClient _gitHubClient;
        private IMapper _mapper;

        private string _token;
        private string _releaseBaseTag;
        private string _releaseHeadTag;
        private Issue _issue;
        private Milestone _milestone;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _token = Environment.GetEnvironmentVariable("GITTOOLS_GITHUB_TOKEN");

            if (string.IsNullOrWhiteSpace(_token))
            {
                Assert.Inconclusive("Unable to locate credentials for accessing GitHub API");
            }

            _mapper = AutoMapperConfiguration.Configure();
            _gitHubClient = new GitHubClient(new ProductHeaderValue("GitReleaseManager")) { Credentials = new Credentials(_token) };
            _gitHubProvider = new GitHubProvider(_gitHubClient, _mapper);
        }

        [Test]
        [Order(1)]
        public async Task Should_Get_Labels()
        {
            var result = await _gitHubProvider.GetLabelsAsync(OWNER, REPOSITORY).ConfigureAwait(false);
            result.Count().ShouldBeGreaterThan(0);
        }

        [Test]
        [Order(2)]
        public async Task Should_Get_Milestones()
        {
            var result = await _gitHubProvider.GetMilestonesAsync(OWNER, REPOSITORY).ConfigureAwait(false);
            result.Count().ShouldBeGreaterThan(0);

            _milestone = result.OrderByDescending(m => m.PublicNumber).First();
        }

        [Test]
        [Order(3)]
        public async Task Should_Get_Issues()
        {
            var result = await _gitHubProvider.GetIssuesAsync(OWNER, REPOSITORY, _milestone).ConfigureAwait(false);
            result.Count().ShouldBeGreaterThan(0);

            _issue = result.First();
        }

        [Test]
        [Order(4)]
        public async Task Should_Get_Issue_Comments()
        {
            var result = await _gitHubProvider.GetIssueCommentsAsync(OWNER, REPOSITORY, _issue).ConfigureAwait(false);
            result.Count().ShouldBeGreaterThan(0);
        }

        [Test]
        [Order(5)]
        public async Task Should_Get_Releases()
        {
            var result = await _gitHubProvider.GetReleasesAsync(OWNER, REPOSITORY, SKIP_PRERELEASES).ConfigureAwait(false);
            result.Count().ShouldBeGreaterThan(0);

            var orderedReleases = result.OrderByDescending(r => r.Id).ToList();

            _releaseBaseTag = orderedReleases[1].TagName;
            _releaseHeadTag = orderedReleases[0].TagName;
        }

        [Test]
        [Order(6)]
        public async Task Should_Get_Commits_Count()
        {
            var result = await _gitHubProvider.GetCommitsCountAsync(OWNER, REPOSITORY, _releaseBaseTag, _releaseHeadTag).ConfigureAwait(false);
            result.ShouldBeGreaterThan(0);
        }

        [Test]
        public async Task GetLinkedIssue()
        {
            // Assert that issue 43 is linked to pull request 108
            var result1 = await _gitHubProvider.GetLinkedIssueAsync("jericho", "_testing", 43).ConfigureAwait(false);
            Assert.AreEqual(108, result1.Number);

            // Assert that pull request 108 is linked to issue 43
            var result2 = await _gitHubProvider.GetLinkedIssueAsync("jericho", "_testing", 108).ConfigureAwait(false);
            Assert.AreEqual(43, result2.Number);
        }
    }
}