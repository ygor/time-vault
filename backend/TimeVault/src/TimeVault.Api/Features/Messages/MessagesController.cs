using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace TimeVault.Api.Features.Messages
{
    [ApiController]
    [Route("api/messages")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MessagesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("vault/{vaultId}")]
        public async Task<IActionResult> GetVaultMessages(Guid vaultId)
        {
            var query = new GetVaultMessages.Query
            {
                VaultId = vaultId,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMessage(Guid id)
        {
            var query = new GetMessage.Query
            {
                MessageId = id,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(query);
            if (result == null)
                return NotFound();

            // Mark message as read
            await _mediator.Send(new MarkMessageAsRead.Command
            {
                MessageId = id,
                UserId = GetCurrentUserId()
            });

            return Ok(result);
        }

        [HttpPost("vault/{vaultId}")]
        public async Task<IActionResult> CreateMessage(Guid vaultId, [FromBody] CreateMessageRequest request)
        {
            var command = new CreateMessage.Command
            {
                VaultId = vaultId,
                UserId = GetCurrentUserId(),
                Title = request.Title,
                Content = request.Content,
                UnlockDateTime = request.UnlockDateTime
            };

            var result = await _mediator.Send(command);
            if (result == null)
                return BadRequest("Failed to create message. Check that you have access to the vault.");

            return CreatedAtAction(nameof(GetMessage), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMessage(Guid id, [FromBody] UpdateMessageRequest request)
        {
            var command = new UpdateMessage.Command
            {
                MessageId = id,
                UserId = GetCurrentUserId(),
                Title = request.Title,
                Content = request.Content,
                UnlockDateTime = request.UnlockDateTime
            };

            var result = await _mediator.Send(command);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(Guid id)
        {
            var command = new DeleteMessage.Command
            {
                MessageId = id,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(command);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpGet("unlocked")]
        public async Task<IActionResult> GetUnlockedMessages()
        {
            var query = new GetUnlockedMessages.Query { UserId = GetCurrentUserId() };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> UnlockMessage(Guid id)
        {
            var query = new UnlockMessage.Query
            {
                MessageId = id,
                UserId = GetCurrentUserId()
            };

            var result = await _mediator.Send(query);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("id");
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }
    }

    public class CreateMessageRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? UnlockDateTime { get; set; }
    }

    public class UpdateMessageRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? UnlockDateTime { get; set; }
    }
} 