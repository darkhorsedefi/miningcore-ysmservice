using System;
using System.Threading.Tasks;
using System.Reactive;
using Autofac;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Koto.Configuration;
using Miningcore.Persistence.Repositories;
using Miningcore.JsonRpc;
using Miningcore.Configuration;
using Miningcore.Mining;
using Miningcore.Notifications;
using Miningcore.Persistence;
using Miningcore.Stratum;
using Miningcore.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace Miningcore.Blockchain.Koto
{
    [CoinFamily(CoinFamily.Koto)]
    public class KotoPool : PoolBase
    {
        private KotoJobManager manager;
        private KotoExtraNonceProvider extraNonceProvider;

        public KotoPool(IComponentContext ctx,
            JsonSerializerSettings serializerSettings,
            IConnectionFactory cf,
            IMasterClock clock,
            NotificationService notificationService,
            IStatsRepository statsRepo) :
            base(ctx, serializerSettings, cf, clock, notificationService, statsRepo)
        {
        }

        protected override async Task SetupJobManager(CancellationToken ct)
        {
            manager = ctx.Resolve<KotoJobManager>();
            extraNonceProvider = ctx.Resolve<KotoExtraNonceProvider>();
            manager.Configure(poolConfig, clusterConfig);
            await manager.StartAsync(ct);
        }

        protected override async Task OnRequestAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
        {
            var request = tsRequest;
            var context = connection.ContextAs<BitcoinWorkerContext>();
            var requestId = request.Value.Id;
            var method = request.Value.Method;

            switch (method)
            {
                case BitcoinStratumMethods.Subscribe:
                    await OnSubscribeAsync(ct, connection, requestId, request.Value.Params);
                    break;

                case BitcoinStratumMethods.Authorize:
                    await OnAuthorizeAsync(ct, connection, requestId, request.Value.Params);
                    break;

                case BitcoinStratumMethods.SubmitShare:
                    await OnSubmitAsync(connection, tsRequest, ct);
                    break;

                case BitcoinStratumMethods.GetTransactions:
                    await OnGetTransactionsAsync(ct, connection, requestId, request.Value.Params);
                    break;

                case BitcoinStratumMethods.GetJob:
                    await OnGetJobAsync(ct, connection, requestId, request.Value.Params);
                    break;

                case BitcoinStratumMethods.MiningSubscribe:
                    await OnMiningSubscribeAsync(ct, connection, requestId, request.Value.Params);
                    break;

                default:
                    logger.Warn(() => $"[{LogCategory}] Unsupported RPC request: {method}");
                    break;
            }
        }

        public override double HashrateFromShares(double shares, double interval)
        {
            var multiplier = BitcoinConstants.Pow2x32;
            var result = shares * multiplier / interval / 1000000 * 2;
        
            result /= hashrateDivisor;
            return result;
        }

        private async Task OnSubscribeAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<BitcoinWorkerContext>();

            var extraNonce1 = extraNonceProvider.Next();
            context.ExtraNonce1 = extraNonce1;
            context.ExtraNonce2Size = extraNonceProvider.Size;

            var response = new object[]
            {
                new object[]
                {
                    new object[] { BitcoinStratumMethods.MiningNotify, extraNonce1, context.ExtraNonce2Size }
                },
                context.SubscriberId
            };

            await client.RespondAsync(response, requestId, false);
        }

        private async Task OnAuthorizeAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var workerName = parameters[0].ToString();
            var password = parameters[1]?.ToString();

            var context = client.ContextAs<BitcoinWorkerContext>();
            context.IsAuthorized = true;
            context.MinerName = workerName;
            context.MinerPassword = password;

            await client.RespondAsync(true, requestId);
        }

    protected virtual async Task OnSubmitAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<BitcoinWorkerContext>();

        try
        {
            if(request.Id == null)
                throw new StratumException(StratumError.MinusOne, "missing request id");

            // check age of submission (aged submissions are usually caused by high server load)
            var requestAge = clock.Now - tsRequest.Timestamp.UtcDateTime;

            if(requestAge > maxShareAge)
            {
                logger.Warn(() => $"[{connection.ConnectionId}] Dropping stale share submission request (server overloaded?)");
                return;
            }

            // check worker state
            context.LastActivity = clock.Now;

            // validate worker
            if(!context.IsAuthorized)
                throw new StratumException(StratumError.UnauthorizedWorker, "unauthorized worker");
            else if(!context.IsSubscribed)
                throw new StratumException(StratumError.NotSubscribed, "not subscribed");

            var requestParams = request.ParamsAs<string[]>();

            // submit
            var share = await manager.SubmitShareAsync(connection, requestParams, ct);
            
            // Nicehash's stupid validator insists on "error" property present
            // in successful responses which is a violation of the JSON-RPC spec
            // [We miss you Oliver <3 We miss you so much <3 Respect the goddamn standards Nicehash :(]
            var response = new JsonRpcResponse<object>(true, request.Id);

            if(context.IsNicehash)
            {
                response.Extra = new Dictionary<string, object>();
                response.Extra["error"] = null;
            }
            
            await connection.RespondAsync(response);

            // publish
            messageBus.SendMessage(share);

            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, true);

            logger.Info(() => $"[{connection.ConnectionId}] Share accepted: D={Math.Round(share.Difficulty, 3)}");

            // update pool stats
            if(share.IsBlockCandidate)
                poolStats.LastPoolBlockTime = clock.Now;

            // update client stats
            context.Stats.ValidShares++;

            await UpdateVarDiffAsync(connection, false, ct);
        }

        catch(StratumException ex)
        {
            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, false);

            // update client stats
            context.Stats.InvalidShares++;
            logger.Info(() => $"[{connection.ConnectionId}] Share rejected: {ex.Message} [{context.UserAgent}]");

            // banning
            ConsiderBan(connection, context, poolConfig.Banning);

            throw;
        }
    }

        private async Task OnGetTransactionsAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<BitcoinWorkerContext>();

            var transactions = manager.GetTransactionsForStratum();
            await client.RespondAsync(transactions, requestId);
        }

        private async Task OnGetJobAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<BitcoinWorkerContext>();
            var jobParams = manager.GetJobParamsForStratum(context.IsAuthorized);

            await client.RespondAsync(jobParams, requestId);
        }

        private async Task OnMiningSubscribeAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<BitcoinWorkerContext>();

            var extraNonce1 = extraNonceProvider.Next();
            context.ExtraNonce1 = extraNonce1;
            context.ExtraNonce2Size = extraNonceProvider.Size;

            var response = new object[]
            {
                new object[] { BitcoinStratumMethods.MiningNotify, extraNonce1, context.ExtraNonce2Size }
            };

            await client.RespondAsync(response, requestId, false);
        }
        protected override WorkerContextBase CreateWorkerContext()
        {
            return new KotoWorkerContext();
        }
    }
}
