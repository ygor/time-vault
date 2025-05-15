using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

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
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new GetAllVaults.Query { UserId = userId });
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVault(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new GetVault.Query { VaultId = id, UserId = userId });
            
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateVault([FromBody] CreateVaultRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new CreateVault.Command 
            { 
                UserId = userId, 
                Name = request.Name, 
                Description = request.Description 
            });

            return CreatedAtAction(nameof(GetVault), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVault(Guid id, [FromBody] UpdateVaultRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new UpdateVault.Command 
            { 
                VaultId = id, 
                UserId = userId, 
                Name = request.Name, 
                Description = request.Description 
            });

            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVault(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new DeleteVault.Command { VaultId = id, UserId = userId });

            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/share")]
        public async Task<IActionResult> ShareVault(Guid id, [FromBody] ShareVaultRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new ShareVault.Command 
            { 
                VaultId = id, 
                OwnerUserId = userId, 
                TargetUserId = request.UserId, 
                CanEdit = request.CanEdit 
            });

            if (!result)
                return BadRequest(new { message = "Failed to share vault" });

            return Ok(new { message = "Vault shared successfully" });
        }

        [HttpDelete("{id}/share/{targetUserId}")]
        public async Task<IActionResult> RevokeVaultShare(Guid id, Guid targetUserId)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _mediator.Send(new RevokeVaultShare.Command 
            { 
                VaultId = id, 
                OwnerUserId = userId, 
                TargetUserId = targetUserId 
            });

            if (!result)
                return BadRequest(new { message = "Failed to revoke vault share" });

            return Ok(new { message = "Vault share revoked successfully" });
        }

        private Guid GetCurrentUserId()
        {
            if (Guid.TryParse(User.FindFirst("id")?.Value, out Guid userId))
                return userId;

            return Guid.Empty;
        }
    }

    public class CreateVaultRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class UpdateVaultRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class ShareVaultRequest
    {
        public Guid UserId { get; set; }
        public bool CanEdit { get; set; }
    }
} 