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

        void Download(Uri url, string description, Action<bool> onCompleted=null);

        string GetSavedFile(Uri url);
    }
}
