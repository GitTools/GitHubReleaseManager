using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitReleaseManager.Core.Configuration;
using GitReleaseManager.Core.Model;
using GitReleaseManager.Core.Provider;
using GitReleaseManager.Core.ReleaseNotes;
using GitReleaseManager.Core.Templates;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Shouldly;
using ItemState = GitReleaseManager.Core.Model.ItemState;
using ItemStateFilter = GitReleaseManager.Core.Model.ItemStateFilter;
using Label = GitReleaseManager.Core.Model.Label;
using Milestone = GitReleaseManager.Core.Model.Milestone;
using NotFoundException = GitReleaseManager.Core.Exceptions.NotFoundException;
using Release = GitReleaseManager.Core.Model.Release;

namespace GitReleaseManager.Core.Tests
{
    public class VcsServiceTests
    {
        private const string OWNER = "owner";
        private const string REPOSITORY = "repository";
        private const int MILESTONE_PUBLIC_NUMBER = 1;
        private const int MILESTONE_INTERNAL_NUMBER = 123;
        private const string MILESTONE_TITLE = "0.1.0";
        private const string TAG_NAME = "0.1.0";
        private const string RELEASE_NOTES = "Release Notes";
        private const string ASSET_CONTENT = "Asset Content";
        private const bool SKIP_PRERELEASES = false;

        private const string UNABLE_TO_FOUND_MILESTONE_MESSAGE = "Unable to find a {State} milestone with title '{Title}' on '{Owner}/{Repository}'";
        private const string UNABLE_TO_FOUND_RELEASE_MESSAGE = "Unable to find a release with tag '{TagName}' on '{Owner}/{Repository}'";

        private static readonly string _tempPath = Path.GetTempPath();
        private static readonly string _releaseNotesTemplate = Path.Combine(_tempPath, "ReleaseNotesTemplate.txt");
        private readonly List<string> _assets = new List<string>();
        private readonly List<string> _files = new List<string>();
        private readonly NotFoundException _notFoundException = new NotFoundException("NotFound");

        private IReleaseNotesExporter _releaseNotesExporter;
        private IReleaseNotesBuilder _releaseNotesBuilder;
        private ILogger _logger;
        private IVcsProvider _vcsProvider;
        private IReleasesProvider _releaseProvider;
        private Config _configuration;
        private VcsService _vcsService;

        private string _releaseNotesFilePath;
        private string _releaseNotesTemplateFilePath;
        private string _releaseNotesEmptyTemplateFilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _releaseNotesFilePath = Path.Combine(_tempPath, "ReleaseNotes.txt");
            _releaseNotesTemplateFilePath = Path.Combine(_tempPath, "ReleaseNotesTemplate.txt");
            _releaseNotesEmptyTemplateFilePath = Path.Combine(_tempPath, "ReleaseNotesEmptyTemplate.txt");

            var fileContent = new Dictionary<string, string>
            {
                { _releaseNotesFilePath, RELEASE_NOTES },
                { _releaseNotesTemplateFilePath, _releaseNotesTemplate },
                { _releaseNotesEmptyTemplateFilePath, string.Empty },
            };

            for (int i = 0; i < 3; i++)
            {
                var fileName = $"Asset{i + 1}.txt";
                var filePath = Path.Combine(_tempPath, fileName);
                _assets.Add(filePath);

                fileContent.Add(filePath, ASSET_CONTENT);
            }

            _files.Add(_releaseNotesFilePath);
            _files.Add(_releaseNotesTemplateFilePath);
            _files.Add(_releaseNotesEmptyTemplateFilePath);
            _files.AddRange(_assets);

