using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using Orchard.ContentManagement;
using Orchard.FileSystems.Media;
using Orchard.Localization;
using Orchard.Security;
using Orchard.Settings;
using Orchard.Validation;
using Orchard;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Transactions;
using Orchard.Messaging.Services;
using Orchard.Logging;
using Orchard.Tasks.Scheduling;
using Orchard.Data;
#if XODB
using XODB.Module.BusinessObjects;
#else
using EXPEDIT.Utils.DAL.Models;
#endif
using XODB.Services;
using Orchard.Media.Services;
using EXPEDIT.Tickets.ViewModels;
using EXPEDIT.Tickets.Helpers;
using Orchard.DisplayManagement;
using ImpromptuInterface;
using XODB.Models;

using LumiSoft.Net.Log;
using LumiSoft.Net.MIME;
using LumiSoft.Net.Mail;
using LumiSoft.Net.IMAP;
using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net;
using System.Threading;
using XODB.Helpers;
using System.Web.Mvc;

using System.Web.Hosting;
using Orchard.Environment.Configuration;

namespace EXPEDIT.Tickets.Services {
    
    [UsedImplicitly]
    public class TicketsService : ITicketsService {

        private const string DIRECTORY_TEMP = "EXPEDIT.Tickets\\Temp";
      

        private readonly IOrchardServices _orchardServices;
        private readonly IContentManager _contentManager;
        private readonly IMessageManager _messageManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IUsersService _users;
        private readonly IMediaService _media;
        private readonly IMailApiService _mailApi;
        private ShellSettings _settings;
        private readonly IStorageProvider _storage;
        public ILogger Logger { get; set; }

        public TicketsService(
            IContentManager contentManager, 
            IOrchardServices orchardServices, 
            IMessageManager messageManager, 
            IScheduledTaskManager taskManager, 
            IUsersService users, 
            IMediaService media,
            IMailApiService mailApi,
            ShellSettings settings,
            IStorageProvider storage)
        {
            _orchardServices = orchardServices;
            _contentManager = contentManager;
            _messageManager = messageManager;
            _taskManager = taskManager;
            _media = media;
            _users = users;
            _mailApi = mailApi;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            _settings = settings;
            _storage = storage;
        }

        public Localizer T { get; set; }


        public void PrepareTicket(ref TicketsViewModel m)
        {
            if (m == null)
                throw new Exception("Invalid Ticket Object");
            var supplier = _users.ApplicationCompanyID;
            var application = _users.ApplicationID;
            var contact = _users.GetContact(_users.Username);
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var table = d.GetTableName<Communication>();
                if (m.CommunicationID.HasValue)
                {
                    var id = m.CommunicationID;
                    var tx = (from o in d.Communications where o.CommunicationID==id && o.Version == 0 && o.VersionDeletedBy == null select o).FirstOrDefault();
                    if (tx != null)
                    {
                        if (!m.RegardingWorkTypeID.HasValue)
                            m.RegardingWorkTypeID = tx.RegardingWorkTypeID;
                        if (!m.StatusWorkTypeID.HasValue)
                            m.StatusWorkTypeID = tx.StatusWorkTypeID;
                        if (string.IsNullOrWhiteSpace(m.CommunicationEmail))
                            m.CommunicationEmail = tx.CommunicationEmail;
                        if (string.IsNullOrWhiteSpace(m.CommunicationMobile))
                            m.CommunicationMobile = tx.CommunicationMobile;
                        if (string.IsNullOrWhiteSpace(m.Comment))
                            m.Comment = tx.Comment;
                    }
                    m.CommunicationEmailsAdditional = (from o in d.CommunicationEmails where o.Version == 0 && o.VersionDeletedBy == null && o.CommunicationID == id select o)
                        .ToDictionary((f) => f.CommunicationEmailID, (e) => new Tuple<Guid?, string>(e.ContactID.Value, e.CommunicationEmail));
                    m.CommunicationRegardingData = (from o in d.CommunicationRegardingDatas where o.Version == 0 && o.VersionDeletedBy == null && o.CommunicationID == id && o.ReferenceID!=null select o)
                       .ToDictionary((f)=>f.CommunicationRegardingDataID, (e) => new TicketRegarding { ReferenceID = e.ReferenceID.Value, TableType = e.TableType, Description = e.ReferenceName });

                    if (m.CommunicationRegardingData.Any())
                    {
                        var regarding = m.CommunicationRegardingData.FirstOrDefault();
                        m.RegardingID = string.Format("[{0}][{1}]", regarding.Value.ReferenceID, regarding.Value.TableType);
                    }
                    m.OldComments = (from o in d.Communications where o.CommunicationID == id && o.Version != 0 && o.VersionDeletedBy == null select o.Comment).ToList();
                    m.OldFiles = (from o in d.FileDatas
                                  where o.Version == 0 && o.VersionDeletedBy == null && o.TableType == table && o.ReferenceID == id
                                  select new {o.FileDataID, o.FileName}).AsEnumerable().Select(f=>new Tuple<Guid, string>(f.FileDataID, f.FileName)).ToList();
                }
                else
                {
                    m.OpenedBy = contact.ContactID;
                    m.CommunicationContactID = contact.ContactID;
                    m.CommunicationEmail = contact.DefaultEmail;
                    m.CommunicationMobile = contact.DefaultMobile;

                }
                

                //Fill Select Lists
                var allRegarding = (from o in d.E_SP_GetSupportItems(null, application, supplier, ConstantsHelper.DEVICE_TYPE_SOFTWARE, null, null, null)
                           select new SelectListItem
                           {
                               Value = string.Format("[{0}][{1}]", o.ReferenceID, o.TableType),
                               Text = o.Description
                           });
                m.SlRegarding = new SelectList(allRegarding.ToArray(), "Value", "Text");

                var allStatusWT = (from o in d.DictionaryWorkTypeRelations
                                   where
                                       o.Version == 0 && o.VersionDeletedBy == null
                                       && o.ParentWorkTypeID == ConstantsHelper.WORK_TYPE_SUPPORT_STATUS
                                   select new { o.WorkTypeID, o.WorkType.WorkTypeName }).AsEnumerable().Select(f =>
                                     new SelectListItem
                                    {
                                        Value = string.Format("{0}", f.WorkTypeID),
                                        Text = f.WorkTypeName
                                    });
                m.SlWorkTypesStatus = new SelectList(allStatusWT.ToArray(), "Value", "Text");

                var allRegardingWT = (from o in d.DictionaryWorkTypeRelations
                                      where
                                          o.Version == 0 && o.VersionDeletedBy == null
                                          && o.ParentWorkTypeID == ConstantsHelper.WORK_TYPE_SUPPORT_REGARDING
                                      select new { o.WorkTypeID, o.WorkType.WorkTypeName }).AsEnumerable().Select(f =>
                                     new SelectListItem
                                     {
                                         Value = string.Format("{0}", f.WorkTypeID),
                                         Text = f.WorkTypeName
                                     });
                m.SlWorkTypesRegarding = new SelectList(allRegardingWT.ToArray(), "Value", "Text");

 
            }

        }



