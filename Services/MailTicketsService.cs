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

using EXPEDIT.Tickets.Models;

namespace EXPEDIT.Tickets.Services
{

    [UsedImplicitly]
    public class MailTicketsService : IMailTicketsService
    {
        private readonly IContentManager _contentManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IConcurrentTaskService _concurrentTasks;
        private readonly IOrchardServices _services;
        private readonly ITicketsService _tickets;
        private readonly IUsersService _users;
        private readonly IMailApiService _mailApi;
        public ILogger Logger { get; set; }

        public MailTicketsService(
            IContentManager contentManager,
            IScheduledTaskManager taskManager,
            IConcurrentTaskService concurrentTasks,
            ITicketsService tickets,
            IUsersService users,
            IMailApiService mailApi)
        {
            _contentManager = contentManager;
            _taskManager = taskManager;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            _concurrentTasks = concurrentTasks;
            _tickets = tickets;
            _users = users;
            _mailApi = mailApi;
        }

        public Localizer T { get; set; }

        public void GetMailFolders()
        {
            var mail = new IMAP_Client();
            mail.Logger = new Logger();
            //pop3.Logger.WriteLog += m_pLogCallback;
            mail.Connect(ConstantsHelper.MailHost, ConstantsHelper.MailPort, false);
            mail.Login(ConstantsHelper.MailUserEmail, ConstantsHelper.MailPassword);
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

        public void CheckMail(MailTicketPart m) //Todo Appaware
        {

            try
            {
                var mail = new IMAP_Client();
                mail.Logger = new Logger();
                mail.Logger.WriteLog += (object o, WriteLogEventArgs w) =>
                {
                    var y = w;
                };
                mail.Connect(ConstantsHelper.MailHost, ConstantsHelper.MailPort, true);
                mail.Login(ConstantsHelper.MailUserEmail, ConstantsHelper.MailPassword);
                var folderSupport = mail.GetFolders(ConstantsHelper.EMAIL_FOLDER_SUPPORT);
                if (!folderSupport.Any())
                {
                    try
                    {
                        mail.CreateFolder(ConstantsHelper.EMAIL_FOLDER_SUPPORT);
                    }
                    catch { }
                }
                var deleted = new List<IMAP_t_SeqSet>();
                var moved = new List<IMAP_t_SeqSet>();
                try
                {
                    mail.SelectFolder(ConstantsHelper.EMAIL_FOLDER_INBOX);
                    // Start fetching.
                    mail.Fetch(
                        false,
                        IMAP_t_SeqSet.Parse("1:*"), //FETCH ALL
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
                                    string fromText = "";
                                    if (fetchResp.Envelope.From != null)
                                    {
                                        for (int i = 0; i < fetchResp.Envelope.From.Length; i++)
                                        {
                                            // Don't add ; for last item
                                            if (i == fetchResp.Envelope.From.Length - 1)
                                            {
                                                fromText += fetchResp.Envelope.From[i].ToString();
                                            }
                                            else
                                            {
                                                fromText += fetchResp.Envelope.From[i].ToString() + ";";
                                            }
                                        }
                                    }
                                    else
                                    {
                                        fromText = "<none>";
                                    }
                                    
                                    

                                    //Move file or delete it
                                    IMAP_t_SeqSet sequence_set = IMAP_t_SeqSet.Parse(fetchResp.UID.UID.ToString());
                                    if (fromText.IndexOf(ConstantsHelper.MailSuffix) > -1)
                                    {
                                        moved.Add(sequence_set);
                                    }
                                    else
                                    {
                                        //Func<int> x = () => { return 1; };
                                        //x();
                                        Guid? contactID = null;
                                        string fromAddress = null;
                                        string body = null;
                                        foreach (Mail_t_Mailbox from in fetchResp.Envelope.From)
                                        {
                                            contactID = _users.GetEmailContactID(from.Address, false);
                                            if (contactID.HasValue && contactID.Value != default(Guid))
                                            {
                                                fromAddress = from.Address;
                                                break;
                                            }
                                        }
                                        if (contactID.HasValue && contactID.Value != default(Guid) && !string.IsNullOrWhiteSpace(fromAddress))
                                        {
                                            string newFrom = null;
                                            string[] recipients = new string[] { };
                                            using (new TransactionScope(TransactionScopeOption.Suppress))
                                            {
                                                var d = new XODBC(_users.ApplicationConnectionString, null);

                                                //--Despatch to new Ticket and delete--
                                                //to help -> new ticket
                                                var isNew = true;
                                                Guid id = default(Guid);
                                                Communication c = null;
                                                foreach (Mail_t_Mailbox to in fetchResp.Envelope.To)
                                                {
                                                    var address = string.Format("{0}", to.Address);
                                                    var index = address.IndexOf(ConstantsHelper.MailSuffix);
                                                    if (index > -1)
                                                        address = address.Substring(0, index);
                                                    if (Guid.TryParse(address, out id))
                                                    {
                                                        isNew = false;
                                                        break;
                                                    }
                                                }
                                                if (id != default(Guid))
                                                {
                                                    c = d.Communications.Where<Communication>((f) => f.Version == 0 && f.VersionDeletedBy == null && f.CommunicationID == id).Select(f => f).FirstOrDefault();
                                                }
                                                if (isNew || c == null)
                                                {
                                                    c = new Communication { CommunicationID = Guid.NewGuid() };
                                                    c.VersionAntecedentID = c.CommunicationID;
                                                    c.VersionOwnerContactID = contactID;
                                                    c.CommunicationContactID = contactID;
                                                    c.RegardingDescription = string.Join("", string.Format("{0}", fetchResp.Envelope.Subject).Take(254));
                                                    d.Communications.AddObject(c);
                                                }

                                                //Add all recipients
                                                var emails = new Dictionary<Guid?, string>();
                                                emails[contactID] = fromAddress;
                                                if (fetchResp.Envelope.To != null)
                                                foreach (Mail_t_Mailbox to in fetchResp.Envelope.To)
                                                    emails[_users.GetEmailContactID(to.Address, false) ?? default(Guid)] = to.Address;
                                                if (fetchResp.Envelope.Cc != null)
                                                foreach (Mail_t_Mailbox cc in fetchResp.Envelope.Cc)
                                                    emails[_users.GetEmailContactID(cc.Address, false) ?? default(Guid)] = cc.Address;
                                                foreach (var email in emails)
                                                {
                                                    if (email.Key == default(Guid?) || email.Key == default(Guid))
                                                        continue;
                                                    if (!d.CommunicationEmails.Any(
                                                        f => f.CommunicationID == c.CommunicationID
                                                            && (f.ContactID == email.Key || f.CommunicationEmail == email.Value)))
                                                    {
                                                        c.CommunicationEmailsOther.Add(new CommunicationEmails
                                                        {
                                                            CommunicationEmail = email.Value,
                                                            CommunicationID = c.CommunicationID,
                                                            ContactID = email.Key,
                                                            CommunicationEmailID = Guid.NewGuid(),
                                                        });
                                                    }
                                                }

                                                //Update Both new & old
                                                if (c.CommunicationEmail != fromAddress)
                                                    c.CommunicationEmail = fromAddress;
                                                if (c.VersionUpdatedBy != contactID)
                                                    c.VersionUpdatedBy = contactID;

                                                fetchResp.Rfc822.Stream.Position = 0;
                                                Mail_Message mime = Mail_Message.ParseFromStream(fetchResp.Rfc822.Stream);
                                                fetchResp.Rfc822.Stream.Dispose();
                                                foreach (MIME_Entity entity in mime.Attachments)
                                                {
                                                    var file = new FileData
                                                    {
                                                        FileDataID = Guid.NewGuid(),
                                                        TableType = d.GetTableName<Communication>(),
                                                        ReferenceID = c.CommunicationID,
                                                        FileTypeID = null, //TODO give type
                                                        VersionOwnerContactID = contactID,
                                                        DocumentType = ConstantsHelper.DOCUMENT_TYPE_TICKET_SUBMISSION
                                                    };
                                                    file.FileName = entity.ContentDisposition.Param_FileName;
                                                    if (file.FileName == null)
                                                        continue;
                                                    var f = entity.Body as MIME_b_SinglepartBase;
                                                    if (f == null)
                                                        continue;
                                                    file.FileBytes = f.GetDataStream().ToByteArray();
                                                    file.FileChecksum = file.FileBytes.ComputeHash();
                                                    file.MimeType = f.MediaType;
                                                    if (entity.ContentDisposition.Param_Size > -1)
                                                        file.FileLength = entity.ContentDisposition.Param_Size;
                                                    else
                                                        file.FileLength = file.FileBytes.Length;
                                                    d.FileDatas.AddObject(file);
                                                    
                                                }
                                                if (mime.BodyText != null)
                                                {
                                                    //m_pTabPageMail_MessageText.Text = mime.BodyText;
                                                    if (c.Comment != mime.BodyText)
                                                        c.Comment = mime.BodyText;
                                                }
                                                else if (mime.BodyHtmlText != null)
                                                {
                                                    if (c.Comment != mime.BodyHtmlText)
                                                        c.Comment = mime.BodyHtmlText;
                                                }
                                                body = mime.BodyHtmlText ?? mime.BodyText;
                                               
                                                //currentItem.Text = from;
                                                //currentItem.SubItems.Add(fetchResp.Envelope.Subject != null ? fetchResp.Envelope.Subject : "<none>");
                                                //currentItem.SubItems.Add(fetchResp.InternalDate.Date.ToString("dd.MM.yyyy HH:mm"));
                                                //currentItem.SubItems.Add(((decimal)(fetchResp.Rfc822Size.Size / (decimal)1000)).ToString("f2") + " kb");
                                                //m_pTabPageMail_Messages.Items.Add(currentItem);

                                                d.SaveChanges();
                                                //Now fill email 'BCC/TO' list
                                                recipients = d.CommunicationEmails.Where(f => f.CommunicationID == id && f.VersionDeletedBy == null && f.Version == 0).Select(f => f.CommunicationEmail)
                                                    .Union(new string[] { c.CommunicationEmail }).ToArray();

                                                newFrom = string.Format("{0}{1}", c.CommunicationID, ConstantsHelper.MailSuffix);

                                            }

                                            _mailApi.ProcessApiRequestAsync(MailApiCall.AliasAdd, ConstantsHelper.MailUserEmail, newFrom); //Should happen in ~45 secs
                                            _users.EmailUsersAsync(recipients.ToArray(), "Updated Support Ticket", string.Format("{0}<br/>^^^<br/>{1}", body, ConstantsHelper.EMAIL_FOOTER), false, false, newFrom, ConstantsHelper.EMAIL_FROM_NAME, true);

                                        }
                                        //send
                                        /* NOTE: In IMAP message deleting is 2 step operation.
                                         *  1) You need to mark message deleted, by setting "Deleted" flag.
                                         *  2) You need to call Expunge command to force server to dele messages physically.
                                         */
                                        deleted.Add(sequence_set);
                                    }                                   

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
                foreach (var mov in moved)
                    mail.MoveMessages(true, mov, ConstantsHelper.EMAIL_FOLDER_SUPPORT, false);
                foreach (var del in deleted)
                    mail.StoreMessageFlags(true, del, IMAP_Flags_SetType.Add, new IMAP_t_MsgFlags(new string[] { IMAP_t_MsgFlags.Deleted }));
                mail.Expunge();
            }
            catch (Exception x)
            {

            }
        }

        public void CheckMailAsync()
        {
            try
            {
                _concurrentTasks.ExecuteAsyncTask(CheckMail, null); //TODO Application specific update, include appdata through contentitem
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, string.Format("Failed to check mail.\n\n"));
            }

        }



        private void CheckMail(ContentItem c)
        {
            var m = c.As<MailTicketPart>(); //TODO Application specific update, include appdata through contentitem
            CheckMail(m);
        }



    }
}
