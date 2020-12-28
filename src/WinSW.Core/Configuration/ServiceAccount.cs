namespace WinSW.Configuration
{
    public class ServiceAccount
    {
        public string? User { get; set; }

        public string? Domain { get; set; }

        public string? Password { get; set; }

        public bool AllowServiceLogonRight { get; set; }

        public string? FullUser
        {
            get => this.User is null ? null : (this.Domain ?? ".") + "\\" + this.User;
        }

        public bool HasServiceAccount()
        {
            return !string.IsNullOrEmpty(this.User);
        }
    }
}
