using System.Runtime.InteropServices;
using System;

namespace GetStatistics
{
    public static class SharedFoldersService
    {
        public static bool ConnectToShare(string uncPath, string username = null, string password = null)
        {
            if (!uncPath.StartsWith(@"\\"))
                throw new ArgumentException("Неверный UNC путь");

            var netResource = new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplayType.Share,
                RemoteName = uncPath
            };

            return WNetAddConnection(netResource, password, username, 0) == 0;
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection(NetResource netResource,
            string password, string username, int flags);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplayType DisplayType;
        public int Usage;
        public string LocalName;
        public string RemoteName;
        public string Comment;
        public string Provider;
    }

    public enum ResourceScope { Connected = 1, GlobalNetwork = 2 }
    public enum ResourceType { Disk = 1, Print = 2 }
    public enum ResourceDisplayType { Generic = 0, Share = 1 }
}