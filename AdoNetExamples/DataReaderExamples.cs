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
                ORDER BY Id;

                SELECT Id, CustomerId, Amount, CreatedAt
                FROM dbo.Orders
                ORDER BY CustomerId, Id;";

            await using var cmd = new SqlCommand(sql, conn);

            // ExecuteReaderAsync returns a forward-only, read-only cursor
            await using var reader = await cmd.ExecuteReaderAsync();

            // ----- Result set 1: Customers -----
            Console.WriteLine("=== Customers ===");

            // Cache column ordinals once (faster than repeated GetOrdinal)
            int ordCustId = reader.GetOrdinal("Id");
            int ordName = reader.GetOrdinal("Name");
            int ordEmail = reader.GetOrdinal("Email");
            int ordCustCreated = reader.GetOrdinal("CreatedAt");

            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(ordCustId);
                string name = reader.GetString(ordName);
                string? email = reader.IsDBNull(ordEmail) ? null : reader.GetString(ordEmail);
                DateTime created = reader.GetDateTime(ordCustCreated);

                Console.WriteLine($"{id}: {name,-10} | {email ?? "(no email)"} | {created:u}");
            }

            // ----- Result set 2: Orders -----
            if (await reader.NextResultAsync())
            {
                Console.WriteLine("\n=== Orders ===");

                int ordOrderId = reader.GetOrdinal("Id");
                int ordCustomerId = reader.GetOrdinal("CustomerId");
                int ordAmount = reader.GetOrdinal("Amount");
                int ordOrderCreated = reader.GetOrdinal("CreatedAt");

                while (await reader.ReadAsync())
                {
                    int orderId = reader.GetInt32(ordOrderId);
                    int customerId = reader.GetInt32(ordCustomerId);
                    decimal amount = reader.GetDecimal(ordAmount);
                    DateTime created = reader.GetDateTime(ordOrderCreated);

                    Console.WriteLine($"Order {orderId}: Cust={customerId}, Amount={amount:C}, Date={created:yyyy-MM-dd}");
                }
            }
        }
    }
}
