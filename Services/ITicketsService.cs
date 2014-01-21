using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Orchard;
using System.ServiceModel;
using XODB.Module.BusinessObjects;

namespace EXPEDIT.Tickets.Services
{
     [ServiceContract]
    public interface ITicketsService : IDependency 
    {
         [OperationContract]
         string GetRedirect(string routeURL);

         [OperationContract]
         FileData GetDownload(string downloadID, string requestIPAddress);

         [OperationContract]
         FileData GetFile(Guid fileDataID);

         [OperationContract]
         IEnumerable<IHtmlString> GetSearchResults(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize = null);

         [OperationContract]
         void GetMail();

    }
}