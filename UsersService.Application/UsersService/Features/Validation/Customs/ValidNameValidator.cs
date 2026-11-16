using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.UsersService.Features.Validation.Customs
{
    public class ValidNameValidator : AbstractValidator<string>
    {
        public ValidNameValidator()
        {
            RuleFor(name => name)
                .NotEmpty().WithMessage("Name is required")
                .MinimumLength(2).WithMessage("The minimum length of the name is 2 characters")
                .MaximumLength(30).WithMessage("The maximum length of the name is 30 characters")

                .Matches("^[a-zA-Z0-9\u0400-\u04FF\u0500-\u052F_-]+$")
                .WithMessage("Name can contain only Latin and Russian letters, numbers, underscores (_) and hyphens (-).")

                .Must(username => !username.StartsWith('_')
                                && !username.StartsWith('-')
                                && !username.EndsWith('_')
                                && !username.EndsWith('-'))
                .WithMessage("Name cannot begin or end with an underscore (_) or a hyphen (-).");
        }
    }
}
