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
 * This module will decide build tool based on input file types.
 *  
 * * Build Process:
 * ---------------
 * - Required files:   NIL
 * - Compiler command: csc ToolChainMgr.cs
 * 
 * Public Interface:
 * ----------------
 * class ToolChainMgr{}: 
 * 1. decideToolChain(): This function will return build tool
 *    based on input file types
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

namespace MessagePassingComm
{
    public class ToolChainMgr
    {
        private int csFileCount { get; set; }
        private int javaFileCount { get; set; }

        /*-----< Decide which build tool to use based on file types>------------- */
        public string decideToolChain(List<string> files)
        {
            csFileCount = 0;
            javaFileCount = 0;
            foreach (string file in files)
            {
                if (file.Contains(".cs"))
                    csFileCount++;
                else if (file.Contains(".java"))
                    javaFileCount++;
            }
            if (csFileCount > 0 && javaFileCount == 0)
                return "csc";
            else if (csFileCount == 0 && javaFileCount > 0)
                return "java";
            return "";
        }
    }

    class Program
    {
#if (TEST_TOOLCHAINMGR)
        static void Main(string[] args)
        {
            List<string> files = new List<string>();
            ToolChainMgr toolChainMgr = new ToolChainMgr();
            files.Add("abc.cs");
            Console.WriteLine(toolChainMgr.decideToolChain(files));
            files = new List<string>();
            files.Add("abc.java");
            Console.WriteLine(toolChainMgr.decideToolChain(files));
        }
#endif
    }
}
