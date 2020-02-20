using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DBConnection : IDisposable
    {
        private string server = ".\\SQLEXPRESS";
        private string database = "nxmBackup";
        private string user = "nxm";
        private string password = "test123";

        private SqlConnection connection;

        public DBConnection()
        {
            //start SQL Server connection
            //build connection string
            string connectionString = $"Server={this.server};Database={this.database};User Id={this.user};Password={this.password};";
            this.connection = new SqlConnection(connectionString);

            //open DB connection
            connection.Open();
        }

        //opens a transaction
        public SqlTransaction beginTransaction()
        {
            return connection.BeginTransaction();
        }

        //commits a transaction
        public void commitTransaction(SqlTransaction transaction)
        {
            transaction.Commit();
        }

        //performs a rollback for the given transaction
        public void rollbackTransaction(SqlTransaction transaction)
        {
            transaction.Rollback();
        }

        //sends a sql query
        public List<Dictionary<string, string>> doReadQuery(string query, Dictionary<string, string> parameters, SqlTransaction transaction)
        {
            SqlCommand command;

            if (transaction == null)
            {
                //query without transaction
                command = new SqlCommand(query, connection);
            }
            else
            {
                //query within transaction
                command = new SqlCommand(query, connection, transaction);
            }

            //add all query parameters
            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    command.Parameters.AddWithValue(key, parameters[key]);
                }
            }
                                
                
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
            }
            return result;
        }

        // Do operation.
        public int doWriteQuery(string query, Dictionary<string, string> parameters, SqlTransaction transaction)
        {
            SqlCommand command;

            if (transaction == null)
            {
                //query without transaction
                command = new SqlCommand(query, connection);
            }
            else
            {
                //query within transaction
                command = new SqlCommand(query, connection, transaction);
            }

            //add all query parameters
            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    command.Parameters.AddWithValue(key, parameters[key]);
                }
            }

            return command.ExecuteNonQuery();
        }

        //closes the db connection
        public void Dispose()
        {
            this.connection.Close();
            this.connection.Dispose();
        }
    }
}
