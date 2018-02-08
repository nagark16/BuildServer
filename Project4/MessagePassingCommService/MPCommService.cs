/////////////////////////////////////////////////////////////////////
// MPCommService.cs - service for MessagePassingComm               //
//                                                                 //
// Author: Naga Rama Krishna, nrchalam@syr.edu                     //
// Application: Remote Build Server Prototypes                     //
// Environment: C# console                                         // 
// Platform: Lenovo T460                                           // 
// Operating System: Windows 10                                    //
// References: Prof. Jim Fawcett                                   //
/////////////////////////////////////////////////////////////////////
/*
 * Started this project with C# Console Project wizard
 * - Added references to:
 *   - System.ServiceModel
 *   - System.Runtime.Serialization
 *   - System.Threading;
 *   - System.IO;
 *   
 * Package Operations:
 * -------------------
 * This package defines three classes:
 * - Sender which implements the public methods:
 *   -------------------------------------------
 *   - connect          : opens channel and attempts to connect to an endpoint, 
 *                        trying multiple times to send a connect message
 *   - close            : closes channel
 *   - postMessage      : posts to an internal thread-safe blocking queue, which
 *                        a sendThread then dequeues msg, inspects for destination,
 *                        and calls connect(address, port)
 *   - postFile         : attempts to upload a file in blocks
 *   - getLastError     : returns exception messages on method failure
 * - Receiver which implements the public methods:
 *   ---------------------------------------------
 *   - start            : creates instance of ServiceHost which services incoming messages
 *   - postMessage      : Sender proxies call this message to enqueue for processing
 *   - getMessage       : called by Receiver application to retrieve incoming messages
 *   - close            : closes ServiceHost
 *   - openFileForWrite : opens a file for storing incoming file blocks
 *   - writeFileBlock   : writes an incoming file block to storage
 *   - closeFile        : closes newly uploaded file
 * - Comm which implements, using Sender and Receiver instances, the public methods:
 *   -------------------------------------------------------------------------------
 *   - postMessage      : send CommMessage instance to a Receiver instance
 *   - getMessage       : retrieves a CommMessage from a Sender instance
 *   - postFile         : called by a Sender instance to transfer a file
 *    
 * The Package also implements the class TestPCommService with public methods:
 * ---------------------------------------------------------------------------
 * - testSndrRcvr()     : test instances of Sender and Receiver
 * - testComm()         : test Comm instance
 * - compareMsgs        : compare two CommMessage instances for near equality
 * - compareFileBytes   : compare two files byte by byte
 *   
 * Maintenance History:
 * --------------------
 * ver 1: 5th Dec 2017
 * - first release
 */
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.IO;

namespace MessagePassingComm
{

    ///////////////////////////////////////////////////////////////////
    // Receiver class - receives CommMessages and Files from Senders
    public class Receiver : IMessagePassingComm
    {
        public static SWTools.BlockingQueue<CommMessage> rcvQ { get; set; } = null;
        ServiceHost commHost = null;
        FileStream fs = null;
        string lastError = "";

        /*----< constructor >------------------------------------------*/

        public Receiver()
        {
            if (rcvQ == null)
                rcvQ = new SWTools.BlockingQueue<CommMessage>();
        }
        /*----< create ServiceHost listening on specified endpoint >---*/
        /*
        * baseAddress is of the form: http://IPaddress or http://networkName
        */
        public void start(string baseAddress, int port)
        {
            string address = baseAddress + ":" + port.ToString() + "/IMessagePassingComm";
            TestUtilities.putLine(string.Format("starting Receiver on thread {0}", Thread.CurrentThread.ManagedThreadId));
            createCommHost(address);
        }
        /*----< create ServiceHost listening on specified endpoint >---*/
        /*
        * address is of the form: http://IPaddress:8080/IMessagePassingComm
        */
        public void createCommHost(string address)
        {
            WSHttpBinding binding = new WSHttpBinding();
            Uri baseAddress = new Uri(address);
            commHost = new ServiceHost(typeof(Receiver), baseAddress);
            commHost.AddServiceEndpoint(typeof(IMessagePassingComm), binding, baseAddress);
            commHost.Open();
        }
        /*----< enqueue a message for transmission to a Receiver >-----*/

