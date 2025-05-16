using AutoMapper;
using TimeVault.Api.Features.Messages;
using TimeVault.Domain.Entities;
using System;

namespace TimeVault.Api.Features.Messages.Mapping
{
    public class MessagesMappingProfile : Profile
    {
        public MessagesMappingProfile()
        {
            // Map Message entity to MessageDto
            CreateMap<Message, MessageDto>()
                .ForMember(dest => dest.Content, opt => opt.MapFrom(src => 
                    // Use only expression-compatible operators (ternary)
                    src.IsEncrypted ? string.Empty : 
                    (src.Content == null ? string.Empty : 
                     src.Content.StartsWith("[Error:") ? string.Empty : 
                     src.Content)))
                .ForMember(dest => dest.UnlockDateTime, opt => opt.MapFrom(src => src.UnlockTime))
                // IsEncrypted determines if the message content is still encrypted
                .ForMember(dest => dest.IsEncrypted, opt => opt.MapFrom(src => src.IsEncrypted))
                // IsLocked is a display property indicating if a message is time-locked
                // A message is locked if it's encrypted AND has a future unlock time
                .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => 
                    src.IsEncrypted && src.UnlockTime.HasValue && src.UnlockTime.Value > DateTime.UtcNow));
        }
    }
} 