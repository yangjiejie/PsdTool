using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Editor
{
    public static class PsdUtil
    {
        public static void CreateDir(string path)
        {
            if (File.Exists(path))
            {
                return;
            }
            if (Directory.Exists(path))
            {
                return;
            }

            var father = Directory.GetParent(path);

            while (!father.Exists)
            {
                Directory.CreateDirectory(father.FullName);
                father = Directory.GetParent(father.FullName);
            }
        }
        public static string GetLinuxPath(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            return s.Replace("\\", "/");
        }

        public static string ToLinuxPath(this string s)
        {
            return GetLinuxPath(s);
        }
    }
}
