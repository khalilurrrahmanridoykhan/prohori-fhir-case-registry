using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Prohori.BulkClient;

namespace Prohori.Api.Tests;

public class BackendAuthenticationTests
{
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
    [Fact]
    public void Assertion_uses_RS384_exact_audience_short_expiry_and_unique_jti()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var token = BackendKey.Assertion(key, "backend", "https://issuer.test/token", now).Split('.');
        using var header = JsonDocument.Parse(Decode(token[0]));
        using var payload = JsonDocument.Parse(Decode(token[1]));
        header.RootElement.GetProperty("alg").GetString().ShouldBe("RS384");
        header.RootElement.GetProperty("kid").GetString().ShouldBe(BackendKey.KeyId(key));
        payload.RootElement.GetProperty("iss").GetString().ShouldBe("backend");
        payload.RootElement.GetProperty("sub").GetString().ShouldBe("backend");
        payload.RootElement.GetProperty("aud").GetString().ShouldBe("https://issuer.test/token");
        payload.RootElement.GetProperty("exp").GetInt64().ShouldBe(now.AddMinutes(5).ToUnixTimeSeconds());
        key.VerifyData(Encoding.ASCII.GetBytes(token[0] + "." + token[1]), Decode(token[2]), HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1).ShouldBeTrue();
        using var second = JsonDocument.Parse(Decode(BackendKey.Assertion(key, "backend", "https://issuer.test/token", now).Split('.')[1]));
        second.RootElement.GetProperty("jti").GetString().ShouldNotBe(payload.RootElement.GetProperty("jti").GetString());
    }
    [Fact]
    public void Key_generation_exposes_only_public_material_and_refuses_overwrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            BackendKey.Generate(directory);
            using var jwks = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "jwks.json")));
            var key = jwks.RootElement.GetProperty("keys")[0];
            key.TryGetProperty("d", out _).ShouldBeFalse();
            key.GetProperty("alg").GetString().ShouldBe("RS384");
            Should.Throw<IOException>(() => BackendKey.Generate(directory));
            if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(Path.Combine(directory, "client-key.pem")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally { Directory.Delete(directory, true); }
    }
}
