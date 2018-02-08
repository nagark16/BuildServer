/////////////////////////////////////////////////////////////////////
// Environment.cs - Defines environment for the complete project   //
//                                                                 //
// Author: Naga Rama Krishna, nrchalam@syr.edu                     //
// Application: Core Build Server                                  //
// Environment: C# console                                         // 
// Platform: Lenovo T460                                           // 
// Operating System: Windows 10                                    // 
/////////////////////////////////////////////////////////////////////
/*
 * 
 * Module Operation:
 * =================
 * This module defines environment settings for the complete project via structs.
 *  
 * Build Process:
 * ---------------
 * - Required files: NIL
 * - Compiler command: csc Environment.cs
 * 
 * Public Interface:
 * ----------------
 *    NA
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 5th Dec 2017
 * - first release
 * 
 */

using System.Collections.Generic;

namespace MessagePassingComm
{
    /*Struct for environment*/
    public struct Environment
    {
        public static string root { get; set; }
        public static long blockSize { get; set; } = 1024;
        public static string endPoint { get; set; }
        public static string address { get; set; }
        public static int port { get; set; }
        public static bool verbose { get; set; }
        public static string remoteRoot { get; set; }
    }
    /*Struct for Clientenvironment*/
    public struct ClientEnvironment
    {
        public static string root { get; set; } = "../../../directories/clientTempDirectory/";
        public static string devModeRoot { get; set; } = "directories/clientTempDirectory/";
        public static long blockSize { get; set; } = 1024;
        public static string endPoint { get; set; } = "http://localhost:49152/IMessagePassingComm";
        public static string address { get; set; } = "http://localhost";
        public static int port { get; set; } = 49152;
        public static bool verbose { get; set; } = false;
        public static string remoteRoot { get; set; } 
    }
    /*Struct for RepoEnvironment*/
    public struct RepoEnvironment
    {
        public static string root { get; set; } = "../../../directories/repository/";
        public static string devModeRoot { get; set; } = "directories/repository/";
        public static long blockSize { get; set; } = 1024;
        public static string endPoint { get; set; } = "http://localhost:49154/IMessagePassingComm";
        public static string address { get; set; } = "http://localhost";
        public static int port { get; set; } = 49154;
        public static bool verbose { get; set; } = false;
        public static string remoteRoot { get; set; }
    }
    /*Struct for BuilderEnvironment*/
    public struct BuilderEnvironment
    {
        public static string root { get; set; } = "../../../directories/build/";
        public static string devModeRoot { get; set; } = "directories/build/";
        public static long blockSize { get; set; } = 1024;
        public static string endPoint { get; set; } = "http://localhost:49156/IMessagePassingComm";
        public static string address { get; set; } = "http://localhost";
        public static int port { get; set; } = 49156;
        public static bool verbose { get; set; } = false;
        public static string remoteRoot { get; set; }
    }
    /*Struct for TestHarnessEnvironment*/
    public struct TestHarnessEnvironment
    {
        public static string root { get; set; } = "../../../directories/testHarness/";
        public static string devModeRoot { get; set; } = "directories/testHarness/";
        public static long blockSize { get; set; } = 1024;
        public static string endPoint { get; set; } = "http://localhost:49158/IMessagePassingComm";
        public static string address { get; set; } = "http://localhost";
        public static int port { get; set; } = 49158;
        public static bool verbose { get; set; } = false;
        public static string remoteRoot { get; set; }
    }
    /*-----< Will be used to pass project data for XML>------------- */
    public class Project
    {
        public string projectName { get; set; } = "";
        public string dllName { get; set; } = "";
        public List<string> dependencies { get; set; } = new List<string>();
    }
}