        public bool UpdateTicket(ref TicketsViewModel m)
        {
            if (m == null)
                return false;
            var supplier = _users.ApplicationCompanyID;
            var application = _users.ApplicationID;
            var contact = _users.GetContact(_users.Username);
            var id = m.CommunicationID;
            var antecedent = id;
            string from = null;
            string[] recipients = null;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {

                var d = new XODBC(_users.ApplicationConnectionString, null);
                var c = d.Communications.Where<Communication>((f) => f.Version == 0 && f.VersionDeletedBy == null && f.CommunicationID == id).Select(f => f).FirstOrDefault();
                if (c == null)
                {
                    //Update new
                    c = new Communication();
                    c.CommunicationID = id.Value;
                    c.VersionAntecedentID = c.CommunicationID;
                    c.VersionOwnerContactID = contact.ContactID;
                    c.CommunicationContactID = contact.ContactID;
                    d.AddToCommunications(c);
                }
                //Reusable Vars
                antecedent = c.VersionAntecedentID.Value;
                string tt = null;
                Guid refID = default(Guid);
                if (!string.IsNullOrWhiteSpace(m.RegardingID))
                {
                    var temp = m.RegardingID.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    if (temp.Length != 2)
                        return false;
                    if (Guid.TryParse(temp[0], out refID))
                        m.RegardingReferenceID = refID;
                    if (temp[1].Length < 255 && temp[1].IndexOf('_') > -1)
                    {
                        tt = temp[1];
                        m.RegardingTableType = tt;
                    }
                }
                //Update related datasets
                if (!string.IsNullOrWhiteSpace(tt) && refID != default(Guid))
                {
                    if (!d.CommunicationRegardingDatas.Any(f => f.CommunicationID == c.CommunicationID && f.TableType == tt && f.ReferenceID == refID))
                    {
                        c.CommunicationRegarding.Add(new CommunicationRegardingData
                        {
                            CommunicationID = c.CommunicationID,
                            CommunicationRegardingDataID = Guid.NewGuid(),
                            TableType = m.RegardingTableType,
                            ReferenceID = m.RegardingReferenceID,
                            VersionOwnerContactID = contact.ContactID,
                            VersionUpdatedBy = contact.ContactID
                        });
                    }
                    var support = (from o in d.E_SP_GetSupportEmail(m.RegardingTableType, m.RegardingReferenceID)
                                   where o.ContactID != null && o.Email!=null
                                   select new CommunicationEmails
                                   {
                                       CommunicationID = id.Value,
                                       CommunicationEmail = o.Email,
                                       ContactID = o.ContactID,
                                       VersionOwnerContactID = contact.ContactID,
                                       VersionUpdatedBy = contact.ContactID
                                   }).FirstOrDefault();
                    if (support != null && !d.CommunicationEmails.Any(
                        f => f.CommunicationID == id.Value
                            && (f.ContactID == support.ContactID || f.CommunicationEmail == support.CommunicationEmail)))
                    {
                        c.CommunicationEmailsOther.Add(support);
                    }
                }
                //Update Both
                if (c.CommunicationEmail != _users.Email)
                    c.CommunicationEmail = _users.Email;
                if (c.Comment != m.Comment)
                    c.Comment = m.Comment;
                if (c.VersionUpdatedBy != contact.ContactID)                
                    c.VersionUpdatedBy = contact.ContactID;
                if (c.RegardingWorkTypeID != m.RegardingWorkTypeID)
                    c.RegardingWorkTypeID = m.RegardingWorkTypeID;
                if (c.StatusWorkTypeID != m.StatusWorkTypeID)
                    c.StatusWorkTypeID = m.StatusWorkTypeID;
                d.SaveChanges();
                //Now fill email 'BCC/TO' list
                recipients = d.CommunicationEmails.Where(f => f.CommunicationID == id && f.VersionDeletedBy == null && f.Version == 0).Select(f => f.CommunicationEmail)
                    .Union(new string[] { c.CommunicationEmail, _users.Email }).ToArray();

                from = string.Format("{0}{1}", antecedent, ConstantsHelper.MailSuffix);
            }
            _mailApi.ProcessApiRequestAsync(MailApiCall.AliasAdd, ConstantsHelper.MailUserEmail, from); //Should happen in ~45 secs
            _users.EmailUsersAsync(recipients, "Updated Support Ticket", string.Format("{0}<br/>^^^<br/>{1} on {2}<br/>{3}", m.Comment, _users.Username, _orchardServices.WorkContext.CurrentSite.SiteName, ConstantsHelper.EMAIL_FOOTER), false, false, from, _orchardServices.WorkContext.CurrentSite.SiteName, true);
            return true;
        }

      


