using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LinearPro_.IO
{
    internal sealed class FileService
    {
        public string ChooseInputFile()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Text files|*.txt;*.lp;*.dat|All files|*.*";
                ofd.Title = "Select LP/IP Input File";
                return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
            }
        }

        public string ReadAllText(string path, Action<int> onProgress)
        {
            var fi = new FileInfo(path);
            long total = fi.Length == 0 ? 1 : fi.Length;
            long read = 0;

            var sb = new StringBuilder();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    sb.AppendLine(line);
                    read += line.Length + Environment.NewLine.Length;
                    int pct = (int)(100.0 * read / total);
                    onProgress?.Invoke(pct);
                }
            }
            onProgress?.Invoke(100);
            return sb.ToString();
        }
    }
}
