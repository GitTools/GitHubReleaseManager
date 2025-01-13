using System.Collections.Generic;
using CommandLine;

namespace GitReleaseManager.Core.Options
{
    [Verb("create", HelpText = "Creates a draft release notes from a milestone.")]
    public class CreateSubOptions : BaseVcsOptions
    {
        [Option('a', "assets", Separator = ',', HelpText = "Paths to the files to include in the release.", Required = false)]
        public IList<string> AssetPaths { get; set; }

        [Option('c', "targetcommitish", HelpText = "The commit to tag. Can be a branch or SHA. Defaults to repository's default branch.", Required = false)]
        public string TargetCommitish { get; set; }

        [Option('m', "milestone", HelpText = "The milestone to use. (Can't be used together with a release notes file path).", Required = false)]
        public string Milestone { get; set; }

        [Option('n', "name", HelpText = "The name of the release (Typically this is the generated SemVer Version Number).", Required = false)]
        public string Name { get; set; }

        [Option('i', "inputFilePath", HelpText = "The path to the file to be used as the content of the release notes. (Can't be used together with a milestone)", Required = false)]
        public string InputFilePath { get; set; }

        [Option('t', "template", HelpText = "The name of the template file to use. Can also be a relative or absolute path (relative paths are resolved from yaml template-dir configuration). Defaults to 'default'")]
        public string Template { get; set; }

        [Option('e', "pre", Required = false, HelpText = "Creates the release as a pre-release.")]
        public bool Prerelease { get; set; }

        [Option("allowEmpty", Required = false, HelpText = "Allow the creation of an empty set of release notes. In this mode, milestone and input file path will be ignored.")]
        public bool AllowEmpty { get; set; }

        [Option("output", Required = false, HelpText = "The path to a local file location where the release notes will be created, instead of creating in remotely on the specified provider.")]
        public string OutputPath { get; set; }
    }
}