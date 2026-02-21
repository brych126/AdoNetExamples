using Microsoft.Data.SqlClient;
using System.Data;

namespace AdoNetExamples
{
    internal static class DbCommandExamples
    {
        public static void CallGetCustomerOrdersTotalAfterDateAsResultSetStoredProcedure()
        {
            int customerId = 1;
            DateTime ordersDate = new(2025, 1, 1);
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand("dbo.GetCustomerOrdersTotalAfterDateAsResultSet", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@CustomerID", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(
                new SqlParameter("@OrdersDate", SqlDbType.DateTime2) { Value = new DateTime(2025, 1, 1) });

            // exec dbo.GetCustomerOrdersTotalAfterDateAsResultSet @CustomerID=1,@OrdersDate='2025-01-01 00:00:00'
            using SqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                // Handle NULLs safely (though proc already uses ISNULL)
                decimal totalAmount = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                int orderCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);

                Console.WriteLine($"Customer {customerId}, Orders after {ordersDate:yyyy-MM-dd}:");
                Console.WriteLine($"  Total Amount = {totalAmount}");
                Console.WriteLine($"  Order Count  = {orderCount}");
            }

            Console.WriteLine($"Records affected: {reader.RecordsAffected}\n");
        }

        public static void CallGetCustomerOrdersTotalAfterDateAsOutputStoredProcedure()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand("dbo.GetCustomerOrdersTotalAfterDateAsOutput", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@CustomerID", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(
                new SqlParameter("@OrdersDate", SqlDbType.DateTime2) { Value = new DateTime(2025, 1, 1) });

            var totalAmountParam = new SqlParameter("@TotalAmount", SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 2,
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(totalAmountParam);

            var orderCountParam = new SqlParameter("@OrderCount", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(orderCountParam);

            /*
            declare @p3 numeric(18,2)
               set @p3=NULL
               declare @p4 int
               set @p4=NULL
               exec dbo.GetCustomerOrdersTotalAfterDateAsOutput @CustomerID=1,@OrdersDate='2025-01-01 00:00:00',@TotalAmount=@p3 output,@OrderCount=@p4 output
               select @p3, @p4
            */
            cmd.ExecuteNonQuery();

            decimal totalAmount = (decimal)(totalAmountParam.Value ?? 0m);
            int orderCount = (int)(orderCountParam.Value ?? 0);

            Console.WriteLine($"Customer 1, Orders after 2025-01-01:");
            Console.WriteLine($"  Total Amount = {totalAmount}");
            Console.WriteLine($"  Order Count  = {orderCount}");
        }

        public static void CallGetCustomerOrdersTotalAfterDateWithStatusStoredProcedure()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand("dbo.GetCustomerOrdersTotalAfterDateWithStatus", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@CustomerID", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(
                new SqlParameter("@OrdersDate", SqlDbType.DateTime2) { Value = new DateTime(2025, 1, 1) });

            var pAmount = new SqlParameter("@TotalAmount", SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 2,
                Direction = ParameterDirection.Output
            };
            var pCount = new SqlParameter("@OrderCount", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(pAmount);
            cmd.Parameters.Add(pCount);

            // Return value parameter
            var pRet = new SqlParameter("@return_value", SqlDbType.Int)
            {
                Direction = ParameterDirection.ReturnValue
            };
            cmd.Parameters.Add(pRet);
            /*
               declare @p3 numeric(18,2)
               set @p3=NULL
               declare @p4 int
               set @p4=NULL
               exec dbo.GetCustomerOrdersTotalAfterDateWithStatus @CustomerID=1,@OrdersDate='2025-01-01 00:00:00',@TotalAmount=@p3 output,@OrderCount=@p4 output
               select @p3, @p4
            */
            /*
            Unlike OUTPUT params (which are included in the exec … output; select … replay script we saw in Profiler), 
            the return value is pulled from the TDS header, so we won’t see it in that “synthetic” script.
            */
            cmd.ExecuteNonQuery();

            int status = (int)(pRet.Value ?? 0);
            decimal total = (decimal)(pAmount.Value ?? 0m);
            int orderCount = (int)(pCount.Value ?? 0);

            Console.WriteLine($"Status={status}; Total={total}; Count={orderCount}");
        }

        public static void CallGetCustomerOrdersTotalAfterDate_InOut_StoredProcedure()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand("dbo.GetCustomerOrdersTotalAfterDate_InOut", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@CustomerID", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(
                new SqlParameter("@OrdersDate", SqlDbType.DateTime2) { Value = new DateTime(2025, 1, 1) });

            // This is the IN/OUT parameter: set Direction = InputOutput and seed Value
            var runningTotal = new SqlParameter("@RunningTotal", SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 2,
                Direction = ParameterDirection.InputOutput,
                Value = 100m      // initial seed from the caller
            };
            cmd.Parameters.Add(runningTotal);

            /*
            declare @p3 numeric(18,2)
               set @p3=100.00
               exec dbo.GetCustomerOrdersTotalAfterDate_InOut @CustomerID=1,@OrdersDate='2025-01-01 00:00:00',@RunningTotal=@p3 output
               select @p3
            */
            cmd.ExecuteNonQuery();

            decimal updated = (decimal)runningTotal.Value;
            Console.WriteLine($"Updated running total = {updated}");
        }
        public static void CallGetCustomerOrderedProductsByEmail_ReturnsRecordsSet_StoredProcedure()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand("dbo.GetCustomerOrderedProductsByEmail", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = "alice@example.com" });

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var orderId = reader.GetInt32(reader.GetOrdinal("OrderId"));
                DateTime created = reader.GetDateTime(reader.GetOrdinal("OrderCreatedAt"));
                var payed = reader.GetBoolean(reader.GetOrdinal("Payed"));
                var productId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                var name = reader.GetString(reader.GetOrdinal("ProductName"));
                var qty = reader.GetInt32(reader.GetOrdinal("Quantity"));
                var unit = reader.GetDecimal(reader.GetOrdinal("UnitPriceUsed"));
                var lineTotal = reader.GetDecimal(reader.GetOrdinal("LineTotal"));

                Console.WriteLine($"Order #{orderId} | {created:u} | Payed={payed} | " +
                                  $"Product {productId} {name} | Qty={qty} | Unit={unit:0.00} | Total={lineTotal:0.00}");
            }
        }

        public static void CallFn_GetCustomerOrdersTotalAfterDate_Function()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            conn.Open();

            using var cmd = new SqlCommand(
                "SELECT dbo.fn_GetCustomerOrdersTotalAfterDate(@CustomerID, @OrdersDate);", conn);

            cmd.Parameters.Add(new SqlParameter("@CustomerID", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(
                new SqlParameter("@OrdersDate", SqlDbType.DateTime2) { Value = new DateTime(2025, 1, 1) });

            /*
            exec sp_executesql N'SELECT dbo.fn_GetCustomerOrdersTotalAfterDate(@CustomerID,
            @OrdersDate);',N'@CustomerID int,@OrdersDate datetime2(7)',
            @CustomerID=1,@OrdersDate='2025-01-01 00:00:00'
             */
            var result = cmd.ExecuteScalar();
            decimal total = result != DBNull.Value ? (decimal)result : 0m;

            Console.WriteLine($"Total Amount for Customer 1 after 2025-01-01 = {total}");
        }
    }
}