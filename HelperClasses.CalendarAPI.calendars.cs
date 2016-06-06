using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.GData.Extensions;
using Google.GData.Calendar;
using Google.GData.Client;
using System.Configuration;
using System.Xml;
using System.IO;
using System.Web;



namespace Trumba_Service.HelperClasses
{


    class CalendarAPI
    {
        //todo move to config file
        public static List<calendars> GetCalendars()
        {

            XmlDocument xmlDoc = new XmlDocument();

           const string removeString = "\\bin";
            string str_directory = System.IO.Directory.GetParent(Environment.CurrentDirectory.ToString()).ToString();
            string top_director = str_directory.Remove(str_directory.IndexOf(removeString),removeString.Length);
            string xmlPath = (top_director + "\\App_Data\\CalendarURLs.xml");

            xmlDoc.Load(xmlPath);
            XmlNodeList nodeList = xmlDoc.DocumentElement.SelectNodes("Calendar"); 
            List<calendars> iCal = new List<calendars>();    
            foreach(XmlNode node in nodeList)
            {
                calendars c = new calendars();
                c.name = node.SelectSingleNode("name").InnerText;
                c.url = node.SelectSingleNode("url").InnerText;
                c.delta = node.SelectSingleNode("delta").InnerText;
                c.webname = node.SelectSingleNode("webname").InnerText;

                iCal.Add(c);
            }                      
            return iCal;
        }

        public class calendars
        {
            public string name { get; set; }
            public string url { get; set; }
            public string delta { get; set; }
            public string webname { get; set; }
        }

    }
}
