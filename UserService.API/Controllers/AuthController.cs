using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Features.Validation;
using UsersService.Application.UsersService.Models;

namespace UserService.API.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ApiBaseController
    {
        private readonly UserRegValidator _validations;
        private readonly IAuthService _authService;
        private readonly IUserVerificationService _userVerification;

        public AuthController
            (
                 UserRegValidator validations,
                 IAuthService authService,
                 IUserVerificationService userVerification
            )
        {
            _validations = validations;
            _authService = authService;
            _userVerification = userVerification;
        }
        [HttpPost("verification")]
        public async Task<ActionResult<string>> Verification(UserVerificationDto VerRequest, CancellationToken ct)
        {
            var request = new UserRegDto(VerRequest);
            ValidationResult result = await _validations.ValidateAsync(request);

            if (result.IsValid)
            {
                await _userVerification.RememberUserAndSendCodeAsync(request,ct);
                return Ok(request.Login);
            }
            return BadRequest(result);

        }

        [HttpPost("refresh-code")]
        public async Task<ActionResult> RefreshCode(string login, CancellationToken ct)
        {
            await _userVerification.RefreshAndSendCodeAsync(login, ct);
            return Ok(login);
        }

        [HttpPost("register")]
        public async Task<ActionResult> Registration(string login, string code)
        {
            var token = await _userVerification.UserCheckAndLoginAsync(login, code);
            if (token is null) return BadRequest("Wrong login or verification code!");
            return Ok(token);
        }
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(UserLogDto request)
        {   
            var token = await _authService.LoginAsync(request);
            return Ok(token);

        }
        [HttpPost("refresh-token")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken(RefreshTokenRequestDto request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("MainPage")]
        public async Task<ActionResult<string>> AuthenticatedEdp()
        {

            return Ok("Hi, user!");
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("admin")]
        public async Task<ActionResult<string>> AdminEdp()
        {

            return Ok("Hi, admin!");
        }
    }
}