            foreach (var file in _files)
            {
                if (!File.Exists(file))
                {
                    File.WriteAllText(file, fileContent[file]);
                }
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            foreach (var file in _files)
            {
                if (!File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [SetUp]
        public void Setup()
        {
            _releaseNotesExporter = Substitute.For<IReleaseNotesExporter>();
            _releaseNotesBuilder = Substitute.For<IReleaseNotesBuilder>();
            _logger = Substitute.For<ILogger>();
            _vcsProvider = Substitute.For<IVcsProvider>();
            _releaseProvider = Substitute.For<IVcsProvider>();
            _configuration = new Config();
            _vcsService = new VcsService(_vcsProvider, _releaseProvider, _logger, _releaseNotesBuilder, _releaseNotesExporter, _configuration);
        }

        [Test]
        public async Task Should_Add_Assets()
        {
            var release = new Release { Assets = new List<ReleaseAsset>() };

            var assetsCount = _assets.Count;

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(release);

            await _vcsService.AddAssetsAsync(OWNER, REPOSITORY, TAG_NAME, _assets).ConfigureAwait(false);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().DeleteAssetAsync(OWNER, REPOSITORY, Arg.Any<ReleaseAsset>()).ConfigureAwait(false);
            await _vcsProvider.Received(assetsCount).UploadAssetAsync(release, Arg.Any<ReleaseAssetUpload>()).ConfigureAwait(false);

            _logger.DidNotReceive().Warning(Arg.Any<string>(), Arg.Any<string>());
            _logger.Received(assetsCount).Verbose(Arg.Any<string>(), Arg.Any<string>(), TAG_NAME, OWNER, REPOSITORY);
            _logger.Received(assetsCount).Debug(Arg.Any<string>(), Arg.Any<ReleaseAssetUpload>());
        }

        [Test]
        public async Task Should_Add_Assets_With_Deleting_Existing_Assets()
        {
            var releaseAsset = new ReleaseAsset { Id = 1, Name = "Asset1.txt" };
            var release = new Release { Assets = new List<ReleaseAsset> { releaseAsset } };

            var releaseAssetsCount = release.Assets.Count;
            var assetsCount = _assets.Count;

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(release);

            await _vcsService.AddAssetsAsync(OWNER, REPOSITORY, TAG_NAME, _assets).ConfigureAwait(false);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _vcsProvider.Received(releaseAssetsCount).DeleteAssetAsync(OWNER, REPOSITORY, releaseAsset).ConfigureAwait(false);
            await _vcsProvider.Received(assetsCount).UploadAssetAsync(release, Arg.Any<ReleaseAssetUpload>()).ConfigureAwait(false);

            _logger.Received(releaseAssetsCount).Warning(Arg.Any<string>(), Arg.Any<string>());
            _logger.Received(assetsCount).Verbose(Arg.Any<string>(), Arg.Any<string>(), TAG_NAME, OWNER, REPOSITORY);
            _logger.Received(assetsCount).Debug(Arg.Any<string>(), Arg.Any<ReleaseAssetUpload>());
        }

        [Test]
        public async Task Should_Throw_Exception_On_Adding_Assets_When_Asset_File_Not_Exists()
        {
            var release = new Release();
            var assetFilePath = Path.Combine(_tempPath, "AssetNotExists.txt");

            _assets[0] = assetFilePath;

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(release);

            var ex = await Should.ThrowAsync<FileNotFoundException>(() => _vcsService.AddAssetsAsync(OWNER, REPOSITORY, TAG_NAME, _assets)).ConfigureAwait(false);
            ex.Message.ShouldContain(assetFilePath);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().DeleteAssetAsync(OWNER, REPOSITORY, Arg.Any<ReleaseAsset>()).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().UploadAssetAsync(release, Arg.Any<ReleaseAssetUpload>()).ConfigureAwait(false);
        }

        [TestCaseSource(nameof(Assets_TestCases))]
        public async Task Should_Do_Nothing_On_Missing_Assets(IList<string> assets)
        {
            await _vcsService.AddAssetsAsync(OWNER, REPOSITORY, TAG_NAME, assets).ConfigureAwait(false);

            await _releaseProvider.DidNotReceive().GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().DeleteAssetAsync(OWNER, REPOSITORY, Arg.Any<ReleaseAsset>()).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().UploadAssetAsync(Arg.Any<Release>(), Arg.Any<ReleaseAssetUpload>()).ConfigureAwait(false);
        }

        public static IEnumerable Assets_TestCases()
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new List<string>());
        }

        [Test]
        public async Task Should_Create_Labels()
        {
            var labels = new List<Label>
            {
                new Label { Name = "Bug" },
                new Label { Name = "Feature" },
                new Label { Name = "Improvement" },
            };

            _vcsProvider.GetLabelsAsync(OWNER, REPOSITORY)
                .Returns(Task.FromResult((IEnumerable<Label>)labels));

            _vcsProvider.DeleteLabelAsync(OWNER, REPOSITORY, Arg.Any<Label>())
                .Returns(Task.CompletedTask);

            _vcsProvider.CreateLabelAsync(OWNER, REPOSITORY, Arg.Any<Label>())
                .Returns(Task.CompletedTask);

            await _vcsService.CreateLabelsAsync(OWNER, REPOSITORY).ConfigureAwait(false);

            await _vcsProvider.Received(1).GetLabelsAsync(OWNER, REPOSITORY).ConfigureAwait(false);
            await _vcsProvider.Received(labels.Count).DeleteLabelAsync(OWNER, REPOSITORY, Arg.Any<Label>()).ConfigureAwait(false);
            await _vcsProvider.Received(_configuration.Labels.Count).CreateLabelAsync(OWNER, REPOSITORY, Arg.Any<Label>()).ConfigureAwait(false);

            _logger.Received(1).Verbose(Arg.Any<string>(), OWNER, REPOSITORY);
            _logger.Received(2).Verbose(Arg.Any<string>());
            _logger.Received(2).Debug(Arg.Any<string>(), Arg.Any<IEnumerable<Label>>());
        }

        [Test]
        public async Task Should_Log_An_Warning_When_Labels_Not_Configured()
        {
            _configuration.Labels.Clear();

            await _vcsService.CreateLabelsAsync(OWNER, REPOSITORY).ConfigureAwait(false);

            _logger.Received(1).Warning(Arg.Any<string>());
        }

        [Test]
        public async Task Should_Close_Milestone()
        {
            var milestone = new Milestone { PublicNumber = MILESTONE_PUBLIC_NUMBER, InternalNumber = MILESTONE_INTERNAL_NUMBER };

            _vcsProvider.GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Open)
                .Returns(Task.FromResult(milestone));

            _vcsProvider.SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Closed)
                .Returns(Task.CompletedTask);

