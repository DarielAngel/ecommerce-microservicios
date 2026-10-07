using Ecommerce.Users.Domain.Exceptions;

namespace Ecommerce.Users.Domain.Entities;

/// <summary>
/// La libreta de direcciones de UN cliente. Concentra las reglas que miran todas sus
/// direcciones a la vez:
/// <list type="bullet">
/// <item>como máximo <see cref="MaxAddresses"/> direcciones;</item>
/// <item>si hay direcciones, exactamente una es la predeterminada (la primera lo es sola);</item>
/// <item>al borrar la predeterminada, pasa a serlo la usada/editada más recientemente.</item>
/// </list>
/// Trabaja sobre la lista que trae el repositorio; quien la usa persiste lo que devuelve
/// (la nueva dirección al agregar, la borrada al quitar).
/// </summary>
public class AddressBook
{
    public const int MaxAddresses = 10;

    private readonly Guid _userId;
    private readonly List<Address> _addresses;

    public AddressBook(Guid userId, IEnumerable<Address> addresses)
    {
        _userId = userId;
        _addresses = addresses.ToList();
        if (_addresses.Any(a => a.UserId != userId))
            throw new InvalidOperationException("La libreta solo puede contener direcciones de un mismo cliente.");
    }

    /// <summary>Predeterminada primero, después de la más nueva a la más vieja.</summary>
    public IReadOnlyList<Address> Addresses =>
        _addresses.OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAtUtc).ToList();

    public Address? Default => _addresses.FirstOrDefault(a => a.IsDefault);

    public Address Add(AddressData data, bool makeDefault, DateTime nowUtc)
    {
        if (_addresses.Count >= MaxAddresses)
            throw new DomainException($"Puedes guardar hasta {MaxAddresses} direcciones. Borra alguna para agregar otra.");

        var address = new Address(_userId, data, nowUtc);
        _addresses.Add(address);

        if (makeDefault || _addresses.Count == 1) SetDefault(address.Id, nowUtc);
        return address;
    }

    public Address Update(Guid addressId, AddressData data, bool makeDefault, DateTime nowUtc)
    {
        var address = Find(addressId);
        address.Apply(data, nowUtc);
        if (makeDefault) SetDefault(addressId, nowUtc);
        return address;
    }

    public Address Remove(Guid addressId, DateTime nowUtc)
    {
        var address = Find(addressId);
        _addresses.Remove(address);

        if (address.IsDefault && _addresses.Count > 0)
        {
            var next = _addresses.OrderByDescending(a => a.UpdatedAtUtc).First();
            next.SetDefault(true, nowUtc);
        }
        return address;
    }

    public void SetDefault(Guid addressId, DateTime nowUtc)
    {
        var target = Find(addressId);
        foreach (var a in _addresses) a.SetDefault(a.Id == target.Id, nowUtc);
    }

    public bool Contains(Guid addressId) => _addresses.Any(a => a.Id == addressId);

    private Address Find(Guid addressId) =>
        _addresses.FirstOrDefault(a => a.Id == addressId)
        ?? throw new AddressNotFoundException();
}

/// <summary>La dirección no existe o no es de este cliente (para el cliente es lo mismo: 404).</summary>
public class AddressNotFoundException : DomainException
{
    public AddressNotFoundException() : base("La dirección no existe.") { }
}
