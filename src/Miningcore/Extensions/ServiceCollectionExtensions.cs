using System;
using Microsoft.Extensions.DependencyInjection;
using Miningcore.Notifications;
using Miningcore.Messaging;
using Miningcore.Persistence;

namespace Miningcore.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNotificationService(
            this IServiceCollection services,
            IConnectionFactory connectionFactory,
            IMessageBus messageBus)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(connectionFactory);
            services.AddSingleton(messageBus);
            services.AddHostedService<NotificationService>();

            return services;
        }
    }
}