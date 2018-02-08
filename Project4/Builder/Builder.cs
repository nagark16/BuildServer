/////////////////////////////////////////////////////////////////////
// Builder.cs - Controls child builder                             //
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
 * This module will act as a mother builder by creating child builders
 * and forward build requests to child builders coming from repo.
 *  
 * Build Process:
 * ---------------
 * - Required files: IMessagePassingCommService  IMessagePassingCommService  TestUtilites
 * - Compiler command: csc IMessagePassingCommService.cs  IMessagePassingCommService.cs  TestUtilites.cs Builder.cs
 * 
 * This package has 2 classes
 * - Builder class implements following public methods
 *   --------------------------------------------------
 *      start : Starts Builder by creating communication channel
 * - ChildInfo class has following properties, which are used to contain information about ChildBuilder
 *   --------------------------------------------------
 *      childId -- Child id for easy identification
        rcvrPort -- Receiver port number of Child Builder
        sndrPort -- Sender port number of Child Builder
        process -- Process via which we create Child Builders
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
using System.Threading;
using System.Diagnostics;
using System.IO;
using SWTools;

namespace MessagePassingComm
{
    public class Builder
    {
        private int childBuilderCount { get; set; }
        private List<ChildInfo> childBuilders { get; set; }
        private Comm commChannel = null;
        private Dictionary<int, ChildInfo> childDic { get; set; }
        private static BlockingQueue<ChildInfo> childBuilderQ { get; set; } = null;
        private static BlockingQueue<CommMessage> buildRequestQ { get; set; } = null;
        private int pordId { get; set; } 
        private int i { get; set; }

