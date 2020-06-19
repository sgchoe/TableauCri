using System.IO;
using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauFileBytes
    {
        public TableauFileBytes(
            string path,
            byte[] bytes = null,
            string contentType = null,
            string contentDispositionName = null
        )
        {
            FilePath = path;
            Bytes = bytes;
            ContentType = contentType;
            ContentDispositionName = contentDispositionName;
        }

        public byte[] Bytes { get; set; }
        public string FilePath { get; set; }
        public string ContentType { get; set; }
        public string ContentDispositionName { get; set; }

        public string Name => Path.GetFileName(FilePath);

    }
}
