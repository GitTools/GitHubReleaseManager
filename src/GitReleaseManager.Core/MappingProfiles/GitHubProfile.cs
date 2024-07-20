using System;
using System.Text.Json;
using AutoMapper;
using GitReleaseManager.Core.Extensions;

namespace GitReleaseManager.Core.MappingProfiles
{
    public class GitHubProfile : Profile
    {
        public GitHubProfile()
        {
            // These mappings convert the result of Octokit queries to model classes
            CreateMap<Octokit.Issue, Model.Issue>()
                .ForMember(dest => dest.PublicNumber, act => act.MapFrom(src => src.Number))
                .ForMember(dest => dest.InternalNumber, act => act.MapFrom(src => src.Id))
                .ForMember(dest => dest.IsPullRequest, act => act.MapFrom(src => src.HtmlUrl.Contains("/pull/", StringComparison.OrdinalIgnoreCase)))
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

            // These mappings convert the result of GraphQL queries to model classes
            CreateMap<JsonElement, Model.Issue>()
                .ForMember(dest => dest.PublicNumber, act => act.MapFrom(src => src.GetProperty("number").GetInt32()))
                .ForMember(dest => dest.InternalNumber, act => act.MapFrom(src => -1)) // Not available in graphQL (there's a "id" property but it contains a string which represents the Node ID of the object).
                .ForMember(dest => dest.Title, act => act.MapFrom(src => src.GetProperty("title").GetString()))
                .ForMember(dest => dest.HtmlUrl, act => act.MapFrom(src => src.GetProperty("url").GetString()))
                .ForMember(dest => dest.IsPullRequest, act => act.MapFrom(src => src.GetProperty("url").GetString().Contains("/pull/", StringComparison.OrdinalIgnoreCase)))
                .ForMember(dest => dest.User, act => act.MapFrom(src => src.GetProperty("author")))
                .ForMember(dest => dest.Labels, act => act.MapFrom(src => src.GetJsonElement("labels.nodes").EnumerateArray()))
                .ReverseMap();

            CreateMap<JsonElement, Model.Label>()
                .ForMember(dest => dest.Name, act => act.MapFrom(src => src.GetProperty("name").GetString()))
                .ForMember(dest => dest.Color, act => act.MapFrom(src => src.GetProperty("color").GetString()))
                .ForMember(dest => dest.Description, act => act.MapFrom(src => src.GetProperty("description").GetString()))
                .ReverseMap();

            CreateMap<JsonElement, Model.User>()
                .ForMember(dest => dest.Login, act => act.MapFrom(src => src.GetProperty("login").GetString()))
                .ForMember(dest => dest.HtmlUrl, act => act.MapFrom(src => $"https://github.com{src.GetProperty("resourcePath").GetString()}")) // The resourcePath contains a value similar to "/jericho". That's why we must manually prepend "https://github.com
                .ForMember(dest => dest.AvatarUrl, act => act.MapFrom(src => src.GetProperty("avatarUrl").GetString()))
                .ReverseMap();
        }
    }
}