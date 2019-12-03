using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser
{
    public interface IDxxStorageManager
    {
        bool IsDownloaded(Uri url);

        Task<bool> Download(Uri url);

        Task<string> GetSavedFile(Uri url);
    }
}