        public void postMessage(CommMessage msg)
        {
            msg.threadId = Thread.CurrentThread.ManagedThreadId;
            TestUtilities.putLine(string.Format("Receiver enqueuing message on thread {0}", Thread.CurrentThread.ManagedThreadId));
            rcvQ.enQ(msg);

        }
        /*----< retrieve a message sent by a Sender instance >---------*/

        public CommMessage getMessage()
        {
            CommMessage msg = rcvQ.deQ();
            if (msg.type == CommMessage.MessageType.closeReceiver)
            {
                TestUtilities.putLine("Receiver thread quitting");
                close();
            }
            return msg;
        }

        /*----< close ServiceHost >------------------------------------*/

        public void close()
        {
            Console.Out.Flush();
            commHost.Close();
        }
        /*---< called by Sender's proxy to open file on Receiver >-----*/

        public bool openFileForWrite(string name)
        {
            try
            {
                fs = File.OpenWrite(name);
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Console.WriteLine("\n Receiver exception is {0}", lastError);
                return false;
            }
        }
        /*----< write a block received from Sender instance >----------*/

        public bool writeFileBlock(byte[] block)
        {
            try
            {
                fs.Write(block, 0, block.Length);
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                return false;
            }
        }
        /*----< close Receiver's uploaded file >-----------------------*/

        public void closeFile()
        {
            fs.Close();
        }
    }

    ///////////////////////////////////////////////////////////////////
    // Sender class - sends messages and files to Receiver
    public class Sender
    {
        private IMessagePassingComm channel;
        private ChannelFactory<IMessagePassingComm> factory = null;
        private SWTools.BlockingQueue<CommMessage> sndQ = null;
        private int port = 0;
        private string fromAddress = "";
        private string toAddress = "";
        Thread sndThread = null;
        int tryCount = 0, maxCount = 10;
        string lastError = "";
        string lastUrl = "";

        /*----< constructor >------------------------------------------*/

        public Sender(string baseAddress, int listenPort)
        {
            port = listenPort;
            fromAddress = baseAddress + listenPort.ToString() + "/IMessagePassingComm";
            sndQ = new SWTools.BlockingQueue<CommMessage>();
            TestUtilities.putLine(string.Format("starting Sender on thread {0}", Thread.CurrentThread.ManagedThreadId));
            sndThread = new Thread(threadProc);
            sndThread.Start();
        }
        /*----< creates proxy with interface of remote instance >------*/

        public void createSendChannel(string address)
        {
            EndpointAddress baseAddress = new EndpointAddress(address);
            WSHttpBinding binding = new WSHttpBinding();
            factory = new ChannelFactory<IMessagePassingComm>(binding, address);
            channel = factory.CreateChannel();
        }
        /*----< attempts to connect to Receiver instance >-------------*/

        public bool connect(string baseAddress, int port)
        {
            toAddress = baseAddress + ":" + port.ToString() + "/IMessagePassingComm";
            return connect(toAddress);
        }
        /*----< attempts to connect to Receiver instance >-------------*/
        /*
        * - attempts a finite number of times to connect to a Receiver
        * - first attempt to send will throw exception of no listener
        *   at the specified endpoint
        * - to test that we attempt to send a connect message
        */
        public bool connect(string toAddress)
        {
            int timeToSleep = 500;
            createSendChannel(toAddress);
            CommMessage connectMsg = new CommMessage(CommMessage.MessageType.connect);
            while (true)
            {
                try
                {
                    tryCount = 0;
                    return true;
                }
                catch (Exception ex)
                {
                    if (++tryCount < maxCount)
                    {
                        TestUtilities.putLine("failed to connect - waiting to try again");
                        Thread.Sleep(timeToSleep);
                    }
                    else
                    {
                        TestUtilities.putLine("failed to connect - quitting");
                        lastError = ex.Message;
                        return false;
                    }
                }
            }
        }
        /*----< closes Sender's proxy >--------------------------------*/

