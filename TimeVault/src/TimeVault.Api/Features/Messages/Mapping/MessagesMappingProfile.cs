using AutoMapper;
using TimeVault.Api.Features.Messages;
using TimeVault.Domain.Entities;

namespace TimeVault.Api.Features.Messages.Mapping
{
    public class MessagesMappingProfile : Profile
    {
        public MessagesMappingProfile()
        {
            // Map Message entity to MessageDto
            CreateMap<Message, MessageDto>()
                .ForMember(dest => dest.Content, opt => opt.MapFrom(src => 
                    src.IsEncrypted ? string.Empty : src.Content));
        }
    }
} 