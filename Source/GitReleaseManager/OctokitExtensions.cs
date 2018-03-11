﻿//-----------------------------------------------------------------------
// <copyright file="OctokitExtensions.cs" company="GitTools Contributors">
//     Copyright (c) 2015 - Present - GitTools Contributors
// </copyright>
//-----------------------------------------------------------------------

namespace GitReleaseManager.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Octokit;

    public static class OctokitExtensions
    {
        public static bool IsPullRequest(this Issue issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException("issue");
            }

            return issue.PullRequest != null;
        }

        public static async Task<IEnumerable<Issue>> AllIssuesForMilestone(this GitHubClient gitHubClient, Milestone milestone)
        {
            var closedIssueRequest = new RepositoryIssueRequest
            {
                Milestone = milestone.Number.ToString(CultureInfo.InvariantCulture),
                State = ItemStateFilter.Closed
            };
            var openIssueRequest = new RepositoryIssueRequest
            {
                Milestone = milestone.Number.ToString(CultureInfo.InvariantCulture),
                State = ItemStateFilter.Open
            };
            var parts = milestone.Url.Split('/');
            var user = parts[2];
            var repository = parts[3];
            var closedIssues = await gitHubClient.Issue.GetAllForRepository(user, repository, closedIssueRequest);
            var openIssues = await gitHubClient.Issue.GetAllForRepository(user, repository, openIssueRequest);
            return openIssues.Union(closedIssues);
        }

        public static Uri HtmlUrl(this Milestone milestone)
        {
            if (milestone == null)
            {
                throw new ArgumentNullException("milestone");
            }

            var parts = milestone.Url.Split('/');
            var user = parts[2];
            var repository = parts[3];

            return new Uri(string.Format(CultureInfo.InvariantCulture, "https://github.com/{0}/{1}/issues?milestone={2}&state=closed", user, repository, milestone.Number));
        }
    }
}