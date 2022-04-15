using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Base.Controllers;
using Coflnet.Sky.Core;
using System.Collections.Generic;

namespace Coflnet.Sky.Base.Services
{

    public class BaseBackgroundService : BackgroundService
    {
        private IServiceScopeFactory scopeFactory;
        private IConfiguration config;
        private ILogger<BaseBackgroundService> logger;
        private Prometheus.Counter consumeCount = Prometheus.Metrics.CreateCounter("sky_base_conume", "How many messages were consumed");

        public BaseBackgroundService(
            IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<BaseBackgroundService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.config = config;
            this.logger = logger;
        }
        /// <summary>
        /// Called by asp.net on startup
        /// </summary>
        /// <param name="stoppingToken">is canceled when the applications stops</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            List<GoogleUser> users;
            using (var oldContext = new HypixelContext())
            {
                users = await oldContext.Users.Where(u => u.PremiumExpires > System.DateTime.Now || u.ReferedBy != 0).ToListAsync();
            }
            await MigrateRefs(scope, users);
            await MigratePayments(scope, users);
        }

        private async Task MigratePayments(IServiceScope scope, List<GoogleUser> users)
        {
            using (var paymentDb = scope.ServiceProvider.GetRequiredService<Payments.Models.PaymentContext>())
            {
                if (paymentDb.Users.Any())
                {
                    logger.LogInformation("skipping payments as there are already users in db");
                    return;
                }
                var premiumId = await paymentDb.Products.Where(p => p.Slug == "premium").FirstAsync();
                foreach (var user in users.Where(u => u.PremiumExpires > System.DateTime.Now))
                {
                    var pUser = new Payments.Models.User()
                    {
                        ExternalId = user.Id.ToString()
                    };
                    pUser.Owns = new List<Payments.Models.OwnerShip>();
                    pUser.Owns.Add(new Payments.Models.OwnerShip()
                    {
                        Expires = user.PremiumExpires,
                        Product = premiumId,
                        User = pUser
                    });
                    paymentDb.Users.Add(pUser);
                }
                await paymentDb.SaveChangesAsync();
            }
        }

        private async Task MigrateRefs(IServiceScope scope, List<GoogleUser> users)
        {
            using (var refDb = scope.ServiceProvider.GetRequiredService<Sky.Referral.Models.ReferralDbContext>())
            {
                if (refDb.Referrals.Any())
                {
                    logger.LogInformation("skipping referrals as there are already some in db");
                    return;
                }
                foreach (var user in users.Where(u => u.ReferedBy > 0))
                {
                    refDb.Referrals.Add(new Referral.Models.ReferralElement()
                    {
                        Invited = user.Id.ToString(),
                        Inviter = user.ReferedBy.ToString(),
                        Flags = Referral.Models.ReferralFlags.NONE
                    });
                }
                await refDb.SaveChangesAsync();
            }
        }
    }
}