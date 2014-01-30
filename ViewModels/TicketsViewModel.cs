using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Web;
namespace EXPEDIT.Tickets.ViewModels
{
    public class TicketsViewModel
    {
        [JsonIgnore]
        public TicketViewModel[] Tickets { get; set; }
        
    }
    
}