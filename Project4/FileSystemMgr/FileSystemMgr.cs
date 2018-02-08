/////////////////////////////////////////////////////////////////////
// FileSystemMgr.cs - handles file system related activites        //
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
 * This module will manage creation and deletion of directory. Also, it 
 * will copy any file from source destination.
 *  
 * Build Process:
 * ---------------
 * - Required files:   NIL
 * - Compiler command: csc FileSystemMgr.cs
 * 
 * Public Interface:
 * ----------------
 * class FileSystemMgr{}: 
 * 1. createDirectory(): Creates mentioned folder
 * 2. deleteDirectory(): Deletes mentioned folder
 * 3. deleteFile(): Will delete a file
 * 4. copyFile(): Will copy files from one location to another
 *    
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 5th Dec 2017
 * - first release
 * 
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MessagePassingComm
{

    public class FileSystemMgr
    {
        public string currentPath { get; set; } = "";

        /*-----< Creates a directory at specified location, if doesn't exist>------------- */
        public void createDirectory(string directoryName)
        {
            Console.WriteLine("Checking existence of {0} directory", directoryName);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
                Console.WriteLine("{0} directory not present so created it", directoryName);
            }
            else
                Console.WriteLine("{0} directory is already present", directoryName);
        }

        /*-----< Delete a directory at specified location, if it exists>------------- */
        public void deleteDirectory(string directoryName)
        {
            Console.WriteLine("Deleting {0} directory", directoryName);
            if (Directory.Exists(directoryName))
                Directory.Delete(directoryName, true);
        }

        /*-----< Delete a file at specified location, if it exist>------------- */
        public void deleteFile(string fileName)
        {
            Console.WriteLine("Deleting {0} file", fileName);
            try
            {
                if (File.Exists(fileName))
                {
                    File.SetAttributes(fileName, FileAttributes.Normal);
                    File.Delete(fileName);
                }
                else
                    Console.WriteLine("File doesnt exist");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /*-----< Copy a file from one location to another location>------------- */
        public void copyFile(string source, string destination)
        {
            if (File.Exists(source))
                File.Copy(source, destination, true);
        }
        //----< get names of all files in current directory >------------
        public IEnumerable<string> getFiles()
        {
            Console.WriteLine("currentPath {0}", currentPath);
            Console.WriteLine("Environment.root {0}", Environment.root);
            List<string> files = new List<string>();
            string path = Path.Combine(Environment.root, currentPath);
            string absPath = Path.GetFullPath(path);
            files = Directory.GetFiles(path).ToList<string>();
            for (int i = 0; i < files.Count(); ++i)
            {
                files[i] = Path.Combine(currentPath, Path.GetFileName(files[i]));
            }
            return files;
        }
        //----< get names of all subdirectories in current directory >---
        public IEnumerable<string> getDirs()
        {
            List<string> dirs = new List<string>();
            string path = Path.Combine(Environment.root, currentPath);
            dirs = Directory.GetDirectories(path).ToList<string>();
            for (int i = 0; i < dirs.Count(); ++i)
            {
                string dirName = new DirectoryInfo(dirs[i]).Name;
                dirs[i] = Path.Combine(currentPath, dirName);
            }
            return dirs;
        }
    }

    class Program
    {
#if (TEST_FILESYSTEMMGR)
        static void Main(string[] args)
        {
            FileSystemMgr fileSystemMgr = new FileSystemMgr();
            fileSystemMgr.createDirectory("../../../directories/temp");
            fileSystemMgr.deleteDirectory("../../../directories/temp");
        }
#endif
    }
}
