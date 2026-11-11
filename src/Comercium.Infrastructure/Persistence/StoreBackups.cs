using Comercium.Application;
using Comercium.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class StoreBackups(StoreDatabase database) : IBackups
{
    public void Create(Session session, string destination)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ManageBackups);
        var live = (SqliteConnection)db.Database.GetDbConnection();
        destination = CheckPath(destination, live.DataSource);
        live.Open();
        SaveCopy(live, destination);
        StoreAccess.Log(db, session.UserId, DateTime.UtcNow, "Respaldo creado", Path.GetFileName(destination));
        db.SaveChanges();
    }

    public string Restore(Session session, string source)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ManageBackups);
        var live = (SqliteConnection)db.Database.GetDbConnection();
        source = CheckPath(source, live.DataSource);
        var folder = Path.GetDirectoryName(Path.GetFullPath(live.DataSource))!;
        var staging = Path.Combine(folder, $"restore-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var incoming = Connect(source, SqliteOpenMode.ReadOnly))
            using (var copy = Connect(staging, SqliteOpenMode.ReadWriteCreate))
                incoming.BackupDatabase(copy);

            var migrations = db.Database.GetMigrations().ToArray();
            using (var validation = Connect(staging, SqliteOpenMode.ReadOnly))
                Validate(validation, migrations);
            new StoreDatabase(staging, pooling: false).Migrate();

            using var candidate = Connect(staging, SqliteOpenMode.ReadOnly);
            var recovery = Path.Combine(folder, $"antes-de-restaurar-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.comercium");
            live.Open();
            SaveCopy(live, recovery);
            candidate.BackupDatabase(live);
            return recovery;
        }
        catch (SqliteException error) when (error.SqliteErrorCode is 11 or 26)
        {
            throw new BusinessException("El archivo está dañado o no es un respaldo válido de Comercium.");
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private static void SaveCopy(SqliteConnection source, string destination)
    {
        var staging = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var copy = Connect(staging, SqliteOpenMode.ReadWriteCreate))
                source.BackupDatabase(copy);
            File.Move(staging, destination, overwrite: true);
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private static void Validate(SqliteConnection candidate, IEnumerable<string> migrations)
    {
        using var command = candidate.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
            throw new BusinessException("El respaldo tiene errores de integridad.");
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read())
                throw new BusinessException("El respaldo contiene relaciones inválidas.");

        command.CommandText = "SELECT count(*) FROM sqlite_schema WHERE type='table' AND name='__EFMigrationsHistory'";
        if ((long)command.ExecuteScalar()! != 1)
            throw new BusinessException("El archivo seleccionado no es un respaldo de Comercium.");
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        var applied = new List<string>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                applied.Add(reader.GetString(0));
        if (applied.Count == 0 || !applied.SequenceEqual(migrations.Take(applied.Count)))
            throw new BusinessException("El respaldo corresponde a una versión incompatible de Comercium.");

        command.CommandText = "SELECT count(*) FROM Users WHERE Active = 1";
        if ((long)command.ExecuteScalar()! == 0)
            throw new BusinessException("El respaldo no contiene una cuenta activa.");
    }

    private static SqliteConnection Connect(string file, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = file, Mode = mode, Pooling = false, DefaultTimeout = 5 }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static string CheckPath(string file, string live)
    {
        var fullPath = Path.GetFullPath(file);
        if (!string.Equals(Path.GetExtension(fullPath), ".comercium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, Path.GetFullPath(live), StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Selecciona un archivo de respaldo con extensión .comercium.");
        return fullPath;
    }
}
