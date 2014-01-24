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

using LumiSoft.MailServer.API.UserAPI;

namespace EXPEDIT.Tickets.Services {
    
    [UsedImplicitly]
    public class TicketsService : ITicketsService {
        private readonly IOrchardServices _orchardServices;
        private readonly IContentManager _contentManager;
        private readonly IMessageManager _messageManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IUsersService _users;
        private readonly IMediaService _media;
        public ILogger Logger { get; set; }

        public TicketsService(
            IContentManager contentManager, 
            IOrchardServices orchardServices, 
            IMessageManager messageManager, 
            IScheduledTaskManager taskManager, 
            IUsersService users, 
            IMediaService media)
        {
            _orchardServices = orchardServices;
            _contentManager = contentManager;
            _messageManager = messageManager;
            _taskManager = taskManager;
            _media = media;
            _users = users;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
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
                    m.RegardingWorkTypeID = tx.RegardingWorkTypeID;
                    m.StatusWorkTypeID = tx.StatusWorkTypeID;
                    m.CommunicationRegardingData = (from o in d.CommunicationRegardingDatas where o.Version == 0 && o.VersionDeletedBy == null && o.CommunicationID == id && o.ReferenceID!=null select o)
                       .ToDictionary((f)=>f.CommunicationRegardingDataID, (e) => new TicketRegarding { ReferenceID = e.ReferenceID.Value, TableType = e.TableType, Description = e.ReferenceName });
                    m.CommunicationEmail = tx.CommunicationEmail;
                    m.CommunicationMobile = tx.CommunicationMobile;
                    m.CommunicationEmailsAdditional = (from o in d.CommunicationEmails where o.Version == 0 && o.VersionDeletedBy == null && o.CommunicationID == id select o)
                        .ToDictionary((f) => f.CommunicationEmailID, (e) => new Tuple<Guid?, string>(e.ContactID.Value, e.CommunicationEmail));
                    m.OldComments = (from o in d.Communications where o.CommunicationID == id && o.Version != 0 && o.VersionDeletedBy == null select o.Comment).ToList();
                    m.OldFiles = (from o in d.FileDatas
                                  where o.Version == 0 && o.VersionDeletedBy == null && o.TableType == table && o.ReferenceID == id
                                  select new Tuple<Guid, string>(o.FileDataID, o.FileName)).ToList();
                }
                else
                {
                    m.CommunicationID = Guid.NewGuid();
                    m.OpenedBy = contact.ContactID;
                }
                m.CommunicationContactID = contact.ContactID;
                m.CommunicationEmail = contact.DefaultEmail;
                m.CommunicationMobile = contact.DefaultMobile;
                

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


        public void GetMailFolders()
        {
            var mail = new IMAP_Client();
            mail.Logger = new Logger();
            //pop3.Logger.WriteLog += m_pLogCallback;
            mail.Connect("imap.gmail.com", 993, false);
            mail.Login("staff@miningappstore.com", "=652ymQ*");
            IMAP_r_u_List[] folders = mail.GetFolders(null);

            char folderSeparator = mail.FolderSeparator;
            var tree = new TreeHelper<string>();
            foreach (IMAP_r_u_List folder in folders)
            {
                string[] folderPath = folder.FolderName.Split(folderSeparator);


                // Conatins sub folders.
                if (folderPath.Length > 1)
                {

                    string currentPath = "";

                    foreach (string fold in folderPath)
                    {
                        if (currentPath.Length > 0)
                        {
                            currentPath += "/" + fold;
                        }
                        else
                        {
                            currentPath = fold;
                        }

                        TreeNode<string> node = tree.FindNode(fold);
                        if (node == null)
                        {
                            node = new TreeNode<string>(fold);
                            node.Data = currentPath;
                            tree.AddChild(node);
                        }
                    }
                }
                else
                {
                    TreeNode<string> node = new TreeNode<string>(folder.FolderName);
                    node.Data = folder.FolderName;
                    tree.AddSibling(node);
                }
            }


        }

