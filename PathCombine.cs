using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetStatistics
{
    internal class PathCombine
    {
        public string CombinePath(string path, string TypeOfConnect, string selectedFile) 
        {
            switch (TypeOfConnect)
            {
                case "Local":
                    string resultPath = "";
                    resultPath = CombineLocalPath(path);
                    Console.WriteLine("");
                    return resultPath;
                case "SSH":
                    resultPath = CombineSSHPath(path);
                    Console.WriteLine("");
                    return resultPath;
                default:
                    resultPath = CombineLocalPath(path);
                    Console.WriteLine("");
                    return resultPath;
            }
        }

        private string CombineLocalPath(string path) 
        {
            string resultPath = "";
            return resultPath;
        }

        private string CombineSSHPath(string path)
        {
            string resultPath = "";
            return resultPath;
        }
    }
}
