using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AutoMapper;
using GitReleaseManager.Core;
using GitReleaseManager.Core.Configuration;
using GitReleaseManager.Core.Helpers;
using GitReleaseManager.Core.Options;
using GitReleaseManager.Core.Provider;
using GitReleaseManager.Core.ReleaseNotes;
using GitReleaseManager.Core.Templates;
using NUnit.Framework;
using Octokit;
using Serilog;

namespace GitReleaseManager.IntegrationTests
{
    [TestFixture]
    [Explicit]
    public class ReleaseNotesBuilderIntegrationTests
    {
        private IGitHubClient _gitHubClient;
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private ILogger _logger;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private IMapper _mapper;
        private string _token;

        public TestContext TestContext { get; set; }

        [OneTimeSetUp]
        public void Configure()
        {
            _mapper = AutoMapperConfiguration.Configure();
            _logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            Log.Logger = _logger;

            _token = Environment.GetEnvironmentVariable("GITTOOLS_GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(_token))
            {
                Assert.Inconclusive("Unable to locate credentials for accessing GitHub API");
            }

            _gitHubClient = new GitHubClient(new ProductHeaderValue("GitReleaseManager")) { Credentials = new Credentials(_token) };
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Log.CloseAndFlush();
            (_logger as IDisposable)?.Dispose();
        }

        [Test]
        [Explicit]
        public async Task SingleMilestone()
        {
            var fileSystem = new FileSystem(new CreateSubOptions());
            var currentDirectory = Environment.CurrentDirectory;
            var configuration = ConfigurationProvider.Provide(currentDirectory, fileSystem);

            var vcsProvider = new GitHubProvider(_gitHubClient, _mapper);
            var releaseNotesBuilder = new ReleaseNotesBuilder(vcsProvider, _logger, fileSystem, configuration, new TemplateFactory(fileSystem, configuration, TemplateKind.Create));
            var result = await releaseNotesBuilder.BuildReleaseNotesAsync("Chocolatey", "ChocolateyGUI", "0.12.4", ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            Debug.WriteLine(result);
            ClipBoardHelper.SetClipboard(result);
        }

        [Test]
        [Explicit]
        public async Task SingleMilestone3()
        {
            var fileSystem = new FileSystem(new CreateSubOptions());
            var currentDirectory = Environment.CurrentDirectory;
            var configuration = ConfigurationProvider.Provide(currentDirectory, fileSystem);

            var vcsProvider = new GitHubProvider(_gitHubClient, _mapper);
            var releaseNotesBuilder = new ReleaseNotesBuilder(vcsProvider, _logger, fileSystem, configuration, new TemplateFactory(fileSystem, configuration, TemplateKind.Create));
            var result = await releaseNotesBuilder.BuildReleaseNotesAsync("Chocolatey", "ChocolateyGUI", "0.13.0", ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            Debug.WriteLine(result);
            ClipBoardHelper.SetClipboard(result);
        }

        [Test]
        [Explicit]
        public void OctokitTests()
        {
            try
            {
                var client = ClientBuilder.Build();
                Assert.That(client, Is.Not.Null);
            }
            finally
            {
                ClientBuilder.Cleanup();
            }
        }
    }
}