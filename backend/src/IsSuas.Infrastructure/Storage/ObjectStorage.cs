using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Storage;

public sealed record StoredObjectResult(string StorageKey, string Provider, long SizeBytes, string ContentHash);

public interface IObjectStorage
{
    string ProviderName { get; }
    Task<StoredObjectResult> PutAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed class LocalFileObjectStorage : IObjectStorage
{
    private readonly string _root;

    public LocalFileObjectStorage(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["Storage:LocalRoot"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "storage")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public string ProviderName => "Local";

    public async Task<StoredObjectResult> PutAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"{hash[..2]}/{hash}/{Sanitize(suggestedFileName)}";
        var path = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return new StoredObjectResult(key, ProviderName, bytes.LongLength, hash);
    }

    public Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException("Súbor nebol nájdený v úložisku.", storageKey);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult(File.Exists(path));
    }

    internal static string Sanitize(string name)
    {
        var file = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars())
            file = file.Replace(c, '_');
        return string.IsNullOrWhiteSpace(file) ? "file.bin" : file;
    }
}

/// <summary>
/// S3-compatible MinIO provider (path-style). Requires Storage:Endpoint/AccessKey/SecretKey/Bucket.
/// </summary>
public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _bucket;
    private readonly ILogger<MinioObjectStorage> _logger;

    public MinioObjectStorage(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<MinioObjectStorage> logger)
    {
        _http = httpClientFactory.CreateClient(nameof(MinioObjectStorage));
        _endpoint = (configuration["Storage:Endpoint"] ?? "http://localhost:9000").TrimEnd('/');
        _accessKey = configuration["Storage:AccessKey"] ?? "issuas";
        _secretKey = configuration["Storage:SecretKey"] ?? "issuas_dev_secret";
        _bucket = configuration["Storage:Bucket"] ?? "issuas-docs";
        _logger = logger;
    }

    public string ProviderName => "MinIO";

    public async Task<StoredObjectResult> PutAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"{hash[..2]}/{hash}/{LocalFileObjectStorage.Sanitize(suggestedFileName)}";
        await EnsureBucketAsync(cancellationToken);
        await PutObjectAsync(key, contentType, bytes, cancellationToken);
        return new StoredObjectResult(key, ProviderName, bytes.LongLength, hash);
    }

    public async Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoint}/{_bucket}/{storageKey}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        Sign(request, storageKey, contentHash: null);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new FileNotFoundException("Súbor nebol nájdený v MinIO.", storageKey);
        response.EnsureSuccessStatusCode();
        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoint}/{_bucket}/{storageKey}";
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        Sign(request, storageKey, contentHash: null);
        using var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var url = $"{_endpoint}/{_bucket}";
        using var head = new HttpRequestMessage(HttpMethod.Head, url);
        Sign(head, key: null, contentHash: null);
        using var headResp = await _http.SendAsync(head, cancellationToken);
        if (headResp.IsSuccessStatusCode)
            return;

        using var put = new HttpRequestMessage(HttpMethod.Put, url);
        Sign(put, key: null, contentHash: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        using var putResp = await _http.SendAsync(put, cancellationToken);
        if (!putResp.IsSuccessStatusCode)
            _logger.LogWarning("MinIO bucket ensure failed: {Status}", putResp.StatusCode);
        putResp.EnsureSuccessStatusCode();
    }

    private async Task PutObjectAsync(string key, string contentType, byte[] bytes, CancellationToken cancellationToken)
    {
        var url = $"{_endpoint}/{_bucket}/{key}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        Sign(request, key, payloadHash);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void Sign(HttpRequestMessage request, string? key, string? contentHash)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");
        var region = "us-east-1";
        var service = "s3";
        var payloadHash = contentHash ?? "UNSIGNED-PAYLOAD";
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.Host = new Uri(_endpoint).Authority;

        var canonicalUri = key is null ? $"/{_bucket}" : $"/{_bucket}/{key}";
        var canonicalHeaders = $"host:{request.Headers.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"{request.Method.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var canonicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalHash}";
        var signingKey = GetSignatureKey(_secretKey, dateStamp, region, service);
        var signature = Convert.ToHexString(HmacSha256(signingKey, stringToSign)).ToLowerInvariant();
        var auth = $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", auth);
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}

public static class ObjectStorageRegistration
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(nameof(MinioObjectStorage));
        var provider = configuration["Storage:Provider"] ?? "Local";
        if (provider.Equals("MinIO", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        }
        else
        {
            services.AddSingleton<IObjectStorage, LocalFileObjectStorage>();
        }

        return services;
    }
}
