using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Data;
using Autofac;
using Microsoft.Extensions.Hosting;
using Dapper;
using Npgsql;
using Newtonsoft.Json;
using NLog;
using Miningcore.Configuration;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Messaging;
using Contract = Miningcore.Contracts.Contract;

namespace Miningcore.Notifications
{
    public class NotificationService : IHostedService
    {
        private readonly IConnectionFactory cf;
        private readonly IMessageBus messageBus;
        private readonly ClusterConfig clusterConfig;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource cts;

        public NotificationService(
            IConnectionFactory cf,
            IMessageBus messageBus,
            ClusterConfig clusterConfig)
        {
            Contract.RequiresNonNull(cf);
            Contract.RequiresNonNull(messageBus);
            Contract.RequiresNonNull(clusterConfig);

            this.cf = cf;
            this.messageBus = messageBus;
            this.clusterConfig = clusterConfig;
        }

        private async Task SendEmailAsync(string subject, string body, string recipient)
        {
            try
            {
                if(string.IsNullOrEmpty(clusterConfig.Notifications?.Email?.Host))
                    return;

                using(var client = new SmtpClient(
                    clusterConfig.Notifications.Email.Host,
                    clusterConfig.Notifications.Email.Port))
                {
                    if (!string.IsNullOrEmpty(clusterConfig.Notifications.Email.User))
                        client.Credentials = new System.Net.NetworkCredential(
                            clusterConfig.Notifications.Email.User,
                            clusterConfig.Notifications.Email.Password);

                    client.EnableSsl = clusterConfig.Notifications.Email.EnableSsl;

                    using(var msg = new MailMessage(
                        clusterConfig.Notifications.Email.FromAddress,
                        recipient))
                    {
                        msg.Subject = subject;
                        msg.Body = body;
                        msg.IsBodyHtml = true;

                        await client.SendMailAsync(msg);
                    }
                }
            }
            catch(Exception ex)
            {
                logger.Error(ex, () => "Error sending notification email");
            }
        }

        public async Task ProcessBlockFoundAsync(Block block)
        {
            logger.Info(() => $"Processing block notification for height {block.BlockHeight}");

            try
            {
                using(var con = await cf.OpenConnectionAsync())
                {
                    using(var tx = con.BeginTransaction())
                    {
                        var notificationData = new
                        {
                            poolId = block.PoolId,
                            blockHeight = block.BlockHeight,
                            networkDifficulty = block.NetworkDifficulty,
                            status = block.Status,
                            effort = block.Effort,
                            miner = block.Miner,
                            reward = block.Reward
                        };

                        await con.ExecuteAsync(
                            "INSERT INTO notifications(type, data, created) VALUES(@type, @data::jsonb, now())",
                            new { type = "block", data = JsonConvert.SerializeObject(notificationData) },
                            tx);

                        tx.Commit();
                    }
                }

                if(clusterConfig.Notifications?.Admin?.NotifyBlockFound == true)
                {
                    var subject = $"New Block Found - Height {block.BlockHeight}";
                    var body = $@"
                        <h3>New Block Found</h3>
                        <p>Details:</p>
                        <ul>
                            <li>Pool: {block.PoolId}</li>
                            <li>Height: {block.BlockHeight}</li>
                            <li>Miner: {block.Miner}</li>
                            <li>Reward: {block.Reward}</li>
                            <li>Effort: {block.Effort:F2}%</li>
                        </ul>";

                    await SendEmailAsync(subject, body, clusterConfig.Notifications.Admin.EmailAddress);
                }
            }
            catch(Exception ex)
            {
                logger.Error(ex, () => "Error processing block notification");
            }
        }

        public async Task ProcessPaymentAsync(Payment payment)
        {
            logger.Info(() => $"Processing payment notification for {payment.Address}");

            try
            {
                using(var con = await cf.OpenConnectionAsync())
                {
                    using(var tx = con.BeginTransaction())
                    {
                        var notificationData = new
                        {
                            poolId = payment.PoolId,
                            address = payment.Address,
                            amount = payment.Amount,
                            transactionConfirmationData = payment.TransactionConfirmationData,
                            created = payment.Created
                        };

                        await con.ExecuteAsync(
                            "INSERT INTO notifications(type, data, created) VALUES(@type, @data::jsonb, now())",
                            new { type = "payment", data = JsonConvert.SerializeObject(notificationData) },
                            tx);

                        tx.Commit();
                    }
                }

                if(clusterConfig.Notifications?.Admin?.NotifyPaymentAbove.HasValue == true &&
                   payment.Amount >= clusterConfig.Notifications.Admin.NotifyPaymentAbove.Value)
                {
                    var subject = $"Large Payment Notification - {payment.Amount}";
                    var body = $@"
                        <h3>Large Payment Processed</h3>
                        <p>Details:</p>
                        <ul>
                            <li>Pool: {payment.PoolId}</li>
                            <li>Amount: {payment.Amount}</li>
                            <li>Address: {payment.Address}</li>
                            <li>Transaction: {payment.TransactionConfirmationData}</li>
                        </ul>";

                    await SendEmailAsync(subject, body, clusterConfig.Notifications.Admin.EmailAddress);
                }
            }
            catch(Exception ex)
            {
                logger.Error(ex, () => "Error processing payment notification");
            }
        }

        #region IHostedService

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            logger.Info(() => "Notification service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cts?.Cancel();
            logger.Info(() => "Notification service stopped");
            return Task.CompletedTask;
        }

        #endregion
    }
}