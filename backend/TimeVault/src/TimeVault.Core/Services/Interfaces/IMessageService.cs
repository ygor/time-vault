using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeVault.Domain.Entities;

namespace TimeVault.Core.Services.Interfaces
{
    public interface IMessageService
    {
        Task<Message?> CreateMessageAsync(Guid vaultId, Guid userId, string title, string content, DateTime? unlockDateTime);
        Task<Message?> GetMessageByIdAsync(Guid messageId, Guid userId);
        Task<IEnumerable<Message>> GetVaultMessagesAsync(Guid vaultId, Guid userId);
        Task<bool> UpdateMessageAsync(Guid messageId, Guid userId, string title, string content, DateTime? unlockDateTime);
        Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
        Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId);
        Task<Message?> UnlockMessageAsync(Guid messageId, Guid userId);
        Task<IEnumerable<Message>> GetUnlockedMessagesAsync(Guid userId);
    }
} 