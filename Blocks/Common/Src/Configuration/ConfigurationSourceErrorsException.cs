﻿//===============================================================================
// Microsoft patterns & practices Enterprise Library
// Core
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Common.Properties;
using System.Runtime.Serialization;

namespace Microsoft.Practices.EnterpriseLibrary.Common.Configuration
{
    /// <summary/>
    [Serializable]
    public class ConfigurationSourceErrorsException : ConfigurationErrorsException
    {
        /// <summary/>
        public ConfigurationSourceErrorsException()
        {
        }

        /// <summary/>
        public ConfigurationSourceErrorsException(string message)
            : base(message)
        {
        }

        /// <summary/>
        public ConfigurationSourceErrorsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary/>
        public ConfigurationSourceErrorsException(string message, Exception innerException, string filename, int line)
            :base(message, innerException, filename, line)
        {
        }

        /// <summary/>
        protected ConfigurationSourceErrorsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }
    }
}
