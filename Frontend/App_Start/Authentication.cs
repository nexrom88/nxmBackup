using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Frontend.App_Start
{
    public class Authentication
    {
        private static Object lockObject = new Object();
        private static List<OneSession> sessions = new List<OneSession>();
        private static UInt32 sessionTimeout = 60 * 10; // session timeout: 10 minutes

        public static List<OneSession> Sessions {
            get
            {
                cleanUp();
                return sessions;
            }
        }

        //checks if a given session id is authenticated
        public static bool isAuthenticated(string session)
        {
            cleanUp();
            lock (lockObject)
            {
                foreach (OneSession oneSession in sessions)
                {
                    if (oneSession.sessionID == session)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        //adds a given session
        public static void addSession (string sessionID)
        {
            lock (lockObject)
            {
                OneSession newSession = new OneSession();
                newSession.sessionID = sessionID;
                newSession.timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                sessions.Add(newSession);
            }
        }

        //removes a given session
        public static void removeSession(string session)
        {
            lock (lockObject)
            {
                for(int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i].sessionID == session)
                    {
                        sessions.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        //updates the timestamp of a given session
        public static void updateSession(string session)
        {
            lock (lockObject)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i].sessionID == session)
                    {
                        OneSession newSession = new OneSession();
                        newSession.timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                        newSession.sessionID = session;
                        sessions[i] = newSession;
                        return;
                    }
                }
            }
        }

        //cleans the session list
        private static void cleanUp()
        {
            lock (lockObject)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (DateTimeOffset.Now.ToUnixTimeSeconds() - sessions[i].timestamp >= sessionTimeout)
                    {
                        sessions.RemoveAt(i);
                        i--;
                    }
                }
            }
        }


    }
}