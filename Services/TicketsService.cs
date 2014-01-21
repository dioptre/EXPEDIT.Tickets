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
      

        public string GetRedirect(string routeURL)
        {
            try
            {
                var application = _users.ApplicationID;
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new XODBC(_users.ApplicationConnectionString, null, false);
                    var route = (from o in d.ApplicationRoutes where o.ApplicationID == application && o.RouteURL == routeURL orderby o.Sequence descending select o).FirstOrDefault();
                    var routeTable = d.GetTableName(route.GetType());
                    if (route.IsCapturingStatistic.HasValue && route.IsCapturingStatistic.Value)
                    {
                        var stat = (from o in d.StatisticDatas
                                    where o.ReferenceID == route.ApplicationRouteID && o.TableType == routeTable
                                    && o.StatisticDataName == ConstantsHelper.STAT_NAME_ROUTES
                                    select o).FirstOrDefault();
                        if (stat == null)
                        {
                            stat = new StatisticData { StatisticDataID = Guid.NewGuid(), TableType = routeTable, ReferenceID = route.ApplicationRouteID, StatisticDataName = ConstantsHelper.STAT_NAME_ROUTES, Count = 0 };
                            d.StatisticDatas.AddObject(stat);
                        }
                        stat.Count++;
                        d.SaveChanges();
                    }
                    if (route.IsExternal.HasValue && route.IsExternal.Value)
                        return route.RedirectURL;
                    else
                        return VirtualPathUtility.ToAbsolute(route.RedirectURL);
                }
            }
            catch
            {
                return null;
            }
        }

        public FileData GetDownload(string downloadID, string requestIPAddress)
        {
            try
            {        
                var application = _users.ApplicationID;
                var contact = _users.ContactID;                
                var company = _users.ApplicationCompanyID;
                var server = _users.ServerID;                
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new XODBC(_users.ApplicationConnectionString, null, false);
                    var download = (from o in d.Downloads where o.DownloadID==new Guid(downloadID) select o).First();
                    var downloadTable = d.GetTableName(download.GetType());
                    if (download.FilterApplicationID.HasValue && download.FilterApplicationID.Value != application)
                        throw new OrchardSecurityException(T("Application {0} not permitted to download {1}", application, downloadID));
                    if (download.FilterContactID.HasValue && download.FilterContactID.Value != contact)
                        throw new OrchardSecurityException(T("Contact {0} not permitted to download {1}", contact, downloadID));
                    if (download.FilterCompanyID.HasValue && download.FilterCompanyID.Value != company)
                        throw new OrchardSecurityException(T("Company {0} not permitted to download {1}", company, downloadID));
                    if (download.FilterServerID.HasValue && download.FilterServerID.Value != company)
                        throw new OrchardSecurityException(T("Server {0} not permitted to upload {1}", server, downloadID));
                    if (!string.IsNullOrWhiteSpace(download.FilterClientIP) && string.Format("{0}", requestIPAddress).Trim().ToUpperInvariant() != download.FilterClientIP.Trim().ToUpperInvariant())
                        throw new OrchardSecurityException(T("IP {0} not permitted to download {1}", requestIPAddress, downloadID));
                    if (download.RemainingDownloads < 1)
                        throw new OrchardSecurityException(T("No more remaining downloads for {0}.", downloadID));
                    if (download.ValidFrom.HasValue && download.ValidFrom.Value > DateTime.Now)
                        throw new OrchardSecurityException(T("Download {0} not yet valid.", downloadID));
                    if (download.ValidUntil.HasValue && download.ValidUntil.Value < DateTime.Now)
                        throw new OrchardSecurityException(T("Download {0} no longer valid.", downloadID));
                    var stat = (from o in d.StatisticDatas
                                where o.ReferenceID == download.DownloadID && o.TableType == downloadTable
                                && o.StatisticDataName == ConstantsHelper.STAT_NAME_DOWNLOADS
                                select o).FirstOrDefault();
                    if (stat == null)
                    {
                        stat = new StatisticData { StatisticDataID = Guid.NewGuid(), TableType = downloadTable, ReferenceID = download.DownloadID, StatisticDataName = ConstantsHelper.STAT_NAME_DOWNLOADS, Count = 0 };
                        d.StatisticDatas.AddObject(stat);
                    }
                    stat.Count++;
                    FileData file;
                    if (!string.IsNullOrWhiteSpace(download.FileChecksum))
                        file = d.FileDatas.First(f => f.VersionAntecedentID == download.FileDataID && f.FileChecksum == download.FileChecksum); //version aware download
                    else
                        file = d.FileDatas.First(f => f.FileDataID == download.FileDataID);
                    if (file != null)
                        download.RemainingDownloads--;
                    d.SaveChanges();
                    return file;
                }
            }
            catch
            {
                return null;
            }
        }

        public FileData GetFile(Guid fileDataID)
        {
            try
            {
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    var d = new XODBC(_users.ApplicationConnectionString, null, false);
                    var table = d.GetTableName(typeof(FileData));
                    var root = (from o in d.FileDatas where o.FileDataID == fileDataID && o.Version == 0 && o.VersionDeletedBy == null select new { o.VersionAntecedentID, o.VersionOwnerCompanyID, o.VersionOwnerContactID }).FirstOrDefault(); 
                    var verified = false;
                    if (root == null)
                        return null;
                    else if (!root.VersionOwnerCompanyID.HasValue && !root.VersionOwnerContactID.HasValue)
                        verified = true;
                    else if (root.VersionAntecedentID.HasValue)
                        verified = _users.CheckPermission(new SecuredBasic
                        {
                            AccessorApplicationID = _users.ApplicationID,
                            AccessorContactID = _users.ContactID,
                            OwnerReferenceID = root.VersionAntecedentID.Value,
                            OwnerTableType = table
                        }, XODB.Models.ActionPermission.Read);
                    else
                        verified = _users.CheckPermission(new SecuredBasic
                        {
                            AccessorApplicationID = _users.ApplicationID,
                            AccessorContactID = _users.ContactID,
                            OwnerReferenceID = fileDataID,
                            OwnerTableType = table
                        }, XODB.Models.ActionPermission.Read);
                    if (!verified)
                        throw new AuthorityException(string.Format("Can not download file: {0} Unauthorised access by contact: {1}", fileDataID, _users.ContactID));                  
                    var stat = (from o in d.StatisticDatas
                                where o.ReferenceID == fileDataID && o.TableType == table
                                && o.StatisticDataName == ConstantsHelper.STAT_NAME_DOWNLOADS
                                select o).FirstOrDefault();
                    if (stat == null)
                    {
                        stat = new StatisticData { StatisticDataID = Guid.NewGuid(), TableType = table, ReferenceID = fileDataID, StatisticDataName = ConstantsHelper.STAT_NAME_DOWNLOADS, Count = 0 };
                        d.StatisticDatas.AddObject(stat);
                    }
                    stat.Count++;
                    d.SaveChanges();
                    var file = (from o in d.FileDatas where o.FileDataID == fileDataID && o.Version == 0 && o.VersionDeletedBy == null select o).Single(); 
                    return file;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns search within XODB. Only search products for now.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="supplierModelID"></param>
        /// <param name="startRowIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public IEnumerable<IHtmlString> GetSearchResults(string text = null, Guid? supplierModelID = null, int? startRowIndex = null, int? pageSize = null)
        {
            var contact = _users.ContactID;
            var application = _users.ApplicationID;
            var directory = _media.GetPublicUrl(@"EXPEDIT.Transactions");
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                var d = new XODBC(_users.ApplicationConnectionString, null);
                var verified = new System.Data.Objects.ObjectParameter("verified", typeof(int));
                var found = new System.Data.Objects.ObjectParameter("found", typeof(int));
                return (from o in d.E_SP_GetSecuredSearch(text, contact, application, null, startRowIndex, pageSize, verified, found)
                        select GetSearchResultShape(new TicketsViewModel
                        {
                            ReferenceID = o.ReferenceID,
                            TableType = o.TableType,
                            Title = o.Title,
                            Description = o.Description,
                            Sequence = o.Row,
                            Total = o.TotalRows,
                            UrlInternal = o.InternalURL
                        })
                       ).ToArray();
            }
        }


        [Shape]
        public IHtmlString GetSearchResultShape(TicketsViewModel model)
        {
            return new HtmlString(string.Format("<a href='/Tickets/go/{0}'><h2>{1}</h2>{2}</a>", model.UrlInternal, model.Title, model.Description)); //TODO:Extend search different object types
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
                mail.Connect("imap.gmail.com", 993, true);
                mail.Login("staff@miningappstore.com", "=652ymQ*");
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

                                    /* NOTE: In IMAP message deleting is 2 step operation.
                                     *  1) You need to mark message deleted, by setting "Deleted" flag.
                                     *  2) You need to call Expunge command to force server to dele messages physically.
                                     */

                                    IMAP_t_SeqSet sequence_set = IMAP_t_SeqSet.Parse(fetchResp.UID.UID.ToString());
                                    mail.StoreMessageFlags(true, sequence_set, IMAP_Flags_SetType.Add, new IMAP_t_MsgFlags(new string[] { IMAP_t_MsgFlags.Deleted }));
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
