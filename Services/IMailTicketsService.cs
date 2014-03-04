using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using NKD.Module.BusinessObjects;
using NKD.Models;
using System.ServiceModel;
using EXPEDIT.Tickets.Models;

namespace EXPEDIT.Tickets.Services
{
    [ServiceContract]
    public interface IMailTicketsService : IDependency
    {
        [OperationContract]
        void CheckMailAsync();

        [OperationContract]
        void CheckMail(MailTicketPart m);
    }
  
}