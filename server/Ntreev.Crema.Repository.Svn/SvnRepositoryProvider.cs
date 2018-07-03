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

using Ntreev.Crema.Data;
using Ntreev.Crema.ServiceModel;
using Ntreev.Crema.Services;
using Ntreev.Crema.Services.Users;
using Ntreev.Library;
using Ntreev.Library.IO;
using Ntreev.Library.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Ntreev.Crema.Repository.Svn
{
    [Export]
    [Export(typeof(IRepositoryProvider))]
    [Export(typeof(IConfigurationPropertyProvider))]
    class SvnRepositoryProvider : IRepositoryProvider, IConfigurationPropertyProvider
    {
        private const string propertyPrefix = "prop:";
        //private const string commentHeader = "# revision properties";
        //private static readonly Serializer propertySerializer = new SerializerBuilder().Build();
        //private static readonly Deserializer propertyDeserializer = new Deserializer();

        [Import]
        private Lazy<ICremaHost> cremaHost = null;

        [ImportingConstructor]
        public SvnRepositoryProvider()
        {

        }

        public string Name => SvnString.Name;

        public IRepository CreateInstance(RepositorySettings settings)
        {
            var baseUri = new Uri(settings.BasePath);
            var repositoryName = settings.RepositoryName == string.Empty ? SvnString.Default : settings.RepositoryName;
            var url = repositoryName == SvnString.Default ? UriUtility.Combine(baseUri, SvnString.Trunk) : UriUtility.Combine(baseUri, SvnString.Branches, settings.RepositoryName);

            if (Directory.Exists(settings.WorkingPath) == false)
            {
                var checkoutCommand = new SvnCommand("checkout")
                {
                    (SvnPath)url,
                    (SvnPath)settings.WorkingPath,
                };
                checkoutCommand.Run();
            }
            else
            {
                var updateCommand = new SvnCommand("update")
                {
                    (SvnPath)settings.WorkingPath,
                };
                updateCommand.Run();
            }

            var repositoryInfo = this.GetRepositoryInfo(settings.BasePath, repositoryName);
            return new SvnRepository(this, settings.LogService, settings.WorkingPath, settings.TransactionPath, repositoryInfo);
        }

        public void InitializeRepository(string basePath, string initPath)
        {
            var baseUri = new Uri(basePath);
            var tempPath = PathUtility.GetTempPath(true);
            var tagsPath = DirectoryUtility.Prepare(tempPath, SvnString.Tags);
            var branchesPath = DirectoryUtility.Prepare(tempPath, SvnString.Branches);
            var trunkPath = DirectoryUtility.Prepare(tempPath, SvnString.Trunk);

            if (baseUri.Scheme == Uri.UriSchemeFile)
            {
                var createCommand = new SvnAdminCommand("create")
                {
                    (SvnPath)basePath,
                    "--fs-type",
                    "fsfs"
                };
                createCommand.Run();
            }

            DirectoryUtility.Copy(initPath, trunkPath);

            var importCommand = new SvnCommand("import")
            {
                SvnCommandItem.FromMessage("first"),
                (SvnPath)tempPath,
                (SvnPath)baseUri,
            };
            importCommand.Run();
        }

        public void CreateRepository(string author, string basePath, string initPath, string comment, params LogPropertyInfo[] properties)
        {
            var uri = UriUtility.Combine(new Uri(basePath), SvnString.Branches);
            var props = GeneratePropertiesArgument(properties);
            var importCommand = new SvnCommand("import")
            {
                (SvnPath)initPath,
                (SvnPath)uri,
                SvnCommandItem.FromMessage(comment),
                SvnCommandItem.Force,
                props,
                SvnCommandItem.FromUsername(author),
            };
            importCommand.Run();
        }

        public void CopyRepository(string author, string basePath, string repositoryName, string newRepositoryName, string comment, params LogPropertyInfo[] properties)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var newUri = this.GenerateUrl(basePath, newRepositoryName);
            var props = GeneratePropertiesArgument(properties);
            var copyCommand = new SvnCommand("copy")
            {
                SvnCommandItem.FromMessage(comment),
                (SvnPath)uri,
                (SvnPath)newUri,
                props,
                SvnCommandItem.FromUsername(author),
            };
            copyCommand.Run();
        }

        public void RenameRepository(string author, string basePath, string repositoryName, string newRepositoryName, string comment, params LogPropertyInfo[] properties)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var newUri = this.GenerateUrl(basePath, newRepositoryName);
            var props = GeneratePropertiesArgument(properties);
            var moveCommand = new SvnCommand("move")
            {
                SvnCommandItem.FromMessage(comment),
                (SvnPath)uri,
                (SvnPath)newUri,
                props,
                SvnCommandItem.FromUsername(author),
            };
            moveCommand.Run();
        }

        public void DeleteRepository(string author, string basePath, string repositoryName, string comment, params LogPropertyInfo[] properties)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var props = GeneratePropertiesArgument(properties);
            var deleteCommand = new SvnCommand("delete")
            {
                SvnCommandItem.FromMessage(comment),
                (SvnPath)uri,
                props,
                SvnCommandItem.FromUsername(author),
            };
            deleteCommand.Run();
        }

        public void ValidateRepository(string basePath, string repositoryPath)
        {
            if (DirectoryUtility.Exists(basePath) == false)
                throw new DirectoryNotFoundException($"base path does not exists :\"{basePath}\"");
            if (DirectoryUtility.Exists(repositoryPath) == false)
                throw new DirectoryNotFoundException($"repository path does not exists :\"{repositoryPath}\"");
        }

        public string[] GetRepositories(string basePath)
        {
            var uri = new Uri(basePath);
            var listCommand = new SvnCommand("list") { (SvnPath)uri };
            var lines = listCommand.ReadLines();
            var itemList = new List<string>();
            foreach (var line in lines)
            {
                if (line.EndsWith(PathUtility.Separator) == true)
                {
                    var name = line.Substring(0, line.Length - PathUtility.Separator.Length);
                    if (name == SvnString.Trunk)
                    {
                        itemList.Add("default");
                    }
                    else if (name == SvnString.Tags || name == SvnString.Branches)
                    {
                        var subPath = Path.Combine(basePath, name);
                        itemList.AddRange(this.GetRepositories(subPath));
                    }
                    else
                    {
                        itemList.Add(name);
                    }
                }
            }
            return itemList.ToArray();
        }

        public string GetRevision(string basePath, string repositoryName)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var infoCommand = new SvnCommand("info")
            {
                (SvnPath)uri,
                new SvnCommandItem("show-item", "last-changed-revision"),
            };
            return infoCommand.Run().Trim();
        }

        public RepositoryInfo GetRepositoryInfo(string basePath, string repositoryName)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var latestLog = SvnLogEventArgs.Run(uri.ToString(), null, 1).First();

            var ss = this.GetFirst(uri.ToString(), out var comment);

            this.GetBranchInfo(uri.ToString(), out var branchRevision, out var branchSource, out var branchSourceRevision);

            //var branchLog = SvnLogEventArgs.Run(uri.ToString(), branchRevision, 1).First();
            //var branchUserID = branchLog.GetPropertyString(LogPropertyInfo.UserIDKey) ?? string.Empty;
            //var latestUserID = latestLog.GetPropertyString(LogPropertyInfo.UserIDKey) ?? string.Empty;

            var repositoryInfo = new RepositoryInfo()
            {
                ID = GuidUtility.FromName(repositoryName + branchRevision),
                Name = repositoryName,
                Comment = comment,
                Revision = latestLog.Revision,
                //BranchRevision = branchRevision,
                //BranchSource = branchSource,
                //BranchSourceRevision = branchSourceRevision,
                CreationInfo = ss,
                ModificationInfo = new SignatureDate(latestLog.Author, latestLog.DateTime),
            };
            return repositoryInfo;
        }

        

        public string[] GetRepositoryItemList(string basePath, string repositoryName)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var listCommand = new SvnCommand("list") { (SvnPath)uri, SvnCommandItem.Recursive };
            var lines = listCommand.ReadLines();
            var query = from item in lines
                        where item.Trim() != string.Empty
                        select PathUtility.Separator + item;
            return query.ToArray();
        }

        public LogInfo[] GetLog(string basePath, string repositoryName, int count)
        {
            var uri = this.GetUrl(basePath, repositoryName);
            var logs = SvnLogEventArgs.Run(uri.ToString(), null, count);
            return logs.Select(item => (LogInfo)item).ToArray();
        }

        public IEnumerable<KeyValuePair<string, Uri>> GetRepositoryPaths(string basePath)
        {
            var uri = new Uri(basePath);
            var listCommand = new SvnCommand("list") { (SvnPath)uri };
            var lines = listCommand.ReadLines();
            foreach (var line in lines)
            {
                if (line.EndsWith(PathUtility.Separator) == true)
                {
                    var name = line.Substring(0, line.Length - PathUtility.Separator.Length);
                    if (name == SvnString.Trunk)
                    {
                        yield return new KeyValuePair<string, Uri>(SvnString.Default, UriUtility.Combine(uri, name));
                    }
                    else if (name == SvnString.Tags || name == SvnString.Branches)
                    {
                        var subPath = Path.Combine(basePath, name);
                        foreach (var item in this.GetRepositoryPaths(subPath))
                        {
                            yield return item;
                        }
                    }
                    else
                    {
                        yield return new KeyValuePair<string, Uri>(name, UriUtility.Combine(uri, name));
                    }
                }
            }
        }

        public static string GeneratePropertiesArgument(LogPropertyInfo[] properties)
        {
            return string.Join(" ", properties.Select(item => $"--with-revprop \"{propertyPrefix}{item.Key}={item.Value}\""));
        }

        //public string GenerateComment(string comment, params LogPropertyInfo[] properties)
        //{
        //    var propText = propertySerializer.Serialize(properties);
        //    var sb = new StringBuilder();
        //    sb.AppendLine(comment);
        //    sb.AppendLine();
        //    if (propText != string.Empty)
        //    {
        //        sb.AppendLine(commentHeader);
        //        sb.Append(propText);
        //    }
        //    return sb.ToString();
        //}

        //public static void ParseComment(string message, out string comment, out LogPropertyInfo[] properties)
        //{
        //    comment = string.Empty;
        //    properties = new LogPropertyInfo[] { };

        //    try
        //    {
        //        var index = message.IndexOf(commentHeader);
        //        if (index >= 0)
        //        {
        //            var propText = message.Substring(index);
        //            comment = message.Remove(index);

        //            var sr = new StringReader(comment);
        //            var lineList = new List<string>();
        //            var line = null as string;
        //            while ((line = sr.ReadLine()) != null)
        //            {
        //                lineList.Add(line);
        //            }

        //            if (lineList.Last() == string.Empty)
        //                lineList.RemoveAt(lineList.Count - 1);
        //            comment = string.Join(Environment.NewLine, lineList);

        //            properties = propertyDeserializer.Deserialize<LogPropertyInfo[]>(propText);
        //        }
        //        else
        //        {
        //            comment = null;
        //            properties = null;
        //        }
        //    }
        //    catch
        //    {
        //        comment = null;
        //        properties = null;
        //    }
        //}

        private Uri GetUrl(string basePath, string repositoryName)
        {
            var paths = this.GetRepositoryPaths(basePath).ToDictionary(item => item.Key, item => item.Value);
            return repositoryName == string.Empty ? paths[SvnString.Default] : paths[repositoryName];
        }

        private Uri GenerateUrl(string basePath, string repositoryName)
        {
            var baseUri = new Uri(basePath);
            return UriUtility.Combine(baseUri, SvnString.Branches, repositoryName);
        }

        private SignatureDate GetFirst(string path, out string comment)
        {
            var info = SvnInfoEventArgs.Run(path);
            var revision = info.LastChangedRevision;
            comment = null;

            var localPath = PathUtility.Separator + UriUtility.MakeRelativeOfDirectory(info.RepositoryRoot, info.Uri);
            while (revision != "1")
            {
                var logs = SvnLogEventArgs.Run(info.Uri.ToString(), "1", revision, 100);
                foreach (var item in logs)
                {
                    bool b = false;
                    foreach (var changedPath in item.ChangedPaths)
                    {
                        if (changedPath.Action == "A")
                        {
                            if (changedPath.Path == localPath)
                            {
                                localPath = changedPath.CopyFromPath;
                                b = true;
                            }
                        }
                    }

                    bool hasdelete = false;
                    if (b == true)
                    {
                        foreach (var changedPath in item.ChangedPaths)
                        {
                            if (changedPath.Action == "D")
                            {
                                if (changedPath.Path == localPath)
                                {
                                    hasdelete = true;
                                }
                            }
                        }

                        if (hasdelete == false)
                        {
                            comment = item.Comment;

                            foreach (var p in item.Properties)
                            {
                                if (p.Prefix == propertyPrefix && p.Key == LogPropertyInfo.UserIDKey)
                                {
                                    return new SignatureDate(p.Value, item.DateTime);
                                }
                            }

                            return new SignatureDate(item.Author, item.DateTime);
                        }
                    }
                }
                if (logs.Count() == 1)
                    revision = "1";
                else
                    revision = logs.Last().Revision;
            }

            var firstLog = SvnLogEventArgs.Run(info.Uri.ToString(), "1").First();
            comment = firstLog.Comment;
            return new SignatureDate(firstLog.Author, firstLog.DateTime);
        }

        private void GetBranchInfo(string path, out string revision, out string source, out string sourceRevision)
        {
            revision = null;
            source = null;
            sourceRevision = null;
            return;
            var info = SvnInfoEventArgs.Run(path);
            this.GetBranchRevision(info.RepositoryRoot, info.Uri, out revision, out source, out sourceRevision);
        }

        private void GetBranchRevision(Uri repositoryRoot, Uri uri, out string revision, out string source, out string sourceRevision)
        {
            var log = SvnLogEventArgs.RunForGetBranch(uri).Last();
            var relativeUri = repositoryRoot.MakeRelativeUri(uri);

            var localPath = $"/{relativeUri}";
            var oldPath = string.Empty;
            var oldRevision = null as string;

            revision = log.Revision;
            source = null;
            sourceRevision = log.Revision;
            foreach (var item in log.ChangedPaths)
            {
                if (item.Action == "A" && item.Path == localPath)
                {
                    oldPath = item.CopyFromPath;
                    oldRevision = item.CopyFromRevision;
                    source = Path.GetFileName(item.CopyFromPath);
                    sourceRevision = item.CopyFromRevision;
                }
            }

            if (oldPath == string.Empty)
                return;

            foreach (var item in log.ChangedPaths)
            {
                if (item.Action == "D" && item.Path == oldPath)
                {
                    var url = new Uri(repositoryRoot + item.Path.Substring(1) + "@" + oldRevision);
                    GetBranchRevision(repositoryRoot, url, out revision, out source, out sourceRevision);
                    return;
                }
            }
        }

        private ICremaHost CremaHost => this.cremaHost.Value;
    }
}
