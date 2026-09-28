namespace Ecommerce.Notifications.Application.Common;

/// <summary>Falló el envío con el proveedor de email (credenciales, red, rechazo del proveedor).</summary>
public class EmailSendException : Exception
{
    public EmailSendException(string message, Exception? inner = null) : base(message, inner) { }
}
