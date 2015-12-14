using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabelComponent
{
    class DataAdapter
    {
        public void InsertResult(int d, int v, long cputime, long gputime, int colors, int totalGputime, int iterations)
        {
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = "Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=ExAlgo;Data Source=AM-Thinkpad";
                conn.Open();
                SqlCommand insertCommand = new SqlCommand("INSERT INTO [dbo].[ExperimentalResults]([d],[v] ,[cputime] ,[gputime] ,[colors] ,[totalgpu],[iterations]) VALUES ("+d+","+v+","+cputime+","+gputime+","+colors+","+totalGputime+","+iterations+")", conn);
                insertCommand.ExecuteNonQuery();
                conn.Close();
            }
        }

       
    }
}
