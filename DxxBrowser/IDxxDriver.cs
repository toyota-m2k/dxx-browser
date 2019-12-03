﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DxxBrowser
{
    public interface IDxxDriver
    {
        string Name { get; }
        string ID { get; }
        bool Setup();
        bool LoadSettins(XmlElement settings);
        bool SaveSettings(XmlElement settings);

        bool IsSupported(string url);

        IDxxLinkExtractor LinkExtractor { get; }
        IDxxStorageManager StorageManager { get; }
    }
}