            await _vcsService.CloseMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);

            await _vcsProvider.Received(1).GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Open).ConfigureAwait(false);
            await _vcsProvider.Received(1).SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Closed).ConfigureAwait(false);
        }

        [Test]
        public async Task Should_Log_An_Warning_On_Closing_When_Milestone_Cannot_Be_Found()
        {
            var milestone = new Milestone { PublicNumber = MILESTONE_PUBLIC_NUMBER, InternalNumber = MILESTONE_INTERNAL_NUMBER };

            _vcsProvider.GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Open)
                .Returns(Task.FromException<Milestone>(_notFoundException));

            await _vcsService.CloseMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);

            await _vcsProvider.Received(1).GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Open).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Closed).ConfigureAwait(false);
            _logger.Received(1).Warning(UNABLE_TO_FOUND_MILESTONE_MESSAGE, "open", MILESTONE_TITLE, OWNER, REPOSITORY);
        }

        [Test]
        public async Task Should_Open_Milestone()
        {
            var milestone = new Milestone { PublicNumber = MILESTONE_PUBLIC_NUMBER, InternalNumber = MILESTONE_INTERNAL_NUMBER };

            _vcsProvider.GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Closed)
                .Returns(Task.FromResult(milestone));

            _vcsProvider.SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Open)
                .Returns(Task.CompletedTask);

            await _vcsService.OpenMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);

            await _vcsProvider.Received(1).GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Closed).ConfigureAwait(false);
            await _vcsProvider.Received(1).SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Open).ConfigureAwait(false);
            _logger.Received(2).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
        }

        [Test]
        public async Task Should_Log_An_Warning_On_Opening_When_Milestone_Cannot_Be_Found()
        {
            var milestone = new Milestone { PublicNumber = MILESTONE_PUBLIC_NUMBER, InternalNumber = MILESTONE_INTERNAL_NUMBER };

            _vcsProvider.GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Closed)
                .Returns(Task.FromException<Milestone>(_notFoundException));

            await _vcsService.OpenMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);

            await _vcsProvider.Received(1).GetMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ItemStateFilter.Closed).ConfigureAwait(false);
            await _vcsProvider.DidNotReceive().SetMilestoneStateAsync(OWNER, REPOSITORY, milestone, ItemState.Open).ConfigureAwait(false);
            _logger.Received(1).Warning(UNABLE_TO_FOUND_MILESTONE_MESSAGE, "closed", MILESTONE_TITLE, OWNER, REPOSITORY);
        }

        [Test]
        public async Task Should_Create_Release_From_Milestone()
        {
            var release = new Release();

            _releaseNotesBuilder.BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME)
                .Returns(Task.FromResult(RELEASE_NOTES));

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult<Release>(null));

            _releaseProvider.CreateReleaseAsync(OWNER, REPOSITORY, Arg.Any<Release>())
                .Returns(Task.FromResult(release));

            var result = await _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, null).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseNotesBuilder.Received(1).BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).CreateReleaseAsync(OWNER, REPOSITORY, Arg.Is<Release>(o =>
                o.Body == RELEASE_NOTES &&
                o.Name == MILESTONE_TITLE &&
                o.TagName == MILESTONE_TITLE)).ConfigureAwait(false);

            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), Arg.Any<Release>());
        }

        [Test]
        public async Task Should_Create_Release_From_Milestone_With_Assets()
        {
            var release = new Release { Assets = new List<ReleaseAsset>() };

            var assetsCount = _assets.Count;

            _releaseNotesBuilder.BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME)
                .Returns(Task.FromResult(RELEASE_NOTES));

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult<Release>(null));

            _releaseProvider.CreateReleaseAsync(OWNER, REPOSITORY, Arg.Any<Release>())
                .Returns(Task.FromResult(release));

            var result = await _vcsService.CreateReleaseFromMilestoneAsync(
                    OWNER,
                    REPOSITORY,
                    MILESTONE_TITLE,
                    MILESTONE_TITLE,
                    null,
                    _assets,
                    false,
                    null).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseNotesBuilder.Received(1).BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).CreateReleaseAsync(OWNER, REPOSITORY, Arg.Is<Release>(o =>
                o.Body == RELEASE_NOTES &&
                o.Name == MILESTONE_TITLE &&
                o.TagName == MILESTONE_TITLE)).ConfigureAwait(false);

            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), Arg.Any<Release>());
        }

        [Test]
        public async Task Should_Create_Release_From_Milestone_Using_Template_File()
        {
            var release = new Release();

            _releaseNotesBuilder.BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, _releaseNotesTemplate)
                .Returns(Task.FromResult(RELEASE_NOTES));

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult<Release>(null));

            _releaseProvider.CreateReleaseAsync(OWNER, REPOSITORY, Arg.Any<Release>())
                .Returns(Task.FromResult(release));

            var result = await _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, _releaseNotesTemplateFilePath).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseNotesBuilder.Received(1).BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, _releaseNotesTemplate).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).CreateReleaseAsync(OWNER, REPOSITORY, Arg.Is<Release>(o =>
                o.Body == RELEASE_NOTES &&
                o.Name == MILESTONE_TITLE &&
                o.TagName == MILESTONE_TITLE)).ConfigureAwait(false);

            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), Arg.Any<Release>());
        }

        [Test]
        [Ignore("This may be handled by the TemplateLoader instead")]
        public async Task Should_Throw_Exception_On_Creating_Release_With_Empty_Template()
        {
            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromException<Release>(_notFoundException));

            await Should.ThrowAsync<ArgumentException>(() => _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, _releaseNotesEmptyTemplateFilePath)).ConfigureAwait(false);

            await _releaseNotesBuilder.DidNotReceive().BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, Arg.Any<string>()).ConfigureAwait(false);
        }

        [Test]
        [Ignore("This may be handled by the TemplateLoader instead")]
        public async Task Should_Throw_Exception_On_Creating_Release_With_Invalid_Template_File_Path()
        {
            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromException<Release>(_notFoundException));

            var fileName = "InvalidReleaseNotesTemplate.txt";
            var invalidReleaseNotesTemplateFilePath = Path.Combine(_tempPath, fileName);

            var ex = await Should.ThrowAsync<FileNotFoundException>(() => _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, invalidReleaseNotesTemplateFilePath)).ConfigureAwait(false);
            ex.Message.ShouldContain(invalidReleaseNotesTemplateFilePath);
            ex.FileName.ShouldBe(fileName);

            await _releaseNotesBuilder.DidNotReceive().BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, Arg.Any<string>()).ConfigureAwait(false);
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public async Task Should_Update_Published_Release_On_Creating_Release_From_Milestone(bool isDraft, bool updatePublishedRelease)
        {
            var release = new Release { Draft = isDraft };

            _configuration.Create.AllowUpdateToPublishedRelease = updatePublishedRelease;

            _releaseNotesBuilder.BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME)
                .Returns(Task.FromResult(RELEASE_NOTES));

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult(release));

            _releaseProvider.UpdateReleaseAsync(OWNER, REPOSITORY, release)
                .Returns(Task.FromResult(new Release()));

            var result = await _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, null).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseNotesBuilder.Received(1).BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).UpdateReleaseAsync(OWNER, REPOSITORY, release).ConfigureAwait(false);

            _logger.Received(1).Warning(Arg.Any<string>(), MILESTONE_TITLE);
            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), release);
        }

        [Test]
        public async Task Should_Throw_Exception_While_Updating_Published_Release_On_Creating_Release_From_Milestone()
        {
            var release = new Release { Draft = false };

            _configuration.Create.AllowUpdateToPublishedRelease = false;

            _releaseNotesBuilder.BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME)
                .Returns(Task.FromResult(RELEASE_NOTES));

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult(release));

            var ex = await Should.ThrowAsync<InvalidOperationException>(() => _vcsService.CreateReleaseFromMilestoneAsync(OWNER, REPOSITORY, MILESTONE_TITLE, MILESTONE_TITLE, null, null, false, null)).ConfigureAwait(false);
            ex.Message.ShouldBe($"Release with tag '{MILESTONE_TITLE}' not in draft state, so not updating");

            await _releaseNotesBuilder.Received(1).BuildReleaseNotesAsync(OWNER, REPOSITORY, MILESTONE_TITLE, ReleaseTemplates.DEFAULT_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
        }

        [Test]
        public async Task Should_Create_Release_From_InputFile()
        {
            var release = new Release();

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult<Release>(null));

            _releaseProvider.CreateReleaseAsync(OWNER, REPOSITORY, Arg.Any<Release>())
                .Returns(Task.FromResult(release));

            var result = await _vcsService.CreateReleaseFromInputFileAsync(OWNER, REPOSITORY, MILESTONE_TITLE, _releaseNotesFilePath, null, null, false).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).CreateReleaseAsync(OWNER, REPOSITORY, Arg.Is<Release>(o =>
                o.Body == RELEASE_NOTES &&
                o.Name == MILESTONE_TITLE &&
                o.TagName == MILESTONE_TITLE)).ConfigureAwait(false);

            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), Arg.Any<Release>());
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public async Task Should_Update_Published_Release_On_Creating_Release_From_InputFile(bool isDraft, bool updatePublishedRelease)
        {
            var release = new Release { Draft = isDraft };

            _configuration.Create.AllowUpdateToPublishedRelease = updatePublishedRelease;

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult(release));

            _releaseProvider.UpdateReleaseAsync(OWNER, REPOSITORY, release)
                .Returns(Task.FromResult(new Release()));

            var result = await _vcsService.CreateReleaseFromInputFileAsync(OWNER, REPOSITORY, MILESTONE_TITLE, _releaseNotesFilePath, null, null, false).ConfigureAwait(false);
            result.ShouldBeSameAs(release);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
            await _releaseProvider.Received(1).UpdateReleaseAsync(OWNER, REPOSITORY, release).ConfigureAwait(false);

            _logger.Received(1).Warning(Arg.Any<string>(), MILESTONE_TITLE);
            _logger.Received(1).Verbose(Arg.Any<string>(), MILESTONE_TITLE, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), release);
        }

        [Test]
        public async Task Should_Throw_Exception_While_Updating_Published_Release_On_Creating_Release_From_InputFile()
        {
            var release = new Release { Draft = false };

            _configuration.Create.AllowUpdateToPublishedRelease = false;

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE)
                .Returns(Task.FromResult(release));

            var ex = await Should.ThrowAsync<InvalidOperationException>(() => _vcsService.CreateReleaseFromInputFileAsync(OWNER, REPOSITORY, MILESTONE_TITLE, _releaseNotesFilePath, null, null, false)).ConfigureAwait(false);
            ex.Message.ShouldBe($"Release with tag '{MILESTONE_TITLE}' not in draft state, so not updating");

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, MILESTONE_TITLE).ConfigureAwait(false);
        }

        [Test]
        public async Task Should_Throw_Exception_Creating_Release_From_InputFile()
        {
            var fileName = "NonExistingReleaseNotes.txt";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);

            var ex = await Should.ThrowAsync<FileNotFoundException>(() => _vcsService.CreateReleaseFromInputFileAsync(OWNER, REPOSITORY, MILESTONE_TITLE, filePath, null, null, false)).ConfigureAwait(false);
            ex.Message.ShouldBe("Unable to locate input file.");
            ex.FileName.ShouldBe(fileName);
        }

        [Test]
        public async Task Should_Delete_Draft_Release()
        {
            var release = new Release
            {
                Id = 1,
                Draft = true,
            };

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromResult(release));

            _releaseProvider.DeleteReleaseAsync(OWNER, REPOSITORY, release)
                .Returns(Task.CompletedTask);

            await _vcsService.DiscardReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).DeleteReleaseAsync(OWNER, REPOSITORY, release).ConfigureAwait(false);
        }

        [Test]
        public async Task Should_Not_Delete_Published_Release()
        {
            var release = new Release { Id = 1 };

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromResult(release));

            await _vcsService.DiscardReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.DidNotReceive().DeleteReleaseAsync(OWNER, REPOSITORY, release).ConfigureAwait(false);
            _logger.Received(1).Warning(Arg.Any<string>(), TAG_NAME);
        }

        [Test]
        public async Task Should_Log_An_Warning_On_Deleting_Release_For_Non_Existing_Tag()
        {
            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromException<Release>(_notFoundException));

            await _vcsService.DiscardReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);

            await _releaseProvider.Received().GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.DidNotReceiveWithAnyArgs().DeleteReleaseAsync(OWNER, REPOSITORY, default).ConfigureAwait(false);
            _logger.Received(1).Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, TAG_NAME, OWNER, REPOSITORY);
        }

        [Test]
        public async Task Should_Publish_Release()
        {
            var release = new Release { Id = 1 };

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromResult(release));

            _releaseProvider.PublishReleaseAsync(OWNER, REPOSITORY, TAG_NAME, release)
                .Returns(Task.CompletedTask);

            await _vcsService.PublishReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.Received(1).PublishReleaseAsync(OWNER, REPOSITORY, TAG_NAME, release).ConfigureAwait(false);
            _logger.Received(1).Verbose(Arg.Any<string>(), TAG_NAME, OWNER, REPOSITORY);
            _logger.Received(1).Debug(Arg.Any<string>(), Arg.Any<Release>());
        }

        [Test]
        public async Task Should_Log_An_Warning_On_Publishing_Release_For_Non_Existing_Tag()
        {
            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromException<Release>(_notFoundException));

            await _vcsService.PublishReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);

            await _releaseProvider.Received().GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.DidNotReceiveWithAnyArgs().PublishReleaseAsync(OWNER, REPOSITORY, TAG_NAME, default).ConfigureAwait(false);
            _logger.Received(1).Warning(Arg.Any<string>(), TAG_NAME, OWNER, REPOSITORY);
        }

        [Test]
        public async Task Should_Get_Release_Notes()
        {
            var releases = Enumerable.Empty<Release>();
            var releaseNotes = "Release Notes";

            _releaseProvider.GetReleasesAsync(OWNER, REPOSITORY, SKIP_PRERELEASES)
                .Returns(Task.FromResult(releases));

            _releaseNotesExporter.ExportReleaseNotes(Arg.Any<IEnumerable<Release>>())
                .Returns(releaseNotes);

            var result = await _vcsService.ExportReleasesAsync(OWNER, REPOSITORY, null, SKIP_PRERELEASES).ConfigureAwait(false);
            result.ShouldBeSameAs(releaseNotes);

            await _releaseProvider.DidNotReceive().GetReleaseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).ConfigureAwait(false);
            await _releaseProvider.Received(1).GetReleasesAsync(OWNER, REPOSITORY, SKIP_PRERELEASES).ConfigureAwait(false);
            _logger.Received(1).Verbose(Arg.Any<string>(), OWNER, REPOSITORY);
            _releaseNotesExporter.Received(1).ExportReleaseNotes(Arg.Any<IEnumerable<Release>>());
        }

        [Test]
        public async Task Should_Get_Release_Notes_For_Tag()
        {
            var release = new Release();

            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromResult(release));

            _releaseNotesExporter.ExportReleaseNotes(Arg.Any<IEnumerable<Release>>())
                .Returns(RELEASE_NOTES);

            var result = await _vcsService.ExportReleasesAsync(OWNER, REPOSITORY, TAG_NAME, SKIP_PRERELEASES).ConfigureAwait(false);
            result.ShouldBeSameAs(RELEASE_NOTES);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            await _releaseProvider.DidNotReceive().GetReleasesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).ConfigureAwait(false);
            _logger.Received(1).Verbose(Arg.Any<string>(), OWNER, REPOSITORY, TAG_NAME);
            _releaseNotesExporter.Received(1).ExportReleaseNotes(Arg.Any<IEnumerable<Release>>());
        }

        [Test]
        public async Task Should_Get_Default_Release_Notes_For_Non_Existent_Tag()
        {
            _releaseProvider.GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME)
                .Returns(Task.FromException<Release>(_notFoundException));

            _releaseNotesExporter.ExportReleaseNotes(Arg.Any<IEnumerable<Release>>())
                .Returns(RELEASE_NOTES);

            var result = await _vcsService.ExportReleasesAsync(OWNER, REPOSITORY, TAG_NAME, SKIP_PRERELEASES).ConfigureAwait(false);
            result.ShouldBeSameAs(RELEASE_NOTES);

            await _releaseProvider.Received(1).GetReleaseAsync(OWNER, REPOSITORY, TAG_NAME).ConfigureAwait(false);
            _logger.Received(1).Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, TAG_NAME, OWNER, REPOSITORY);
            _releaseNotesExporter.Received(1).ExportReleaseNotes(Arg.Any<IEnumerable<Release>>());
        }
    }
}