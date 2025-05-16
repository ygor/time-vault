using AutoMapper;
using TimeVault.Api.Features.Auth;
using TimeVault.Domain.Entities;

namespace TimeVault.Api.Features.Auth.Mapping
{
    public class AuthMappingProfile : Profile
    {
        public AuthMappingProfile()
        {
            // Map User entity to UserDto
            CreateMap<User, UserDto>();
        }
    }
} 