using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitReleaseManager.Core.Configuration;
using GitReleaseManager.Core.Exceptions;
using GitReleaseManager.Core.Extensions;
using GitReleaseManager.Core.Model;
using GitReleaseManager.Core.Provider;
using GitReleaseManager.Core.ReleaseNotes;
using GitReleaseManager.Core.Templates;
using Serilog;

namespace GitReleaseManager.Core
{
    public class VcsService : IVcsService
    {
        private const string UNABLE_TO_FOUND_MILESTONE_MESSAGE = "Unable to find a {State} milestone with title '{Title}' on '{Owner}/{Repository}'";
        private const string UNABLE_TO_FOUND_RELEASE_MESSAGE = "Unable to find a release with tag '{TagName}' on '{Owner}/{Repository}'";

        private readonly IVcsProvider _vcsProvider;
        private readonly ILogger _logger;
        private readonly IReleaseNotesBuilder _releaseNotesBuilder;
        private readonly IReleaseNotesExporter _releaseNotesExporter;
        private readonly Config _configuration;

        public VcsService(IVcsProvider vcsProvider, ILogger logger, IReleaseNotesBuilder releaseNotesBuilder, IReleaseNotesExporter releaseNotesExporter, Config configuration)
        {
            _vcsProvider = vcsProvider;
            _logger = logger;
            _releaseNotesBuilder = releaseNotesBuilder;
            _releaseNotesExporter = releaseNotesExporter;
            _configuration = configuration;
        }

        public async Task<Release> CreateEmptyReleaseAsync(string owner, string repository, string name, string targetCommitish, bool prerelease)
        {
            var release = await CreateReleaseAsync(owner, repository, name, name, string.Empty, prerelease, targetCommitish, null).ConfigureAwait(false);
            return release;
        }

        public async Task<Release> CreateReleaseFromMilestoneAsync(string owner, string repository, string milestone, string releaseName, string targetCommitish, IList<string> assets, bool prerelease, string templateFilePath)
        {
            var templatePath = ReleaseTemplates.DEFAULT_NAME;

            if (!string.IsNullOrWhiteSpace(templateFilePath))
            {
                templatePath = templateFilePath;
            }

            var releaseNotes = await _releaseNotesBuilder.BuildReleaseNotesAsync(owner, repository, milestone, templatePath).ConfigureAwait(false);
            var release = await CreateReleaseAsync(owner, repository, releaseName, milestone, releaseNotes, prerelease, targetCommitish, assets).ConfigureAwait(false);

            return release;
        }

        public async Task<Release> CreateReleaseFromInputFileAsync(string owner, string repository, string name, string inputFilePath, string targetCommitish, IList<string> assets, bool prerelease)
        {
            Ensure.FileExists(inputFilePath, "Unable to locate input file.");

            _logger.Verbose("Reading release notes from: '{FilePath}'", inputFilePath);

            var releaseNotes = File.ReadAllText(inputFilePath);
            var release = await CreateReleaseAsync(owner, repository, name, name, releaseNotes, prerelease, targetCommitish, assets).ConfigureAwait(false);

            return release;
        }

        private async Task<Release> CreateReleaseAsync(string owner, string repository, string name, string tagName, string body, bool prerelease, string targetCommitish, IList<string> assets)
        {
            Release release;

            release = await _vcsProvider.GetReleaseAsync(owner, repository, tagName).ConfigureAwait(false);

            if (release == null)
            {
                release = CreateReleaseModel(name, tagName, body, prerelease, targetCommitish);

                _logger.Verbose("Creating new release with tag '{TagName}' on '{Owner}/{Repository}'", tagName, owner, repository);
                _logger.Debug("{@Release}", release);

                release = await _vcsProvider.CreateReleaseAsync(owner, repository, release).ConfigureAwait(false);
            }
            else
            {
                if (!release.Draft && !_configuration.Create.AllowUpdateToPublishedRelease)
                {
                    throw new InvalidOperationException($"Release with tag '{tagName}' not in draft state, so not updating");
                }

                release.Body = body;

                _logger.Warning("A release for milestone '{Milestone}' already exists, and will be updated", tagName);
                _logger.Verbose("Updating release with tag '{TagName}' on '{Owner}/{Repository}'", tagName, owner, repository);
                _logger.Debug("{@Release}", release);

                await _vcsProvider.UpdateReleaseAsync(owner, repository, release).ConfigureAwait(false);
            }

            await AddAssetsAsync(owner, repository, tagName, assets, release).ConfigureAwait(false);

            return release;
        }

