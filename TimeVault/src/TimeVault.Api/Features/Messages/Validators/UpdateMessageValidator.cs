using FluentValidation;

namespace TimeVault.Api.Features.Messages.Validators
{
    public class UpdateMessageValidator : AbstractValidator<UpdateMessage.Command>
    {
        public UpdateMessageValidator()
        {
            RuleFor(x => x.MessageId).NotEmpty().WithMessage("Message ID is required");
            RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithMessage("Title is required and must be less than 100 characters");
            RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required");
        }
    }
} 