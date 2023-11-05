using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace Common
{
    public class DBConnection : IDisposable
    {
        //private string server = "localhost";
        //private string database = "nxmBackup";
        //private string user = "nxm";
        //private string password = "31ACE875A263C33BD30465F8FD1008FF";

        public bool ConnectionEstablished { set; get; }

        private SQLiteConnection connection;

        private static string DBPathBase{ get; set;}

        public DBConnection (string filename)
        {

            loadDBFile(filename);
        }

        public DBConnection()
        {
            loadDBFile("nxm.db");
        }

        //loads a given sqlite db file
        private void loadDBFile(string filename)
        {
            //start SQLite Server connection

            //read base path from registry if necessary
            if (DBPathBase == null)
            {
                string basePath = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\nxmBackup", "BasePath", "");
                if (basePath == null)
                {
                    ConnectionEstablished = false;
                    return;
                }
                DBPathBase = basePath;
            }

            //build complete db file path
            string dbPath = System.IO.Path.Combine(DBPathBase, filename);

            if (!System.IO.File.Exists(dbPath))
            {
                ConnectionEstablished = false;
                return;
            }

            string connectionString = "Data Source=" + dbPath + "; foreign keys=true";
            this.connection = new SQLiteConnection(connectionString);



            try
            {
                //open DB connection
                connection.Open();

                //read db version but just if main database file
                if (filename == "nxm.db")
                {
                    List<Dictionary<string, object>> dbResult = doReadQuery("SELECT value FROM settings WHERE name='dbversion'", null, null);
                    if (dbResult.Count != 1)
                    {
                        //wrong number of results
                        ConnectionEstablished = false;
                        return;
                    }
                    else
                    {
                        if ((string)dbResult[0]["value"] != "102")
                        {
                            //wrong db version
                            ConnectionEstablished = false;
                            return;
                        }
                    }
                }
                else
                {
                    //not main database file
                    ConnectionEstablished = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                ConnectionEstablished = false;
                return;
            }

            ConnectionEstablished = true;
        }


        //opens a transaction
        public SQLiteTransaction beginTransaction()
        {
            return connection.BeginTransaction();
        }

        //commits a transaction
        public void commitTransaction(SQLiteTransaction transaction)
        {
            transaction.Commit();
        }

        //performs a rollback for the given transaction
        public void rollbackTransaction(SQLiteTransaction transaction)
        {
            transaction.Rollback();
        }

        //sends a sql query
        public List<Dictionary<string, object>> doReadQuery(string query, Dictionary<string, object> parameters, SQLiteTransaction transaction)
        {
            try
            {


                SQLiteCommand command;

                if (transaction == null)
                {
                    //query without transaction
                    command = new SQLiteCommand(query, connection);
                }
                else
                {
                    //query within transaction
                    command = new SQLiteCommand(query, connection, transaction);
                }

                //add all query parameters
                if (parameters != null)
                {
                    foreach (string key in parameters.Keys)
                    {
                        command.Parameters.AddWithValue(key, parameters[key]);
                    }
                }


                SQLiteDataReader reader = command.ExecuteReader();

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
                try
                {
                    Common.DBQueries.addLog("error on performing DB read query", Environment.StackTrace, ex);
                }
                catch (Exception ex2) { }
                return null;
            }
        }

        // Do operation.
        public int doWriteQuery(string query, Dictionary<string, object> parameters, SQLiteTransaction transaction)
        {
            SQLiteCommand command;

            if (transaction == null)
            {
                //query without transaction
                command = new SQLiteCommand(query, connection);
            }
            else
            {
                //query within transaction
                command = new SQLiteCommand(query, connection, transaction);
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

        //gets the last inserted id
        public Int64 getLastInsertedID()
        {
            return connection.LastInsertRowId;
        }

        //closes the db connection
        public void Dispose()
        {
            if (this.connection != null) {
                this.connection.Close();
                this.connection.Dispose();
                this.ConnectionEstablished = false;
            }
        }
    }
}