using Orchard.ContentManagement;
using System.ComponentModel.DataAnnotations;
using Orchard.ContentManagement.Aspects;
using Orchard.ContentManagement.Records;
using Orchard.Data.Conventions;
using System.Linq;

namespace EXPEDIT.Tickets.Models {

    //TODO: Check App specific reqts is working

    public class MailTicketPartRecord : ContentPartRecord
    {
        
    }

    public class MailTicketPart : ContentPart<MailTicketPartRecord>
    {
    }

}