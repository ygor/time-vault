using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TimeVault.Api.Features.Vaults;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TimeVault.Infrastructure.Data;
using System.Linq;

namespace TimeVault.Api.Features.Vaults
{
    [ApiController]
    [Route("api/vaults")]
    [Authorize]
    public class VaultsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ApplicationDbContext _context;

        public VaultsController(IMediator mediator, ApplicationDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllVaults()
        {
            var query = new GetAllVaults.Query { UserId = GetCurrentUserId() };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("shared")]
        public async Task<IActionResult> GetSharedVaults()
        {
            var query = new GetAllVaults.Query 
            { 
                UserId = GetCurrentUserId(),
                SharedOnly = true
            };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVault(Guid id)
        {
            try 
            {
                var query = new GetVault.Query
                {
                    VaultId = id,
                    UserId = GetCurrentUserId()
                };

                var result = await _mediator.Send(query);
                if (result == null)
                {
                    // Check if vault exists but user doesn't have access
                    var vaultExists = await _context.Vaults.AnyAsync(v => v.Id == id);
                    if (vaultExists)
                        return Forbid();
                    else
                        return NotFound();
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateVault([FromBody] CreateVaultRequest request)
        {
            try
            {
                var command = new CreateVault.Command
                {
                    UserId = GetCurrentUserId(),
                    Name = request.Name,
                    Description = request.Description
                };

                var result = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetVault), new { id = result.Id }, result);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { Success = false, Errors = ex.Errors.Select(e => e.ErrorMessage).ToList() });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVault(Guid id, [FromBody] UpdateVaultRequest request)
        {
            var command = new UpdateVault.Command
            {
                VaultId = id,
                UserId = GetCurrentUserId(),
                Name = request.Name,
                Description = request.Description
            };

            var result = await _mediator.Send(command);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVault(Guid id)
        {
            var command = new DeleteVault.Command
            {
                VaultId = id,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(command);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/share")]
        public async Task<IActionResult> ShareVault(Guid id, [FromBody] ShareVaultRequest request)
        {
            // First find the target user by email
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => 
                u.Email.ToLower() == request.UserEmail.ToLower());
            
            if (targetUser == null)
            {
                return BadRequest(new { success = false, error = $"User with email {request.UserEmail} not found" });
            }

            var command = new ShareVault.Command
            {
                VaultId = id,
                OwnerUserId = GetCurrentUserId(),
                TargetUserId = targetUser.Id,
                CanEdit = request.CanEdit
            };

            var result = await _mediator.Send(command);
            if (!result)
                return BadRequest("Failed to share vault, check that the vault exists and you are the owner.");

            return Ok();
        }

        [HttpDelete("{id}/share/{targetUserId}")]
        public async Task<IActionResult> RevokeVaultShare(Guid id, Guid targetUserId)
        {
            var command = new RevokeVaultShare.Command
            {
                VaultId = id,
                OwnerUserId = GetCurrentUserId(),
                TargetUserId = targetUserId
            };

            var result = await _mediator.Send(command);
            if (!result)
                return NotFound(new { error = "Failed to revoke vault share. Either the vault doesn't exist, you are not the owner, or the share doesn't exist." });

            return Ok();
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("id");
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }
    }

    public class CreateVaultRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateVaultRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ShareVaultRequest
    {
        public string UserEmail { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
    }
} 