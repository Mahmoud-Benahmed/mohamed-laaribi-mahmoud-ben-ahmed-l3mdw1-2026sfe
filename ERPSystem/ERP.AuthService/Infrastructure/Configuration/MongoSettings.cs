namespace ERP.AuthService.Infrastructure.Configuration
{
    public class MongoSettings
    {
        public string RootConnectionString { get; set; } = default!;
        public string ConnectionString { get; set; } = default!;
        public string DatabaseName { get; set; } = default!;
        public string AppUsername { get; set; } = default!;
        public string AppPassword { get; set; } = default!;
    }
}
