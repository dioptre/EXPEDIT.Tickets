using JetBrains.Annotations;
using Orchard.Logging;
using Orchard.Tasks;
using Orchard;
using Orchard.Localization;
using Orchard.ContentManagement;
using Orchard.Services;
using NKD.Models;
using System.Linq;
using Orchard.Data;
using Orchard.ContentManagement.Handlers;
using Orchard.Caching;
using System;
using NKD.Services;

namespace EXPEDIT.Tickets.Services
{
    /// <summary>
    /// Regularly fires user sync events
    /// </summary>
    [UsedImplicitly]
    public class MailTicketsBackgroundTask : IBackgroundTask
    {
        public const string SIGNAL_KEY = "EXPEDIT.Tickets.MailTicketsBackgroundTask.NextUpdate";

        private readonly ICacheManager _cache;
        private readonly ISignals _signals;        
        private readonly IClock _clock;
        private readonly IMailTicketsService _mailTickets;
        public IOrchardServices Services { get; set; }
        public ILogger Logger { get; set; }
        public Localizer T { get; set; }


        public MailTicketsBackgroundTask(IOrchardServices services, IUsersService userService, IClock clock, ICacheManager cache, ISignals signals, IMailTicketsService mailTickets)
        {
            _signals = signals;
            _cache = cache;
            Services = services;
            _clock = clock;
            _mailTickets = mailTickets;
            Logger = NullLogger.Instance;
        }


        public void Sweep()
        {
            DateTime? nextUpdate = _cache.Get<string, DateTime?>(SIGNAL_KEY, ctx =>
            {
                ctx.Monitor(_signals.When(SIGNAL_KEY));
                return _clock.UtcNow.AddMinutes(2);
            });

            if (_clock.UtcNow > nextUpdate.GetValueOrDefault())
            {
                _signals.Trigger(SIGNAL_KEY);
                _mailTickets.CheckMail(null); //Could go async further here, but will need to decouple context dependent db writes (_users.sendmail etc)               

            }
        }
    }
}
