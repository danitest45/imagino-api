using FluentValidation;
using Imagino.Api.DTOs.Image;

namespace Imagino.Api.Validators
{
    public class CreateImageJobRequestValidator : AbstractValidator<CreateImageJobRequest>
    {
        public CreateImageJobRequestValidator()
        {
            RuleFor(x => x.ModelSlug).NotEmpty();
            RuleFor(x => x.Params).NotNull();
            RuleFor(x => x.PresetId).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.PresetId));
        }
    }
}
