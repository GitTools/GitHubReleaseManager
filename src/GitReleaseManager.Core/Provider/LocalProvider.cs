using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitReleaseManager.Core.Helpers;
using GitReleaseManager.Core.Model;

namespace GitReleaseManager.Core.Provider
{
    public partial class LocalProvider : IReleasesProvider
    {
        private const string NAME_AND_TAG_REGEX = @"^#+\s*(?<NAME>[^\(\r\n]+)(?:\s*\((?<TAG_NAME>[^\)\r\n]+))?";
        private readonly IFileSystem _fileSystem;
        private readonly string _outputPath;

        public LocalProvider(IFileSystem fileSystem, string outputPath)
        {
            _fileSystem = fileSystem;
            _outputPath = outputPath;
        }

        public bool SupportReleases => true;

        public async Task<Release> CreateReleaseAsync(string owner, string repository, Release release)
        {
            ArgumentNullException.ThrowIfNull(release);

            string directory = Path.GetDirectoryName(_outputPath) ?? Environment.CurrentDirectory;
            _fileSystem.CreateDirectory(directory);

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await using (var stream = _fileSystem.OpenWrite(_outputPath, overwrite: true))
            await using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)))
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
            {
                await writer.WriteAsync("# ").ConfigureAwait(false);
                await writer.WriteAsync(release.Name ?? release.TagName).ConfigureAwait(false);

                if (release.Name != null && release.Name != release.TagName)
                {
                    await writer.WriteAsync(" (").ConfigureAwait(false);
                    await writer.WriteAsync(release.TagName).ConfigureAwait(false);
                    await writer.WriteLineAsync(")").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync().ConfigureAwait(false);
                }

                await writer.WriteLineAsync().ConfigureAwait(false);

                await writer.WriteLineAsync(release.Body).ConfigureAwait(false);

                await writer.FlushAsync().ConfigureAwait(false);
            }

            release.CreatedAt = DateTime.UtcNow;
            release.HtmlUrl = _outputPath;

            return release;
        }

        public Task DeleteReleaseAsync(string owner, string repository, Release release)
        {
            _fileSystem.Delete(_outputPath);

            return Task.CompletedTask;
        }

        public async Task<Release> GetReleaseAsync(string owner, string repository, string tagName)
        {
            Release release = new Release();
            await using (var stream = _fileSystem.OpenRead(_outputPath))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                var line = await reader.ReadLineAsync();
                var titleMatch = NameAndTagNameRegex().Match(line);

                if (titleMatch.Success)
                {
                    release.Name = titleMatch.Groups["NAME"].Value;

                    if (titleMatch.Groups["TAG_NAME"].Success)
                    {
                        release.TagName = titleMatch.Groups["TAG_NAME"].Value;
                    }
                }

                line = await reader.ReadLineAsync();

                while (string.IsNullOrEmpty(line))
                {
                    line = await reader.ReadLineAsync();
                }

                var body = new StringBuilder(line).AppendLine();
                var block = await reader.ReadToEndAsync();

                if (block.Length > 0)
                {
                    body.Append(block);
                }

                if (release.Name.Length == 0 && body.Length == 0)
                {
                    return null;
                }
                else
                {
                    release.Body = body.ToString();
                    release.Draft = true;
                    release.HtmlUrl = _outputPath;
                    release.Prerelease = Regex.IsMatch(@"^\d\.+-[a-z]", release.Name, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
            }

            return release;
        }

        public Task<IEnumerable<Release>> GetReleasesAsync(string owner, string repository, bool skipPrereleases)
        {
            return Task.FromResult<IEnumerable<Release>>(null);
        }

        public Task PublishReleaseAsync(string owner, string repository, string tagName, Release release)
        {
            // No-op
            return Task.CompletedTask;
        }

        public async Task UpdateReleaseAsync(string owner, string repository, Release release)
        {
            await DeleteReleaseAsync(owner, repository, release).ConfigureAwait(false);
            await CreateReleaseAsync(owner, repository, release).ConfigureAwait(false);
        }

#if NET7_0_OR_GREATER && USE_GENERATED_REGEX
        [GeneratedRegex(NAME_AND_TAG_REGEX, RegexOptions.IgnoreCase)]
        private static partial Regex NameAndTagNameRegex();
#else
        private static Regex NameAndTagNameRegex()
        {
            return new Regex(NAME_AND_TAG_REGEX, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
#endif
    }
}
