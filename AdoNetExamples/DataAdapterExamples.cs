using Microsoft.Data.SqlClient;
using System.Data;

namespace AdoNetExamples
{
    internal static class DataAdapterExamples
    {        
        public static void PopulateDataTable()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

            //Note: Fill method implicitly opens the Connection that the DataAdapter is using if it finds that the connection is not already open.
            //If Fill opened the connection, it also closes the connection when Fill is finished.
            //However, if you are performing multiple operations that require an open connection,
            //you can improve the performance of your application by explicitly calling the Open method of the Connection
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

        public static void MultipleResultSetsWithRelations()
        {
            const string sql = @"
SELECT Id, [Name], Email, CreatedAt
FROM dbo.Customers;

SELECT Id, CustomerId, Payed, Amount, CreatedAt
FROM dbo.Orders;

SELECT OrderId, ProductId, Quantity, Price
FROM dbo.OrderDetails;

SELECT Id, [Name], CurrentPrice, CreatedAt
FROM dbo.Products;";

            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);
            var dataSet = new DataSet();
            using var dataAdapter = new SqlDataAdapter(sql, conn);
            dataAdapter.TableMappings.Add("Table", "NorthwindCustomers");

            dataAdapter.Fill(dataSet);
            dataSet.Tables[0].TableName = "Customers";
            dataSet.Tables[1].TableName = "Orders";
            dataSet.Tables[2].TableName = "OrderDetails";
            dataSet.Tables[3].TableName = "Products";

            // ---- Relations
            // Customers.Id -> Orders.CustomerId
            dataSet.Relations.Add(
                "CustOrders",
                dataSet.Tables["Customers"]!.Columns["Id"]!,
                dataSet.Tables["Orders"]!.Columns["CustomerId"]!);

            // Orders.Id -> OrderDetails.OrderId
            dataSet.Relations.Add(
                "Order_OrderDetails",
                dataSet.Tables["Orders"]!.Columns["Id"]!,
                dataSet.Tables["OrderDetails"]!.Columns["OrderId"]!);

            // Products.Id -> OrderDetails.ProductId
            dataSet.Relations.Add(
                "Product_OrderDetails",
                dataSet.Tables["Products"]!.Columns["Id"]!,
                dataSet.Tables["OrderDetails"]!.Columns["ProductId"]!);

            // ---- Printing ----
            foreach (DataRow cust in dataSet.Tables["Customers"]!.Rows)
            {
                int custId = (int)cust["Id"];
                string name = (string)cust["Name"];
                string email = cust.IsNull("Email") ? "(no email)" : (string)cust["Email"];
                DateTime custCreated = (DateTime)cust["CreatedAt"];

                Console.WriteLine($"\nCUSTOMER #{custId}: {name}  <{email}>  Created: {custCreated:yyyy-MM-dd HH:mm:ss}");

                // children: Orders
                var orders = cust.GetChildRows("CustOrders");
                if (orders.Length == 0)
                {
                    Console.WriteLine("  (no orders)");
                    continue;
                }

                foreach (DataRow ord in orders)
                {
                    int orderId = (int)ord["Id"];
                    bool payed = (bool)ord["Payed"];
                    decimal? amount = ord.IsNull("Amount") ? null : (decimal?)ord["Amount"];
                    DateTime orderCreated = (DateTime)ord["CreatedAt"];

                    Console.WriteLine($"  ORDER #{orderId}  Payed={(payed ? "Yes" : "No")}  " +
                                      $"Amount={(amount.HasValue ? amount.Value.ToString("0.00") : "(null)")}  " +
                                      $"Created: {orderCreated:yyyy-MM-dd HH:mm:ss}");

                    // children: OrderDetails
                    var orderDetailsList = ord.GetChildRows("Order_OrderDetails");
                    if (orderDetailsList.Length == 0)
                    {
                        Console.WriteLine("    (no order lines)");
                        continue;
                    }

                    decimal computedTotal = 0m;

                    foreach (DataRow orderDetails in orderDetailsList)
                    {
                        int productId = (int)orderDetails["ProductId"];
                        int qty = (int)orderDetails["Quantity"];
                        decimal? linePrice = orderDetails.IsNull("Price") ? null : (decimal?)orderDetails["Price"];

                        // parent: Product (via Product_OrderDetails)
                        DataRow product = orderDetails.GetParentRow("Product_OrderDetails")!;
                        string productName = (string)product["Name"];
                        decimal currentPrice = (decimal)product["CurrentPrice"];

                        // compute orderDetails total by snapshot Price if present, else by current price
                        decimal unitPrice = linePrice ?? currentPrice;
                        decimal total = unitPrice * qty;
                        computedTotal += total;

                        Console.WriteLine($"    • {productName} (ProductId={productId})  " +
                                          $"Qty={qty}  Unit={(linePrice.HasValue ? $"{linePrice:0.00} (snapshot)" : $"{currentPrice:0.00} (current)")}" +
                                          $"  LineTotal={total:0.00}");
                    }

                    Console.WriteLine($"    => Computed Order Total: {computedTotal:0.00}");
                }
            }
        }

        public static void GetParentAndItsChildren()
        {
            using var conn = new SqlConnection(AdoNetExamplesConnectionStringBuilder.ConnectionString);

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

            Console.WriteLine("\nNavigate child -> parent:");
            foreach (DataRow order in ds.Tables["Orders"]!.Rows)
            {
                var parentCustomer = order.GetParentRow(ds.Relations[0]); // FK_Orders_Customers
                Console.WriteLine($"Order {order["Id"],-2}: Amount={order["Amount"],6} -> Customer={parentCustomer!["Name"]}");
            }
        }
    }

}
