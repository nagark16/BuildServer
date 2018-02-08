/////////////////////////////////////////////////////////////////////////
// MainWindow.xaml.cs - Client prototype GUI for Build Server          //
//                                                                     //
// Author: Naga Rama Krishna, nrchalam@syr.edu                         //
// Application: Core Build Server                                      //
// Environment: C# console                                             // 
// Platform: Lenovo T460                                               // 
// Operating System: Windows 10                                        //
/////////////////////////////////////////////////////////////////////////
/*  
 *  Purpose:
 *    GUI for the Build Server
 *    This application creates build request, trigger build and copy build log.
 *
 * Public Interface:
 * ----------------
 * class Builder{}: 
 * 1. processMessage(): This function will receive message from various projects and 
 *    based on message type and from where it received message appropriate method will
 *    get trigger to process incoming message.
 * 
 * Build Process:
 * ---------------
 * - Required files: IMessagePassingCommService  IMessagePassingCommService  TestUtilites 
 * - Compiler command: csc IMessagePassingCommService.cs  IMessagePassingCommService.cs  TestUtilites.cs MainWindow.xaml.cs
 *
 *  Maintenance History:
 *  --------------------
 *  ver 1.0 : 5th Dec 2017
 *  - first release
 */

using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System;
using System.Windows.Input;
using System.ServiceModel;

