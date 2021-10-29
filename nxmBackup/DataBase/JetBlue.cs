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
                Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.Recovery, 0, "Off");

                //init DB
                Api.JetInit(ref instance);

                //begin session
                Api.JetBeginSession(instance, out sesid, null, null);

                //attach DB file in read only mode
                Api.JetAttachDatabase(sesid, this.dbPath, AttachDatabaseGrbit.ReadOnly);

                //open DB file, then we are ready
                Api.JetOpenDatabase(sesid, this.dbPath, null, out this.dbid, OpenDatabaseGrbit.ReadOnly);

                return true;
            }catch(Exception ex)
            {
                DBQueries.addLog(ex.Message, Environment.StackTrace);
                return false;
            }
        }

        public List<string> getTables()
        {
            try
            {
                IEnumerable<string> tablesEnum = Api.GetTableNames(this.sesid, this.dbid);

                //convert to string list
                return tablesEnum.ToList();

            }catch(Exception ex)
            {
                DBQueries.addLog(ex.Message, Environment.StackTrace);
                return null;
            }
        }

        //closes the DB connection
        public void closeDB()
        {
            Api.JetCloseDatabase(sesid, this.dbid, CloseDatabaseGrbit.None);
            Api.JetDetachDatabase(sesid, this.dbPath);
            Api.JetEndSession(sesid, EndSessionGrbit.None);
            Api.JetTerm(instance);
        }
    }
}
