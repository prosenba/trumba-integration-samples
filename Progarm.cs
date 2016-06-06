Windows Service call to pull calendar from URL, push to trumba using the Trumba Service
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.IO;
using System.Net;
using System.Xml.Linq;
using System.Net.Mail;


namespace Trumba_Service
{
    class WindowsService : ServiceBase
    {

        /// <summary>
        /// Public Constructor for WindowsService.
        /// - Put all of your Initialization code here.
        /// </summary>
        public WindowsService()
        {
            InitializeComponent();
            this.ServiceName = "Trumba Services";
            this.EventLog.Log = "Application";

            // These Flags set whether or not to handle that specific
            //  type of event. Set to true if you need it, false otherwise.
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanPauseAndContinue = true;
            this.CanShutdown = true;
            this.CanStop = true;
            this.AutoLog = true;
            GetCalendarData();


        }
        public void GetCalendarData()
        {  
            List<HelperClasses.CalendarAPI.calendars> calendarsList = HelperClasses.CalendarAPI.GetCalendars();

            for (int i = 0; i < calendarsList.Count; i++)
            {
                var calendarName = calendarsList[i].name;
                string url = calendarsList[i].url;
                string delta = calendarsList[i].delta;
                string webname = calendarsList[i].webname;

                //pull calendar into stream from URL
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(url);
                myRequest.Method = "GET";
                WebResponse myResponse = myRequest.GetResponse();
                StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                string result = sr.ReadToEnd();
                sr.Close();  
              
                var IcalData = Encoding.UTF8.GetBytes(result);


                //The delta parameter controls whether the incoming feed is considered to be a full feed of data or only changes. 
                //When the value is false, the system imports the events in the feed and deletes any events that exist in Trumba but do not exist in the feed. 
                //When the value is true, the system will only delete events if an explicit CANCEL method is specified in the iCalendar file.

                string username = ConfigurationManager.AppSettings["TRUMBA_ACCOUNT"];
                string password = ConfigurationManager.AppSettings["TRUMBA_ACCOUNT_PASSOWRD"];

                //push into Trumba Service API
                string UriStr = "https://www.trumba.com/service/" + webname + ".ics?delta=" + delta;
                CredentialCache MyCredentialCache = new CredentialCache();               
                NetworkCredential creds = new NetworkCredential(username, password, "");
                MyCredentialCache.Add(new Uri(UriStr), "Basic", creds);
                HttpWebRequest request = HttpWebRequest.Create(UriStr) as HttpWebRequest;

                request.Accept = "application/xml";
                request.UseDefaultCredentials = true;
                request.PreAuthenticate = true;
                request.Credentials = MyCredentialCache;
                request.Method = "PUT";
                request.ContentLength = IcalData.Length;
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(IcalData, 0, IcalData.Length); // file sizes > 2048 bytes may need chunking
                requestStream.Close();
                WebResponse response = null;
                int statusCode = 0;
                try
                {
                    response = request.GetResponse();
                    statusCode = Convert.ToInt32(((HttpWebResponse)response).StatusCode);
                    Stream ResponseStream = response.GetResponseStream();
                    StreamReader responseReader = new StreamReader(ResponseStream);
                    string responseString = responseReader.ReadToEnd();                   

                    XDocument doc = XDocument.Parse(responseString);
                    var code = (from f in doc.Elements().Descendants()
                                select (f.Attribute("Code").Value));

                    var descritpion = (from f in doc.Elements().Descendants()
                                       select (f.Attribute("Description").Value));

                    var level = (from f in doc.Elements().Descendants()
                                 select (f.Attribute("Level").Value));

                    var date = DateTime.Now;
                    responseString += date + @"\r\n";
                   

                    const string removeString = "\\bin";
                    string str_directory = System.IO.Directory.GetParent(Environment.CurrentDirectory.ToString()).ToString();
                    string top_director = str_directory.Remove(str_directory.IndexOf(removeString), removeString.Length);
                    string filePath = (top_director + "\\Logs\\");  

                    if (level.ToString() != "Information")
                    {                        
                        Console.Write(responseString);
                        File.AppendAllText(filePath + "log.txt", responseString);
                    }
                    else
                    {
                         File.AppendAllText(filePath + "log.txt", "ErrorCode: " + code.First() + "<br/>" + "Level: " + level.First() + "<br/>" + "Description: " + descritpion.First());
                    }

                    Console.Write(responseString);
                    ResponseStream.Close();
                    response.Close();
                }
                catch (WebException ex)
                {

                    Console.Write(ex.ToString());
                    //MailAddress mailTo = new MailAddress("prosenba@uw.edu", "Trumba Service");
                    //HelperClasses.SendErrorEmail.Emailer.SendEmail("Calendar " + webname + " Trumba Service Exception", ex.ToString(), mailTo);

                } 

            }

        }



        static void Main(string[] args)
        {
            //if (args[0] != null)
            //{
            //    if (args[0].ToString().ToLower() == "-service")
            //    {
            //        ServiceBase[] WindowsService = new ServiceBase[] { new WindowsService() };
            //        ServiceBase.Run(WindowsService);
            //        return;
            //    }
            //}
            //Run the main form if the argument isn't present, like when a user opens the app from Explorer.
            if (args.Length == 0)
            {
                 
                ServiceBase.Run(new WindowsService());
            }


        }


        /// <summary>
        /// Dispose of objects that need it here.
        /// </summary>
        /// <param name="disposing">Whether
        ///    or not disposing is going on.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// OnStart(): Put startup code here
        ///  - Start threads, get inital data, etc.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);


            //TODO: place your start code here
            //check args
        }

        /// <summary>
        /// OnStop(): Put your stop code here
        /// - Stop threads, set final data, etc.
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();
            //TODO: clean up any variables and stop any threads
        }

        /// <summary>
        /// OnPause: Put your pause code here
        /// - Pause working threads, etc.
        /// </summary>
        protected override void OnPause()
        {
            base.OnPause();
        }

        /// <summary>
        /// OnContinue(): Put your continue code here
        /// - Un-pause working threads, etc.
        /// </summary>
        protected override void OnContinue()
        {
            base.OnContinue();
        }

        /// <summary>
        /// OnShutdown(): Called when the System is shutting down
        /// - Put code here when you need special handling
        ///   of code that deals with a system shutdown, such
        ///   as saving special data before shutdown.
        /// </summary>
        protected override void OnShutdown()
        {
            base.OnShutdown();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command)
        {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        /// <summary>
        /// OnPowerEvent(): Useful for detecting power status changes,
        ///   such as going into Suspend mode or Low Battery for laptops.
        /// </summary>
        /// <param name="powerStatus">The Power Broadcast Status
        /// (BatteryLow, Suspend, etc.)</param>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            return base.OnPowerEvent(powerStatus);
        }

        /// <summary>
        /// OnSessionChange(): To handle a change event
        ///   from a Terminal Server session.
        ///   Useful if you need to determine
        ///   when a user logs in remotely or logs off,
        ///   or when someone logs into the console.
        /// </summary>
        /// <param name="changeDescription">The Session Change
        /// Event that occured.</param>
        protected override void OnSessionChange(
                  SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
        }
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            this.ServiceName = "Trumba Calendar Inporter";
            //# Author: Phillip Rosenbaum Prosenba@uw.edu
        }
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
    }
}
