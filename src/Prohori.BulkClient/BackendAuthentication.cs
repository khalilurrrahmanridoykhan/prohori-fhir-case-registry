using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Prohori.BulkClient;

public static class BackendKey
{
    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string KeyId(RSA rsa) => Base64Url(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));

    public static void Generate(string directory)
    {
        Directory.CreateDirectory(directory);
        var privatePath = Path.Combine(directory, "client-key.pem");
        if (File.Exists(privatePath)) throw new IOException("Key already exists; refusing to replace a registered key.");
        using var rsa = RSA.Create(3072);
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = new FileStream(privatePath, fileOptions))
        using (var writer = new StreamWriter(file)) writer.Write(rsa.ExportPkcs8PrivateKeyPem());
        var key = rsa.ExportParameters(false);
        File.WriteAllText(Path.Combine(directory, "jwks.json"), JsonSerializer.Serialize(new
        {
            keys = new[] { new { kty = "RSA", use = "sig", alg = "RS384", kid = KeyId(rsa), n = Base64Url(key.Modulus!), e = Base64Url(key.Exponent!) } }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string Assertion(RSA rsa, string clientId, string tokenEndpoint, DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS384", typ = "JWT", kid = KeyId(rsa) }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = clientId, sub = clientId, aud = tokenEndpoint,
            iat = now.ToUnixTimeSeconds(), exp = now.AddMinutes(5).ToUnixTimeSeconds(), jti = Guid.NewGuid().ToString("N")
        }));
        var input = header + "." + payload;
        return input + "." + Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1));
    }
}

/// <summary>Uses a fresh private_key_jwt assertion for each short-lived access token; no refresh token or user login.</summary>
public sealed class BackendAuthentication(HttpClient http, string clientId, Uri tokenEndpoint, RSA key)
{
    private string? _token;
    private DateTimeOffset _expires;

    public async Task Authorize(HttpRequestMessage request, CancellationToken cancellation)
    {
        if (_token == null || _expires <= DateTimeOffset.UtcNow.AddSeconds(30))
        {
            var now = DateTimeOffset.UtcNow;
            using var message = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["scope"] = "system/*.read",
                    ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                    ["client_assertion"] = BackendKey.Assertion(key, clientId, tokenEndpoint.AbsoluteUri, now)
                })
            };
            using var response = await http.SendAsync(message, cancellation);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Token endpoint refused client authentication: HTTP {(int)response.StatusCode}.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            var root = json.RootElement;
            if (!root.GetProperty("scope").GetString()!.Split(' ').Contains("system/*.read", StringComparer.Ordinal))
                throw new InvalidOperationException("Token endpoint did not grant system/*.read.");
            if (!string.Equals(root.GetProperty("token_type").GetString(), "Bearer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Expected a bearer access token.");
            _token = root.GetProperty("access_token").GetString();
            var lifetime = root.GetProperty("expires_in").GetInt32();
            if (string.IsNullOrWhiteSpace(_token) || lifetime <= 0) throw new InvalidOperationException("Invalid token response.");
            _expires = now.AddSeconds(lifetime);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }
}
