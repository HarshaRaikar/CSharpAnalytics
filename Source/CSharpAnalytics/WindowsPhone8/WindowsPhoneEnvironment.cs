﻿﻿// Copyright (c) Attack Pattern LLC.  All rights reserved.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Globalization;
using System.Windows;

namespace CSharpAnalytics.WindowsPhone8
{
    /// <summary>
    /// Implements the IEnvironment interface required by analytics to track details of the machine
    /// in a WindowsStore application.
    /// </summary>
    internal class WindowsPhone8Environment : IEnvironment
    {
        public string CharacterSet { get { return "UTF-8"; } }

        public string LanguageCode { get { return CultureInfo.CurrentCulture.ToString(); } }

        public string FlashVersion { get { return null; } }
        public bool? JavaEnabled { get { return null; } }
        public string IpAddress { get { return null; } }

        public uint ScreenColorDepth { get { return 32; } }

        public uint ScreenHeight
        {
            get { return (uint)Application.Current.Host.Content.ActualHeight; }
        }

        public uint ScreenWidth
        {
            get { return (uint)Application.Current.Host.Content.ActualWidth; }
        }

        public uint ViewportHeight
        {
            get { return ScreenHeight; }
        }

        public uint ViewportWidth
        {
            get { return ScreenWidth; }
        }
    }
}