/////////////////////////////////////////////////////////////////////
// ChildBuilder.cs - Does build activities                         //
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
 * This module will request repo to send cs files, then build them and send build
 * logs to repo.
 *  
 * Build Process:
 * ---------------
 * - Required files: IMessagePassingCommService  IMessagePassingCommService  TestUtilites
 * - Compiler command: csc IMessagePassingCommService.cs  IMessagePassingCommService.cs  TestUtilites.cs ChildBuilder.cs
 * 
 * Public Interface:
 * ----------------
 * ChildBuilder class implements following public methods
 *  start : Starts ChildBuilder by creating communication channel
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
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;

namespace MessagePassingComm
{
    public class ChildBuilder
    {
        private string id { get; set; }
        private string receiverPort { get; set; }
        private string motherBuilderPort { get; set; }
        private Comm commChannel = null;
        private string projectName { set; get; }
        private string location { set; get; }
        private List<string> files { get; set; } = new List<string>();
        private StringBuilder log { set; get; } = new StringBuilder();
        private XDocument doc = new XDocument();
        private List<string> projectNamesWithExtn { set; get; } = new List<string>();
        private Dictionary<string, ProjectInfo> dic = new Dictionary<string, ProjectInfo>();
        private List<string> dependenciesFiles { get; set; } = new List<string>();
        private List<string> dependencies { set; get; } = new List<string>();
        private ProjectInfo projectInfo;
        private string newDirectory { set; get; }
        private string author { set; get; }
        private FileSystemMgr fileSystemMgr { get; set; }
        private string logName { set; get; }
        private string toolChain { set; get; }
        private Boolean devMode { get; set; } = true;
        private Boolean buildStatus { get; set; } = false;
        private List<string> arguments { get; set; }
        private Boolean javaPresent { get; set; } = false;
        private string javaProjectName { get; set; }
        private string logLocation { get; set; }
        private string builderLocation { get; set; }
        
        public ChildBuilder(string childId, string rcvrPort, string mthrRcvrPort)
        {
            fileSystemMgr = new FileSystemMgr();
            id = childId;
            receiverPort = rcvrPort;
            motherBuilderPort = mthrRcvrPort;
            string dir = Directory.GetCurrentDirectory();
            if (dir.Contains("Debug"))
                devMode = false;
            if (devMode)
            {
                builderLocation = BuilderEnvironment.devModeRoot + "builder" + id;
                logLocation = BuilderEnvironment.devModeRoot + "Logs";
            }
            else
            {
                builderLocation = BuilderEnvironment.root + "builder" + id;
                logLocation = BuilderEnvironment.root + "Logs";
            }
            initializeEnvironment();
            arguments = new List<string>();
        }
        /*----< Initializing environment struct based on builder environement >---*/
        void initializeEnvironment()
        {
            if (devMode)
                Environment.root = BuilderEnvironment.devModeRoot;
            else
                Environment.root = BuilderEnvironment.root;
            Environment.address = BuilderEnvironment.address;
            Environment.port = BuilderEnvironment.port;
            Environment.endPoint = BuilderEnvironment.endPoint;
            Environment.verbose = BuilderEnvironment.verbose;
        }
        /*----< Starting ChildBuilder as a thread >---*/
        public bool start()
        {
            try
            {
                createCommIfNeeded();
                Thread rcvThrd = new Thread(threadProc);
                rcvThrd.IsBackground = true;
                rcvThrd.Start();
                TestUtilities.putLine(string.Format("Child Builder {0} started with thread id {1}", id, rcvThrd.ManagedThreadId.ToString()));
                sendReadyMessage();
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n  -- {0}", ex.Message);
                return false;
            }
        }
        /*----< Create a channel for ChildBuilder on specific ports>---*/
        void createCommIfNeeded()
        {
            TestUtilities.putLine(string.Format("Creating communicatioon channel for Child Builder running at {0} port", receiverPort));
            try
            {
                if (commChannel == null)
                {
                    commChannel = new Comm(BuilderEnvironment.address, Int32.Parse(receiverPort));
                }
            }
            catch (Exception ex)
            {
                Console.Write("\n-- {0}", ex.Message);
                GC.SuppressFinalize(this);
                System.Diagnostics.Process.GetCurrentProcess().Close();
            }
        }

