using Microsoft.Data.SqlClient;
using System.Data;

namespace AdoNetExamples
{
    internal static class DataAdapterExamples
    {
        private static readonly SqlConnectionStringBuilder ConnectionStringBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "tcp:localhost",
            InitialCatalog = "AdoNetExamples",
            UserID = "sa",
            Password = "Password1",
            TrustServerCertificate = true
        };


        
        public static void PopulateDataTable()
        {
            using var conn = new SqlConnection(ConnectionStringBuilder.ConnectionString);
            conn.Open();

            const string sql = @"SELECT Id, [Name], Email, CreatedAt
                             FROM dbo.Customers
                             ORDER BY Id;";

            // Create adapter
            using var adapter = new SqlDataAdapter(sql, conn);

            // Create DataTable to hold results
            var table = new DataTable();

            // Fill DataTable
            adapter.Fill(table);

            // Work with data disconnected
            foreach (DataRow row in table.Rows)
            {
                int id = (int)row["Id"];
                string name = (string)row["Name"];
                string? email = row.IsNull("Email") ? null : (string)row["Email"];
                DateTime created = (DateTime)row["CreatedAt"];

                Console.WriteLine($"{id}: {name,-10} | {email ?? "(no email)"} | {created:u}");
            }
        }

        public static void GetParentAndItsChildren()
        {
            using var conn = new SqlConnection(ConnectionStringBuilder.ConnectionString);

            var ds = new DataSet();

            // Load Customers
            var customers = new SqlDataAdapter("SELECT * FROM dbo.Customers", conn);
            customers.MissingSchemaAction = MissingSchemaAction.AddWithKey; // bring PK info
            customers.FillSchema(ds, SchemaType.Source);
            customers.Fill(ds, "Customers");

            // Load Orders
            var orders = new SqlDataAdapter("SELECT * FROM dbo.Orders", conn);
            orders.MissingSchemaAction = MissingSchemaAction.AddWithKey; // bring PK info
            orders.FillSchema(ds, SchemaType.Source);
            orders.Fill(ds, "Orders");

            // IMPORTANT: Add the relation explicitly (FillSchema does not add FKs)
            if (ds.Relations["FK_Orders_Customers"] is null)
            {
                var parentCol = ds.Tables["Customers"]?.Columns["Id"];
                var childCol = ds.Tables["Orders"]?.Columns["CustomerId"];
                if (parentCol != null && childCol != null)
                {
                    var rel = new DataRelation("FK_Orders_Customers", parentCol, childCol, createConstraints: true);
                    ds.Relations.Add(rel);
                }
            }

            Console.WriteLine("Relations discovered/added:");
            foreach (DataRelation rel in ds.Relations)
                Console.WriteLine($" - {rel.RelationName}: {rel.ParentTable.TableName} -> {rel.ChildTable.TableName}");

            Console.WriteLine("\nNavigate child → parent:");
            foreach (DataRow order in ds.Tables["Orders"].Rows)
            {
                var parentCustomer = order.GetParentRow(ds.Relations[0]); // FK_Orders_Customers
                Console.WriteLine($"Order {order["Id"],-2}: Amount={order["Amount"],6} -> Customer={parentCustomer["Name"]}");
            }
        }

        #region GetSchema
        public static void TestDataAdapterFillSchema() 
        {
            using var conn = new SqlConnection(ConnectionStringBuilder.ConnectionString);

            // FillSchema automatically fetches keys and constraints
            var adapter = new SqlDataAdapter("SELECT * FROM dbo.Customers", conn);

            var table = new DataTable("Customers");
            adapter.FillSchema(table, SchemaType.Source); // fetches schema + constraints
            adapter.Fill(table);
            Console.WriteLine($"Rows count :{table.Rows.Count}");

            Console.WriteLine("Columns:");
            foreach (DataColumn col in table.Columns)
            {
                Console.WriteLine($" - {col.ColumnName} ({col.DataType}) AllowNull={col.AllowDBNull}");
            }

            Console.WriteLine("\nPrimary Keys:");
            foreach (DataColumn pk in table.PrimaryKey)
            {
                Console.WriteLine($" - {pk.ColumnName}");
            }

            Console.WriteLine("\nConstraints:");
            foreach (Constraint c in table.Constraints)
            {
                Console.WriteLine($" - {c.ConstraintName} ({c.GetType().Name})");
            }
        }

        public static void TestSqlConnectionGetSchema()
        {
            using (var conn = new SqlConnection(ConnectionStringBuilder.ConnectionString))
            {
                conn.Open();
                DataTable constraints = conn.GetSchema("IndexColumns", [null, null, "Customers"]);

                foreach (DataRow row in constraints.Rows)
                {
                    Console.WriteLine($"{row["CONSTRAINT_NAME"]} → Column: {row["COLUMN_NAME"]}");
                }
            }
        }
        #endregion
    }

}
