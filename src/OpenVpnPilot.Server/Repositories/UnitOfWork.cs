using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Data;
using OpenVpnPilot.Server.Repositories.Interfaces;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Repositories;

public sealed class UnitOfWork(PilotServerDbContext db) : IUnitOfWork
{
    // An arbitrary key that only this application takes. Writers hold it exclusively, readers shared.
    private const long SyncLockKey = 0x4F5650_53594E43;

    public async Task<ISyncedWrite> BeginSyncedWriteAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(transaction, $"SELECT pg_advisory_xact_lock({SyncLockKey})", cancellationToken);
        object? next = await ExecuteAsync(
            transaction, $"SELECT nextval('{PilotServerDbContext.ChangeSequence}')", cancellationToken);
        return new SyncedWrite(this, transaction, Convert.ToInt64(next, System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<ISyncedRead> BeginSyncedReadAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        await ExecuteAsync(transaction, $"SELECT pg_advisory_xact_lock_shared({SyncLockKey})", cancellationToken);
        object? cursor = await ExecuteAsync(
            transaction,
            $"SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM {PilotServerDbContext.ChangeSequence}",
            cancellationToken);
        return new SyncedRead(transaction, Convert.ToInt64(cursor, System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new SyncedWrite(this, await db.Database.BeginTransactionAsync(cancellationToken), 0);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ServiceException.PreconditionFailed(
                "The resource was changed by someone else since it was read. Read it again and retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ServiceException.Conflict(ErrorCodes.Conflict, "The change collides with one made at the same moment. Retry it.");
        }
    }

    private static async Task<object?> ExecuteAsync(IDbContextTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        DbTransaction dbTransaction = transaction.GetDbTransaction();
        await using DbCommand command = dbTransaction.Connection!.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private sealed class SyncedWrite(UnitOfWork owner, IDbContextTransaction transaction, long changeSeq) : ISyncedWrite
    {
        public long ChangeSeq { get; } = changeSeq;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await owner.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class SyncedRead(IDbContextTransaction transaction, long cursor) : ISyncedRead
    {
        public long Cursor { get; } = cursor;

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
