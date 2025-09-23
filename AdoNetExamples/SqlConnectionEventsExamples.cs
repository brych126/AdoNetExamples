using Microsoft.Data.SqlClient;
using System.Data;

namespace AdoNetExamples
{
    public static class SqlConnectionEventsExamples
    {
        /// <summary>
        /// Demonstrates handling the SqlConnection.InfoMessage event.
        /// </summary>
        public static void DemoInfoMessage()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            // Subscribe to InfoMessage before opening the connection
            conn.InfoMessage += OnInfoMessage;

            conn.Open();

            Console.WriteLine("\n#Example 1: PRINT -> InfoMessage\n");
            using (var cmd = new SqlCommand("PRINT 'Hello from SQL Server';", conn))
            {
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine("\n#Example 2: RAISERROR with severity 10 -> InfoMessage\n");
            using (var cmd = new SqlCommand("RAISERROR('Low-severity warning from SQL', 10, 1);", conn))
            {
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine("\n#Example 3: RAISERROR with severity 16 -> SqlException\n");
            try
            {
                using var cmd = new SqlCommand("RAISERROR('This is an actual error', 16, 1);", conn);
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                Console.WriteLine("Caught SqlException:");
                Console.WriteLine($"  {ex.Message}");
            }

            //Console.WriteLine("\n#Example 4: SELECT TOP 1* from Customers\n");
            //using (var cmd = new SqlCommand("SELECT TOP 1* from Customers;", conn))
            //{
            //    cmd.ExecuteNonQuery();
            //}
        }

        public static void DemoStatisticsInfoMessage()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            // Capture PRINT / STATISTICS messages
            conn.InfoMessage += OnInfoMessageWithoutPrintingErrors;

            conn.Open();

            const string sql = @"
PRINT '--- Demo: STATISTICS IO/TIME ---';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

-- Example A: simple scan on Customers
SELECT Id, [Name], Email
FROM dbo.Customers;

-- Example B: join with Orders
SELECT o.Id, o.Amount, c.[Name]
FROM dbo.Orders AS o
JOIN dbo.Customers AS c ON c.Id = o.CustomerId;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
PRINT '--- End Demo ---';
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }


        public static void PrintSessionIsolationLevelInfo()
        {
            Console.WriteLine("\n#Print isolation level having previously set it to Serializable\n");
            using var conn1 = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            // (Optional) capture PRINT output via your existing handler
            conn1.InfoMessage += OnInfoMessage;

            conn1.Open();
            const string setSerializableTranLevelSql = "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;";
            const string sql = @"
DECLARE @level NVARCHAR(30);

SELECT @level = CASE transaction_isolation_level
                  WHEN 0 THEN 'Unspecified'
                  WHEN 1 THEN 'ReadUncommitted'
                  WHEN 2 THEN 'ReadCommitted'
                  WHEN 3 THEN 'RepeatableRead'
                  WHEN 4 THEN 'Serializable'
                  WHEN 5 THEN 'Snapshot'
                END
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;

PRINT 'SPID: ' + CAST(@@SPID AS NVARCHAR(10));
PRINT 'Current Isolation Level: ' + @level;";

            using var cmd1 = new SqlCommand(setSerializableTranLevelSql + sql, conn1);
            cmd1.ExecuteNonQuery();
            conn1.Close();

            Console.WriteLine("\n#Reuse physical connection and print isolation level without manual resetting\n");
            using var conn2 = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            conn2.InfoMessage += OnInfoMessage;

            conn2.Open(); 
            using var cmd2 = new SqlCommand(sql, conn2);
            cmd2.ExecuteNonQuery();

            Console.WriteLine("\n#Another physical connection and print its isolation level\n");
            using var conn3 = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            conn3.InfoMessage += OnInfoMessage;

            conn3.Open();
            using var cmd3 = new SqlCommand(sql, conn3);
            cmd3.ExecuteNonQuery();


            conn2.Close();
            conn3.Close();
        }

        /// <summary>
        /// Demonstrates basic StateChange transitions: Closed → Open → Closed.
        /// </summary>
        public static void DemoStateChangeBasic()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            // idempotent attach pattern
            conn.StateChange -= OnStateChange;
            conn.StateChange += OnStateChange;

            Console.WriteLine("# DemoStateChangeBasic");
            Console.WriteLine("Opening…");
            conn.Open();    // expect: Closed → Open

            // Do something trivial
            using (var cmd = new SqlCommand("SELECT 1", conn))
                cmd.ExecuteScalar();

