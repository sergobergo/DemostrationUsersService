using FluentValidation;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Features.Validation.Customs;

namespace UsersService.Application.NotificationService.Features.Validation.EmailValidator
{
    public class EmailDtoValidator : AbstractValidator<EmailDto>
    {
        public EmailDtoValidator()
        {
            RuleFor(x => x.Sender)
                .ValidEmailContact();
            RuleForEach(x => x.Recipients)
                .ValidEmailContact();
            RuleFor(x => x.Text)
                .NotEmpty().WithMessage("Text field is empty!");
        }
    }
}
