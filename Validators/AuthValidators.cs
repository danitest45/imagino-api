using FluentValidation;
using Imagino.Api.Controllers;

namespace Imagino.Api.Validators
{
    public class RegisterRequestValidator : AbstractValidator<AuthController.RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
            RuleFor(x => x.Username).MaximumLength(50);
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
            RuleFor(x => x.Credits).GreaterThanOrEqualTo(0);
        }
    }

    public class LoginRequestValidator : AbstractValidator<AuthController.LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class ResendVerificationRequestValidator : AbstractValidator<AuthController.ResendVerificationRequest>
    {
        public ResendVerificationRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class VerifyEmailRequestValidator : AbstractValidator<AuthController.VerifyEmailRequest>
    {
        public VerifyEmailRequestValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
        }
    }

    public class ForgotPasswordRequestValidator : AbstractValidator<AuthController.ForgotPasswordRequest>
    {
        public ForgotPasswordRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ResetPasswordRequestValidator : AbstractValidator<AuthController.ResetPasswordRequest>
    {
        public ResetPasswordRequestValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
        }
    }
}
