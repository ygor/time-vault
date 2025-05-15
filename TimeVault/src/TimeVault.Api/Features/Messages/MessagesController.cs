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
            var result = await _mediator.Send(new GetVaultMessages.Query { VaultId = vaultId, UserId = GetCurrentUserId() });
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMessage(Guid id)
        {
            var result = await _mediator.Send(new GetMessage.Query { MessageId = id, UserId = GetCurrentUserId() });
            
            if (result == null)
                return NotFound();

            // Mark as read if not already
            if (!result.IsRead)
            {
                await _mediator.Send(new MarkMessageAsRead.Command { MessageId = id, UserId = GetCurrentUserId() });
            }

            return Ok(result);
        }

        [HttpPost("vault/{vaultId}")]
        public async Task<IActionResult> CreateMessage(Guid vaultId, [FromBody] CreateMessageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _mediator.Send(new CreateMessage.Command 
            { 
                VaultId = vaultId, 
                UserId = GetCurrentUserId(),
                Title = request.Title,
                Content = request.Content,
                UnlockDateTime = request.UnlockDateTime
            });

            if (result == null)
                return BadRequest(new { message = "Failed to create message" });

            return CreatedAtAction(nameof(GetMessage), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMessage(Guid id, [FromBody] UpdateMessageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _mediator.Send(new UpdateMessage.Command 
            { 
                MessageId = id, 
                UserId = GetCurrentUserId(),
                Title = request.Title,
                Content = request.Content,
                UnlockDateTime = request.UnlockDateTime
            });

            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(Guid id)
        {
            var result = await _mediator.Send(new DeleteMessage.Command { MessageId = id, UserId = GetCurrentUserId() });

            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpGet("unlocked")]
        public async Task<IActionResult> GetUnlockedMessages()
        {
            var result = await _mediator.Send(new GetUnlockedMessages.Query { UserId = GetCurrentUserId() });
            return Ok(result);
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> UnlockMessage(Guid id)
        {
            var result = await _mediator.Send(new UnlockMessage.Query { MessageId = id, UserId = GetCurrentUserId() });

            if (result == null)
                return NotFound();

            if (result.IsEncrypted)
                return BadRequest(new { message = "Message cannot be unlocked yet" });

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            if (Guid.TryParse(User.FindFirst("id")?.Value, out Guid userId))
                return userId;

            return Guid.Empty;
        }
    }

    public class CreateMessageRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime? UnlockDateTime { get; set; }
    }

    public class UpdateMessageRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime? UnlockDateTime { get; set; }
    }
} 