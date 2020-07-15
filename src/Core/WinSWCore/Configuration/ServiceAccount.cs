using YamlDotNet.Serialization;

namespace winsw.Configuration
{
    public class ServiceAccount
    {
        [YamlMember(Alias = "user")]
        public string? ServiceAccountName { get; set; }

        [YamlMember(Alias = "domain")]
        public string? ServiceAccountDomain { get; set; }

        [YamlMember(Alias = "Password")]
        public string? ServiceAccountPassword { get; set; }

        [YamlMember(Alias = "allowservicelogon")]
        public bool AllowServiceAcountLogonRight { get; set; }

        public string? ServiceAccountUser
        {
            get => ServiceAccountName is null ? null : (ServiceAccountDomain ?? ".") + "\\" + ServiceAccountName;
            set { }
        }

        public bool HasServiceAccount()
        {
            return !string.IsNullOrEmpty(this.ServiceAccountName);
        }
    }
}
