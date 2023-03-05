using nxmBackup.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nxmBackup.Language
{
    public class LanguageHandler
    {
        //gets a text string in the given language 
        public static string getString (string name, string language)
        {
            Common.DBConnection connection = new Common.DBConnection("lang.db");

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("language", language);

            List<Dictionary<string, object>> retVal = connection.doReadQuery("SELECT text FROM LangStrings WHERE language=@language", parameters, null);
            connection.Dispose();

            if (retVal == null || retVal.Count == 0) //text value not found
            {
                return "text not found (" + name + ")";
            }
            else
            {
                return (string)retVal[0]["text"];
            }

        }
    }
}