        public void close()
        {
            if (factory != null)
                factory.Close();
        }
        /*----< processing for receive thread >------------------------*/
        /*
        * - send thread dequeues send message and posts to channel proxy
        * - thread inspects message and routes to appropriate specified endpoint
        */
        void threadProc()
        {
            while (true)
            {
                TestUtilities.putLine(string.Format("Sender dequeuing message on thread {0}", Thread.CurrentThread.ManagedThreadId));
                CommMessage msg = sndQ.deQ();
                if (msg != null)
                {
                    if (msg.type == CommMessage.MessageType.closeSender)
                    {
                        TestUtilities.putLine("Sender send thread quitting");
                        break;
                    }
                    if (!connect(msg.to))
                        return;
                    lastUrl = msg.to;
                    channel.postMessage(msg);
                    close();
                }
                else
                {
                    TestUtilities.putLine("No new messages in queue");
                }
            }
        }
        /*----< main thread enqueues message for sending >-------------*/

        public void postMessage(CommMessage msg)
        {
            sndQ.enQ(msg);
        }
        /*----< uploads file to Receiver instance >--------------------*/

        public bool postFile(string fileName, string destination, string destinationPort)
        {
            FileStream fs = null;
            long bytesRemaining;
            try
            {
                createSendChannel(destinationPort);
                fs = File.OpenRead(fileName);
                bytesRemaining = fs.Length;
                channel.openFileForWrite(destination);
                while (true)
                {
                    long bytesToRead = Math.Min(Environment.blockSize, bytesRemaining);
                    byte[] blk = new byte[bytesToRead];
                    long numBytesRead = fs.Read(blk, 0, (int)bytesToRead);
                    bytesRemaining -= numBytesRead;

                    channel.writeFileBlock(blk);
                    if (bytesRemaining <= 0)
                        break;
                }
                channel.closeFile();
                fs.Close();
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Console.WriteLine("\n sender exception is {0}", lastError);
                return false;
            }
            return true;
        }
    }
    ///////////////////////////////////////////////////////////////////
    // Comm class combines Receiver and Sender

    public class Comm
    {
        private Receiver rcvr = null;
        private Sender sndr = null;

        /*----< constructor >------------------------------------------*/
        /*
        * - starts listener listening on specified endpoint
        */
        public Comm(string baseAddress, int rcvrPort)
        {
            rcvr = new Receiver();
            rcvr.start(baseAddress, rcvrPort);
            sndr = new Sender(baseAddress, rcvrPort);
        }
        /*----< post message to remote Comm >--------------------------*/

        public void postMessage(CommMessage msg)
        {
            sndr.postMessage(msg);
        }
        /*----< retrieve message from remote Comm >--------------------*/

        public CommMessage getMessage()
        {
            return rcvr.getMessage();
        }
        /*----< called by remote Comm to upload file >-----------------*/

        public bool postFile(string filename, string destination, string destinationPort)
        {
            return sndr.postFile(filename, destination, destinationPort);
        }
    }
    ///////////////////////////////////////////////////////////////////
    // TestPCommService class - tests Receiver, Sender, and Comm

    class TestPCommService
    {
        /*----< collect file names from client's FileStore >-----------*/

