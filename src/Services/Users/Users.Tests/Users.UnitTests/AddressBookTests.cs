using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Users.UnitTests;

public class AddressBookTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AddressData Data(string label = "Casa", string street = "Av. Siempre Viva 742") =>
        new(label, "Ana Martínez", "+54 351 555-1234", street, "Piso 3, depto B", "Córdoba", "Córdoba", "5000", "Argentina");

    [Fact]
    public void LaPrimeraDireccion_EsPredeterminadaAunqueNoSePida()
    {
        var book = new AddressBook(UserId, []);

        var address = book.Add(Data(), makeDefault: false, T0);

        address.IsDefault.Should().BeTrue();
        book.Default.Should().BeSameAs(address);
    }

    [Fact]
    public void Agregar_ConMakeDefault_PasaLaPredeterminadaYQuedaUnaSola()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data("Casa"), false, T0);
        var oficina = book.Add(Data("Oficina"), makeDefault: true, T0.AddMinutes(1));

        oficina.IsDefault.Should().BeTrue();
        casa.IsDefault.Should().BeFalse();
        book.Addresses.Count(a => a.IsDefault).Should().Be(1);
        book.Addresses.First().Should().BeSameAs(oficina, "la predeterminada va primero");
    }

    [Fact]
    public void Agregar_SinMakeDefault_NoCambiaLaPredeterminada()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data("Casa"), false, T0);
        var oficina = book.Add(Data("Oficina"), false, T0.AddMinutes(1));

        casa.IsDefault.Should().BeTrue();
        oficina.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void NoSePuedenGuardarMasDelMaximo()
    {
        var book = new AddressBook(UserId, []);
        for (var i = 0; i < AddressBook.MaxAddresses; i++) book.Add(Data($"Dir {i}"), false, T0.AddMinutes(i));

        var act = () => book.Add(Data("Una más"), false, T0.AddHours(1));

        act.Should().Throw<DomainException>().WithMessage($"Puedes guardar hasta {AddressBook.MaxAddresses}*");
    }

    [Fact]
    public void BorrarLaPredeterminada_PromueveLaMasRecienteDeLasQueQuedan()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data("Casa"), false, T0);
        var oficina = book.Add(Data("Oficina"), false, T0.AddMinutes(1));
        var abuela = book.Add(Data("Abuela"), false, T0.AddMinutes(2));
        book.Update(oficina.Id, Data("Oficina nueva"), false, T0.AddMinutes(5)); // la más recientemente tocada

        book.Remove(casa.Id, T0.AddMinutes(10));

        oficina.IsDefault.Should().BeTrue();
        abuela.IsDefault.Should().BeFalse();
        book.Addresses.Should().HaveCount(2);
    }

    [Fact]
    public void BorrarLaUltima_DejaLaLibretaVacia()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data(), false, T0);

        book.Remove(casa.Id, T0.AddMinutes(1));

        book.Addresses.Should().BeEmpty();
        book.Default.Should().BeNull();
    }

    [Fact]
    public void UnaDireccionDeOtroCliente_NoExisteEnEstaLibreta()
    {
        var mine = new AddressBook(UserId, []);
        var other = new AddressBook(Guid.NewGuid(), []);
        var ajena = other.Add(Data(), false, T0);

        var act = () => mine.SetDefault(ajena.Id, T0);

        act.Should().Throw<AddressNotFoundException>();
    }

    [Fact]
    public void Editar_RecortaEspaciosYOpcionalesVaciosQuedanEnNull()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data(), false, T0);

        book.Update(casa.Id, new AddressData("  Casa  ", " Ana ", "  ", " Calle 1 ", "", " Rosario ", null, " ", " Argentina "),
            false, T0.AddMinutes(1));

        casa.Label.Should().Be("Casa");
        casa.Street.Should().Be("Calle 1");
        casa.Phone.Should().BeNull();
        casa.Details.Should().BeNull();
        casa.PostalCode.Should().BeNull();
        casa.UpdatedAtUtc.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void FaltaUnCampoObligatorio_ExplicaCual()
    {
        var book = new AddressBook(UserId, []);

        var act = () => book.Add(Data(street: "   "), false, T0);

        act.Should().Throw<DomainException>().WithMessage("Completa el campo «Calle y número».");
    }

    [Fact]
    public void Format_ArmaUnaSolaLineaParaElPedido()
    {
        var book = new AddressBook(UserId, []);
        var casa = book.Add(Data(), false, T0);

        casa.Format().Should().Be(
            "Ana Martínez, Av. Siempre Viva 742, Piso 3, depto B, 5000 Córdoba, Argentina · Tel. +54 351 555-1234");
    }

    [Fact]
    public void Format_ConProvinciaDistintaDeLaCiudadYSinOpcionales()
    {
        var book = new AddressBook(UserId, []);
        var a = book.Add(new AddressData("Casa", "Luis Pérez", null, "Calle 9 #12", null, "Medellín", "Antioquia", null, "Colombia"),
            false, T0);

        a.Format().Should().Be("Luis Pérez, Calle 9 #12, Medellín, Antioquia, Colombia");
    }
}
