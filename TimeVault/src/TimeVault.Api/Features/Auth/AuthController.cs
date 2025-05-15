using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace TimeVault.Api.Features.Auth
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _mediator.Send(new Login.Command 
            { 
                Email = request.Email, 
                Password = request.Password 
            });

            if (!result.Success)
                return Unauthorized(new { message = result.Error });

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _mediator.Send(new Register.Command 
            { 
                Email = request.Email, 
                Password = request.Password 
            });

            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.Token))
                return BadRequest(new { message = "Token is required" });

            var result = await _mediator.Send(new RefreshToken.Command { Token = request.Token });

            if (!result.Success)
                return Unauthorized(new { message = result.Error });

            return Ok(new { token = result.Token });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new ChangePassword.Command 
            { 
                UserId = userId,
                CurrentPassword = request.CurrentPassword, 
                NewPassword = request.NewPassword 
            });

            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Password changed successfully" });
        }

        private Guid GetCurrentUserId()
        {
            if (Guid.TryParse(User.FindFirst("id")?.Value, out Guid userId))
                return userId;

            return Guid.Empty;
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string Token { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }
} 