        public static List<string> getClientFileList()
        {
            List<string> names = new List<string>();
            string[] files = Directory.GetFiles(Environment.root);
            foreach (string file in files)
            {
                names.Add(Path.GetFileName(file));
            }
            return names;
        }
        /*----< compare CommMessages property by property >------------*/
        /*
        * - skips threadId property
        */
        public static bool compareMsgs(CommMessage msg1, CommMessage msg2)
        {
            bool t1 = (msg1.type == msg2.type);
            bool t2 = (msg1.to == msg2.to);
            bool t3 = (msg1.from == msg2.from);
            bool t4 = (msg1.author == msg2.author);
            bool t5 = (msg1.command == msg2.command);
            bool t7 = (msg1.errorMsg == msg2.errorMsg);
            if (msg1.arguments.Count != msg2.arguments.Count)
                return false;
            for (int i = 0; i < msg1.arguments.Count; ++i)
            {
                if (msg1.arguments[i] != msg2.arguments[i])
                    return false;
            }
            return t1 && t2 && t3 && t4 && t5 && t7;
        }
        /*----< compare binary file's bytes >--------------------------*/

        static bool compareFileBytes(string filename)
        {
            TestUtilities.putLine(string.Format("testing byte equality for \"{0}\"", filename));

            string fileSpec1 = Path.Combine(Environment.root, filename);
            string fileSpec2 = Path.Combine(Environment.remoteRoot, filename);
            try
            {
                byte[] bytes1 = File.ReadAllBytes(fileSpec1);
                byte[] bytes2 = File.ReadAllBytes(fileSpec2);
                if (bytes1.Length != bytes2.Length)
                    return false;
                for (int i = 0; i < bytes1.Length; ++i)
                {
                    if (bytes1[i] != bytes2[i])
                        return false;
                }
            }
            catch (Exception ex)
            {
                TestUtilities.putLine(string.Format("\n  {0}\n", ex.Message));
                return false;
            }
            return true;
        }

        /*----< test Comm instance >-----------------------------------*/

        public static bool testComm()
        {
            TestUtilities.vbtitle("testing Comm");
            bool test = true;
            Comm comm = new Comm("http://localhost", 8081);
            CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);
            csndMsg.command = "show";
            csndMsg.author = "Jim Fawcett";
            csndMsg.to = "http://localhost:8081/IPluggableComm";
            csndMsg.from = "http://localhost:8081/IPluggableComm";
            comm.postMessage(csndMsg);
            CommMessage crcvMsg = comm.getMessage();
            if (ClientEnvironment.verbose)
                crcvMsg.show();
            crcvMsg = comm.getMessage();
            if (ClientEnvironment.verbose)
                crcvMsg.show();
            if (!compareMsgs(csndMsg, crcvMsg))
                test = false;
            TestUtilities.checkResult(test, "csndMsg equals crcvMsg");
            bool testFileTransfer = true;
            List<string> names = getClientFileList();
            foreach (string name in names)
            {
                TestUtilities.putLine(string.Format("transferring file \"{0}\"", name));
                bool transferSuccess = comm.postFile(name, "", "");
                TestUtilities.checkResult(transferSuccess, "transfer");
            }
            foreach (string name in names)
            {
                if (!compareFileBytes(name))
                {
                    testFileTransfer = false;
                    break;
                }
            }
            TestUtilities.checkResult(testFileTransfer, "file transfers");
            csndMsg.type = CommMessage.MessageType.closeReceiver;
            comm.postMessage(csndMsg);
            crcvMsg = comm.getMessage();
            if (ClientEnvironment.verbose)
                crcvMsg.show();
            if (!compareMsgs(csndMsg, crcvMsg))
                test = false;
            TestUtilities.checkResult(test, "closeReceiver");
            csndMsg.type = CommMessage.MessageType.closeSender;
            comm.postMessage(csndMsg);
            if (ClientEnvironment.verbose)
                csndMsg.show();
            return test && testFileTransfer;
        }
        /*----< do the tests >-----------------------------------------*/

        static void Main(string[] args)
        {
            ClientEnvironment.verbose = true;
            TestUtilities.vbtitle("testing Message-Passing Communication", '=');

            TestUtilities.checkResult(testComm(), "Comm");
            TestUtilities.putLine();

            TestUtilities.putLine("Press key to quit\n");
            if (ClientEnvironment.verbose)
                Console.ReadKey();
        }
    }
}
