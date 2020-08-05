using YamlDotNet.Serialization;

namespace WinSW.Configuration
{
    public class ServiceAccount
    {
        [YamlMember(Alias = "user")]
        public string? ServiceAccountName { get; set; }

        [YamlMember(Alias = "domain")]
        public string? ServiceAccountDomain { get; set; }

        [YamlMember(Alias = "password")]
        public string? ServiceAccountPassword { get; set; }

        [YamlMember(Alias = "allowservicelogon")]
        public bool AllowServiceAcountLogonRight { get; set; }

        public string? ServiceAccountUser
        {
            get => this.ServiceAccountName is null ? null : (this.ServiceAccountDomain ?? ".") + "\\" + this.ServiceAccountName;
        }

        public bool HasServiceAccount()
        {
            return !string.IsNullOrEmpty(this.ServiceAccountName);
        }
    }
}
