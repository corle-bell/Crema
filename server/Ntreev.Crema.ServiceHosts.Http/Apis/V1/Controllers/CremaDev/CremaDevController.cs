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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Ntreev.Crema.Runtime.Serialization;
using Ntreev.Crema.RuntimeService;
using Ntreev.Crema.ServiceHosts.Http.Apis.Infrastructures.ActionResults;
using Ntreev.Crema.ServiceHosts.Http.Apis.V1.Requests.CremaDev;
using Ntreev.Crema.ServiceHosts.Http.Apis.V1.Responses.CremaDev;
using Ntreev.Crema.Services;

namespace Ntreev.Crema.ServiceHosts.Http.Apis.V1.Controllers.CremaDev
{
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [RoutePrefix("api/v1/cremadev/databases/{databaseName}")]
    public class CremaDevController : CremaApiController
    {
        private readonly ICremaHost cremaHost;

        [ImportingConstructor]
        public CremaDevController(ICremaHost cremaHost) : base(cremaHost)
        {
            this.cremaHost = cremaHost;
        }

        [HttpGet]
        [Route("output-types")]
        public string[] GetOutputTypeList()
        {
            var serializers = this.GetDataSerializers();
            if (serializers == null || !serializers.Any()) return new string[] { };

            return serializers.Select(serializer => serializer.Name).ToArray();
        }

        [HttpPost]
        [Route("data.binary")]
        [AllowAnonymous]
        public IHttpActionResult GetDataAsBinary(string databaseName, [FromBody] DataRequest request)
        {
            var serializationSet = GetSerializationSet(databaseName, request);

            if (request.Split)
            {
                var entries = this.SerializePerTable(serializationSet, request);
                return new ZipFileResult(entries, request.ResponseFileName);
            }
            else
            {
                var stream = this.SerializeAll(serializationSet, request);
                return new FileResult(stream, request.ResponseFileName);
            }
        }

        [HttpPost]
        [Route("data.json")]
        [AllowAnonymous]
        public IHttpActionResult GetDataAsJson(string databaseName, [FromBody] DataRequest request)
        {
            var serializationSet = GetSerializationSet(databaseName, request);
            var formatter = this.RequestContext.Configuration.Formatters.JsonFormatter;

            if (request.Split)
            {
                var entries = this.SerializePerTable(serializationSet, request);
                return new EncodedFilesResult(entries, formatter);
            }
            else
            {
                var stream = this.SerializeAll(serializationSet, request);
                return new EncodedFilesResult(new[] { new ZipFileResultEntry(request.ResponseFileName, stream) }, formatter);
            }
        }

        private SerializationSet GetSerializationSet(string databaseName, DataRequest request)
        {
            var runtimeService = this.cremaHost.GetService(typeof(IRuntimeService)) as IRuntimeService;
            if (runtimeService == null) throw new NullReferenceException(nameof(runtimeService));

            var result = runtimeService.GetDataGenerationData(databaseName, request.Tags, request.FilterExpression, request.IsDevMode, request.Revision);
            result.Validate();

            var filteredMetaData = ReplaceOptionProcessor.Process(result.Value, request);
            return filteredMetaData;
        }

        //private IHttpActionResult SerializeAll(SerializationSet serializationSet, DataRequest request)
        //{
        //    var serializationSetStream = new MemoryStream();

        //    var serializer = this.GetDataSerializer(request.OutputType);
        //    serializer.Serialize(serializationSetStream, serializationSet);

        //    return request.ResponseType == DataResponseType.Binary
        //        ? new FileResult(serializationSetStream, request.ResponseFileName)
        //        : new EncodedFileResult(this.RequestContext.Configuration.Formatters.JsonFormatter, serializationSetStream, request.ResponseFileName);
        //}

        //private IHttpActionResult SerializePerTable(SerializationSet serializationSet, DataRequest request)
        //{
        //    var filteredMetaDataList = new List<SerializationSet>();

        //    foreach (var table in serializationSet.Tables)
        //    {
        //        var filteredMetaData = serializationSet.Filter(table.Name);
        //        filteredMetaData = ReplaceOptionProcessor.Process(filteredMetaData, request);

        //        if (filteredMetaData.Tables.Any())
        //        {
        //            filteredMetaDataList.Add(filteredMetaData);
        //        }
        //    }

        //    var entries = new List<ZipFileResultEntry>();
        //    foreach (var dataSet in filteredMetaDataList)
        //    {
        //        var filename = $"{dataSet.Tables[0].Name}.{request.Ext}";
        //        var serializationSetStream = new MemoryStream();
        //        var serializer = this.GetDataSerializer(request.OutputType);
        //        serializer.Serialize(serializationSetStream, dataSet);

        //        entries.Add(new ZipFileResultEntry(filename, serializationSetStream));
        //    }

        //    return new ZipFileResult(entries.ToArray(), request.ResponseFileName);
        //}

        private Stream SerializeAll(SerializationSet serializationSet, DataRequest request)
        {
            var serializationSetStream = new MemoryStream();

            var serializer = this.GetDataSerializer(request.OutputType);
            serializer.Serialize(serializationSetStream, serializationSet);

            return serializationSetStream;
        }

        private ZipFileResultEntry[] SerializePerTable(SerializationSet serializationSet, DataRequest request)
        {
            var filteredMetaDataList = new List<SerializationSet>();

            foreach (var table in serializationSet.Tables)
            {
                var filteredMetaData = serializationSet.Filter(table.Name);
                filteredMetaData = ReplaceOptionProcessor.Process(filteredMetaData, request);

                if (filteredMetaData.Tables.Any())
                {
                    filteredMetaDataList.Add(filteredMetaData);
                }
            }

            var entries = new List<ZipFileResultEntry>();
            foreach (var dataSet in filteredMetaDataList)
            {
                var filename = $"{dataSet.Tables[0].Name}.{request.Ext}";
                var serializationSetStream = new MemoryStream();
                var serializer = this.GetDataSerializer(request.OutputType);
                serializer.Serialize(serializationSetStream, dataSet);

                entries.Add(new ZipFileResultEntry(filename, serializationSetStream));
            }

            return entries.ToArray();
        }

        private IEnumerable<IDataSerializer> GetDataSerializers()
        {
            return this.cremaHost.GetService(typeof(IEnumerable<IDataSerializer>)) as IEnumerable<IDataSerializer>;
        }

        private IDataSerializer GetDataSerializer(string outputType)
        {
            return this.GetDataSerializers().FirstOrDefault(o => o.Name == outputType);
        }
    }

    static class ReplaceOptionProcessor
    {
        public static SerializationSet Process(SerializationSet serializationSet, DataRequest request)
        {
            if (request.ReplaceRevision != null)
            {
                serializationSet.Revision = request.ReplaceRevision ?? -1;
            }

            if (request.ReplaceHashValue != null)
            {
                serializationSet.TablesHashValue = request.ReplaceHashValue;
                serializationSet.TypesHashValue = request.ReplaceHashValue;
            }

            return serializationSet;
        }
    }
}