        public void GetMail()
        {

            try
            {
                var mail = new IMAP_Client();
                mail.Logger = new Logger();
                mail.Logger.WriteLog += (object o, WriteLogEventArgs w) =>
                {
                    var y = w;
                };
                mail.Connect("support.miningappstore.com", 993, true);
                mail.Login("help@support.miningappstore.com", "help");
                try
                {
                    mail.SelectFolder("inbox");
                    
                    // Start fetching.
                    mail.Fetch(
                        false,
                        IMAP_t_SeqSet.Parse("1:*"),
                        new IMAP_t_Fetch_i[]{
                        new IMAP_t_Fetch_i_Envelope(),
                        new IMAP_t_Fetch_i_Flags(),
                        new IMAP_t_Fetch_i_InternalDate(),
                        new IMAP_t_Fetch_i_Rfc822Size(),
                        new IMAP_t_Fetch_i_Uid(),
                        new IMAP_t_Fetch_i_Body(),
                        new IMAP_t_Fetch_i_Rfc822()
                    },
                        (object sender, EventArgs<IMAP_r_u> e) =>
                        {
                            if (e.Value is IMAP_r_u_Fetch)
                            {
                                IMAP_r_u_Fetch fetchResp = (IMAP_r_u_Fetch)e.Value;

                                try
                                {
                                    string from = "";
                                    if (fetchResp.Envelope.From != null)
                                    {
                                        for (int i = 0; i < fetchResp.Envelope.From.Length; i++)
                                        {
                                            // Don't add ; for last item
                                            if (i == fetchResp.Envelope.From.Length - 1)
                                            {
                                                from += fetchResp.Envelope.From[i].ToString();
                                            }
                                            else
                                            {
                                                from += fetchResp.Envelope.From[i].ToString() + ";";
                                            }
                                        }
                                    }
                                    else
                                    {
                                        from = "<none>";
                                    }


                                    Mail_Message mime = Mail_Message.ParseFromStream(fetchResp.Rfc822.Stream);
                                    fetchResp.Rfc822.Stream.Dispose();
                                    foreach (MIME_Entity entity in mime.Attachments)
                                    {
                                        if (entity.ContentDisposition != null && entity.ContentDisposition.Param_FileName != null)
                                        {
                                            // item.Text = entity.ContentDisposition.Param_FileName;
                                        }
                                        else
                                        {
                                            //item.Text = "untitled";
                                        }
                                    }

                                    if (mime.BodyText != null)
                                    {
                                        //m_pTabPageMail_MessageText.Text = mime.BodyText;
                                    }

                                    //currentItem.Text = from;

                                    //currentItem.SubItems.Add(fetchResp.Envelope.Subject != null ? fetchResp.Envelope.Subject : "<none>");

                                    //currentItem.SubItems.Add(fetchResp.InternalDate.Date.ToString("dd.MM.yyyy HH:mm"));

                                    //currentItem.SubItems.Add(((decimal)(fetchResp.Rfc822Size.Size / (decimal)1000)).ToString("f2") + " kb");

                                    //m_pTabPageMail_Messages.Items.Add(currentItem);

                                    //Move file or delete it
                                    IMAP_t_SeqSet sequence_set = IMAP_t_SeqSet.Parse(fetchResp.UID.UID.ToString());
                                    Orchard.Email.Models.SmtpSettingsPart smtpSettings = null;
                                    if (_orchardServices.WorkContext != null)
                                        smtpSettings = _orchardServices.WorkContext.CurrentSite.As<Orchard.Email.Models.SmtpSettingsPart>();
                                    if (smtpSettings != null && !string.IsNullOrWhiteSpace(smtpSettings.Address) && from.IndexOf(smtpSettings.Address) >= 0)
                                    {
                                        if (!mail.GetFolders("Support").Any())
                                            mail.CreateFolder("Support");                                        
                                        mail.MoveMessages(true, sequence_set, "Support", false);
                                    }
                                    else
                                    {
                                        /* NOTE: In IMAP message deleting is 2 step operation.
                                         *  1) You need to mark message deleted, by setting "Deleted" flag.
                                         *  2) You need to call Expunge command to force server to dele messages physically.
                                         */
                                        mail.StoreMessageFlags(true, sequence_set, IMAP_Flags_SetType.Add, new IMAP_t_MsgFlags(new string[] { IMAP_t_MsgFlags.Deleted }));
                                    }
                                    mail.Expunge();


                                    //currentItem.Tag = fetchResp.UID.UID;

                                }
                                catch (Exception ex)
                                {

                                }
                            }
                        }
                    );
                }
                catch (Exception x)
                {
                }
            }
            catch (Exception x)
            {

            }
        }
       
    }
}
