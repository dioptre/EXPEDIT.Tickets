using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using XODB.Module.BusinessObjects;
using XODB.Models;
using System.ServiceModel;

namespace EXPEDIT.Tickets.Services
{
    [ServiceContract]
    public interface IMailTicketsService : IDependency
    {
        [OperationContract]
        void CheckMailAsync();

        [OperationContract]
        void CheckMail();
    }
  
}