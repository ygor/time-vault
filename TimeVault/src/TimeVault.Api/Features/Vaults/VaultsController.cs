using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TimeVault.Api.Features.Vaults;

namespace TimeVault.Api.Features.Vaults
{
    [ApiController]
    [Route("api/vaults")]
    [Authorize]
    public class VaultsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VaultsController(IMediator mediator)
        {
            _mediator = mediator;
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
            var query = new GetVault.Query
            {
                VaultId = id,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(query);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateVault([FromBody] CreateVaultRequest request)
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
            var command = new ShareVault.Command
            {
                VaultId = id,
                OwnerUserId = GetCurrentUserId(),
                TargetUserId = request.UserId,
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
                return BadRequest("Failed to revoke vault share, check that the vault exists and you are the owner.");

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
        public Guid UserId { get; set; }
        public bool CanEdit { get; set; }
    }
} 