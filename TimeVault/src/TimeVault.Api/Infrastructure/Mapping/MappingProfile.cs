using AutoMapper;
using System;
using TimeVault.Api.Features.Auth;
using TimeVault.Api.Features.Messages;
using TimeVault.Api.Features.Vaults;
using TimeVault.Domain.Entities;

namespace TimeVault.Api.Infrastructure.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mappings
            CreateMap<User, UserDto>();
            
            // Vault mappings
            CreateMap<Vault, VaultDto>()
                .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
                .ForMember(dest => dest.CanEdit, opt => opt.Ignore())
                .ForMember(dest => dest.OwnerUsername, opt => opt.MapFrom(src => src.Owner != null ? src.Owner.Username : string.Empty))
                .ForMember(dest => dest.MessageCount, opt => opt.MapFrom(src => src.Messages != null ? src.Messages.Count : 0))
                .ForMember(dest => dest.UnreadMessageCount, opt => opt.MapFrom(src => 
                    src.Messages != null ? src.Messages.Count(m => !m.IsRead) : 0))
                .ForMember(dest => dest.SharedWith, opt => opt.Ignore());
                
            // Message mappings
            CreateMap<Message, MessageDto>()
                .ForMember(dest => dest.Content, opt => opt.MapFrom(src => 
                    src.IsEncrypted ? string.Empty : src.Content));
        }
    }
} 