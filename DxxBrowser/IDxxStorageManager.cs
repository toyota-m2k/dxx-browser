using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser
{
    public interface IDxxStorageManager
    {
        bool IsDownloaded(string url);

        Task<bool> Download(string url);

        Task<string> GetSavedFile(string url);
    }
}
