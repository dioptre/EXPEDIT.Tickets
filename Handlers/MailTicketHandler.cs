using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EXPEDIT.Tickets.Models;
using Orchard.ContentManagement.Handlers;
using Orchard.Data;

namespace EXPEDIT.Tickets.Handlers
{
    public class MailTicketHandler : ContentHandler
    {
        public MailTicketHandler(IRepository<MailTicketPartRecord> repository)
        {
            Filters.Add(StorageFilter.For(repository));
            Filters.Add(new ActivatingFilter<MailTicketPart>("MailTicket"));
        }
    }

}