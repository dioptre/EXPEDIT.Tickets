using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Net;
using System.Security.Cryptography;

using LumiSoft.Net;
using LumiSoft.Net.AUTH;
using LumiSoft.Net.IMAP.Client;

namespace OAuth
{
    class Program
    {
        static void Main(string[] args)
        {
            AUTH_Gmail_OAuth1_3leg oAuth = new AUTH_Gmail_OAuth1_3leg();
            // Create gmail access request.
            oAuth.GetRequestToken();
            // Get authorization URL, let user login to gmail and get verification code.
            System.Diagnostics.Process.Start(oAuth.GetAuthorizationUrl());
            Console.WriteLine("Enter(menu->Paste) gmail verification code:");
            // Get access token which is needed for accessing gmail API.
            oAuth.GetAccessToken(Console.ReadLine().Trim());
            
            try{
                using(IMAP_Client imap = new IMAP_Client()){
                    imap.Logger = new LumiSoft.Net.Log.Logger();
                    imap.Logger.WriteLog += delegate(object sender, LumiSoft.Net.Log.WriteLogEventArgs e){
                        Console.WriteLine("log: " + e.LogEntry.Text);                        
                    };
                    imap.Connect("imap.gmail.com",WellKnownPorts.IMAP4_SSL,true);

                    string email = oAuth.GetUserEmail();
                    imap.Authenticate(new AUTH_SASL_Client_XOAuth(email,oAuth.GetXOAuthStringForImap(email)));
                    imap.SelectFolder("inbox");

                    Console.WriteLine("\r\n\r\n----- You are connected now. Press enter for exit.");
                    Console.ReadLine();

                    return;
                }
            }
            catch(Exception x){
                Console.WriteLine(x.ToString());
            }

            Console.WriteLine("\r\n\r\n----- Press enter for exit.");
            Console.ReadLine();
        }
    }
}
