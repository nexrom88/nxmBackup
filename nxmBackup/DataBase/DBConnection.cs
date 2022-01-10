using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace Common
{
    public class DBConnection : IDisposable
    {
        private string server = "localhost";
        private string database = "nxmBackup";
        private string user = "nxm";
        private string password = "test123";

        public bool ConnectionEstablished { set; get; }

        private NpgsqlConnection connection;

        public DBConnection()
        {
            //start SQL Server connection
            //build connection string
            string connectionString = $"Server={this.server};Database={this.database};User Id={this.user};Password={this.password};";
            this.connection = new NpgsqlConnection(connectionString);

            try
            {
                //open DB connection
                connection.Open();
            }catch(Exception ex)
            {
                ConnectionEstablished = false;
                return;
            }

            ConnectionEstablished = true;
        }

        //opens a transaction
        public NpgsqlTransaction beginTransaction()
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
        public List<Dictionary<string, object>> doReadQuery(string query, Dictionary<string, object> parameters, NpgsqlTransaction transaction)
        {
            try
            {


                NpgsqlCommand command;

                if (transaction == null)
                {
                    //query without transaction
                    command = new NpgsqlCommand(query, connection);
                }
                else
                {
                    //query within transaction
                    command = new NpgsqlCommand(query, connection, transaction);
                }

                //add all query parameters
                if (parameters != null)
                {
                    foreach (string key in parameters.Keys)
                    {
                        command.Parameters.AddWithValue(key, parameters[key]);
                    }
                }


                NpgsqlDataReader reader = command.ExecuteReader();

                //retVal is a list of dictionaries
                List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();

                try
                {
                    //iterate through all results
                    while (reader.HasRows && reader.Read())
                    {
                        Dictionary<string, object> resultRow = new Dictionary<string, object>();
                        //read all columns
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            resultRow.Add(columnName, reader[columnName]);
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
            }catch(Exception ex)
            {
                Common.DBQueries.addLog("error on performing DB read query", Environment.StackTrace, ex);
                return null;
            }
        }

        // Do operation.
        public int doWriteQuery(string query, Dictionary<string, object> parameters, NpgsqlTransaction transaction)
        {
            NpgsqlCommand command;

            if (transaction == null)
            {
                //query without transaction
                command = new NpgsqlCommand(query, connection);
            }
            else
            {
                //query within transaction
                command = new NpgsqlCommand(query, connection, transaction);
            }

            //add all query parameters
            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    command.Parameters.AddWithValue(key, parameters[key]);
                }
            }

            try
            {
                return command.ExecuteNonQuery();
            }catch(Exception ex)
            {
                return -1;
            }
        }

        //closes the db connection
        public void Dispose()
        {
            this.connection.Close();
            this.connection.Dispose();
        }
    }
}