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
        [Required]
        public Guid? CommunicationID { get; set; }
        public string CommunicationName { get; set; }
        public string CommunicationMobile { get; set; }
        public string CommunicationEmail { get; set; }
        public Guid? CommunicationContactID { get; set; }
        public Guid? CommunicationCompanyID { get; set; }
        public Guid? StatusWorkTypeID { get; set; }
        public Guid? RegardingWorkTypeID { get; set; }
        public Guid? RegardingCompanyID { get; set; }
        public Guid? RegardingContactID { get; set; }
        public Guid? RegardingFileDataID { get; set; }
        public Guid? RegardingProjectID { get; set; }
        public Guid? RegardingExperienceID { get; set; }
        public string RegardingDescription { get; set; }
        public string RegardingID { get; set; }
        public string RegardingTableType { get; set; }
        public Guid? RegardingReferenceID { get; set; }
        public Guid? OpenedBy { get; set; }
        public Guid? AssignedBy { get; set; }
        public Guid? MaintainedBy { get; set; }
        public Guid? ClosedBy { get; set; }
        public DateTime EstimatedClosure { get; set; }
        public decimal EstimatedDurationHours { get; set; }
        public decimal EstimatedRevenue { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal Probability { get; set; }
        [Required]
        public string Comment { get; set; }
        public List<string> OldComments { get; set; }
        public List<Tuple<Guid,string>> OldFiles { get; set; }
        public Dictionary<Guid,Tuple<Guid?,string>> CommunicationEmailsAdditional { get; set; }
        public Dictionary<Guid, TicketRegarding> CommunicationRegardingData { get; set; } //ReferenceID, TableType

        [JsonIgnore]
        public SelectList SlWorkTypesRegarding { get; set; }
        [JsonIgnore]
        public SelectList SlWorkTypesStatus { get; set; }
        [JsonIgnore]
        public SelectList SlRegarding { get; set; } //Made of [GUID]-[TableType], Description
        [JsonIgnore]
        public Dictionary<Guid, HttpPostedFileBase> Files { get; set; }
        [JsonIgnore]
        public Dictionary<Guid, int> FileLengths { get; set; }

        
    }

    public class TicketRegarding
    {
        public Guid ReferenceID { get; set; }
        public string TableType { get; set; }
        public string Description { get; set; }
    }

    
}