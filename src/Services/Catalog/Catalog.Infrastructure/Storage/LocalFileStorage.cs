using Ecommerce.Catalog.Application.Common;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Carpeta física donde se guardan las imágenes (mapeada a un volumen de Docker).</summary>
    public string RootPath { get; set; } = "/app/uploads";

    /// <summary>Extensiones permitidas, para no aceptar cualquier tipo de archivo.</summary>
    public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".webp" };

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
}

public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(_options.RootPath);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Extensión de archivo no permitida: '{extension}'. Permitidas: {string.Join(", ", _options.AllowedExtensions)}");
        }

        if (content.Length > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"El archivo excede el tamaño máximo permitido de {_options.MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        // Nombre único para evitar colisiones y no confiar en el nombre original del cliente.
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_options.RootPath, storedFileName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, ct);

        return storedFileName;
    }
}
