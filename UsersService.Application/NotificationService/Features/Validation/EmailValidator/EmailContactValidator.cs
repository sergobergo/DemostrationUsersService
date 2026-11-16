using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Features.Validation.Customs;

namespace UsersService.Application.NotificationService.Features.Validation.EmailValidator
{
    public class EmailContactValidator : AbstractValidator<EmailContact>
    {
        public EmailContactValidator() 
        {
            RuleFor(x => x.Name)
                .ValidName();
            RuleFor(x => x.EmailAddress)
                .NotEmpty().WithMessage("Sender email is required")
                .EmailAddress().WithMessage("Wrong email adress format");
        }
    }
}
