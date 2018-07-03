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

using Ntreev.Crema.ServiceModel;
using Ntreev.Crema.Services;
using Ntreev.Library;
using Ntreev.Library.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ntreev.Crema.Repository.Svn
{
    class SvnRepository : IRepository
    {
        private const string patchExtension = ".patch";

        private readonly string repositoryPath;
        private readonly string transactionPath;
        private readonly SvnRepositoryProvider repositoryProvider;
        private readonly ILogService logService;
        private string transactionAuthor;
        private string transactionName;
        private string transactionMessages;
        private bool needToUpdate;
        private Uri repositoryRoot;
        private Uri repositoryUri;
        private RepositoryInfo repositoryInfo;
        private SvnInfoEventArgs info;

        public SvnRepository(SvnRepositoryProvider repositoryProvider, ILogService logService, string repositoryPath, string transactionPath, RepositoryInfo repositoryInfo)
        {
            this.repositoryProvider = repositoryProvider;
            this.logService = logService;
            this.repositoryPath = repositoryPath;
            this.transactionPath = transactionPath;
            this.repositoryInfo = repositoryInfo;

            var statCommand = new SvnCommand("stat")
            {
                (SvnPath)this.repositoryPath,
                SvnCommandItem.Quiet
            };
            var items = statCommand.ReadLines(true);
            if (items.Length != 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Repository is dirty. Please fix the problem before running the service.");
                sb.AppendLine();
                sb.AppendLine(string.Join(Environment.NewLine, items));
                throw new Exception($"{sb}");
            }

            this.info = SvnInfoEventArgs.Run(this.repositoryPath);
            this.repositoryRoot = this.info.RepositoryRoot;
            this.repositoryUri = this.info.Uri;
        }

        public string Name => "svn";

        public RepositoryInfo RepositoryInfo => this.repositoryInfo;

        public string BasePath => this.repositoryPath;

        public void Add(string path)
        {
            var addCommand = new SvnCommand("add")
            {
                new SvnCommandItem("depth", "files"),
                (SvnPath)path,
            };
            addCommand.Run(this.logService);
        }

        public void BeginTransaction(string author, string name)
        {
            this.logService?.Debug("repository begin transaction \"{0}\" \"{1}\"", this.repositoryPath, name);
            this.transactionAuthor = author;
            this.transactionName = name;
            this.transactionMessages = string.Empty;
        }

        public void EndTransaction()
        {
            this.logService?.Debug("repository end transaction \"{0}\"", this.repositoryPath);
            var patchPath = Path.Combine(this.transactionPath, this.transactionName + patchExtension);

            if (File.Exists(patchPath) == true)
            {
                var patchCommand = new SvnCommand("patch")
                {
                    (SvnPath)patchPath,
                    (SvnPath)this.repositoryPath,
                };
                patchCommand.Run(this.logService);
                this.Commit(this.transactionAuthor, "Transaction" + Environment.NewLine + this.transactionMessages, new LogPropertyInfo[] { });
                FileUtility.Delete(patchPath);
            }

            this.transactionAuthor = null;
            this.transactionName = null;
            this.transactionMessages = null;
        }

        public void CancelTransaction()
        {
            this.logService?.Debug("repository cancel transaction \"{0}\"", this.repositoryPath);
            var patchPath = Path.Combine(this.transactionPath, this.transactionName + patchExtension);
            var revertCommand = new SvnCommand("revert")
            {
                (SvnPath)this.repositoryPath,
                SvnCommandItem.Recursive,
            };
            this.transactionAuthor = null;
            this.transactionName = null;
            this.transactionMessages = null;
            revertCommand.Run(this.logService);
            FileUtility.Delete(patchPath);
        }

        public void Commit(string author, string comment, params LogPropertyInfo[] properties)
        {
            if (this.transactionName != null)
            {
                var patchPath = Path.Combine(this.transactionPath, this.transactionName + ".patch");
                var diffCommand = new SvnCommand("diff")
                {
                    (SvnPath)this.repositoryPath,
                    new SvnCommandItem("patch-compatible")
                };
                var text = diffCommand.Run(this.logService);
                FileUtility.WriteAllText(text, Encoding.UTF8, patchPath);
                this.transactionMessages = this.transactionMessages + comment + Environment.NewLine;
            }

            this.logService?.Debug($"repository committing {(SvnPath)this.repositoryPath}");
            var result = string.Empty;
            var commentPath = PathUtility.GetTempFileName();
            var propText = SvnRepositoryProvider.GeneratePropertiesArgument(properties);
            var updateCommand = new SvnCommand("update") { (SvnPath)this.repositoryPath };
            var commitCommand = new SvnCommand("commit")
            {
                (SvnPath)this.repositoryPath,
                SvnCommandItem.FromMessage(comment),
                propText,
                SvnCommandItem.FromEncoding(Encoding.UTF8),
                SvnCommandItem.FromUsername(author),
            };

            try
            {
                if (this.needToUpdate == true)
                {
                    updateCommand.Run(this.logService);
                }

                result = commitCommand.Run(this.logService);
            }
            catch (Exception e)
            {
                this.logService?.Warn(e);
                updateCommand.Run(this.logService);
                result = commitCommand.Run(this.logService);
            }
            finally
            {
                this.needToUpdate = false;
                FileUtility.Delete(commentPath);
            }

            if (result.Trim() != string.Empty)
            {
                this.logService?.Debug(result);
                this.logService?.Debug($"repository committed {(SvnPath)this.repositoryPath}");
                this.info = SvnInfoEventArgs.Run(this.repositoryPath);
                this.repositoryInfo.Revision = this.info.LastChangedRevision;
                this.repositoryInfo.ModificationInfo = new SignatureDate(this.info.LastChangedAuthor, this.info.LastChangedDate);
            }
            else
            {
                this.logService?.Debug("repository no changes. \"{0}\"", this.repositoryPath);
            }
        }

        public void Copy(string srcPath, string toPath)
        {
            var copyCommand = new SvnCommand("copy")
            {
                (SvnPath)srcPath,
                (SvnPath)toPath
            };
            copyCommand.Run(this.logService);
        }

        public void Delete(string path)
        {
            var deleteCommand = new SvnCommand("delete")
            {
                (SvnPath)path,
                SvnCommandItem.Force,
            };

            if (DirectoryUtility.IsDirectory(path) == true)
            {
                var updateCommand = new SvnCommand("update") { (SvnPath)path };
                updateCommand.Run(this.logService);
            }

            deleteCommand.Run(this.logService);
        }

        public string Export(Uri uri, string exportPath)
        {
            var pureUri = new Uri(Regex.Replace($"{uri}", "@\\d+$", string.Empty));
            var relativeUri = UriUtility.MakeRelativeOfDirectory(this.repositoryUri, pureUri);
            var uriTarget = uri.LocalPath;
            var filename = FileUtility.Prepare(exportPath, $"{relativeUri}");
            var exportCommand = new SvnCommand("export") { (SvnPath)uri, (SvnPath)filename };
            var result = exportCommand.Run(this.logService);
            return new FileInfo(Path.Combine(exportPath, $"{relativeUri}")).FullName;
        }

        //public void GetBranchInfo(string path, out string revision, out string source, out string sourceRevision)
        //{
        //    var info = SvnInfoEventArgs.Run(path);
        //    this.GetBranchRevision(info.RepositoryRoot, info.Uri, out revision, out source, out sourceRevision);
        //}

        public LogInfo[] GetLog(string[] paths, string revision, int count)
        {
            var logs = SvnLogEventArgs.Run(paths, revision, count);
            return logs.Select(item => (LogInfo)item).ToArray();
        }

        public string GetRevision(string path)
        {
            var info = SvnInfoEventArgs.Run(path);
            var repositoryInfo = SvnInfoEventArgs.Run($"{info.Uri}");
            return repositoryInfo.LastChangedRevision;
        }

        public Uri GetUri(string path, string revision)
        {
            var revisionValue = revision ?? this.repositoryInfo.Revision;
            var info = SvnInfoEventArgs.Run(path, revisionValue);
            return new Uri($"{info.Uri}@{revisionValue}");
        }

        public RepositoryItem[] Status(params string[] paths)
        {
            var args = SvnStatusEventArgs.Run(paths);
            var itemList = new List<RepositoryItem>(args.Length);
            foreach (var item in args)
            {
                itemList.Add(new RepositoryItem()
                {
                    Path = item.Path,
                    OldPath = item.OldPath,
                    Status = item.Status,
                });
            }
            return itemList.ToArray();
        }

        public void Move(string srcPath, string toPath)
        {
            var moveCommand = new SvnCommand("move")
            {
                (SvnPath)srcPath,
                (SvnPath)toPath,
            };

            if (DirectoryUtility.IsDirectory(srcPath) == true)
            {
                var updateCommand = new SvnCommand("update") { (SvnPath)srcPath };
                updateCommand.Run(this.logService);
            }

            moveCommand.Run(this.logService);
        }

        public void Revert()
        {
            var revertCommand = new SvnCommand("revert")
            {
                SvnCommandItem.Recursive,
                (SvnPath)this.repositoryPath
            };
            try
            {
                revertCommand.Run(this.logService);
            }
            catch
            {
                var cleanUpCommand = new SvnCommand("cleanup") { (SvnPath)this.repositoryPath };
                cleanUpCommand.Run(this.logService);
                revertCommand.Run(this.logService);
            }
        }

        public void Revert(string revision)
        {
            var updateCommand = new SvnCommand("update") { (SvnPath)this.repositoryPath };
            var mergeCommand = new SvnCommand("merge")
            {
                new SvnCommandItem('r',$"head:{revision}"),
                (SvnPath)this.repositoryPath,
                (SvnPath)this.repositoryPath,
            };
            updateCommand.Run(this.logService);
            mergeCommand.Run(this.logService);
        }

        public void Dispose()
        {
            DirectoryUtility.Delete(this.repositoryPath);
        }

        private string GetOriginPath(Uri repoUri, SvnLogEventArgs[] logs, string revision)
        {
            var repoPath = PathUtility.SeparatorChar + repoUri.OriginalString;

            foreach (var log in logs)
            {
                if (log.Revision == revision)
                    continue;

                foreach (var changedPath in log.ChangedPaths)
                {
                    if (changedPath.CopyFromPath == null)
                        continue;
                    if (repoPath.StartsWith(changedPath.Path) == true)
                    {
                        repoPath = Regex.Replace(repoPath, "^" + changedPath.Path, changedPath.CopyFromPath);
                        break;
                    }
                }
            }

            return repoPath;
        }

        //private void GetBranchRevision(Uri repositoryRoot, Uri uri, out string revision, out string source, out string sourceRevision)
        //{
        //    var log = SvnLogEventArgs.RunForGetBranch(uri).Last();
        //    var relativeUri = repositoryRoot.MakeRelativeUri(uri);

        //    var localPath = $"/{relativeUri}";
        //    var oldPath = string.Empty;
        //    var oldRevision = null as string;

        //    revision = log.Revision;
        //    source = null;
        //    sourceRevision = log.Revision;
        //    foreach (var item in log.ChangedPaths)
        //    {
        //        if (item.Action == "A" && item.Path == localPath)
        //        {
        //            oldPath = item.CopyFromPath;
        //            oldRevision = item.CopyFromRevision;
        //            source = Path.GetFileName(item.CopyFromPath);
        //            sourceRevision = item.CopyFromRevision;
        //        }
        //    }

        //    if (oldPath == string.Empty)
        //        return;

        //    foreach (var item in log.ChangedPaths)
        //    {
        //        if (item.Action == "D" && item.Path == oldPath)
        //        {
        //            var url = new Uri(repositoryRoot + item.Path.Substring(1) + "@" + oldRevision);
        //            GetBranchRevision(repositoryRoot, url, out revision, out source, out sourceRevision);
        //            return;
        //        }
        //    }
        //}
    }
}
