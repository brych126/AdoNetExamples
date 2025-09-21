using Microsoft.Data.SqlClient;

namespace AdoNetExamples
{
    internal static class DataReaderExamples
    {
        public static async Task ReadDataFromTableAsync()
        {
            await using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            await conn.OpenAsync();

            const string sql = @"
            SELECT Id, [Name], Email, CreatedAt
            FROM dbo.Customers
            ORDER BY Id;";

            await using var cmd = new SqlCommand(sql, conn);
            
            // ExecuteReaderAsync returns a forward-only, read-only cursor
            await using var reader = await cmd.ExecuteReaderAsync();

            // Cache column ordinals once (faster than GetOrdinal each row)
            int ordId = reader.GetOrdinal("Id");
            int ordName = reader.GetOrdinal("Name");
            int ordEmail = reader.GetOrdinal("Email");
            int ordCreated = reader.GetOrdinal("CreatedAt");

            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(ordId);
                string name = reader.GetString(ordName);
                string? email = reader.IsDBNull(ordEmail) ? null : reader.GetString(ordEmail);
                DateTime created = reader.GetDateTime(ordCreated);

                Console.WriteLine($"{id}: {name,-10} | {email ?? "(no email)"} | {created:u}");
            }
        }
    }
}
