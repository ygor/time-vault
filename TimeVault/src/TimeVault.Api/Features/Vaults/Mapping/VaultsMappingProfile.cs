using AutoMapper;
using TimeVault.Api.Features.Vaults;
using TimeVault.Domain.Entities;

namespace TimeVault.Api.Features.Vaults.Mapping
{
    public class VaultsMappingProfile : Profile
    {
        public VaultsMappingProfile()
        {
            // Map Vault entity to VaultDto
            CreateMap<Vault, VaultDto>()
                .ForMember(dest => dest.OwnerEmail, opt => opt.MapFrom(src => 
                    src.Owner != null ? src.Owner.Email : string.Empty))
                .ForMember(dest => dest.MessageCount, opt => opt.MapFrom(src => 
                    src.Messages != null ? src.Messages.Count : 0))
                .ForMember(dest => dest.UnreadMessageCount, opt => opt.MapFrom(src => 
                    src.Messages != null ? src.Messages.Count(m => !m.IsRead) : 0))
                .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
                .ForMember(dest => dest.CanEdit, opt => opt.Ignore())
                .ForMember(dest => dest.SharedWith, opt => opt.Ignore());

            // Map VaultShare entity to VaultShareDto
            CreateMap<VaultShare, VaultShareDto>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => 
                    src.User != null ? src.User.Email : string.Empty));
        }
    }
} 