using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using System.ServiceModel;
using XODB.Module.BusinessObjects;
using EXPEDIT.Tickets.ViewModels;

namespace EXPEDIT.Tickets.Services
{
     [ServiceContract]
    public interface ITicketsService : IDependency 
    {       
         [OperationContract]
         void GetMail();

         [OperationContract]
         void PrepareTicket(ref TicketsViewModel m);

         [OperationContract]
         bool UpdateTicket(ref TicketsViewModel m);
    }
}