        private void getBuildRequest(CommMessage msg)
        {
            fileSystemMgr.createDirectory(builderLocation);
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "TransferBuildRequest";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            projectName = msg.arguments[0];
            location = getBuildDirPath() + "builder" + id + "/";
            csndMsg.location = location;
            csndMsg.arguments = msg.arguments;
            TestUtilities.putLine(string.Format("Sending message to repo to send files for {0} project", projectName));
            csndMsg.show();
            commChannel.postMessage(csndMsg);
        }
        /*----< Process incoming messages based on command type>---*/
        private void processMessage(CommMessage msg)
        {
            if (msg.command == "buildRequest")
            {
                getBuildRequest(msg);
            }
            else if (msg.command == "buildRequestCopied")
            {
                processRepoRequest(msg);
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
                csndMsg.command = "TransferFiles";
                csndMsg.author = "Naga";
                csndMsg.to = msg.from;
                csndMsg.from = msg.to;
                csndMsg.arguments = arguments;
                csndMsg.location = msg.location;
                TestUtilities.putLine(string.Format("Sending message to repo to send files for {0} project", projectName));
                commChannel.postMessage(csndMsg);
            }
            else if (msg.command == "TransferComplete")
            {
                checkTransferFiles(msg);
            }
            else if (msg.command == "buildLogReceived")
            {
                createTestRequest();
                string dllNameWithLoc = location + "/"+projectName+"_TestRequest.xml";
                commChannel.postFile(dllNameWithLoc, getTestHarnessDirPath() + projectName  + "_TestRequest.xml", BuilderEnvironment.endPoint);
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
                csndMsg.command = "testRequestCopied";
                csndMsg.author = "Naga";
                csndMsg.to = TestHarnessEnvironment.endPoint;
                csndMsg.from = msg.to;
                csndMsg.arguments.Add(projectName);
                commChannel.postMessage(csndMsg);
            }
            else if (msg.command == "copyDLL")
            {
                copyDLLs(msg);
            }
            else if (msg.command == "quit")
            {
                TestUtilities.putLine(string.Format("Quitting the Child Builder"));
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeSender);
                commChannel.postMessage(csndMsg);
                Process.GetCurrentProcess().Close();
            }
        }
        private void copyDLLs(CommMessage msg)
        {
            string dllNameWithLoc = "";
            foreach (string dll in msg.arguments)
            {
                if (dll.Contains("Java"))
                {
                    location = location.Substring(0, location.LastIndexOf("/"));
                    location = location.Substring(0, location.LastIndexOf("/"));
                    javaProjectName = dll.Split('.')[0];
                    dllNameWithLoc = location + "/" + javaProjectName + "/" + dll;
                    javaPresent = true;
                }
                else
                    dllNameWithLoc = location + "/" + dll;
                commChannel.postFile(dllNameWithLoc, getTestHarnessDirPath() + dll, BuilderEnvironment.endPoint);
                Thread.Sleep(1000);
            }

            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.reply);
            csndMsg.command = "DLLCopied";
            csndMsg.author = "Naga";
            csndMsg.to = msg.from;
            csndMsg.from = msg.to;
            csndMsg.arguments = msg.arguments;
            commChannel.postMessage(csndMsg);
            delete();
            sendReadyMessage();
        }
        private void delete()
        {
            if (javaPresent)
            {
                string temp = getBuildDirPath();
                temp = temp.Substring(0, temp.LastIndexOf("/"));
                fileSystemMgr.deleteDirectory(temp + "/" + javaProjectName);
            }
            fileSystemMgr.deleteDirectory(getBuildDirPath()+"builder"+id);
        }
        /*-----< Process the copied source code files>------------- */
        private void checkTransferFiles(CommMessage msg)
        {
            processSourceCode(msg);
            try
            {
                Console.WriteLine("sending log file {0}", logLocation + "/" + logName);
                Thread.Sleep(1000);
                commChannel.postFile(logLocation + "/" + logName, msg.location + "/Logs/BuildLogs/" + logName, msg.from);
                TestUtilities.putLine(string.Format("Build log was written to {0} and posted to repo", logName));
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
                csndMsg.command = "buildLogCopied";
                csndMsg.author = "Naga";
                csndMsg.to = msg.from;
                csndMsg.from = msg.to;
                csndMsg.arguments = msg.arguments;
                commChannel.postMessage(csndMsg);
            }
            catch (Exception e)
            {
                Console.WriteLine("exception after build {0}", e.Message);
            }
        }

        private void sendStatusMessage(string project, bool status)
        {
            CommMessage csndMsg2 = new CommMessage(CommMessage.MessageType.request);
            csndMsg2.command = "buildStatus";
            csndMsg2.to = ClientEnvironment.endPoint;
            csndMsg2.from = BuilderEnvironment.endPoint;
            csndMsg2.arguments.Clear();
            if (status)
                csndMsg2.arguments.Add(project + " Passed");
            else
                csndMsg2.arguments.Add(project + " Failed");
            commChannel.postMessage(csndMsg2);
        }
        /*-----< Parse BuildRequest.xml and create temp directory for each project>------------- */
        private void processRepoRequest(CommMessage msg)
        {
            try
            {
                projectNamesWithExtn = new List<string>();
                arguments = new List<string>();
                dic = new Dictionary<string, ProjectInfo>();
                string[] tempFiles = Directory.GetFiles(msg.location, "*.xml");
                Console.WriteLine("Processing {0} build request\n", tempFiles[0]);
                XDocument doc = XDocument.Load(tempFiles[0]);
                string author = doc.Descendants("author").First().Value;
                var projectEles = from elements in doc.Descendants().Elements("project") select elements;
                foreach (XElement project in projectEles)
                {
                    projectName = project.Element("projectName").Value;
                    arguments.Add(projectName);
                    Console.WriteLine("{0} project has following details", projectName);
                    var testedFiles = from elements in project.Elements("dependencies") select elements;
                    dependencies = new List<string>();
                    foreach (XElement testedFile in testedFiles)
                    {
                        dependencies.Add(testedFile.Value);
                        Console.WriteLine(" Dependent file is {0}", testedFile.Value);
                    }
                    projectInfo = new ProjectInfo();
                    projectInfo.dependencies = dependencies;
                    dic.Add(projectName, projectInfo);
                    if (projectName.Contains("Java"))
                        fileSystemMgr.createDirectory(getBuildDirPath() + projectName);
                    else
                        fileSystemMgr.createDirectory(getBuildDirPath() + "builder" + id + "/" + projectName);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("exception in process repo request {0}",e.Message);
            }
            
        }
        /*-----< Creates TestRequest.xml. Saves the created xml locally using saveXml().
         *       Then it will copy to TestHarness directory via copyFile() 
         *       of FileSystemMgr class.>------------- */
        private void createTestRequest()
        {
            Console.WriteLine("Building Test request");
            doc = new XDocument();
            XElement buildRequestElem = new XElement("testRequest");
            doc.Add(buildRequestElem);
            XElement authorElem = new XElement("author");
            authorElem.Add(author);
            buildRequestElem.Add(authorElem);
            XElement dateTimeElem = new XElement("dateTime");
            dateTimeElem.Add(DateTime.Now.ToString());
            buildRequestElem.Add(dateTimeElem);
            XElement testElem = new XElement("tests");
            buildRequestElem.Add(testElem);
            foreach (string projectName in projectNamesWithExtn)
            {
                XElement testDlls = new XElement("testDll");
                testDlls.Add(projectName);
                testElem.Add(testDlls);
            }
            string fileName = projectName+"_TestRequest.xml";
            string fileSpec = System.IO.Path.Combine(location, fileName);
            fileSpec = System.IO.Path.GetFullPath(fileSpec);
            saveXml(fileSpec);
            Console.WriteLine("Test request created");
        }
        /*-----< Will save xml to a specific path, used by createAndCopyTestRequest()>------------- */
        private bool saveXml(string path)
        {
            try
            {
                doc.Save(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n--{0}--\n", ex.Message);
                return false;
            }
        }
        /*-----< Process source code to build libraries. First it checks copied files using checkFiles()
         *       Then it knows the build tool by invoking decideToolChain() of ToolChainMgr class.
         *       Based on build tool it will invoke respective methods like runCSharpCommand() and 
         *       runJavaCommand(). During this process it will maintain a log file, at the end 
         *       it will copy to Repo>------------- */
        protected void processSourceCode(CommMessage msg)
        {
            string temp = "";
            ToolChainMgr toolChainMgr = new ToolChainMgr();
            foreach (KeyValuePair<string, ProjectInfo> pair in dic)
            {
                checkFiles(pair.Key, pair.Value);
                toolChain = toolChainMgr.decideToolChain(dependenciesFiles);
                Console.WriteLine("\nStarting build of {0} project", pair.Key);
                Console.WriteLine("=================================================");
                Console.WriteLine("\tTool chain we are going to use is {0}\n", toolChain);
                log.Append("Building for " + pair.Key);
                log.Append("\n========================================");
                if (toolChain == "csc")
                {
                    buildStatus = runCSharpCommand(pair.Key);
                    temp = pair.Key+".dll";
                }
                else if (toolChain == "java")
                {
                    buildStatus = runJavaCommand(pair.Key);
                    temp = pair.Key+".jar";
                }
                if(buildStatus)
                    projectNamesWithExtn.Add(temp);
                sendStatusMessage(pair.Key ,buildStatus);
                Console.WriteLine("=================================================");
            }
            Console.WriteLine("Build is completed");
            logName = "builder" + id + "_buildLog" + DateTime.Now.ToFileTime();
            if (log.Length > 0)
                File.AppendAllText(logLocation + "/" + logName, log.ToString());
            else
                File.AppendAllText(logLocation + "/" + logName, "Build Succeded. Zero Errors. Zero Warning");
            log.Clear();
        }
        /*-----< Check copied source code against file mentioned in BuildRequest.xml
         *       Used by processSourceCode()>------------- */
        private Boolean checkFiles(string projectName, ProjectInfo fileInfo)
        {
            dependenciesFiles = new List<string>();
            string[] sourceFiles = Directory.GetFiles(location, "*.*");
            foreach (string file in fileInfo.dependencies)
                dependenciesFiles.Add(file);
            if (sourceFiles.Length == dependenciesFiles.Count)
                return true;
            return false;
        }
        /*-----< Compile java files using compileJavaFiles() and then 
         *       convert the compiled class files to jar using buildJar()
         *       Used by processSourceCode()>------------- */
        private Boolean runJavaCommand(string testName)
        {
            Console.WriteLine("\tRunning java files for {0}", testName);
            getFilePath(location , "*.java");
            Boolean compileStatus = true;
            string loc = location;
            loc = loc.Substring(0, loc.LastIndexOf("/"));
            loc = loc.Substring(0, loc.LastIndexOf("/"));
            compileStatus = compileJavaFiles(testName, loc+"/");
            if (!compileStatus)
                return false;
            return buildJar(testName, loc + "/");
        }
        /*-----<Will compile java files for given project>------------- */
        private Boolean compileJavaFiles(string testName, string loc)
        {
            Boolean status = false;
            Process process = new Process();
            try
            {
                Console.WriteLine("\tArgument for javac is {0}", loc + testName + "/*.java");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "javac";
                startInfo.Arguments = loc + testName +"/*.java";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                process.StartInfo = startInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                if (output.Length > 0)
                    log.Append("\n" + output + "\n");
                else
                    log.Append("\nJava compilation successful with zero errors and zero Warning\n");
                Console.WriteLine("\tProcess exit code of above command is {0}", process.ExitCode);
                if (process.ExitCode == 1)
                {
                    Console.WriteLine("\tCompilation failed");
                    status = false;
                }
                else
                {
                    Console.WriteLine("\tCompilation Succeeded");
                    status = true;
                }
            }
            catch (Exception ex)
            {
                Console.Write("there is an exception {0} \n\n", ex.Message);
                status = false;
            }
            finally
            {
                process.Close();
            }
            return status;
        }
        /*-----<Will build jar for given project class files>------------- */
        private Boolean buildJar(string testName, string loc)
        {
            Boolean status = false;
            Process process = new Process();
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                string prefixPackage = location;
                prefixPackage = prefixPackage.Substring(prefixPackage.IndexOf("directories"));
                prefixPackage = prefixPackage.Replace("/", ".");
                string argument = "cfe " + loc + testName +"/"+ testName + ".jar directories.build.HelloWorldJava.TestDriver " + loc + testName+ "/*.class";
                Console.WriteLine("\tArgument for jar build is {0}", argument);
                startInfo.FileName = "jar";
                startInfo.Arguments = argument;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                process.StartInfo = startInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                if (output.Length > 0)
                    log.Append("\n" + output + "\n");
                else
                    log.Append("\nJar compilation successful with zero errors and zero Warning\n");
                Console.WriteLine("\tProcess exit code of above command is {0}", process.ExitCode);
                if (process.ExitCode == 1)
                {
                    Console.WriteLine("\tJar build failed");
                    status = false;
                }
                else
                {
                    Console.WriteLine("\tJar build Succeeded");
                    status = true;
                }
            }
            catch (Exception ex)
            {
                Console.Write("there is an exception {0} \n\n", ex.Message);
                status = false;
            }
            finally
            {
                process.Close();
            }
            return status;
        }
        /*-----<Will initiate CS build by first getting all files using getFilePath(),
         *      then trigger build() to spawn a process using argument built in this funtion>------------- */
        private Boolean runCSharpCommand(string testName)
        {
            files.Clear();
            getFilePath(location + testName+"/", "*.cs");
            string fileList = "/nologo /target:library /out:" + location + testName + ".dll";
            Console.WriteLine("files.Count {0}", files.Count);
            if (files.Count == 0)
            {
                Console.Write("Build directory doesn't have any files to build\n\n");
                return false;
            }
            Console.Write("\tWe are building following files\n");
            foreach (string file in files)
            {
                Console.WriteLine("{0}",Path.GetFullPath(file));
                fileList = fileList + " " + Path.GetFullPath(file);

                //Console.Write("\t" + file + "\n");
            }
            return build(fileList);
        }
        /*-----< For given path and pattern, it will return all files in that path>------------- */
        private void getFilePath(string path, string pattern)
        {
            string[] tempFiles = Directory.GetFiles(path, pattern);
            files.AddRange(tempFiles);
            string[] dirs = Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                getFilePath(dir, pattern);
            }
        }
        /*-----< Will trigger Process for appropriate toolChain and 
         *       argument passed to it>------------- */
        private Boolean build(string argument)
        {
            Boolean status = false;
            Process process = new Process();
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = toolChain;
                startInfo.Arguments = argument;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                process.StartInfo = startInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                if (process.ExitCode == 0)
                    status = true;
                else if (process.ExitCode == 1)
                    status = false;
                Console.WriteLine("\n\tProcess exit code of building above files are {0}", process.ExitCode);
                if (output.Length > 0)
                {
                    log.Append("\n" + output + "\n");
                    Console.WriteLine("\t" + output);
                }
                else
                    log.Append("\nCS build is successful with zero errors and zero Warning\n\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\tThere is an exception {0}", ex.Message);
                status = false;
            }
            finally
            {
                process.Close();
            }
            return status;
        }
        /*----< Method to be run on repo thread>---*/
        void threadProc()
        {
            while (true)
            {
                try
                {
                    TestUtilities.putLine(string.Format("Child Builder waiting for message"));
                    CommMessage msg = commChannel.getMessage();
                    TestUtilities.putLine(string.Format("\n Child Builder received following message"));
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
        /*----< Method to send ready message to Mother builder >---*/
        private void sendReadyMessage()
        {
            TestUtilities.putLine(string.Format("Sending ready message to Mother Builder"));
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "ready";
            csndMsg.author = "Naga";
            csndMsg.to = BuilderEnvironment.address + ":" + motherBuilderPort + "/IMessagePassingComm";
            csndMsg.from = BuilderEnvironment.address + ":" + receiverPort + "/IMessagePassingComm";
            commChannel.postMessage(csndMsg);
        }
        /*----< get dir path based on dev mode >---*/
        private string getBuildDirPath()
        {
            if (devMode)
                return BuilderEnvironment.devModeRoot;
            else
                return BuilderEnvironment.root;
        }

        /*----< get test harness dir path based on dev mode >---*/
        private string getTestHarnessDirPath()
        {
            if (devMode)
                return TestHarnessEnvironment.devModeRoot;
            else
                return TestHarnessEnvironment.root;
        }
    }
    public class ProjectInfo
    {
        public string mainClass { set; get; }
        public List<string> dependencies { set; get; }
    }
    //Main program which receives trigger from other process to start Child Builder
    class Program
    {
        static void Main(string[] args)
        {
            BuilderEnvironment.verbose = true;
            if (args.Count() == 0)
            {
                TestUtilities.putLine(string.Format("\n Need two port values to initiate child builder"));
            }
            else
            {
                string[] arr = args.ToArray();
                ChildBuilder child = new ChildBuilder(arr[0], arr[1], arr[2]);
                child.start();
            }

        }
    }
}
