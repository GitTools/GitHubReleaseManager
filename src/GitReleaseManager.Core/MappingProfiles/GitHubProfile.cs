using System;
using AutoMapper;
using GitReleaseManager.Core.Extensions;

namespace GitReleaseManager.Core.MappingProfiles
{
    public class GitHubProfile : Profile
    {
        public GitHubProfile()
        {
            CreateMap<Octokit.Issue, Model.Issue>()
                .ForMember(dest => dest.PublicNumber, act => act.MapFrom(src => src.Number))
                .ForMember(dest => dest.InternalNumber, act => act.MapFrom(src => src.Id))
                .ForMember(dest => dest.IsPullRequest, act => act.MapFrom(src => src.HtmlUrl.IndexOf("/pull/", StringComparison.OrdinalIgnoreCase) >= 0))
                .ReverseMap();
            CreateMap<Model.IssueComment, Octokit.IssueComment>().ReverseMap();
            CreateMap<Model.ItemState, Octokit.ItemState>().ReverseMap();
            CreateMap<Model.ItemStateFilter, Octokit.ItemStateFilter>().ReverseMap();
            CreateMap<Model.RateLimit, Octokit.RateLimit>().ReverseMap();
            CreateMap<Model.Release, Octokit.Release>().ReverseMap();
            CreateMap<Model.Release, Octokit.NewRelease>().ReverseMap();
            CreateMap<Model.ReleaseAsset, Octokit.ReleaseAsset>().ReverseMap();
            CreateMap<Model.ReleaseAssetUpload, Octokit.ReleaseAssetUpload>().ReverseMap();
            CreateMap<Model.Label, Octokit.Label>().ReverseMap();
            CreateMap<Model.Label, Octokit.NewLabel>().ReverseMap();
            CreateMap<Model.User, Octokit.User>().ReverseMap();
            CreateMap<Model.Milestone, Octokit.Milestone>();
            CreateMap<Octokit.Milestone, Model.Milestone>()
                .ForMember(dest => dest.PublicNumber, act => act.MapFrom(src => src.Number))
                .ForMember(dest => dest.InternalNumber, act => act.MapFrom(src => src.Number))
                .AfterMap((src, dest) => dest.Version = src.Version());
        }
    }
}