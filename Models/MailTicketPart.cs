using Orchard.ContentManagement;
using System.ComponentModel.DataAnnotations;
using Orchard.ContentManagement.Aspects;
using Orchard.ContentManagement.Records;
using Orchard.Data.Conventions;
using System.Linq;
using System;
namespace EXPEDIT.Tickets.Models {

    //TODO: Check App specific reqts is working

    public class MailTicketPartRecord : ContentPartRecord
    {
        public virtual Guid? ApplicationID { get; set; }
    }

    public class MailTicketPart : ContentPart<MailTicketPartRecord>
    {
        public Guid? ApplicationID { get { return Record.ApplicationID; } set { Record.ApplicationID = value; } }
    }

}