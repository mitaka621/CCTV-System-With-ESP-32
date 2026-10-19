using CamPortal.Contracts.Abstractions.Services;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace CamPortal.Core.Services.RemoteStorage
{
    public class SmbShareSession : ISmbShareSession
    {
        private readonly SMB2Client _client;
        private readonly ISMBFileStore _fileStore;
        private readonly SmbSharePathDto _sharePath;

        private SmbShareSession(SMB2Client client, ISMBFileStore fileStore, SmbSharePathDto sharePath)
        {
            _client = client;
            _fileStore = fileStore;
            _sharePath = sharePath;
        }

        public bool IsConnected => _client.IsConnected;

        public static SmbShareSession Open(SmbSharePathDto sharePath, string username, string password, int responseTimeoutInMilliseconds)
        {
            var client = new SMB2Client(responseTimeoutInMilliseconds, true);

            try
            {
                if (!client.Connect(sharePath.Server, SMBTransportType.DirectTCPTransport))
                {
                    throw new IOException($"Could not reach {sharePath.Server} on port {SMB2Client.DirectTCPPort}.");
                }

                var (domainName, userName) = SplitUsername(username);

                HandleNTStates(client.Login(domainName, userName, password), $"Logging in to {sharePath.Server}");

                var fileStore = client.TreeConnect(sharePath.ShareName, out var treeConnectStatus);

                HandleNTStates(treeConnectStatus, $"Opening share {sharePath.ShareName}");

                return new SmbShareSession(client, fileStore, sharePath);
            }
            catch
            {
                client.Disconnect();
                throw;
            }
        }

        public void CreateDirectory(string relativePath)
        {
            var currentPath = string.Empty;

            foreach (var segment in SplitPath(GetSharePath(relativePath)))
            {
                currentPath = currentPath.Length == 0 ? segment : $@"{currentPath}\{segment}";

                var status = _fileStore.CreateFile(
                    out var handle,
                    out _,
                    currentPath,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN_IF,
                    CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                HandleNTStates(status, $"Creating folder {currentPath}");

                _fileStore.CloseFile(handle);
            }
        }

        public void UploadFile(Stream source, string relativePath)
        {
            var sharePath = GetSharePath(relativePath);

            var status = _fileStore.CreateFile(
                out var handle,
                out _,
                sharePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OVERWRITE_IF,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            HandleNTStates(status, $"Creating file {sharePath}");

            try
            {
                var buffer = new byte[(int)Math.Min(_client.MaxWriteSize, SMB2Client.ClientMaxWriteSize)];
                long offset = 0;
                int bytesRead;

                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var data = bytesRead == buffer.Length ? buffer : buffer.AsSpan(0, bytesRead).ToArray();

                    HandleNTStates(_fileStore.WriteFile(out var bytesWritten, handle, offset, data), $"Writing file {sharePath}");

                    if (bytesWritten != data.Length)
                    {
                        throw new IOException($"Writing file {sharePath} stopped after {offset + bytesWritten} bytes.");
                    }

                    offset += bytesWritten;
                }
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        public void RenameFile(string sourceRelativePath, string destinationRelativePath)
        {
            var sourceSharePath = GetSharePath(sourceRelativePath);
            var destinationSharePath = GetSharePath(destinationRelativePath);

            var status = _fileStore.CreateFile(
                out var handle,
                out _,
                sourceSharePath,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            HandleNTStates(status, $"Opening file {sourceSharePath}");

            try
            {
                var renameInformation = new FileRenameInformationType2
                {
                    ReplaceIfExists = true,
                    FileName = destinationSharePath
                };

                HandleNTStates(_fileStore.SetFileInformation(handle, renameInformation), $"Renaming {sourceSharePath} to {destinationSharePath}");
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        public byte[]? ReadAllBytes(string relativePath)
        {
            var sharePath = GetSharePath(relativePath);

            var status = _fileStore.CreateFile(
                out var handle,
                out _,
                sharePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status is NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return null;
            }

            HandleNTStates(status, $"Opening file {sharePath}");

            try
            {
                HandleNTStates(
                    _fileStore.GetFileInformation(out var fileInformation, handle, FileInformationClass.FileStandardInformation),
                    $"Reading the size of {sharePath}");

                var content = new byte[((FileStandardInformation)fileInformation).EndOfFile];
                var maxReadSize = (int)Math.Min(_client.MaxReadSize, SMB2Client.ClientMaxReadSize);
                var offset = 0;

                while (offset < content.Length)
                {
                    HandleNTStates(
                        _fileStore.ReadFile(out var data, handle, offset, Math.Min(maxReadSize, content.Length - offset)),
                        $"Reading file {sharePath}");

                    if (data.Length == 0)
                    {
                        break;
                    }

                    data.CopyTo(content, offset);
                    offset += data.Length;
                }

                return content;
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        public List<string> GetDirectoryNames(string relativePath)
        {
            return QueryDirectory(relativePath)
                .Where(x => (x.FileAttributes & FileAttributes.Directory) != 0)
                .Select(x => x.FileName)
                .ToList();
        }

        public List<string> GetFileNames(string relativePath)
        {
            return QueryDirectory(relativePath)
                .Where(x => (x.FileAttributes & FileAttributes.Directory) == 0)
                .Select(x => x.FileName)
                .ToList();
        }

        public void DeleteFile(string relativePath)
        {
            DeleteEntry(GetSharePath(relativePath), CreateOptions.FILE_NON_DIRECTORY_FILE);
        }

        public void DeleteDirectory(string relativePath)
        {
            foreach (var fileName in GetFileNames(relativePath))
            {
                DeleteFile($@"{relativePath}\{fileName}");
            }

            foreach (var directoryName in GetDirectoryNames(relativePath))
            {
                DeleteDirectory($@"{relativePath}\{directoryName}");
            }

            DeleteEntry(GetSharePath(relativePath), CreateOptions.FILE_DIRECTORY_FILE);
        }

        private List<FileDirectoryInformation> QueryDirectory(string relativePath)
        {
            var sharePath = GetSharePath(relativePath);

            var status = _fileStore.CreateFile(
                out var handle,
                out _,
                sharePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status is NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return new List<FileDirectoryInformation>();
            }

            HandleNTStates(status, $"Opening folder {sharePath}");

            try
            {
                status = _fileStore.QueryDirectory(out var entries, handle, "*", FileInformationClass.FileDirectoryInformation);

                if (status != NTStatus.STATUS_NO_MORE_FILES)
                {
                    HandleNTStates(status, $"Listing folder {sharePath}");
                }

                return entries
                    .OfType<FileDirectoryInformation>()
                    .Where(x => x.FileName != "." && x.FileName != "..")
                    .ToList();
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        private void DeleteEntry(string sharePath, CreateOptions entryTypeOption)
        {
            var status = _fileStore.CreateFile(
                out var handle,
                out _,
                sharePath,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                entryTypeOption | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status is NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return;
            }

            HandleNTStates(status, $"Opening {sharePath} for deletion");

            try
            {
                HandleNTStates(
                    _fileStore.SetFileInformation(handle, new FileDispositionInformation { DeletePending = true }),
                    $"Deleting {sharePath}");
            }
            finally
            {
                _fileStore.CloseFile(handle);
            }
        }

        private string GetSharePath(string relativePath)
        {
            return string.Join('\\', SplitPath(_sharePath.BaseFolder).Concat(SplitPath(relativePath)));
        }

        private static string[] SplitPath(string path)
        {
            return path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        }

        private static (string DomainName, string UserName) SplitUsername(string username)
        {
            var trimmedUsername = username.Trim();
            var separatorIndex = trimmedUsername.IndexOf('\\');

            return separatorIndex > 0
                ? (trimmedUsername[..separatorIndex], trimmedUsername[(separatorIndex + 1)..])
                : (string.Empty, trimmedUsername);
        }

        private static void HandleNTStates(NTStatus status, string operation)
        {
            if (status == NTStatus.STATUS_SUCCESS)
            {
                return;
            }

            var reason = status switch
            {
                NTStatus.STATUS_LOGON_FAILURE => "wrong username or password",
                NTStatus.STATUS_ACCOUNT_DISABLED or NTStatus.STATUS_ACCOUNT_LOCKED_OUT => "the account is disabled or locked",
                NTStatus.STATUS_BAD_NETWORK_NAME => "the share does not exist",
                NTStatus.STATUS_ACCESS_DENIED => "access denied",
                NTStatus.STATUS_DISK_FULL => "the disk is full",
                NTStatus.STATUS_IO_TIMEOUT => "the server did not respond in time",
                _ => status.ToString()
            };

            throw new IOException($"{operation} failed: {reason}.");
        }

        public void Dispose()
        {
            try
            {
                if (_client.IsConnected)
                {
                    _fileStore.Disconnect();
                    _client.Logoff();
                }
            }
            catch
            {
            }
            finally
            {
                _client.Disconnect();
            }
        }
    }
}
