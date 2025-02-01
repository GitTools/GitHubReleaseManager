using System;

namespace GitReleaseManager.Core.Model
{
    public sealed class Milestone
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public int PublicNumber { get; set; }

        public long InternalNumber { get; set; }

        public string HtmlUrl { get; set; }

        public string Url { get; set; }

        public Version Version { get; set; }

        public DateTimeOffset? DueOn { get; set; }
    }
}