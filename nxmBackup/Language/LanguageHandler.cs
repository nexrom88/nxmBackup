using nxmBackup.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace nxmBackup.Language
{
    public class LanguageHandler
    {
        //gets a text string in the given language 
        public static string getString (string name, string language)
        {
            Common.DBConnection connection = new Common.DBConnection("lang.db");

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "language", language },
                { "name", name }
            };

            List<Dictionary<string, object>> retVal = connection.doReadQuery("SELECT text FROM LangStrings WHERE language=@language AND name=@name", parameters, null);
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

        //gets all text strings for a given language
        public static Dictionary<string, string> getLanguage(string language)
        {
            Dictionary<string, string> languageStrings = new Dictionary<string, string>();

            Common.DBConnection connection = new Common.DBConnection("lang.db");

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "language", language }
            };

            List<Dictionary<string, object>> retVal = connection.doReadQuery("SELECT text, name FROM LangStrings WHERE language=@language", parameters, null);
            connection.Dispose();

            if (retVal == null || retVal.Count == 0)
            {
                return languageStrings;
            }
            else
            {
                //iterate through each kvp
                foreach (Dictionary<string, object> kvp in retVal)
                {
                    //build new language kvp
                    languageStrings.Add((string)kvp["name"], (string)kvp["text"]);
                }
                return languageStrings;
            }
        }
    }
}
