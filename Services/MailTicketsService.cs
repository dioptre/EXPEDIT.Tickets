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


namespace EXPEDIT.Tickets.Services
{

    [UsedImplicitly]
    public class MailTicketsService : IMailTicketsService
    {
        private readonly IContentManager _contentManager;
        private readonly IScheduledTaskManager _taskManager;
        private readonly IConcurrentTaskService _concurrentTasks;
        private readonly IOrchardServices _services;
        public ILogger Logger { get; set; }

        public MailTicketsService(
            IContentManager contentManager,
            IScheduledTaskManager taskManager,
            IConcurrentTaskService concurrentTasks,
            IOrchardServices services,
            ITicketsService tickets)
        {
            _contentManager = contentManager;
            _taskManager = taskManager;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            _concurrentTasks = concurrentTasks;
            _services = services;
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

        public void CheckMail()
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
                                    if (_services.WorkContext != null)
                                        smtpSettings = _services.WorkContext.CurrentSite.As<Orchard.Email.Models.SmtpSettingsPart>();
                                    if (from.IndexOf(ConstantsHelper.MailSuffix) > -1
                                        || (smtpSettings != null && !string.IsNullOrWhiteSpace(smtpSettings.Address) && from.IndexOf(smtpSettings.Address) >= 0)
                                        )
                                    {
                                        if (!mail.GetFolders(ConstantsHelper.EMAIL_FOLDER_SUPPORT).Any())
                                            mail.CreateFolder(ConstantsHelper.EMAIL_FOLDER_SUPPORT);
                                        mail.MoveMessages(true, sequence_set, ConstantsHelper.EMAIL_FOLDER_SUPPORT, false);
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
            //var m = c.As<MailTicketPart>(); //TODO Application specific update, include appdata through contentitem
            CheckMail();
        }



    }
}