            Console.WriteLine("Closing…");
            conn.Close();   // expect: Open → Closed
        }

        /// <summary>
        /// Demo: subscribes to StateChange and normalizes isolation level on Open.
        /// </summary>
        public static void DemoStateChange_EnsureReadCommitted()
        {
            Console.WriteLine("# DemoStateChange_EnsureReadCommitted");

            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            // Idempotent subscription
            conn.StateChange -= OnStateChangeWithIsolationLevelCheck;
            conn.StateChange += OnStateChangeWithIsolationLevelCheck;

            Console.WriteLine("Opening…");
            conn.Open(); // triggers handler → checks/resets isolation level

            // Simulate doing work
            using (var cmd = new SqlCommand("SELECT 1;", conn))
                cmd.ExecuteScalar();

            // (Optional) prove it correct after someone changed it:
            Console.WriteLine("Forcing a non-default isolation level (REPEATABLE READ) and reopening…");
            using (var setOther = new SqlCommand("SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;", conn))
                setOther.ExecuteNonQuery();

            conn.Close();   // Closed
            conn.Open();    // handler runs again → resets to READ COMMITTED

            Console.WriteLine("Closing…");
            conn.Close();
        }

        /// <summary>
        /// Handles InfoMessage events raised by SqlConnection.
        /// </summary>
        private static void OnInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
           PrintInfoMessage(e, true);
        }

        private static void OnInfoMessageWithoutPrintingErrors(object sender, SqlInfoMessageEventArgs e)
        {
            PrintInfoMessage(e, false);
        }

        private static void PrintInfoMessage(SqlInfoMessageEventArgs e, bool printErrors)
        {
            Console.WriteLine("SQL Server InfoMessage:");
            Console.WriteLine($"  Message: {e.Message}");
            Console.WriteLine($"  Source: {e.Source}");
            Console.WriteLine($"  Errors: {e.Errors.Count}");

            if (!printErrors)
            {
                return;
            }

            foreach (SqlError err in e.Errors)
            {
                Console.WriteLine(
                    $"    Number={err.Number}, State={err.State}, " +
                    $"Severity={err.Class}, Line={err.LineNumber}, Text={err.Message}"
                );
            }
        }

        public static void OnStateChange(object? sender, StateChangeEventArgs e)
        {
            var conn = (SqlConnection?)sender;
            // Try to print SPID if we’re already open; otherwise skip
            string spid = "(n/a)";
            if (conn?.State == ConnectionState.Open)
            {
                try
                {
                    using var cmd = new SqlCommand("SELECT @@SPID", conn);
                    spid = Convert.ToString(cmd.ExecuteScalar()) ?? "(n/a)";
                }
                catch { /* ignore if mid-transition */ }
            }

            Console.WriteLine($"[StateChange] {e.OriginalState} -> {e.CurrentState}; SPID={spid}");
        }

        // Single demo handler: on Open → verify/reset isolation level (READ COMMITTED)
        private static void OnStateChangeWithIsolationLevelCheck(object? sender, StateChangeEventArgs e)
        {
            Console.WriteLine($"[StateChange] {e.OriginalState} -> {e.CurrentState}");

            var conn = sender as SqlConnection;
            if (conn == null || e.CurrentState != ConnectionState.Open)
                return;

            try
            {
                // Read current isolation level for this session
                const string getLevelSql = @"
SELECT CASE transaction_isolation_level
         WHEN 0 THEN 'Unspecified'
         WHEN 1 THEN 'ReadUncommitted'
         WHEN 2 THEN 'ReadCommitted'
         WHEN 3 THEN 'RepeatableRead'
         WHEN 4 THEN 'Serializable'
         WHEN 5 THEN 'Snapshot'
       END
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;";
                using (var get = new SqlCommand(getLevelSql, conn))
                {
                    var level = get.ExecuteScalar() as string ?? "Unknown";
                    Console.WriteLine($"  Current isolation level: {level}");

                    if (!string.Equals(level, "ReadCommitted", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("  Resetting isolation level to READ COMMITTED…");
                        using var reset = new SqlCommand("SET TRANSACTION ISOLATION LEVEL READ COMMITTED;", conn);
                        reset.ExecuteNonQuery();

                        // (Optional) confirm after reset
                        using var confirm = new SqlCommand(getLevelSql, conn);
                        Console.WriteLine($"  After reset: {(confirm.ExecuteScalar() as string) ?? "Unknown"}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Keep demo resilient; avoid throwing from event handler
                Console.WriteLine($"  [Isolation check/reset skipped due to error] {ex.Message}");
            }
        }

    }
}
