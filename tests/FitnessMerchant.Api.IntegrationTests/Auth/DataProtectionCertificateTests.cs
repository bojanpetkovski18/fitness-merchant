using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Auth;

/// <summary>D034: with a certificate configured, the key ring in the database is encrypted with it.</summary>
public class DataProtectionCertificateTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_a_certificate_the_stored_keys_are_encrypted_and_sessions_still_work()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var pemPath = WriteCertificatePem();
        try
        {
            await using var factory = new ApiFactory(connectionString, new Dictionary<string, string?>
            {
                ["DataProtection:CertificatePemPath"] = pemPath,
            });
            using var client = factory.CreateBrowserClient();
            var email = BrowserClient.NewEmail();
            (await client.RegisterAsync(email)).EnsureSuccessStatusCode();
            Assert.Equal(email, (await client.MeAsync()).User?.Email);

            await using var db = PostgresFixture.CreateDbContext(connectionString);
            var keys = await db.DataProtectionKeys.Select(k => k.Xml).ToListAsync(Ct);
            Assert.NotEmpty(keys);
            // The master key is wrapped in an EncryptedXml envelope instead of sitting there as plain base64.
            Assert.All(keys, xml =>
            {
                Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal);
                Assert.DoesNotContain("<masterKey", xml, StringComparison.Ordinal);
            });
        }
        finally
        {
            File.Delete(pemPath);
        }
    }

    /// <summary>A throwaway self-signed certificate with its private key, in one PEM file like the mounted secret.</summary>
    private static string WriteCertificatePem()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=fitness-merchant-test-data-protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(Path.GetTempPath(), $"fm-dp-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, certificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem());
        return path;
    }
}
