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
         void PrepareTicket(ref TicketViewModel m);

         [OperationContract]
         bool UpdateTicket(ref TicketViewModel m);

         [OperationContract]
         bool SubmitFile(TicketViewModel m);

         [OperationContract]
         List<dynamic> GetFiles(Guid m);

         [OperationContract]
         TicketViewModel[] GetMyTickets();

         [OperationContract]
         TicketViewModel[] GetSupportedTickets();

         [OperationContract]
         TicketViewModel[] GetAllTickets();
    }
}