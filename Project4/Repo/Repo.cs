/////////////////////////////////////////////////////////////////////
// ToolChainMgr.cs - does Test activities of federation servers    //
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
 * This module will send build request to Builder. On receving request from 
 * ChildBuilder it will send respective project files.
 *  
 * Build Process:
 * ---------------
 * - Required files: IMessagePassingCommService  IMessagePassingCommService  TestUtilites
 * - Compiler command: csc IMessagePassingCommService.cs  IMessagePassingCommService.cs  TestUtilites.cs Repo.cs
 * 
 * Public Interface:
 * ----------------
 * Repo class implements following public methods
 *  start : Starts Repo by creating communication channel
 *    
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 5th Dec 2017
 * - first release
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace MessagePassingComm
{
    public class Repo
    {
        private Comm commChannel = null;
        private string receiverPort { get; set; }
        private FileSystemMgr fileSystemMgr { get; set; }
        private List<string> files { get; set; } = new List<string>();
        private Boolean devMode { get; set; } = true;
        private Boolean closeThread { get; set; } = false;
        private string fileName { get; set; }

        public Repo()
        {
            fileSystemMgr = new FileSystemMgr();
            string dir = Directory.GetCurrentDirectory();
            if (dir.Contains("Debug"))
                devMode = false;
            if (devMode)
                fileSystemMgr.createDirectory(RepoEnvironment.devModeRoot);
            else
                fileSystemMgr.createDirectory(RepoEnvironment.root);
            initializeEnvironment();
        }
        /*----< Initializing environment struct based on repo environement >---*/
        void initializeEnvironment()
        {
            Console.WriteLine("devMode {0}", devMode);
            if(devMode)
                Environment.root = RepoEnvironment.devModeRoot;
            else
                Environment.root = RepoEnvironment.root;
            Console.WriteLine("Environment.root {0}", Environment.root);
            Environment.address = RepoEnvironment.address;
            Environment.port = RepoEnvironment.port;
            Environment.endPoint = RepoEnvironment.endPoint;
            Environment.verbose = RepoEnvironment.verbose;
        }
        /*----< Starting Repo as a thread >---*/
        public bool start()
        {
            try
            {
                createCommIfNeeded();
                Thread rcvThrd = new Thread(threadProc);
                rcvThrd.IsBackground = true;
                rcvThrd.Start();
                TestUtilities.putLine(string.Format("Repo started with thread id {0}", rcvThrd.ManagedThreadId.ToString()));
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n  -- {0}", ex.Message);
                return false;
            }
        }
        /*----< Create a channel for repo on specific ports>---*/
        void createCommIfNeeded()
        {
            TestUtilities.putLine(string.Format("Creating communicatioon channel for Repo"));
            try
            {
                if (commChannel == null)
                {
                    commChannel = new Comm(RepoEnvironment.address, RepoEnvironment.port);
                }
            }
            catch (Exception ex)
            {
                Console.Write("\n-- {0}", ex.Message);
                System.Diagnostics.Process.GetCurrentProcess().Close();
            }
        }
        /*----< Method to be run on repo thread>---*/
        void threadProc()
        {
            while (true)
            {
                try
                {
                    if (closeThread)
                    {
                        TestUtilities.putLine("Repo thread quitting");
                        break;
                    }
                    TestUtilities.putLine(string.Format("Repo waiting for new message"));
                    CommMessage msg = commChannel.getMessage();
                    TestUtilities.putLine(string.Format("\n Repo received following message"));
                    TestUtilities.putLine(string.Format("======================================"));
                    msg.show();
                    processMessage(msg);
                }
                catch
                {
                    break;
                }
            }
        }
        /*Helper function to send build request to child builder*/
        private void transferBuildRequest(CommMessage msg)
        {
            Thread.Sleep(1000);
            commChannel.postFile(getRepoDirPath() + "buildRequests/" + msg.arguments[0] + "_buildRequest.xml", msg.location + "/" + msg.arguments[0] + "_buildRequest.xml", msg.from);
            Thread.Sleep(2000);
            Console.WriteLine("Build request transfer complete");
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "buildRequestCopied";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = msg.arguments;
            csndMsg.location = msg.location;
            commChannel.postMessage(csndMsg);
        }
        /*Helper function to send acknowledgment message to child builder*/
        private void buildLog(CommMessage msg)
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "buildLogReceived";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = msg.arguments;
            commChannel.postMessage(csndMsg);
        }
        /*Helper function to quit repo and send quit message to Client*/
        private void initiateQuit(CommMessage msg)
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeReceiver);
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.command = "quit";
            commChannel.postMessage(csndMsg);
            TestUtilities.putLine(string.Format("Quitting the repo"));
            closeThread = true;
            CommMessage csndMsg2 = new CommMessage(CommMessage.MessageType.closeSender);
            commChannel.postMessage(csndMsg2);
            Process.GetCurrentProcess().Close();
        }
        /*Helper function to acknowledge Client with top files*/
        private void getTopFiles(CommMessage msg)
        {
            fileSystemMgr.currentPath = "";
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "getTopFiles";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = fileSystemMgr.getFiles().ToList<string>();
            commChannel.postMessage(csndMsg);
        }
        /*Helper function to acknowledge Client with top directories*/
        private void getTopDirs(CommMessage msg)
        {
            fileSystemMgr.currentPath = "";
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "getTopDirs";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = fileSystemMgr.getDirs().ToList<string>();
            commChannel.postMessage(csndMsg);
        }
        /*Helper function to acknowledge Client with files in a folder*/
        private void moveIntoFolderFiles(CommMessage msg)
        {
            if (msg.arguments.Count() == 1)
                fileSystemMgr.currentPath = msg.arguments[0];
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "moveIntoFolderFiles";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = fileSystemMgr.getFiles().ToList<string>();
            commChannel.postMessage(csndMsg);
        }
        /*Helper function to acknowledge Client with folders in a folder*/
        private void moveIntoFolderDirs(CommMessage msg)
        {
            if (msg.arguments.Count() == 1)
                fileSystemMgr.currentPath = msg.arguments[0];
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "moveIntoFolderDirs";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = fileSystemMgr.getDirs().ToList<string>();
            commChannel.postMessage(csndMsg);
        }
        /*----< Process incoming messages based on command type>---*/
        private void processMessage(CommMessage msg)
        {
            if (msg.command == "TransferBuildRequest")
            {
                transferBuildRequest(msg);
            }
            else if (msg.command == "TransferFiles")
            {
                copySourceCodeFiles(msg);
            }
            else if(msg.command == "buildLogCopied")
            {
                buildLog(msg);
            }
            else if (msg.command == "quit")
            {
                initiateQuit(msg);
            }
            else if (msg.command == "initiateBuild")
            {
                TestUtilities.putLine(string.Format("Initiating build processby sending build request message"));
                sendMessage(msg);
            }
            else if (msg.command == "getTopFiles")
            {
                getTopFiles(msg);
            }
            else if (msg.command == "getTopDirs")
            {
                getTopDirs(msg);
            }
            else if (msg.command == "moveIntoFolderFiles")
            {
                moveIntoFolderFiles(msg);
            }
            else if (msg.command == "moveIntoFolderDirs")
            {
                moveIntoFolderDirs(msg);
            }
        }
        /*----< Method to copy files to builder environment>---*/
        private void copySourceCodeFiles(CommMessage msg)
        {
            TestUtilities.putLine(string.Format("About to transfer following files"));
            List<string> projectNames = msg.arguments;
            foreach(string projectName in projectNames)
            {
                getFiles(getRepoDirPath() + projectName);
                Thread.Sleep(1000);
                foreach (string file in files)
                {
                    TestUtilities.putLine(string.Format("\n\t{0}", file));
                    fileName = file.Substring(file.LastIndexOf("\\"));
                    if (fileName.Contains("java"))
                    {
                        string location = msg.location;
                        location = location.Substring(0,location.LastIndexOf("/"));
                        location = location.Substring(0, location.LastIndexOf("/"));
                        commChannel.postFile(file, location +"/"+ projectName+"/" + fileName, msg.from);
                    }
                    else
                        commChannel.postFile(file, msg.location + projectName + fileName, msg.from);
                }
            }
                
            TestUtilities.putLine(string.Format("\nFile transfer complete. Now, sending message to Child builder about transfer complete"));
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "TransferComplete";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = msg.arguments;
            csndMsg.location = getRepoDirPath();
            csndMsg.show();
            commChannel.postMessage(csndMsg);
        }
        /*----< Method to get list of files on specific path>---*/
        private void getFiles(string path)
        {
            files = new List<string>();
            string[] tempFiles = Directory.GetFiles(path);
            for (int i = 0; i < tempFiles.Count(); ++i)
            {
                tempFiles[i] = Path.GetFullPath(tempFiles[i]);
            }
            files.AddRange(tempFiles);
        }
        /*----< Method to send build request message to builder>---*/
        public void sendMessage(CommMessage msg)
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "buildRequest";
            csndMsg.author = "Naga";
            csndMsg.arguments = msg.arguments;
            csndMsg.to = BuilderEnvironment.endPoint;
            csndMsg.from = RepoEnvironment.endPoint;
            csndMsg.show();
            commChannel.postMessage(csndMsg);
        }
        /*----< get dir path based on dev mode >---*/
        private string getRepoDirPath()
        {
            if (devMode)
                return RepoEnvironment.devModeRoot;
            else
                return RepoEnvironment.root;
        }
    }
    //Main program which receives trigger from other process to start Repo
    class Program
    {
        static void Main(string[] args)
        {
            RepoEnvironment.verbose = true;
            Repo repo = new Repo();
            repo.start();
        }
    }
}
