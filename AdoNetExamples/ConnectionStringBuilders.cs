using Microsoft.Data.SqlClient;

namespace AdoNetExamples
{
    internal static class ConnectionStringBuilders
    {
        public static readonly SqlConnectionStringBuilder AdoNetExamplesConnectionStringBuilder = new()
        {
            DataSource = "tcp:localhost",
            InitialCatalog = "AdoNetExamples",
            UserID = "sa",
            Password = "Password1",
            TrustServerCertificate = true
        };

        public static readonly SqlConnectionStringBuilder MySchoolDbConnectionStringBuilder = new()
        {
            DataSource = "tcp:localhost",
            InitialCatalog = "MySchool",
            UserID = "sa",
            Password = "Password1",
            TrustServerCertificate = true
        };
    }
}
