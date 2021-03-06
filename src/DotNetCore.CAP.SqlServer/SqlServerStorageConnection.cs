// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DotNetCore.CAP.Infrastructure;
using DotNetCore.CAP.Models;

namespace DotNetCore.CAP.SqlServer
{
    public class SqlServerStorageConnection : IStorageConnection
    {
        private readonly CapOptions _capOptions;

        public SqlServerStorageConnection(SqlServerOptions options, CapOptions capOptions)
        {
            _capOptions = capOptions;
            Options = options;
        }

        public SqlServerOptions Options { get; }

        public IStorageTransaction CreateTransaction()
        {
            return new SqlServerStorageTransaction(this);
        }

        public async Task<CapPublishedMessage> GetPublishedMessageAsync(int id)
        {
            var sql = $@"SELECT * FROM [{Options.Schema}].[Published] WITH (readpast) WHERE Id={id}";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<CapPublishedMessage>(sql);
            }
        }

        public async Task<IEnumerable<CapPublishedMessage>> GetPublishedMessagesOfNeedRetry()
        {
            var fourMinsAgo = DateTime.Now.AddMinutes(-4).ToString("O");
            var sql =
                $"SELECT TOP (200) * FROM [{Options.Schema}].[Published] WITH (readpast) WHERE Retries<{_capOptions.FailedRetryCount} AND Added<'{fourMinsAgo}' AND (StatusName = '{StatusName.Failed}' OR StatusName = '{StatusName.Scheduled}')";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return await connection.QueryAsync<CapPublishedMessage>(sql);
            }
        }

        public async Task<int> StoreReceivedMessageAsync(CapReceivedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var sql = $@"
INSERT INTO [{Options.Schema}].[Received]([Name],[Group],[Content],[Retries],[Added],[ExpiresAt],[StatusName])
VALUES(@Name,@Group,@Content,@Retries,@Added,@ExpiresAt,@StatusName);SELECT SCOPE_IDENTITY();";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return await connection.ExecuteScalarAsync<int>(sql, message);
            }
        }

        public async Task<CapReceivedMessage> GetReceivedMessageAsync(int id)
        {
            var sql = $@"SELECT * FROM [{Options.Schema}].[Received] WITH (readpast) WHERE Id={id}";
            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<CapReceivedMessage>(sql);
            }
        }

        public async Task<IEnumerable<CapReceivedMessage>> GetReceivedMessagesOfNeedRetry()
        {
            var fourMinsAgo = DateTime.Now.AddMinutes(-4).ToString("O");
            var sql =
                $"SELECT TOP (200) * FROM [{Options.Schema}].[Received] WITH (readpast) WHERE Retries<{_capOptions.FailedRetryCount} AND Added<'{fourMinsAgo}' AND (StatusName = '{StatusName.Failed}' OR StatusName = '{StatusName.Scheduled}')";
            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return await connection.QueryAsync<CapReceivedMessage>(sql);
            }
        }

        public bool ChangePublishedState(int messageId, string state)
        {
            var sql =
                $"UPDATE [{Options.Schema}].[Published] SET Retries=Retries+1,ExpiresAt=NULL,StatusName = '{state}' WHERE Id={messageId}";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return connection.Execute(sql) > 0;
            }
        }

        public bool ChangeReceivedState(int messageId, string state)
        {
            var sql =
                $"UPDATE [{Options.Schema}].[Received] SET Retries=Retries+1,ExpiresAt=NULL,StatusName = '{state}' WHERE Id={messageId}";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return connection.Execute(sql) > 0;
            }
        }

        public bool PublishedRequeue(int messageId)
        {
            var sql =
                 $"UPDATE [{Options.Schema}].[Published] SET Retries=0,ExpiresAt=NULL,StatusName = '{StatusName.Scheduled}' WHERE Id={messageId}";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return connection.Execute(sql) > 0;
            }
        }

        public bool ReceivedRequeue(int messageId)
        {
            var sql =
                $"UPDATE [{Options.Schema}].[Received] SET Retries=0,ExpiresAt=NULL,StatusName = '{StatusName.Scheduled}' WHERE Id={messageId}";

            using (var connection = new SqlConnection(Options.ConnectionString))
            {
                return connection.Execute(sql) > 0;
            }
        }

        public void Dispose()
        {
        }
    }
}