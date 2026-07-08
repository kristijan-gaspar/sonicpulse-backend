using FluentValidation;
using SonicPulse.Application.Detections.Dtos;

namespace SonicPulse.Application.Detections.Validators;

public sealed class SubmitDetectionRequestValidator : AbstractValidator<SubmitDetectionRequest>
{
    public SubmitDetectionRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.PeakDbfs).LessThanOrEqualTo(0)
            .WithMessage("dBFS is non-positive by definition.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.GpsAccuracy).GreaterThan(0);
    }
}
