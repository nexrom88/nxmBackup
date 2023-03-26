using Common;
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
        //stores the currently loaded language strings
        private static Dictionary<string, string> currentLanguage;
        private static string currentLanguageName = String.Empty;
        private static object lockObj = new object();


        //gets a text string in the given language
        public static string getString (string name)
        {
            lock (lockObj)
            {
                //return nothing when language didn't get loaded yet
                if (currentLanguage == null)
                {
                    return String.Empty;
                }
                else
                {
                    string langString;
                    try
                    {
                        langString = currentLanguage[name];
                    }catch(Exception ex){
                        DBQueries.addLog("language string not found: " + name, Environment.StackTrace, ex);
                        langString = name;
                    }
                    return langString;
                }
            }

        }


        //inits the given language
        public static void initLanguage()
        {
            lock (lockObj)
            {
                //read language setting
                string language = DBQueries.readGlobalSetting("language");

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
                    currentLanguage = null;
                }
                else
                {
                    //iterate through each kvp
                    foreach (Dictionary<string, object> kvp in retVal)
                    {
                        //build new language kvp
                        languageStrings.Add((string)kvp["name"], (string)kvp["text"]);
                    }
                    currentLanguage = languageStrings;
                    currentLanguageName = language;

                }
            }
        }

        //gets all text strings for a given language
        public static Dictionary<string, string> getLanguage()
        {
            lock(lockObj)
            {
                return currentLanguage;
            }                
            
        }
    }
}
