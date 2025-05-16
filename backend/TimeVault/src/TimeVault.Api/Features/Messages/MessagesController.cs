using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json.Serialization;
using FluentValidation;

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
            // Force the check for content length at the controller level, before any other processing
            const int maxContentLength = 1000000; // 1 million characters
            
            Console.WriteLine($"Content length: {request.Content?.Length ?? 0} characters");
            
            if (request.Content != null && request.Content.Length >= maxContentLength)
            {
                Console.WriteLine($"Content exceeds maximum length of {maxContentLength}");
                return BadRequest(new { Success = false, Errors = new[] { $"Content exceeds maximum allowed length of {maxContentLength} characters." } });
            }

            var command = new UpdateMessage.Command
            {
                MessageId = id,
                UserId = GetCurrentUserId(),
                Title = request.Title,
                Content = request.Content,
                UnlockDateTime = request.UnlockDateTime
            };

            try
            {
                var result = await _mediator.Send(command);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (ValidationException ex)
            {
                Console.WriteLine($"Validation exception: {ex.Message}");
                return BadRequest(new { Success = false, Errors = ex.Errors.Select(e => e.ErrorMessage).ToArray() });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected exception: {ex.GetType().Name} - {ex.Message}");
                return BadRequest(new { Success = false, Errors = new[] { "An unexpected error occurred while updating the message." } });
            }
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
            var messages = await _mediator.Send(query);
            
            // Debug information to understand what's happening
            Console.WriteLine($"Retrieved {messages.Count} messages from service");
            foreach (var message in messages)
            {
                Console.WriteLine($"Message ID: {message.Id}, Title: {message.Title}, IsLocked: {message.IsLocked}, IsEncrypted: {message.IsEncrypted}, UnlockTime: {message.UnlockDateTime}");
            }
            
            // Explicitly filter out any locked messages before returning them
            var unlockedMessages = messages.Where(m => !m.IsLocked).ToList();
            
            Console.WriteLine($"After filtering: {unlockedMessages.Count} messages");
            foreach (var message in unlockedMessages)
            {
                Console.WriteLine($"Filtered Message ID: {message.Id}, Title: {message.Title}, IsLocked: {message.IsLocked}, IsEncrypted: {message.IsEncrypted}, UnlockTime: {message.UnlockDateTime}");
            }
            
            return Ok(unlockedMessages);
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

        // For compatibility with the tests that use "UnlockTime"
        [JsonPropertyName("unlockTime")]
        public DateTime? UnlockTime 
        { 
            get => UnlockDateTime; 
            set => UnlockDateTime = value; 
        }
    }

    public class UpdateMessageRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? UnlockDateTime { get; set; }

        // For compatibility with the tests that use "UnlockTime"
        [JsonPropertyName("unlockTime")]
        public DateTime? UnlockTime 
        { 
            get => UnlockDateTime; 
            set => UnlockDateTime = value; 
        }
    }
} 