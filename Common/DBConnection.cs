using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DBConnection
    {
        private string server, database, user, password;

        public DBConnection(string server, string database, string user, string password)
        {
            this.server = server;
            this.database = database;
            this.user = user;
            this.password = password;
        }

        //sends a sql query
        public List<Dictionary<string, string>> singleQuery(string query, Dictionary<string, string> parameters)
        {
            //build connection string
            string connectionString = $"Server={this.server};Database={this.database};User Id={this.user};Password={this.password};";


            //start sql connection
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);

                //add all query parameters
                if (parameters != null)
                {
                    foreach (string key in parameters.Keys)
                    {
                        command.Parameters.AddWithValue(key, parameters[key]);
                    }
                }
                
                //open DB connection
                connection.Open();
                
                
                SqlDataReader reader = command.ExecuteReader();

                //retVal is a list of dictionaries
                List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();

                try
                {
                    //iterate through all results
                    while (reader.HasRows && reader.Read())
                    {
                        Dictionary<string, string> resultRow = new Dictionary<string, string>();
                        //read all columns
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            resultRow.Add(columnName, reader[columnName].ToString().Trim()); ;
                        }

                        //add dictionary to result list
                        result.Add(resultRow);

                    }
                }
                finally
                {
                    // Always call close when done reading
                    reader.Close();
                    connection.Close();
                }
                return result;
            }

        }
    }
}
