namespace winsw.Configuration
{
    public class ServiceAccount
    {
        public string? name { get; set; }
        public string? domain;
        public string? user;
        public string? password { get; set; }
        public string? allowservicelogon;
    }
}
