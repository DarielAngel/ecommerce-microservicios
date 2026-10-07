using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Users.Application.Features;

// Libreta de direcciones (Fase 5, T5.1). Todos los comandos llevan el UserId del token:
// un cliente nunca ve ni toca direcciones de otro (para él, una dirección ajena "no existe").

public record AddressResult(
    Guid Id, string Label, string RecipientName, string? Phone, string Street, string? Details,
    string City, string? Region, string? PostalCode, string Country, bool IsDefault, string Formatted)
{
    public static AddressResult From(Address a) => new(
        a.Id, a.Label, a.RecipientName, a.Phone, a.Street, a.Details,
        a.City, a.Region, a.PostalCode, a.Country, a.IsDefault, a.Format());
}

public record AddressInput(
    string? Label, string? RecipientName, string? Phone, string? Street, string? Details,
    string? City, string? Region, string? PostalCode, string? Country, bool MakeDefault = false)
{
    public AddressData ToData() => new(Label, RecipientName, Phone, Street, Details, City, Region, PostalCode, Country);
}

/// <summary>Reglas de formulario (las mismas que aplica el dominio, pero todas juntas y por campo).</summary>
public class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(x => x.Label).NotEmpty().WithMessage("Ponle un nombre a la dirección (ej. Casa).")
            .MaximumLength(Address.MaxLabel);
        RuleFor(x => x.RecipientName).NotEmpty().WithMessage("Indica quién recibe el pedido.")
            .MaximumLength(Address.MaxName);
        RuleFor(x => x.Phone).MaximumLength(Address.MaxPhone)
            .Matches(@"^[0-9+()\-\s]*$").WithMessage("El teléfono solo puede tener números, espacios y + ( ) -.");
        RuleFor(x => x.Street).NotEmpty().WithMessage("Escribe la calle y el número.")
            .MaximumLength(Address.MaxLine);
        RuleFor(x => x.Details).MaximumLength(Address.MaxLine);
        RuleFor(x => x.City).NotEmpty().WithMessage("Escribe la ciudad.").MaximumLength(Address.MaxCity);
        RuleFor(x => x.Region).MaximumLength(Address.MaxCity);
        RuleFor(x => x.PostalCode).MaximumLength(Address.MaxPostalCode);
        RuleFor(x => x.Country).NotEmpty().WithMessage("Escribe el país.").MaximumLength(Address.MaxCountry);
    }
}

// ---------- Listar ----------

public record ListMyAddressesQuery(Guid UserId) : IRequest<IReadOnlyList<AddressResult>>;

public class ListMyAddressesQueryHandler : IRequestHandler<ListMyAddressesQuery, IReadOnlyList<AddressResult>>
{
    private readonly IAddressRepository _repository;

    public ListMyAddressesQueryHandler(IAddressRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AddressResult>> Handle(ListMyAddressesQuery request, CancellationToken ct)
    {
        var book = new AddressBook(request.UserId, await _repository.ListByUserAsync(request.UserId, ct));
        return book.Addresses.Select(AddressResult.From).ToList();
    }
}

// ---------- Crear ----------

public record AddAddressCommand(Guid UserId, AddressInput Input) : IRequest<AddressResult>;

public class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new AddressInputValidator());
}

public class AddAddressCommandHandler : IRequestHandler<AddAddressCommand, AddressResult>
{
    private readonly IAddressRepository _repository;
    private readonly TimeProvider _clock;

    public AddAddressCommandHandler(IAddressRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<AddressResult> Handle(AddAddressCommand request, CancellationToken ct)
    {
        var book = new AddressBook(request.UserId, await _repository.ListByUserAsync(request.UserId, ct));
        var address = book.Add(request.Input.ToData(), request.Input.MakeDefault, _clock.GetUtcNow().UtcDateTime);

        await _repository.AddAsync(address, ct);
        await _repository.SaveChangesAsync(ct);
        return AddressResult.From(address);
    }
}

// ---------- Editar ----------

public record UpdateAddressCommand(Guid UserId, Guid AddressId, AddressInput Input) : IRequest<AddressResult>;

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new AddressInputValidator());
}

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, AddressResult>
{
    private readonly IAddressRepository _repository;
    private readonly TimeProvider _clock;

    public UpdateAddressCommandHandler(IAddressRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<AddressResult> Handle(UpdateAddressCommand request, CancellationToken ct)
    {
        var book = new AddressBook(request.UserId, await _repository.ListByUserAsync(request.UserId, ct));
        var address = book.Update(request.AddressId, request.Input.ToData(), request.Input.MakeDefault,
            _clock.GetUtcNow().UtcDateTime);

        await _repository.SaveChangesAsync(ct);
        return AddressResult.From(address);
    }
}

// ---------- Marcar como predeterminada ----------

public record SetDefaultAddressCommand(Guid UserId, Guid AddressId) : IRequest<IReadOnlyList<AddressResult>>;

public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, IReadOnlyList<AddressResult>>
{
    private readonly IAddressRepository _repository;
    private readonly TimeProvider _clock;

    public SetDefaultAddressCommandHandler(IAddressRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AddressResult>> Handle(SetDefaultAddressCommand request, CancellationToken ct)
    {
        var book = new AddressBook(request.UserId, await _repository.ListByUserAsync(request.UserId, ct));
        book.SetDefault(request.AddressId, _clock.GetUtcNow().UtcDateTime);

        await _repository.SaveChangesAsync(ct);
        return book.Addresses.Select(AddressResult.From).ToList();
    }
}

// ---------- Borrar ----------

public record DeleteAddressCommand(Guid UserId, Guid AddressId) : IRequest<IReadOnlyList<AddressResult>>;

public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand, IReadOnlyList<AddressResult>>
{
    private readonly IAddressRepository _repository;
    private readonly TimeProvider _clock;

    public DeleteAddressCommandHandler(IAddressRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AddressResult>> Handle(DeleteAddressCommand request, CancellationToken ct)
    {
        var book = new AddressBook(request.UserId, await _repository.ListByUserAsync(request.UserId, ct));
        var removed = book.Remove(request.AddressId, _clock.GetUtcNow().UtcDateTime);

        _repository.Remove(removed);
        await _repository.SaveChangesAsync(ct);
        return book.Addresses.Select(AddressResult.From).ToList();
    }
}