        public bool SubmitFile(TicketsViewModel m)
        {
            if (m.CommunicationID == default(Guid))
                return false;
            var contact = _users.ContactID;
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                //First check if any data is not owned by me
                if ((from o in d.FileDatas where o.ReferenceID == m.CommunicationID && o.VersionOwnerContactID != contact.Value select o).Any())
                    return false;
                if (m.Files != null)
                {
                    var table = d.GetTableName<Communication>();
                    var mediaPath = HostingEnvironment.IsHosted ? HostingEnvironment.MapPath("~/Media/") ?? "" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
                    var storagePath = Path.Combine(mediaPath, _settings.Name);
                    foreach (var f in m.Files)
                    {
                        var filename = string.Concat(f.Value.FileName.Reverse().Take(50).Reverse());

                        var file = new FileData
                        {
                            FileDataID = f.Key,
                            TableType = table,
                            ReferenceID = m.CommunicationID,
                            FileTypeID = null, //TODO give type
                            FileName = filename,
                            FileLength = f.Value.ContentLength,
                            MimeType = f.Value.ContentType,
                            VersionOwnerContactID = contact,
                            DocumentType = ConstantsHelper.DOCUMENT_TYPE_TICKET_SUBMISSION
                        };
                        m.FileLengths.Add(f.Key, f.Value.ContentLength);
                        _media.GetMediaFolders(DIRECTORY_TEMP);
                        var path = string.Format("{0}\\{1}-{2}-{3}", DIRECTORY_TEMP, m.CommunicationID.ToString().Replace("-", ""), f.Key.ToString().Replace("-", "").Substring(15), filename.ToString().Replace("-", ""));
                        var sf = _storage.CreateFile(path);
                        using (var sw = sf.OpenWrite())
                            f.Value.InputStream.CopyTo(sw);
                        f.Value.InputStream.Close();
                        try
                        {

                            using (var dh = new DocHelper.FilterReader(Path.Combine(storagePath, path)))
                                file.FileContent = dh.ReadToEnd();
                        }
                        catch { }
                        using (var sr = sf.OpenRead())
                            file.FileBytes = sr.ToByteArray();
                        _storage.DeleteFile(path);
                        file.FileChecksum = file.FileBytes.ComputeHash();
                        d.FileDatas.AddObject(file);
                        d.SaveChanges(); //Commit after each file

                    }
                }


            }

            return true;



        }
    }
}
