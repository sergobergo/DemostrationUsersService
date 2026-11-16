using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Features.Validation.Customs;
using UsersService.Application.UsersService.Models;
using UsersService.Domain;

namespace UsersService.Application.UsersService.Features.Validation
{
    public class UserRegValidator : AbstractValidator<UserRegDto>
    {
        private readonly UserServiceDbContext _context;
        private readonly IPwnedPasswordService _pwnedPasswordService;
        private readonly ILogger<UserRegValidator> _logger;

        public UserRegValidator(

            UserServiceDbContext context,
            IPwnedPasswordService pwnedPasswordService,
            ILogger<UserRegValidator> logger

            )
        {
            _logger = logger;
            _context = context;
            _pwnedPasswordService = pwnedPasswordService;

            RuleFor(x => x.Name).ValidName();

            RuleFor(x => x.Login).ValidLogin()
                .MustAsync(BeUniqueLogin).WithMessage("This login has already been registered");

            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required")
                .Matches(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")
                .WithMessage("Incorrect format email")
                .MustAsync(BeUniqueEmail).WithMessage("This email has already been registered");

            RuleFor(x => x.Password)
                .MinimumLength(8).WithMessage("The password must contain at least 8 characters.")
                .MaximumLength(64).WithMessage("The password must not exceed 64 characters")
                .NotEqual(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .WithMessage("The password must not match the username")
                .NotEqual(x => x.Email, StringComparer.OrdinalIgnoreCase)
                .WithMessage("The password must not match the email address")
                .MustAsync(async (password, cancellation) =>
                    !await _pwnedPasswordService.IsPasswordPwned(password, cancellation))
                .WithMessage("This password has been compromised and found in data leaks \nPlease choose a different password");
        }
        private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
        {
            try 
            { 
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
               return user == null;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Data base access denied");
                throw new DbAccessDeniedException("Data base access denied", ex);
            }
        }
        private async Task<bool> BeUniqueLogin(string login, CancellationToken ct)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Login == login, ct);
                return user == null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data base access denied");
                throw new DbAccessDeniedException("Data base access denied", ex);
            }
        }
    }
}
