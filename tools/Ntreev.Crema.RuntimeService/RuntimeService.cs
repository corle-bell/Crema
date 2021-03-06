﻿//Released under the MIT License.
//
//Copyright (c) 2018 Ntreev Soft co., Ltd.
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
//documentation files (the "Software"), to deal in the Software without restriction, including without limitation the 
//rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit 
//persons to whom the Software is furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the 
//Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE 
//WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR 
//COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR 
//OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ntreev.Crema.Runtime.Serialization;
using Ntreev.Crema.Runtime.Generation;
using Ntreev.Crema.Data;

namespace Ntreev.Crema.RuntimeService
{
    [Export(typeof(IRuntimeService))]
    class RuntimeService : IRuntimeService
    {
        public GenerationSet GetCodeGenerationData(string address, string dataBaseName, string tags, string filterExpression, long revision)
        {
            var service = RuntimeServiceFactory.CreateServiceClient(address);
            service.Open();
            try
            {
                var result = service.GetCodeGenerationData(dataBaseName, tags, filterExpression, revision);
                result.Validate();
                return result.Value;
            }
            finally
            {
                service.Close();
            }
        }

        public SerializationSet GetDataGenerationData(string address, string dataBaseName, string tags, string filterExpression, bool isDevmode, long revision)
        {
            var service = RuntimeServiceFactory.CreateServiceClient(address);
            service.Open();
            try
            {
                var result = service.GetDataGenerationData(dataBaseName, tags, filterExpression, isDevmode, revision);
                result.Validate();
                return result.Value;
            }
            finally
            {
                service.Close();
            }
        }

        public Tuple<GenerationSet, SerializationSet> GetMetaData(string address, string dataBaseName, string tags, string filterExpression, bool isDevmode, long revision)
        {
            var service = RuntimeServiceFactory.CreateServiceClient(address);
            service.Open();
            try
            {
                var result = service.GetMetaData(dataBaseName, tags, filterExpression, isDevmode, revision);
                result.Validate();
                return new Tuple<GenerationSet, SerializationSet>(result.Value1, result.Value2);
            }
            finally
            {
                service.Close();
            }
        }

        public void ResetData(string address, string dataBaseName)
        {
            var service = RuntimeServiceFactory.CreateServiceClient(address);
            service.Open();
            try
            {
                var result = service.ResetData(dataBaseName);
                result.Validate();
            }
            finally
            {
                service.Close();
            }
        }

        public long GetRevision(string address, string dataBaseName)
        {
            var service = RuntimeServiceFactory.CreateServiceClient(address);
            service.Open();
            try
            {
                var result = service.GetRevision(dataBaseName);
                result.Validate();
                return result.Value;
            }
            finally
            {
                service.Close();
            }
        }
    }
}
