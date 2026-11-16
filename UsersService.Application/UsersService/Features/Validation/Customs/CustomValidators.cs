using FluentValidation;
using UsersService.Application.NotificationService.Features.Validation.EmailValidator;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UsersService.Application.UsersService.Features.Validation.Customs
{
    public static class CustomValidators
    {
        private static readonly ValidNameValidator _nameValidator = new();
        private static readonly EmailContactValidator _emailContactValidator = new();
        public static IRuleBuilderOptions<T, string> ValidName<T>(
            this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.SetValidator(_nameValidator);
        }
        public static IRuleBuilderOptions<T, EmailContact> ValidEmailContact<T>(
            this IRuleBuilder<T, EmailContact> ruleBuilder)
        {
            return ruleBuilder.SetValidator(_emailContactValidator);
        }
        public static IRuleBuilderOptions<T, string> ValidLogin<T>(
           this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder
                .NotEmpty().WithMessage("Login is required")
                .MinimumLength(4).WithMessage("The minimum length of the login is 4 characters")
                .MaximumLength(30).WithMessage("The maximum length of the login is 30 characters")

                .Matches("^[a-zA-Z0-9_-]+$")
                .WithMessage("Login can contain only Latin letters, numbers, underscores (_) and hyphens (-).")

                .Must(username => !username.StartsWith('_')
                                && !username.StartsWith('-')
                                && !username.EndsWith('_')
                                && !username.EndsWith('-'))
                .WithMessage("Login cannot begin or end with an underscore (_) or a hyphen (-).");
        }
    }
}
