using System.Xml.Linq;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace FitnessMerchant.Api.Infrastructure.Identity;

/// <summary>One data-protection key ring element (the XML the framework writes), stored as-is.</summary>
public sealed class DataProtectionKey
{
    public int Id { get; set; }
    public string? FriendlyName { get; set; }
    public required string Xml { get; set; }
}

/// <summary>
/// D032: the key ring in PostgreSQL. The framework's EF Core package does the same, but its 10.0.12 release pulls
/// in NuGet copies of System.Security.Cryptography.Xml/Pkcs with open advisories (fixed only in a 10.0.13 that is
/// too new to pin), and the interface it implements already ships with ASP.NET Core.
/// </summary>
internal sealed class DatabaseKeyRepository(IServiceScopeFactory scopes) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        // The framework calls this synchronously, rarely (startup and key refresh, about once a day).
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.DataProtectionKeys.AsNoTracking().OrderBy(k => k.Id).Select(k => k.Xml).ToList()
            .Select(xml => XElement.Parse(xml)).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting) });
        db.SaveChanges();
    }
}

internal sealed class DatabaseKeyRepositorySetup(IServiceScopeFactory scopes) : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options) => options.XmlRepository = new DatabaseKeyRepository(scopes);
}

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.Property(k => k.FriendlyName).HasMaxLength(200);
    }
}
