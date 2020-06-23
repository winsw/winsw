using YamlDotNet.Serialization;

namespace winsw.Configuration
{
    public class ServiceAccount
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "domain")]
        public string? Domain { get; set; }

        [YamlMember(Alias = "user")]
        public string? User{ get; set; }

        [YamlMember(Alias = "Password")]
        public string? Password { get; set; }

        [YamlMember(Alias = "allowservicelogon")]
        public string? AllowServiceLogon { get; set; }
    }
}