        public Builder()
        {
            initializeEnvironment();
            if (childBuilderQ == null)
                childBuilderQ = new SWTools.BlockingQueue<ChildInfo>();
            if (buildRequestQ == null)
                buildRequestQ = new SWTools.BlockingQueue<CommMessage>();
            if (childDic == null)
                childDic = new Dictionary<int, ChildInfo>();
            pordId = 49160;
            childDic = new Dictionary<int, ChildInfo>();
            childBuilders = new List<ChildInfo>();
        }
        /*----< Initializing environment struct based on builder environement >---*/
        void initializeEnvironment()
        {
            Environment.verbose = BuilderEnvironment.verbose;
        }
        /*----< Starting Builder as a thread >---*/
        public bool start()
        {
            try
            {
                createCommIfNeeded();
                Thread rcvThrd = new Thread(rcvThreadProc);
                rcvThrd.IsBackground = true;
                rcvThrd.Start();
                TestUtilities.putLine(string.Format("Builder receiver started with thread id  {0}", rcvThrd.ManagedThreadId.ToString()));
                Thread sndThrd = new Thread(sndThreadProc);
                sndThrd.IsBackground = true;
                sndThrd.Start();
                TestUtilities.putLine(string.Format("Builder sender started with thread id {0}", sndThrd.ManagedThreadId.ToString()));
                Thread.Sleep(1000);
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n  -- {0}", ex.Message);
                return false;
            }
        }
        /*----< Create a channel for Builder on specific ports>---*/
        void createCommIfNeeded()
        {
            TestUtilities.putLine(string.Format("Creating communicatioon channel for Builder"));
            try
            {
                if (commChannel == null)
                {
                    commChannel = new Comm(BuilderEnvironment.address, BuilderEnvironment.port);
                }
            }
            catch (Exception ex)
            {
                Console.Write("\n-- {0}", ex.Message);
                GC.SuppressFinalize(this);
                System.Diagnostics.Process.GetCurrentProcess().Close();
            }
        }
        /*----< Process incoming messages based on command type>---*/
        private void processMessage(CommMessage msg)
        {
            string from;
            if (msg.command == "ready")
            {
                from = msg.from;
                from = from.Substring(from.IndexOf(":") + 1);
                from = from.Substring(from.IndexOf(":") + 1);
                from = from.Substring(0, from.IndexOf("/"));
                TestUtilities.putLine(string.Format("Received ready message from Child Builder running at {0} port", from));
                childBuilderQ.enQ(childDic[Int32.Parse(from)]);
            }
            else if (msg.command == "buildRequest")
            {
                TestUtilities.putLine(string.Format("Enqueuing incoming build request to queue"));
                buildRequestQ.enQ(msg);
            }
            else if (msg.command == "quit")
            {
                TestUtilities.putLine(string.Format("Quitting child process in builder"));
                closeChildProcess();
                TestUtilities.putLine(string.Format("Quitting the Mother Builder"));
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeSender);
                commChannel.postMessage(csndMsg);
                Process.GetCurrentProcess().Close();
            }
            else if (msg.command == "createChildBuilders")
            {
                childBuilderCount = Int32.Parse(msg.arguments[0]);
                TestUtilities.putLine(string.Format("Creating {0} Child Builders by creating process", childBuilderCount));
                startChildBuilders(childBuilderCount);
            }
        }
        /*----< Method to be run on receiver end for builder thread>---*/
        void rcvThreadProc()
        {
            while (true)
            {
                try
                {
                    TestUtilities.putLine(string.Format("Builder waiting for new message"));
                    CommMessage msg = commChannel.getMessage();
                    TestUtilities.putLine(string.Format("\n Builder received following message"));
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
        /*----< Method to be run on sender end for builder thread>---*/
        void sndThreadProc()
        {
            while (true)
            {
                try
                {
                    CommMessage msg = buildRequestQ.deQ();
                    ChildInfo childBuilder = childBuilderQ.deQ();
                    TestUtilities.putLine(string.Format("Sending {0} build request to Child Builder running at {1} port", msg.projectName, childBuilder.rcvrPort));
                    forwardBuildRequest(msg, childBuilder);
                }
                catch
                {
                    break;
                }
            }
        }
        /*----< Method to forward build request to child builder>---*/
        private void forwardBuildRequest(CommMessage msg, ChildInfo childBuilder)
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = msg.command;
            csndMsg.author = msg.author;
            csndMsg.projectName = msg.projectName;
            csndMsg.to = BuilderEnvironment.address + ":" + childBuilder.rcvrPort + "/IMessagePassingComm";
            csndMsg.from = msg.from;
            csndMsg.arguments = msg.arguments;
            commChannel.postMessage(csndMsg);
        }
        /*----< Method to create arguments and start specific number of child process>---*/
        private void startChildBuilders(int count)
        {
            ChildInfo childInfo;
            string arguments;
            for (i = 0; i < count; i++)
            {
                TestUtilities.putLine(string.Format("Creating {0} child process", i));
                childInfo = new ChildInfo();
                childInfo.childId = i;
                childInfo.rcvrPort = pordId;
                childBuilders.Add(childInfo);
                pordId++;
                arguments = i.ToString();
                arguments += " " + childInfo.rcvrPort.ToString();
                arguments += " " + BuilderEnvironment.port.ToString();
                childInfo.process = createChildProcess(arguments);
                childDic.Add(childInfo.rcvrPort, childInfo);

            }
        }
        /*----< Method to spawn child builder process>---*/
        private Process createChildProcess(string commandLine)
        {
            Process process = new Process();
            string dir = Directory.GetCurrentDirectory();
            string fileName;
            if (dir.Contains("Debug"))
                fileName = "..\\..\\..\\ChildBuilder\\bin\\debug\\ChildBuilder.exe";
            else
                fileName = "ChildBuilder\\bin\\debug\\ChildBuilder.exe";
            string absPath = Path.GetFullPath(fileName);
            Process proc = null;
            try
            {
                proc = Process.Start(fileName, commandLine);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            return proc;
        }
        /*----< Method to be kill child builder process>---*/
        private void closeChildProcess()
        {
            Console.WriteLine("childBuilders.Count {0}", childBuilders.Count);
            foreach (var item in childBuilders)
            {
                try
                {
                    CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeReceiver);
                    csndMsg.command = "quit";
                    csndMsg.author = "Naga";
                    csndMsg.from = BuilderEnvironment.endPoint;
                    csndMsg.to = BuilderEnvironment.address +":" +item.rcvrPort+"/IMessagePassingComm";
                    commChannel.postMessage(csndMsg);
                }
                catch (Exception e)
                {
                    Console.WriteLine("closing process exception {0}", e.Message);
                }
            }
            Thread.Sleep(1000);
        }
    }
    /*----< Class which contian Child Builders related properties>---*/
    public class ChildInfo
    {
        public int childId { get; set; }
        public int rcvrPort { get; set; }
        public Process process { get; set; }
    }
    //Main program which receives trigger from other process to start Builder
    class Program
    {
        static void Main(string[] args)
        {
            BuilderEnvironment.verbose = true;
            Builder builder = new Builder();
            builder.start();
        }
    }
}