namespace MessagePassingComm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<string, List<string>> files = new Dictionary<string, List<string>>();
        private Comm commChannel = null;
        private Builder build { get; set; }
        private Repo repo { get; set; }
        private FileSystemMgr fileSystemMgr { get; set; }
        Dictionary<string, Action<CommMessage>> messageDispatcher = new Dictionary<string, Action<CommMessage>>();
        Dictionary<string, Action<string>> xmlDisplayDispatcher = new Dictionary<string, Action<string>>();
        Thread rcvThread = null;
        Thread createXMLThread = null;
        private List<string> dependencies { get; set; }
        private string projectName { get; set; }
        private StringBuilder buildStatus { get; set; }
        private StringBuilder testStatus { get; set; }
        private Boolean devMode { get; set; } = true;
        private Boolean createXML { get; set; } = false;
        private List<string> selectedFiles { get; set; } = new List<string>();
        private string authorValue { get; set; }
        private List<Project> projects { get; set; }
        private List<Project> projectsForDemo { get; set; }

        public MainWindow()
        {
            fileSystemMgr = new FileSystemMgr();
            Environment.verbose = true;
            InitializeComponent();
            initializeMessageDispatcher();
            initializeXMLDisplayDispatcher();
            commChannel = new Comm(ClientEnvironment.address, ClientEnvironment.port);
            initializeEnvironment();
            string dir = Directory.GetCurrentDirectory();
            if (dir.Contains("Debug"))
                devMode = false;
            if (devMode)
                fileSystemMgr.createDirectory(ClientEnvironment.devModeRoot);
            else
                fileSystemMgr.createDirectory(ClientEnvironment.root);
            initializeEnvironment();
            rcvThread = new Thread(rcvThreadProc);
            rcvThread.IsBackground = true;
            rcvThread.Start();

            createXMLThread = new Thread(crateXMLThreadProc);
            createXMLThread.IsBackground = true;
            createXMLThread.Start();

            Thread createThrd = new Thread(createThreadProc);
            createThrd.IsBackground = true;
            createThrd.Start();

            ThreadCount.AppendText("3");
            AuthorName.AppendText("Naga");
            xmlInfo();
            generateXMLForDemo();

            buildStatus = new StringBuilder();
            testStatus = new StringBuilder();
            projects = new List<Project>();
        }
        //----< make Environment equivalent to ClientEnvironment >-------

        void initializeEnvironment()
        {
            if (devMode)
                Environment.root = ClientEnvironment.devModeRoot;
            else
                Environment.root = ClientEnvironment.root;
            Environment.address = ClientEnvironment.address;
            Environment.port = ClientEnvironment.port;
            Environment.endPoint = ClientEnvironment.endPoint;
        }
        //----< define how to process each message command >-------------

        void initializeMessageDispatcher()
        {
            messageDispatcher["getTopFiles"] = (CommMessage msg) =>
            {
                remoteFiles.Items.Clear();
                foreach (string file in msg.arguments)
                    remoteFiles.Items.Add(file);
            };
            messageDispatcher["getTopDirs"] = (CommMessage msg) =>
            {
                remoteDirs.Items.Clear();
                foreach (string dir in msg.arguments)
                    remoteDirs.Items.Add(dir);
            };
            messageDispatcher["moveIntoFolderFiles"] = (CommMessage msg) =>
            {
                remoteFiles.Items.Clear();
                foreach (string file in msg.arguments)
                    remoteFiles.Items.Add(file);
            };
            messageDispatcher["moveIntoFolderDirs"] = (CommMessage msg) =>
            {
                remoteDirs.Items.Clear();
                foreach (string dir in msg.arguments)
                    remoteDirs.Items.Add(dir);
            };
            messageDispatcher["buildStatus"] = (CommMessage msg) =>
            {
                foreach (string status in msg.arguments)
                {
                    buildStatus.Append(status+"\n");
                    BuildResults.Text = buildStatus.ToString();
                }
            };
            messageDispatcher["testStatus"] = (CommMessage msg) =>
            {
                foreach (string status in msg.arguments)
                {
                    testStatus.Append(status + "\n");
                    TestResults.Text = testStatus.ToString();
                }
            };
            messageDispatcher["quit"] = (CommMessage msg) =>
            {
                CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeSender);
                commChannel.postMessage(csndMsg);
                Process.GetCurrentProcess().CloseMainWindow();
            };
        }
        /*----< Method to initialize XML dispather for display>---*/
        void initializeXMLDisplayDispatcher()
        {
            xmlDisplayDispatcher["displayXML"] = (string xml) =>
            {
                XmlPreview.Text = xml;
            };
        }

        //----< define processing for GUI's receive thread >-------------

        void rcvThreadProc()
        {
            Console.Write("\n  starting client's receive thread");
            while (true)
            {
                CommMessage msg = commChannel.getMessage();
                msg.show();
                if (msg.command == null)
                    continue;
                // pass the Dispatcher's action value to the main thread for execution
                Dispatcher.Invoke(messageDispatcher[msg.command], new object[] { msg });
            }
        }
        /*----<Thread method to create build and repo>---*/
        void createThreadProc()
        {
            try
            {
                createBuildProcess();
                createRepoProcess();
                createTestHarness();
                initiateChildProcess("2");
                Thread.Sleep(1000);
                initiateBuild("AllSuccessHelloWorldJava");
                initiateBuild("BuildSucessTestFail");
                initiateBuild("BuildFail");
            }
            catch(Exception e)
            {
                Console.WriteLine("exception while closing process {0}",e.Message);
            }
        }
        /*----< Method to communication channel>---*/
        void createCommIfNeeded()
        {
            try
            {
                if (commChannel == null)
                {
                    commChannel = new Comm(ClientEnvironment.address, ClientEnvironment.port);
                }
            }
            catch (System.Exception ex)
            {
                System.Console.Write("\n-- {0}", ex.Message);
                System.Diagnostics.Process.GetCurrentProcess().Close();
            }
        }
        /*----< get repo harness dir path based on dev mode >---*/
        private string getRepoDirPath()
        {
            if (devMode)
                return RepoEnvironment.devModeRoot;
            else
                return RepoEnvironment.root;
        }
        /*----< get repo harness dir path based on dev mode >---*/
        private string getClientDirPath()
        {
            if (devMode)
                return ClientEnvironment.devModeRoot;
            else
                return ClientEnvironment.root;
        }
        /*----< Method to Quit repo, builder and child builders when Quit button is clicked>---*/
        private void QuitButton_click(object sender, RoutedEventArgs e)
        {
            Thread rcvThrd = new Thread(closeThread);
            rcvThrd.IsBackground = true;
            rcvThrd.Start();
        }
        /*----< Thread to Quit repo, builder and child builders. Used by QuitButton_click >---*/
        void closeThread()
        {
            createCommIfNeeded();
            closeRepo();
            closeTestHarness();
            closeBuilder();
        }
        /*----< Helper method to Quit builder>---*/
        void closeBuilder()
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeReceiver);
            csndMsg.command = "quit";
            csndMsg.author = "Naga";
            csndMsg.from = ClientEnvironment.endPoint;
            csndMsg.to = BuilderEnvironment.endPoint;
            commChannel.postMessage(csndMsg);
        }
        /*----< Helper method to Quit repo>---*/
        void closeRepo()
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeReceiver);
            csndMsg.command = "quit";
            csndMsg.author = "Naga";
            csndMsg.from = ClientEnvironment.endPoint;
            csndMsg.to = RepoEnvironment.endPoint;
            commChannel.postMessage(csndMsg);
        }
        /*----< Helper method to Quit repo>---*/
        void closeTestHarness()
        {
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.closeReceiver);
            csndMsg.command = "quit";
            csndMsg.author = "Naga";
            csndMsg.from = ClientEnvironment.endPoint;
            csndMsg.to = TestHarnessEnvironment.endPoint;
            commChannel.postMessage(csndMsg);
        }
        /*----< Helper method to send message to create child builders>---*/
        private void initiateChildProcess(string count)
        {
            createCommIfNeeded();
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "createChildBuilders";
            csndMsg.author = "Naga";
            csndMsg.arguments.Add(count);
            csndMsg.to = BuilderEnvironment.endPoint;
            csndMsg.from = ClientEnvironment.endPoint;
            csndMsg.show();
            commChannel.postMessage(csndMsg);
        }
        /*----< Method to add to XML for demo>---*/
        private void xmlInfo()
        {
            projectsForDemo = new List<Project>();

            Project project = new Project();
            project.projectName = "AllSuccess";
            List<string> dependencies = new List<string>();
            dependencies.Add("TestDriver.cs");
            dependencies.Add("TestedOne.cs");
            dependencies.Add("TestedTwo.cs");
            project.dependencies = dependencies;
            projectsForDemo.Add(project);

            project = new Project();
            project.projectName = "HelloWorldJava";
            dependencies = new List<string>();
            dependencies.Add("TestDriver.java");
            dependencies.Add("TestedOne.java");
            dependencies.Add("TestedTwo.java");
            project.dependencies = dependencies;
            projectsForDemo.Add(project);
        }
        /*----< Helper method to generate XML>---*/
        private void generateXML()
        {
            Console.WriteLine("generate XML is called");
            projectName = "";
            XDocument xd = new XDocument();
            XElement buildRequestElem = new XElement("buildRequest");
            xd.Add(buildRequestElem);
            XElement authorElem = new XElement("author");
            authorElem.Add("naga");
            buildRequestElem.Add(authorElem);
            XElement dateTimeElem = new XElement("dateTime");
            dateTimeElem.Add(DateTime.Now.ToString());
            buildRequestElem.Add(dateTimeElem);
            XElement projectsEle = new XElement("projects");
            foreach (Project project in projects)
            {
                XElement projectEle = new XElement("project");

                XElement projectNameEle = new XElement("projectName");
                projectNameEle.Add(project.projectName);
                projectEle.Add(projectNameEle);
                projectName+= project.projectName;

                foreach (string testFile in project.dependencies)
                {
                    XElement dependenciesElem = new XElement("dependencies");
                    dependenciesElem.Add(testFile);
                    projectEle.Add(dependenciesElem);
                }
                projectsEle.Add(projectEle);
            }
            buildRequestElem.Add(projectsEle);
            Dispatcher.Invoke(xmlDisplayDispatcher["displayXML"], new object[] { xd.ToString() });
            xd.Save(getClientDirPath() + projectName + "_buildRequest.xml");
            commChannel.postFile(getClientDirPath() + projectName + "_buildRequest.xml", getRepoDirPath() + "/buildRequests/" + projectName + "_buildRequest.xml", ClientEnvironment.endPoint);
            projects = new List<Project>();
        }
        /*----< Method to XML for demo>---*/
        private void generateXMLForDemo()
        {
            projectName = "";
            XDocument xd = new XDocument();
            XElement buildRequestElem = new XElement("buildRequest");
            xd.Add(buildRequestElem);
            XElement authorElem = new XElement("author");
            authorElem.Add("naga");
            buildRequestElem.Add(authorElem);
            XElement dateTimeElem = new XElement("dateTime");
            dateTimeElem.Add(DateTime.Now.ToString());
            buildRequestElem.Add(dateTimeElem);
            XElement projectsEle = new XElement("projects");
            foreach (Project project in projectsForDemo)
            {
                XElement projectEle = new XElement("project");

                XElement projectNameEle = new XElement("projectName");
                projectNameEle.Add(project.projectName);
                projectEle.Add(projectNameEle);
                projectName += project.projectName;

                foreach (string testFile in project.dependencies)
                {
                    XElement dependenciesElem = new XElement("dependencies");
                    dependenciesElem.Add(testFile);
                    projectEle.Add(dependenciesElem);
                }
                projectsEle.Add(projectEle);
            }
            buildRequestElem.Add(projectsEle);
            XmlPreview.Text = xd.ToString();
            //xd.Save(getClientDirPath() + projectName + "_buildRequest.xml");
            //commChannel.postFile(getClientDirPath() + projectName + "_buildRequest.xml", getRepoDirPath() + "/buildRequests/" + projectName + "_buildRequest.xml", ClientEnvironment.endPoint);
            projectsForDemo = new List<Project>();
        }
        /*----< Helper method to send message to initiate build>---*/
        private void initiateBuild(string projectName)
        {
            createCommIfNeeded();
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "initiateBuild";
            csndMsg.author = "Naga";
            csndMsg.arguments.Add(projectName);
            csndMsg.to = RepoEnvironment.endPoint;
            csndMsg.from = ClientEnvironment.endPoint;
            csndMsg.show();
            commChannel.postMessage(csndMsg);
        }
        /*----< Method to send to trigger build when Run button is clicked>---*/
        private void RunButtonClick(object sender, RoutedEventArgs e)
        {
            projectName = "";
            foreach (string s in remoteFiles.SelectedItems)
            {
                if (s.Contains("xml"))
                    projectName = s.Split('_')[0];
                if(projectName.Contains("\\"))
                    projectName = projectName.Split('\\')[1];
                initiateBuild(projectName);
                
            }
        }
        /*----< Method to generate XML when Generate XML button is clicked>---*/
        private void GenerateXMLButtonClick(object sender, RoutedEventArgs e)
        {
            authorValue = AuthorName.Text;
            foreach (string s in remoteFiles.SelectedItems)
            {
                if (s.Contains("cs") || s.Contains("java"))
                    selectedFiles.Add(s);
            }
            createXML = true;
        }
        //----< define processing for GUI's receive thread >-------------
        void crateXMLThreadProc()
        {
            Console.Write("\n  starting client's receive thread");
            while (true)
            {
                if (createXML)
                {
                    generateXML();
                    createXML = false;
                }
                Thread.Sleep(1000);
            }
        }
        /*----< Method to child builder pool when BuildPool button is clicked>---*/
        private void startBuildPoolClick(object sender, RoutedEventArgs e)
        {
            string count = ThreadCount.Text;
            initiateChildProcess(count);
        }
        /*----< Method to spawn builder>---*/
        private Process createBuildProcess()
        {
            Process process = new Process();
            string fileName = "..\\..\\..\\Builder\\bin\\debug\\Builder.exe";
            string absPath = Path.GetFullPath(fileName);
            Process proc = null;
            try
            {
                proc = Process.Start(fileName);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            return proc;
        }
        /*----< Method to spawn testHarness>---*/
        private Process createTestHarness()
        {
            Process process = new Process();
            string fileName = "..\\..\\..\\TestHarness\\bin\\debug\\TestHarness.exe";
            string absPath = Path.GetFullPath(fileName);
            Process proc = null;
            try
            {
                proc = Process.Start(fileName);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            return proc;
        }
        /*----< Method to spawn repo>---*/
        private Process createRepoProcess()
        {
            Process process = new Process();
            string fileName = "..\\..\\..\\Repo\\bin\\debug\\Repo.exe";
            string absPath = Path.GetFullPath(fileName);
            Process proc = null;
            try
            {
                proc = Process.Start(fileName);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            return proc;
        }
        //----< move to root of remote directories >---------------------
        /*
         * - sends a message to server to get files from root
         * - recv thread will create an Action<CommMessage> for the UI thread
         *   to invoke to load the remoteFiles listbox
         */
        private void RemoteTop_Click(object sender, RoutedEventArgs e)
        {
            CommMessage msg1 = new CommMessage(CommMessage.MessageType.request);
            msg1.from = ClientEnvironment.endPoint;
            msg1.to = RepoEnvironment.endPoint;
            msg1.author = "Jim Fawcett";
            msg1.command = "getTopFiles";
            msg1.arguments.Add("");
            commChannel.postMessage(msg1);
            CommMessage msg2 = msg1.clone();
            msg2.command = "getTopDirs";
            commChannel.postMessage(msg2);
        }
        //----< move to parent directory of current remote path >--------
        private void RemoteUp_Click(object sender, RoutedEventArgs e)
        {
            Project project = new Project();
            List<string> dependencies = new List<string>();
            foreach (string s in remoteFiles.SelectedItems)
            {
                projectName = s.Split('\\')[0];
                dependencies.Add(s.Split('\\')[1]);
            }
            if (projectName != null)
            {
                project.projectName = projectName;
            }
            project.dependencies = dependencies;
            projects.Add(project);
        }
        //----< download file and display source in popup window >-------

        private void remoteFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            Console.WriteLine("in mouse double click {0}", remoteFiles.SelectedValue as string);

        }
        //----< move into remote subdir and display files and subdirs >--
        /*
         * - sends messages to server to get files and dirs from folder
         * - recv thread will create Action<CommMessage>s for the UI thread
         *   to invoke to load the remoteFiles and remoteDirs listboxs
         */
        private void remoteDirs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommMessage msg1 = new CommMessage(CommMessage.MessageType.request);
            msg1.from = ClientEnvironment.endPoint;
            msg1.to = RepoEnvironment.endPoint;
            msg1.command = "moveIntoFolderFiles";
            msg1.arguments.Add(remoteDirs.SelectedValue as string);
            projectName = remoteDirs.SelectedValue as string;
            commChannel.postMessage(msg1);
            CommMessage msg2 = msg1.clone();
            msg2.command = "moveIntoFolderDirs";
            commChannel.postMessage(msg2);
            Console.WriteLine("project name in double {0}",projectName);
        }
        /*----< Method to for remote files selection>---*/
        private void remoteFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            dependencies.Add(remoteFiles.SelectedValue as string);
        }
        /*----< Method to for remote dirs selection>---*/
        private void remoteDirs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            projectName = remoteDirs.SelectedValue as string;
            dependencies = new List<string>();
        }
    }
}