        public async Task DiscardReleaseAsync(string owner, string repository, string tagName)
        {
            try
            {
                var release = await _vcsProvider.GetReleaseAsync(owner, repository, tagName).ConfigureAwait(false);

                if (release.Draft)
                {
                    await _vcsProvider.DeleteReleaseAsync(owner, repository, release).ConfigureAwait(false);
                }
                else
                {
                    _logger.Warning("Release with tag '{TagName}' is not in draft state, so not discarding.", tagName);
                }
            }
            catch (NotFoundException)
            {
                _logger.Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, tagName, owner, repository);
            }
        }

        public async Task AddAssetsAsync(string owner, string repository, string tagName, IList<string> assets) => await AddAssetsAsync(owner, repository, tagName, assets, null).ConfigureAwait(false);

        private async Task AddAssetsAsync(string owner, string repository, string tagName, IList<string> assets, Release currentRelease)
        {
            if (assets?.Any() == true)
            {
                try
                {
                    var release = currentRelease ?? await _vcsProvider.GetReleaseAsync(owner, repository, tagName).ConfigureAwait(false);

                    foreach (var asset in assets)
                    {
                        if (!File.Exists(asset))
                        {
                            var message = string.Format(CultureInfo.CurrentCulture, "The requested asset to be uploaded doesn't exist: {0}", asset);
                            throw new FileNotFoundException(message);
                        }

                        var assetFileName = Path.GetFileName(asset);
                        var existingAsset = release.Assets.FirstOrDefault(a => a.Name == assetFileName);

                        if (existingAsset != null)
                        {
                            _logger.Warning("Requested asset to be uploaded already exists on draft release, replacing with new file: {AssetPath}", asset);

                            if (_vcsProvider is GitLabProvider)
                            {
                                _logger.Error("Deleting assets is not currently supported when targeting GitLab.");
                            }
                            else
                            {
                                await _vcsProvider.DeleteAssetAsync(owner, repository, existingAsset).ConfigureAwait(false);
                            }
                        }

                        var upload = new ReleaseAssetUpload
                        {
                            FileName = assetFileName,
                            ContentType = "application/octet-stream",
                            RawData = File.Open(asset, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                        };

                        _logger.Verbose("Uploading asset '{FileName}' to release '{TagName}' on '{Owner}/{Repository}'", assetFileName, tagName, owner, repository);
                        _logger.Debug("{@Upload}", upload);

                        await _vcsProvider.UploadAssetAsync(release, upload).ConfigureAwait(false);

                        // Make sure to tidy up the stream that was created above
                        upload.RawData.Dispose();
                    }

                    if (_configuration.Create.IncludeShaSection)
                    {
                        var stringBuilder = new StringBuilder(release.Body);

                        if (!release.Body.Contains(_configuration.Create.ShaSectionHeading))
                        {
                            _logger.Debug("Creating SHA section header");
                            stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "### {0}", _configuration.Create.ShaSectionHeading).AppendLine();
                        }

                        foreach (var asset in assets)
                        {
                            var file = new FileInfo(asset);

                            if (!file.Exists)
                            {
                                continue;
                            }

                            _logger.Debug("Creating SHA checksum for {Name}.", file.Name);

                            stringBuilder.AppendFormat(CultureInfo.InvariantCulture, _configuration.Create.ShaSectionLineFormat, file.Name, ComputeSha256Hash(asset));
                            stringBuilder.AppendLine();
                        }

                        stringBuilder.AppendLine();

                        release.Body = stringBuilder.ToString();

                        _logger.Verbose("Updating release notes with SHA checksum");
                        _logger.Debug("{@Release}", release);

                        await _vcsProvider.UpdateReleaseAsync(owner, repository, release).ConfigureAwait(false);
                    }
                }
                catch (NotFoundException)
                {
                    _logger.Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, tagName, owner, repository);
                }
            }
        }

        public async Task<string> ExportReleasesAsync(string owner, string repository, string tagName, bool skipPrereleases)
        {
            var releases = Enumerable.Empty<Release>();

            if (string.IsNullOrWhiteSpace(tagName))
            {
                _logger.Verbose("Finding all releases on '{Owner}/{Repository}'", owner, repository);
                releases = await _vcsProvider.GetReleasesAsync(owner, repository, skipPrereleases).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    _logger.Verbose("Finding release with tag '{TagName}' on '{Owner}/{Repository}'", owner, repository, tagName);
                    var release = await _vcsProvider.GetReleaseAsync(owner, repository, tagName).ConfigureAwait(false);
                    releases = new List<Release> { release };
                }
                catch (NotFoundException)
                {
                    _logger.Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, tagName, owner, repository);
                }
            }

            return _releaseNotesExporter.ExportReleaseNotes(releases);
        }

        public async Task CloseMilestoneAsync(string owner, string repository, string milestoneTitle)
        {
            try
            {
                _logger.Verbose("Finding open milestone with title '{Title}' on '{Owner}/{Repository}'", milestoneTitle, owner, repository);
                var milestone = await _vcsProvider.GetMilestoneAsync(owner, repository, milestoneTitle, ItemStateFilter.Open).ConfigureAwait(false);

                // Set the due date only if configured to do so
                milestone.DueOn = _configuration.Close.SetDueDate ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;

                _logger.Verbose("Closing milestone '{Title}' on '{Owner}/{Repository}'", milestoneTitle, owner, repository);
                await _vcsProvider.SetMilestoneStateAsync(owner, repository, milestone, ItemState.Closed).ConfigureAwait(false);

                if (_configuration.Close.IssueComments)
                {
                    await AddIssueCommentsAsync(owner, repository, milestone).ConfigureAwait(false);
                }
            }
            catch (NotFoundException)
            {
                _logger.Warning(UNABLE_TO_FOUND_MILESTONE_MESSAGE, "open", milestoneTitle, owner, repository);
            }
        }

        public async Task OpenMilestoneAsync(string owner, string repository, string milestoneTitle)
        {
            try
            {
                _logger.Verbose("Finding closed milestone with title '{Title}' on '{Owner}/{Repository}'", milestoneTitle, owner, repository);
                var milestone = await _vcsProvider.GetMilestoneAsync(owner, repository, milestoneTitle, ItemStateFilter.Closed).ConfigureAwait(false);

                _logger.Verbose("Opening milestone '{Title}' on '{Owner}/{Repository}'", milestoneTitle, owner, repository);
                await _vcsProvider.SetMilestoneStateAsync(owner, repository, milestone, ItemState.Open).ConfigureAwait(false);
            }
            catch (NotFoundException)
            {
                _logger.Warning(UNABLE_TO_FOUND_MILESTONE_MESSAGE, "closed", milestoneTitle, owner, repository);
            }
        }

        public async Task PublishReleaseAsync(string owner, string repository, string tagName)
        {
            try
            {
                var release = await _vcsProvider.GetReleaseAsync(owner, repository, tagName).ConfigureAwait(false);

                _logger.Verbose("Publishing release '{TagName}' on '{Owner}/{Repository}'", tagName, owner, repository);
                _logger.Debug("{@Release}", release);

                await _vcsProvider.PublishReleaseAsync(owner, repository, tagName, release).ConfigureAwait(false);
            }
            catch (NotFoundException)
            {
                _logger.Warning(UNABLE_TO_FOUND_RELEASE_MESSAGE, tagName, owner, repository);
            }
        }

        public async Task CreateLabelsAsync(string owner, string repository)
        {
            if (_configuration.Labels.Any())
            {
                var newLabels = new List<Label>();

                foreach (var label in _configuration.Labels)
                {
                    newLabels.Add(new Label
                    {
                        Name = label.Name,
                        Color = label.Color,
                        Description = label.Description,
                    });
                }

                _logger.Verbose("Grabbing all existing labels on '{Owner}/{Repository}'", owner, repository);
                var labels = await _vcsProvider.GetLabelsAsync(owner, repository).ConfigureAwait(false);

                _logger.Verbose("Removing existing labels");
                _logger.Debug("{@Labels}", labels);
                var deleteLabelsTasks = labels.Select(label => _vcsProvider.DeleteLabelAsync(owner, repository, label));
                await Task.WhenAll(deleteLabelsTasks).ConfigureAwait(false);

                _logger.Verbose("Creating new standard labels");
                _logger.Debug("{@Labels}", newLabels);
                var createLabelsTasks = newLabels.Select(label => _vcsProvider.CreateLabelAsync(owner, repository, label));
                await Task.WhenAll(createLabelsTasks).ConfigureAwait(false);
            }
            else
            {
                _logger.Warning("No labels defined");
            }
        }

        private static Release CreateReleaseModel(string name, string tagName, string body, bool prerelease, string targetCommitish)
        {
            var release = new Release
            {
                Draft = true,
                Body = body,
                Name = name,
                TagName = tagName,
                Prerelease = prerelease,
            };

            if (!string.IsNullOrEmpty(targetCommitish))
            {
                release.TargetCommitish = targetCommitish;
            }

            return release;
        }

        private static string ComputeSha256Hash(string asset)
        {
            // Create a SHA256
            using (var sha256Hash = SHA256.Create())
            {
                using (var fileStream = File.Open(asset, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // ComputeHash - returns byte array
                    var bytes = sha256Hash.ComputeHash(fileStream);

                    // Convert byte array to a string
                    var builder = new StringBuilder();

                    foreach (var t in bytes)
                    {
                        builder.Append(t.ToString("x2"));
                    }

                    return builder.ToString();
                }
            }
        }

        private async Task AddIssueCommentsAsync(string owner, string repository, Milestone milestone)
        {
            const string detectionComment = "<!-- GitReleaseManager release comment -->";
            var issueComment = detectionComment + "\n" + _configuration.Close.IssueCommentFormat.ReplaceTemplate(new { owner, repository, Milestone = milestone.Title });

            _logger.Verbose("Finding issues with milestone: '{Milestone}", milestone.PublicNumber);
            var issues = await _vcsProvider.GetIssuesAsync(owner, repository, milestone, ItemStateFilter.Closed).ConfigureAwait(false);

            foreach (var issue in issues)
            {
                SleepWhenRateIsLimited();

                try
                {
                    var issueType = _vcsProvider.GetIssueType(issue);
                    if (!await CommentsIncludeStringAsync(owner, repository, issue, detectionComment).ConfigureAwait(false))
                    {
                        _logger.Information("Adding release comment for {IssueType} #{IssueNumber}", issueType, issue.PublicNumber);
                        await _vcsProvider.CreateIssueCommentAsync(owner, repository, issue, issueComment).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.Information("{IssueType} #{IssueNumber} already contains release comment, skipping...", issueType, issue.PublicNumber);
                    }
                }
                catch (ForbiddenException)
                {
                    _logger.Error("Unable to add a comment to issue #{IssueNumber}. Insufficient permissions.", issue.PublicNumber);
                    break;
                }
            }
        }

        private async Task<bool> CommentsIncludeStringAsync(string owner, string repository, Issue issue, string comment)
        {
            _logger.Verbose("Finding issue comment created by GitReleaseManager for issue #{IssueNumber}", issue.PublicNumber);
            var issueComments = await _vcsProvider.GetIssueCommentsAsync(owner, repository, issue).ConfigureAwait(false);

            return issueComments.Any(c => c.Body.Contains(comment));
        }

        private void SleepWhenRateIsLimited()
        {
            var rateLimit = _vcsProvider.GetRateLimit();

            if (rateLimit?.Remaining == 0)
            {
                var sleepTime = rateLimit.Reset - DateTimeOffset.Now;
                _logger.Warning("Rate limit exceeded, sleeping for {$SleepTime}", sleepTime);
                Thread.Sleep(sleepTime);
            }
        }
    }
}