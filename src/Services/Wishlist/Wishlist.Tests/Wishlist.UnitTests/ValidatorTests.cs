using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Application.Features;
using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;
using Xunit;

namespace Ecommerce.Wishlist.UnitTests;

public class ValidatorTests
{
    [Fact]
    public void AddValidator_ConIdsVacios_DeberiaReportarAmbos()
    {
        var result = new AddToWishlistCommandValidator().Validate(new AddToWishlistCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("UserId", "ProductId");
    }

    [Fact]
    public void RemoveValidator_ConIdsValidos_DeberiaSerValido()
    {
        var result = new RemoveFromWishlistCommandValidator()
            .Validate(new RemoveFromWishlistCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidationBehavior_ConComandoSinRespuesta_DeberiaValidarYCortarAntesDelHandler()
    {
        // Protege el cambio de restricción en ValidationBehavior: con 'IRequest<TResponse>' este
        // comando (IRequest a secas) se saltaba la validación sin que nadie se enterara.
        var behavior = new ValidationBehavior<AddToWishlistCommand, Unit>(
            new IValidator<AddToWishlistCommand>[] { new AddToWishlistCommandValidator() });
        var next = Substitute.For<RequestHandlerDelegate<Unit>>();

        var act = () => behavior.Handle(new AddToWishlistCommand(Guid.NewGuid(), Guid.Empty), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationAppException>();
        ex.Which.Errors.Should().ContainKey("ProductId");
        await next.DidNotReceive().Invoke();
    }
}
