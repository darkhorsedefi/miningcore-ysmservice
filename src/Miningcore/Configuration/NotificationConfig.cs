using Newtonsoft.Json;

namespace Miningcore.Configuration
{
    public partial class NotificationsConfig
    {
        public bool Enabled { get; set; }

        public EmailConfig Email { get; set; }
        public AdminNotificationsConfig Admin { get; set; }
        public PushoverConfig Pushover { get; set; }
        public WebhookConfig Webhook { get; set; }
    }

    public class EmailConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; }
        public bool EnableSsl { get; set; }
    }

    public class AdminNotificationsConfig
    {
        public bool Enabled { get; set; }
        public string EmailAddress { get; set; }
        public bool NotifyBlockFound { get; set; }
        public decimal? NotifyPaymentAbove { get; set; }
        public double? NotifyHashrateDropThreshold { get; set; }
        public bool NotifyPaymentSuccess { get; set; }
    }

    public class WebhookConfig
    {
        public bool Enabled { get; set; }
        public string[] Urls { get; set; }
        public WebhookAuthConfig Auth { get; set; }
        public WebhookNotificationConfig Notifications { get; set; }
    }

    public class WebhookAuthConfig
    {
        public string Type { get; set; }
        public string Token { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class WebhookNotificationConfig
    {
        public bool NotifyBlockFound { get; set; }
        public bool NotifyPayment { get; set; }
        public bool NotifyHashrateDrop { get; set; }
        public decimal? PaymentThreshold { get; set; }
        public double? HashrateDropThreshold { get; set; }
    }

    public class PoolNotificationsConfig
    {
        public bool Enabled { get; set; }
        public decimal? MinimumPaymentAmount { get; set; }
        public double? HashrateDropThreshold { get; set; }
        public string WebhookUrl { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public WebhookNotificationConfig WebhookNotifications { get; set; }
    }
}