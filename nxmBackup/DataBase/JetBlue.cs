using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class JetBlue
    {
        private JET_INSTANCE instance = JET_INSTANCE.Nil;
        private JET_SESID sesid;
        private JET_DBID dbid;

        private string dbPath;

        public JetBlue(string path)
        {
            this.dbPath = path;
        }

        //initializes and opens the db connection
        public bool openDB()
        {
            try
            {
                //read page size from db
                int pageSize = 0;
                Api.JetGetDatabaseFileInfo(this.dbPath, out pageSize, JET_DbInfo.PageSize);

                //set pagesize
                Microsoft.Isam.Esent.Interop.SystemParameters.DatabasePageSize = pageSize;

                //create instance
                Api.JetCreateInstance(out this.instance, "nxminstance");


                //disable recovery mode
                Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.Recovery, 0, "OFF");

                

                //init DB
                Api.JetInit(ref instance);

                //begin session
                Api.JetBeginSession(instance, out sesid, null, null);

              

                //attach DB file in read only mode
                Api.JetAttachDatabase(sesid, this.dbPath, AttachDatabaseGrbit.ReadOnly);

                //open DB file, then we are ready
                Api.JetOpenDatabase(sesid, this.dbPath, null, out this.dbid, OpenDatabaseGrbit.ReadOnly);

                return true;
            }
            catch (EsentDatabaseDirtyShutdownException)
            {
                //dirty shutdown detected, recover                
                recoverFromDirtyShutdown();

                //try opening DB again
                return openDB();
                
            }
            catch (Exception ex)
            {
                DBQueries.addLog(ex.Message, Environment.StackTrace);
                return false;
            }
        }

        //recovers a DB in dirty-shutdown mode
        private void recoverFromDirtyShutdown()
        {
            //read page size from db
            int pageSize = 0;
            Api.JetGetDatabaseFileInfo(this.dbPath, out pageSize, JET_DbInfo.PageSize);

            //close session
            Api.JetEndSession(sesid, EndSessionGrbit.None);

            //close instance
            Api.JetTerm2(instance, TermGrbit.Complete);

            //create instance
            Api.JetCreateInstance(out this.instance, "nxminstance_recovery");

            //disable recovery mode
            Api.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.Recovery, 0, "ON");

            string logPath = System.IO.Directory.GetParent(this.dbPath).FullName + "\\";
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.LogFilePath, 0, logPath);
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.TempPath, 0, logPath);
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.SystemPath, 0, logPath);

            //init DB
            Api.JetInit(ref instance);

            //begin session
            Api.JetBeginSession(instance, out sesid, null, null);
            
            

            //do recovery
            Api.JetAttachDatabase(sesid, this.dbPath, AttachDatabaseGrbit.DeleteCorruptIndexes);

            //detach DB
            Api.JetDetachDatabase(sesid, this.dbPath);

            //close session
            Api.JetEndSession(sesid, EndSessionGrbit.None);

            //close instance
            Api.JetTerm(instance);
        }



        public List<string> getTables()
        {
            try
            {
                IEnumerable<string> tablesEnum = Api.GetTableNames(this.sesid, this.dbid);

                //convert to string list
                return tablesEnum.ToList();

            }
            catch (Exception ex)
            {
                DBQueries.addLog(ex.Message, Environment.StackTrace);
                return null;
            }
        }

        //gets the content from a given table
        public JBTable getTable(string name)
        {
            JET_TABLEID table;

            //open table
            Api.OpenTable(this.sesid, this.dbid, name, OpenTableGrbit.ReadOnly, out table);

            //read columns
            IEnumerable<ColumnInfo> columnsEnumerator = Api.GetTableColumns(this.sesid, table);

            List<ColumnInfo> columns = columnsEnumerator.ToList();


            JBTable parsedTable = new JBTable();
            parsedTable.rows = new List<Dictionary<string, byte[]>>();

            bool rowAvailable;

            //jump to first column
            rowAvailable = Api.TryMoveFirst(this.sesid, table);

            //iterate through all rows
            while (rowAvailable)
            {
                //read all columns
                Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
                foreach (ColumnInfo columnInfo in columns)
                {
                    byte[] readValue = Api.RetrieveColumn(this.sesid, table, columnInfo.Columnid, RetrieveColumnGrbit.None, null);
                    dict.Add(columnInfo.Name, readValue);
                }
                parsedTable.rows.Add(dict);
                rowAvailable= Api.TryMoveNext(this.sesid, table);
            }


            //close table
            Api.JetCloseTable(this.sesid, table);
            return parsedTable;
        }

        //closes the DB connection
        public void closeDB()
        {
            Api.JetCloseDatabase(sesid, this.dbid, CloseDatabaseGrbit.None);
            Api.JetDetachDatabase(sesid, this.dbPath);
            Api.JetEndSession(sesid, EndSessionGrbit.None);
            Api.JetTerm(instance);
        }


        public struct JBTable
        {
            public List<Dictionary<string, byte[]>> rows;
        }

    }
}
