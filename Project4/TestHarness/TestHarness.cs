/////////////////////////////////////////////////////////////////////
// TestHarness.cs - does Test activities of federation servers     //
//                                                                 //
// Author: Naga Rama Krishna, nrchalam@syr.edu                     //
// Application: Core Build Server                                  //
// Environment: C# console                                         // 
// Platform: Lenovo T460                                           // 
// Operating System: Windows 10                                    //
// Reference:    Ammar                                             //
/////////////////////////////////////////////////////////////////////
/*
 * 
 * Module Operation:
 * =================
 * This module will parse test request xml and request Builder to copy 
 * dll into it's directory. Then it will test correctness of the built code.
 *  
 * Build Process:
 * ---------------
 * - Required files:   Environment.cs, MsgPassing.cs
 * - Compiler command: csc TestHarness.cs Environment.cs MsgPassing.cs
 * 
 * Public Interface:
 * ----------------
 * class Builder{}: 
 * 1. processMessage(): This function will receive message from various projects and 
 *    based on message type and from where it received message appropriate method will
 *    get trigger to process incoming message.
 * 2. directory(): This function will return module of this directory, where it will maintain 
 *    copy of cs and dll files
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
using System.IO;
using System.Threading;
using System.Xml.Linq;
using System.Diagnostics;
using System.Reflection;

namespace MessagePassingComm
{
    public class TestHarness
    {
        private Comm commChannel = null;
        private StringWriter _LogBuilder;
        private List<string> dllNames { get; set; }
        private string logName { get; set; }
        private string Log { get { return _LogBuilder.ToString(); } }
        private bool _Result;
        private bool Result { get { return _Result; } }
        private FileSystemMgr fileSystemMgr { get; set; }
        private Boolean devMode { get; set; } = true;
        private Boolean testStatus { get; set; } = false;
        private string projectName { get; set; }
        private List<string> deleteFiles { get; set; }

        public TestHarness()
        {
            fileSystemMgr = new FileSystemMgr();
            string dir = Directory.GetCurrentDirectory();
            if (dir.Contains("Debug"))
                devMode = false;
            if (devMode)
                fileSystemMgr.createDirectory(TestHarnessEnvironment.devModeRoot);
            else
                fileSystemMgr.createDirectory(TestHarnessEnvironment.root);
            initializeEnvironment();
            _LogBuilder = new StringWriter();
            dllNames = new List<string>();
            deleteFiles = new List<string>();
        }
        /*----< Initializing environment struct based on test harness environement >---*/
        void initializeEnvironment()
        {
            if (devMode)
                Environment.root = TestHarnessEnvironment.devModeRoot;
            else
                Environment.root = TestHarnessEnvironment.root;
            Environment.address = TestHarnessEnvironment.address;
            Environment.port = TestHarnessEnvironment.port;
            Environment.endPoint = TestHarnessEnvironment.endPoint;
            Environment.verbose = TestHarnessEnvironment.verbose;
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
                    commChannel = new Comm(TestHarnessEnvironment.address, TestHarnessEnvironment.port);
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
                    TestUtilities.putLine(string.Format("TestHarness waiting for new message"));
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
        /*----< Process incoming messages based on command type>---*/
        private void processMessage(CommMessage msg)
        {
            if (msg.command == "testRequestCopied")
            {
                processTestRequestXML(msg);
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
                csndMsg.command = "copyDLL";
                csndMsg.author = "Naga";
                csndMsg.to = msg.from;
                csndMsg.from = msg.to;
                csndMsg.arguments = dllNames;
                commChannel.postMessage(csndMsg);
            }
            if (msg.command == "DLLCopied")
            {
                processDlls(msg);
            }
            else if (msg.command == "quit")
            {
                TestUtilities.putLine(string.Format("Quitting the test harness"));
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeSender);
                commChannel.postMessage(csndMsg);
                Process.GetCurrentProcess().Close();
            }
        }
        /*-----< Parse Test Request to check what DLL/Jar to test>------------- */
        private void processTestRequestXML(CommMessage msg)
        {
            try
            {
                string[] tempFiles;
                string fileName = msg.arguments[0] + "_TestRequest.xml";
                dllNames = new List<string>();
                if (devMode)
                {
                    tempFiles = Directory.GetFiles(TestHarnessEnvironment.devModeRoot, fileName);
                    deleteFiles.Add(Path.Combine(TestHarnessEnvironment.devModeRoot, fileName));
                }
                else
                {
                    tempFiles = Directory.GetFiles(TestHarnessEnvironment.root, fileName);
                    deleteFiles.Add(Path.Combine(TestHarnessEnvironment.root, fileName));
                }
                    
                Console.WriteLine("Parsing {0} Test request", tempFiles[0]);
                XDocument doc = XDocument.Load(tempFiles[0]);
                string author = doc.Descendants("author").First().Value;
                var testElements = from elements in doc.Descendants().Elements("testDll") select elements;
                foreach (XElement testElement in testElements)
                    dllNames.Add(testElement.Value);
                fileSystemMgr.deleteFile(tempFiles[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while copying files {0} \n\n", ex.Message);
            }
        }
        /*-----< Tests copied DLL/Jar>------------- */
        private void processDlls(CommMessage msg)
        {
            try
            {
                _LogBuilder.Flush();
                dllNames = msg.arguments;
                foreach (string dll in dllNames)
                {
                    Thread.Sleep(1000);
                    _LogBuilder.WriteLine("\nTesting {0}", dll);
                    _LogBuilder.WriteLine("=========================================");
                    Console.WriteLine("\nTesting library {0}", dll);
                    Console.WriteLine("==============================================");
                    projectName = dll.Split('.')[0];
                    if (dll.Contains(".dll"))
                        testStatus = LoadAndTestDll(getTestHarnessDirPath() + dll);
                    else if (dll.Contains(".jar"))
                        testStatus = LoadAndTestJar(getTestHarnessDirPath() + dll);
                    deleteFiles.Add(getTestHarnessDirPath() + dll);
                    if (testStatus)
                        Console.WriteLine("\tTest Passed");
                    else
                        Console.WriteLine("\tTest Failed");
                    sendStatusMessage(dll, testStatus);
                    Thread.Sleep(1000);
                }
                logName = "testLog" + DateTime.Now.ToFileTime();
                File.AppendAllText(getTestHarnessDirPath() + logName, Log.ToString());
                commChannel.postFile(getTestHarnessDirPath() + logName, getRepoDirPath() + "Logs/TestLogs/" + logName, msg.from);
            }
            catch (Exception e)
            {
                Console.WriteLine("exception in proces dlls {0}",e.Message);
            }
            
        }
        /*Helper function to send test status message to Client*/
        private void sendStatusMessage(string dll, bool status)
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "testStatus";
            csndMsg.author = "Naga";
            csndMsg.to = ClientEnvironment.endPoint;
            csndMsg.from = TestHarnessEnvironment.endPoint;
            if (status)
                csndMsg.arguments.Add(dll.Split('.')[0] + " Passed");
            else
                csndMsg.arguments.Add(dll.Split('.')[0] + " Failed");
            commChannel.postMessage(csndMsg);
        }
        /*-----< Tests Jar files>------------- */
        private bool LoadAndTestJar(string Path)
        {
            Boolean status = false;
            Process process = new Process();
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "java";
                startInfo.Arguments = "-jar " + Path;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                process.StartInfo = startInfo;
                Console.WriteLine("\tStarting process to invoke {0}", Path);
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                _LogBuilder.Write(output);
                Console.WriteLine("\tProcess exit code of above file is {0}", process.ExitCode);
                if (process.ExitCode == 1)
                {
                    _LogBuilder.WriteLine("\tTest failed");
                    status = false;
                }
                else
                {
                    _LogBuilder.WriteLine("\tTest passed");
                    status = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\tThere is an exception: {0} \n\n", ex.Message);
                _LogBuilder.WriteLine("\tTest failed");
                status = false;
            }
            finally
            {
                process.Close();
            }
            return status;
        }
        /*-----< Tests DLL files>------------- */
        private Boolean LoadAndTestDll(string Path)
        {
            TextWriter _old = Console.Out;
            try
            {
                Console.WriteLine("\tLoading the assembly ... ");
                Assembly asm = Assembly.LoadFrom(Path);
                Console.WriteLine("\tSuccess \n  \tChecking Types");
                Type[] types = asm.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsClass && type.GetMethod("test") != null)
                    {
                        MethodInfo testMethod = type.GetMethod("test");
                        Console.WriteLine("\tFound '{1}' in {0}", type.ToString(), testMethod.ToString());
                        Console.WriteLine("\tInvoking Test method '{0}'", testMethod.DeclaringType.FullName + "." + testMethod.Name);
                        Console.SetOut(_LogBuilder);
                        Console.WriteLine("\nInvoking......");
                        _Result = (bool)testMethod.Invoke(Activator.CreateInstance(type), null);
                        Console.WriteLine("before checking result return code");
                        if (_Result) Console.WriteLine("\tTest Passed.");
                        else Console.WriteLine("\tTest Failed.");
                        Console.SetOut(_old);
                        break;
                    }
                }
                if (!_Result)
                    Console.WriteLine("\tCould not find 'bool Test()' in the assembly.\n  Make sure it implements ITest\n  Test failed");
                return Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\tError: {0}", ex.Message);
                Console.SetOut(_old);
                _Result = false;
                return Result;
            }
        }
        /*-----< Deletes temporary files>------------- */
        private void deleteTempFolders()
        {
            Console.WriteLine("\nDeleting temporary files after copying to Repo");
            Console.WriteLine("======================");
            fileSystemMgr.deleteFile(getTestHarnessDirPath() + logName);
            fileSystemMgr.deleteFile(getTestHarnessDirPath() + "TestRequest.xml");
        }
        /*----< get dir path based on dev mode >---*/
        private string getTestHarnessDirPath()
        {
            if (devMode)
                return TestHarnessEnvironment.devModeRoot;
            else
                return TestHarnessEnvironment.root;
        }
        /*----< get repo harness dir path based on dev mode >---*/
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
            TestHarnessEnvironment.verbose = true;
            TestHarness testHarness = new TestHarness();
            testHarness.start();
        }
    }
}
