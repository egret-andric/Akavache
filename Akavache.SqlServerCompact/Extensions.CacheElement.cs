﻿using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Akavache.SqlServerCompact
{
    internal static partial class Extensions
    {
        internal static async Task<bool> CacheElementsTableExists(this SqlCeConnection connection)
        {
            await Ensure.IsOpen(connection);

            var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CacheElement'";
            var reader = command.ExecuteReader();
            var hasRows = reader.Read();
            return hasRows;
        }

        internal static IObservable<Unit> CreateCacheElementTable(this SqlCeConnection connection)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE CacheElement ([Key] NVARCHAR(4000) PRIMARY KEY, TypeName NVARCHAR(4000), Value image NOT NULL, CreatedAt DATETIME NOT NULL, Expiration DATETIME NOT NULL)";
                command.ExecuteNonQuery();
            });
        }

        internal static IObservable<List<CacheElement>> QueryCacheById(this SqlCeConnection connection, string key)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "SELECT TOP 1 [Key],TypeName,Value,CreatedAt,Expiration FROM CacheElement WHERE [Key] = @ID";
                command.Parameters.AddWithValue("ID", key);

                var list = new List<CacheElement>();

                using (var result = command.ExecuteReader())
                {
                    var hasResult = result.Read();
                    if (hasResult)
                    {
                        list.Add(CacheElement.FromDataReader(result));
                    }
                }
                return list;
            });
        }

        internal static IObservable<List<CacheElement>> QueryCacheById(this SqlCeConnection connection, IEnumerable<string> keys)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                var concatKeys = String.Join(",", keys.Select(s => String.Format("'{0}'", s)));
                command.CommandText = String.Format("SELECT [Key],TypeName,Value,CreatedAt,Expiration FROM CacheElement WHERE [Key] IN ({0})", concatKeys);

                var list = new List<CacheElement>();

                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        list.Add(CacheElement.FromDataReader(result));
                    }
                }
                return list;
            });
        }

        internal static IObservable<List<CacheElement>> QueryCacheByExpiration(this SqlCeConnection connection, DateTime time)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "SELECT [Key],TypeName,Value,CreatedAt,Expiration FROM CacheElement WHERE Expiration >= @Expiration";
                command.Parameters.AddWithValue("Expiration", time);

                var list = new List<CacheElement>();

                using (var result = command.ExecuteReader())
                {
                    //if (result.HasRows)
                    {
                        while (result.Read())
                        {
                            list.Add(CacheElement.FromDataReader(result));
                        }
                    }
                }
                return list;
            });
        }


        // TODO: this seems legit https://www.nuget.org/packages/ErikEJ.SqlCeBulkCopy
        internal static IObservable<Unit> InsertAll(this SqlCeConnection connection, IEnumerable<CacheElement> elements)
        {
            return elements.ToObservable()
                .SelectMany(connection.Insert);
        }

        internal static IObservable<Unit> Insert(this SqlCeConnection connection, CacheElement element)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO CacheElement ([Key],TypeName,Value,CreatedAt,Expiration) VALUES (@Key, @TypeName, @Value, @CreatedAt, @Expiration)";
                command.Parameters.AddWithValue("Key", element.Key);
                command.Parameters.AddWithValue("Value", element.Value);
                command.Parameters.AddWithValue("CreatedAt", element.CreatedAt);
                command.Parameters.AddWithValue("Expiration", element.Expiration);
                if (String.IsNullOrWhiteSpace(element.TypeName))
                {
                    command.Parameters.AddWithValue("TypeName", DBNull.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("TypeName", element.TypeName);
                }
                command.ExecuteNonQuery();
            });
        }

        internal static IObservable<Unit> DeleteFromCache(this SqlCeConnection connection, string key)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CacheElement WHERE [Key] = @Key";
                command.Parameters.AddWithValue("@Key", key);
                command.ExecuteNonQuery();
            });
        }

        internal static IObservable<Unit> DeleteAllFromCache(this SqlCeConnection connection)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CacheElement";
                command.ExecuteNonQuery();
            });
        }

        internal static IObservable<Unit> DeleteExpiredElements(this SqlCeConnection connection, DateTime time)
        {
            return Observable.StartAsync(async () =>
            {
                await Ensure.IsOpen(connection);

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CacheElement WHERE Expiration < @expiration";
                command.Parameters.AddWithValue("expiration", time);
                command.ExecuteNonQuery();
            });
        }

        // TODO: actually implement this based on notes here: http://technet.microsoft.com/en-us/library/ms172411(v=sql.110).aspx
        internal static IObservable<Unit> Vacuum(this SqlCeConnection connection, DateTime time)
        {
            return Observable.Return(Unit.Default);
        }
    }
}