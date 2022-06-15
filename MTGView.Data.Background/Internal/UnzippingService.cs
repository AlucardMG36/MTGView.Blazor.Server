﻿using System.IO.Compression;
using MTGView.Data.Background.Interfaces;

namespace MTGView.Data.Background.Internal;

internal sealed class UnzippingService : IUnzippingService
{
    private readonly IHttpClientFactory _mtgJsonClientFactory;
    private readonly ILogger<UnzippingService> _logger;

    private const string CompressedExtension = ".zip";
    private const string AllPrintingsFileName = "AllPrintingsCSVFiles";
    private const string CompleteFileName = $"{AllPrintingsFileName}{CompressedExtension}";
    private string _filePath = String.Empty;

    public UnzippingService(IHttpClientFactory httpClientFactory, ILogger<UnzippingService> logger)
    {
        _mtgJsonClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InitiateFileDownloadProcess(CancellationToken cancellationToken = default)
    {
        using var client = _mtgJsonClientFactory.CreateClient("MtgJsonClient");

        using var message = new HttpRequestMessage(HttpMethod.Get, $"{client.BaseAddress}{CompleteFileName}");

        using var response = await client.SendAsync(message, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);

            await DeserializeStreamToFile(content);

            await UnzipDownloadedFile();
        }
    }

    private Task UnzipDownloadedFile()
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            if (!Directory.Exists(currentDirectory))
            {
                var directoryNotFound = new DirectoryNotFoundException("Could not find requested directory");
                
                _logger.LogError("{@ex}", directoryNotFound);
                
                return Task.FromException(directoryNotFound);
            }

            if (!File.Exists(_filePath))
            {
                var fileNotFound = new FileNotFoundException($"{_filePath} did not contain the file!");
                
                _logger.LogError("Could not process file: {@ex}", fileNotFound);

                return Task.FromException(fileNotFound);
            }

            ZipFile.ExtractToDirectory(_filePath, currentDirectory, true);
         
            File.Delete($"{currentDirectory}\\{CompleteFileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception Occurred while extracting: {@ex}", ex);
        }

        return Task.CompletedTask;
    }

    private async Task DeserializeStreamToFile(Stream stream)
    {
        var fileInfo = new FileInfo($"{CompleteFileName}");

        await using var fileStream = File.Create(fileInfo.FullName);

        stream.Seek(0, SeekOrigin.Begin);

        await stream.CopyToAsync(fileStream);

        _filePath = fileInfo.FullName;
    }
}
