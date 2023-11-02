namespace GitReleaseManager.Core.Model
{
    public class IssueComment
    {
        /// <summary>
        /// Gets or sets the issue comment Id.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets details about the issue comment.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets information about the user who made the comment.
        /// </summary>
        public User User { get; set; }
    }
}