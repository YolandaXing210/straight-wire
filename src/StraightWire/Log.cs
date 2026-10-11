using System;
using System.Collections.Generic;
using Rhino;

namespace StraightWire
{
    /// <summary>
    /// Nothing here may throw inside Grasshopper's paint or message loop, so failures are caught
    /// and reported once on Rhino's command line. A repeated failure goes quiet.
    /// </summary>
    internal static class Log
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Info(string message)
        {
            try { RhinoApp.WriteLine("Straight Wire: " + message); }
            catch { }
        }

        public static void Once(string key, string message)
        {
            lock (Reported)
                if (!Reported.Add(key)) return;
            Info(message);
        }

        public static void Error(string key, Exception ex) => Once(key, key + " failed: " + ex.Message);

        /// <summary>Runs an action, swallowing and reporting anything it throws.</summary>
        public static void Guard(string key, Action action)
        {
            try { action(); }
            catch (Exception ex) { Error(key, ex); }
        }
    }
}
