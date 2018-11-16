using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InfluxDB.Net;
using InfluxDB.Net.Models;
using MySql.Data.MySqlClient;
using System.Configuration;

namespace GetInfluxData
{
    class Program
    {
        static DateTime starttime, endtime,uptime;
        static long seconds;
        static long s1, e1;
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        //static string InfluxHostUrl ;
        //static string GrafanaUrl ;
        static  void  Main(string[] args)
        {
            MainAsync(args).Wait();
        }
        static async Task MainAsync(string[] args)
        { 
       
            var InfluxHostUrl = ConfigurationManager.AppSettings["InfluxHostUrl"];
            var GrafanaUrl = ConfigurationManager.AppSettings["GrafanaUrl"] ;
            //set to default if emp
            if (string.IsNullOrEmpty(InfluxHostUrl)) { InfluxHostUrl = "http://localhost:8086/"; }
            if (string.IsNullOrEmpty(GrafanaUrl)) { GrafanaUrl = "http://localhost:8080/d/0000000025/glances-test-knotron?orgId=1&from={0}000&to={1}000"; }
            var _client = new InfluxDb(InfluxHostUrl, "root", "root");
            List<Serie> series1 = await _client.QueryAsync("tempglances", "select system from  \"localhost.cpu\"  order by time asc limit 1");
            var t1 =  (series1[0].Values[0][0].ToString());
            starttime = DateTime.Parse(t1);

            List<Serie> series2 = await _client.QueryAsync("tempglances", "select system from  \"localhost.cpu\"  order by time desc limit 1");
            var t2 = (series2[0].Values[0][0].ToString());
            endtime = DateTime.Parse(t2);
           
            s1 = getunixtime(starttime);
            e1 = getunixtime(endtime);
            Console.WriteLine("Capture Start Time :" + starttime);
            Console.WriteLine("In Unix format : " + s1);
            Console.WriteLine("Capture End Time :" + endtime);
            Console.WriteLine("In Unix format : " + e1);
            var grafanaurl1 = string.Format(GrafanaUrl,s1,e1);

            List<Serie> series3 = await _client.QueryAsync("tempglances", "SELECT time,seconds FROM \"localhost.uptime\"  order by time asc limit 1;");
            uptime = DateTime.Parse(series3[0].Values[0][0].ToString());
            seconds =  Convert.ToInt64(series3[0].Values[0][1].ToString());
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            string str = time.ToString(@"hh\:mm\:ss\:fff");
            Console.WriteLine("Uptime Capture Time :" + uptime);
            Console.WriteLine("System is Up for :" + str);
            DateTime dt = uptime.AddHours(-time.TotalHours);
            Console.WriteLine("System Restart at  :" + dt);
            Console.WriteLine("grafanaurl  :" + grafanaurl1);
            //Console.ReadLine();
            //Add to database
            var updateQuery = "INSERT INTO glances_capture (start_time,end_time,flight_num,grafana_url) VALUES ('{0}','{1}','{2}','{3}'); ";
            //updateQuery = string.Format(updateQuery, starttime.ToString("yyyy-mm-dd HH:mm:ss"), endtime.ToString("yyyy-mm-dd HH:mm:ss"), null, grafanaurl1);
            updateQuery = string.Format(updateQuery, String.Format("{0:s}", starttime) , String.Format("{0:s}", endtime), args[0], grafanaurl1);
            if (updateTable(updateQuery)) { Console.WriteLine("Update glances_capture table with new capture."); } else { Console.WriteLine("Error while updating glances_capture table with new capture."); }
            //Now update flight datatable.
            var updateQuery1 = "INSERT INTO flightdetails (FlightNumber,FlyDateTime,LandDateTime) VALUES ('{0}','{1}','{2}'); ";
            updateQuery1 = string.Format(updateQuery1, args[0], String.Format("{0:s}", starttime), String.Format("{0:s}", endtime) );
            if (updateTable(updateQuery1)) { Console.WriteLine("Update flightdetails table with new capture."); } else { Console.WriteLine("Error while updating flightdetails table with new capture."); }
            
        }

        public static DateTime FromUnixTime(long unixTime)
        {
            return epoch.AddSeconds(unixTime);
        }
        
        public static long getunixtime(DateTime dt) { 
            var dateTimeOffset = new DateTimeOffset(dt);
            var unixDateTime = dateTimeOffset.ToUnixTimeSeconds();
            return unixDateTime;
        }
        public static bool updateTable(string query)
        {
            bool retval = false;
            try
            {
                string MyConnection = ConfigurationManager.ConnectionStrings["AirHubConn"].ToString();
                string Query = query;
                MySqlConnection MyConn = new MySqlConnection(MyConnection);
                MySqlCommand MyCommand = new MySqlCommand(Query, MyConn);
                MySqlDataReader MyReader;
                MyConn.Open();
                MyReader = MyCommand.ExecuteReader();
                MyConn.Close();
                retval = true;
            }
            catch (Exception ex)
            {

                Console.WriteLine("Error   :" + ex.Message); 
            }
            return retval;
        }
    }
}
