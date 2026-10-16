using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FitnessMerchant.Api.Infrastructure.Data;

/// <summary>
/// A transaction that joins one already open on the context, or opens and owns its own. Seeders use it so they
/// run alone at startup and as one unit inside the demo reset (which must roll back completely on failure).
/// </summary>
internal sealed class JoinableTransaction(IDbContextTransaction? owned) : IAsyncDisposable
{
    public static async Task<JoinableTransaction> BeginAsync(AppDbContext db, CancellationToken cancellationToken) =>
        new(db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null);

    /// <summary>Commits only a transaction this object opened; a joined one is committed by its owner.</summary>
    public Task CommitAsync(CancellationToken cancellationToken) => owned?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}
