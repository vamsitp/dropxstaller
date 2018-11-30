namespace DropXStaller
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Management.Automation;
    using System.Net;
    using System.Reflection;
    using System.Text;

    using Newtonsoft.Json;

    public class Program
    {
        private const string ArchiveFile = "./Drop.zip";
        private const string DropFileLocation = "_apis/resources/Containers";
        private const string SecurityTokensUrl = "_details/security/tokens";

        private static readonly string Account = ConfigurationManager.AppSettings["Account"];
        private static readonly string DropFilePath = ConfigurationManager.AppSettings["DropFilePath"];
        private static readonly string BuildOrContainerId = ConfigurationManager.AppSettings["BuildOrContainerId"];
        private static readonly string PersonalAccessToken = ConfigurationManager.AppSettings["PersonalAccessToken"];
        private static readonly string InstallScriptPath = ConfigurationManager.AppSettings["InstallScriptPath"];
        private static readonly string InstallScriptArguments = ConfigurationManager.AppSettings["InstallScriptArguments"];
        private static readonly bool RedirectScriptOutputToConsole = bool.Parse(ConfigurationManager.AppSettings["RedirectScriptOutputToConsole"]);

        static void Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0 && (args[0].EndsWith("-i", StringComparison.OrdinalIgnoreCase) || args[0].EndsWith("/i", StringComparison.OrdinalIgnoreCase)))
                {
                    ExtractAndInstall();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(PersonalAccessToken))
                    {
                        var tokenUrl = $"{Account}{SecurityTokensUrl}";
                        Console.WriteLine($"PersonalAccessToken is blank in the config!\nHit ENTER to generate it (select 'All Scopes') at: {tokenUrl}");
                        Console.ReadLine();
                        Process.Start(tokenUrl);
                        Console.WriteLine($"Update 'PersonalAccessToken' value in DropXStaller.config with the generated/copied token and restart the app.");
                    }
                    else
                    {
                        DownloadLatestPackage();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }

            Console.ReadLine();
        }

        private static void DownloadLatestPackage()
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{PersonalAccessToken}"));
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
                var resp = wc.DownloadString(Account + DropFileLocation);
                var containers = JsonConvert.DeserializeObject<Containers>(resp)?.value?.Where(x => !x.scopeIdentifier.Equals(Guid.Empty.ToString()));
                Value container = null;
                if (string.IsNullOrWhiteSpace(BuildOrContainerId))
                {
                    container = containers?.OrderByDescending(x => x.id).FirstOrDefault();
                }
                else
                {
                    container = containers?.FirstOrDefault(x => x.id.ToString().Equals(BuildOrContainerId)) ??
                                containers?.FirstOrDefault(x => x.artifactUri.Contains(BuildOrContainerId));
                }

                if (container != null)
                {
                    DownloadPackage(container, wc);
                }
                else
                {
                    Console.WriteLine($"Could not find any Build or Container with ID: {BuildOrContainerId}");
                }
            }
        }

        private static void DownloadPackage(Value container, WebClient wc)
        {
            if (!string.IsNullOrWhiteSpace(container?.contentLocation))
            {
                var file = container.contentLocation + DropFilePath;
                Console.WriteLine($"Build: {container.name} ({container.dateCreated})\nDownloading Package: {file}");
                DownloadPackage(file, wc);
            }
        }

        private static void DownloadPackage(string file, WebClient wc)
        {
            wc.DownloadProgressChanged += (sender, eventArgs) => Console.Write("\r{0} MB ", eventArgs.BytesReceived / 1000000);
            wc.DownloadFileCompleted += (sender, eventArgs) => ExtractAndInstall();
            wc.DownloadFileAsync(new Uri(file), ArchiveFile);
        }

        /// <summary>
        /// https://blogs.msdn.microsoft.com/kebab/2014/04/28/executing-powershell-scripts-from-c/
        /// </summary>
        private static void ExtractAndInstall()
        {
            try
            {
                Console.WriteLine("\nNote: If Windows finds the downloaded file (Drop.zip) harmful - Right-click 'Drop.zip' > Properties > General tab > Click 'Unblock' at the bottom > OK.\nThen re-run the app with -i parameter.");
                ZipFile.ExtractToDirectory(ArchiveFile, "./");
            }
            catch (InvalidDataException ide)
            {
                WriteError(ide.Message);
                return;
            }
            catch (IOException ioe)
            {
                WriteError(ioe.Message);
            }

            Console.WriteLine("\nNote: If there are privilege issues running the Installation script - From an elevated PowerShell window, run: Set-ExecutionPolicy RemoteSigned");
            var scriptPath = Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), InstallScriptPath);
            var args = string.Format(InstallScriptArguments, scriptPath);

            // ExecuteScript(scriptPath);
            // Process.Start("Powershell.exe", $@"""{scriptPath}"" 'arg1'");
            if (RedirectScriptOutputToConsole)
            {
                ExecuteProcess("Powershell", args, "./");
            }
            else
            {
                Process.Start("Powershell", args);
            }

            Console.WriteLine("\nDone!");
        }

        private static void WriteError(string error)
        {
            Console.WriteLine("Error: " + error);
        }

        public static bool ExecuteProcess(string exePath, string arguments, string workingDirectory)
        {
            var error = false;
            var exitCode = 0;
            using (var process = new Process())
            {
                DataReceivedEventHandler outputHandler = null;
                DataReceivedEventHandler errorHandler = null;

                try
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory
                    };

                    process.StartInfo.RedirectStandardError = true;
                    process.Start();

                    errorHandler = (sender, args) =>
                    {
                        var err = args.Data;
                        if (!string.IsNullOrEmpty(err))
                        {
                            WriteError(err);
                            error = true;
                        }
                    };

                    process.ErrorDataReceived += errorHandler;
                    process.BeginErrorReadLine();

                    outputHandler = (sender, args) =>
                    {
                        var output = args.Data;
                        if (!string.IsNullOrEmpty(output))
                        {
                            Console.WriteLine(output);
                        }
                    };

                    process.OutputDataReceived += outputHandler;
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    error = true;
                }
                finally
                {
                    process.OutputDataReceived -= outputHandler;
                    process.ErrorDataReceived -= errorHandler;
                    exitCode = process.ExitCode;
                    process.Close();
                }
            }

            return !error && exitCode == 0;
        }

        private static void ExecuteScript(string scriptPath)
        {
            using (var powerShellInstance = PowerShell.Create())
            {
                // Hack
                powerShellInstance.AddScript("function write-host {}").Invoke();
                powerShellInstance.Commands.Clear();
                powerShellInstance.AddScript(scriptPath);
                var results = powerShellInstance.Invoke();
                foreach (var item in results)
                {
                    Console.WriteLine(item);
                }

                if (powerShellInstance.Streams.Error.Count > 0)
                {
                    Console.WriteLine("\n{0} error(s)", powerShellInstance.Streams.Error.Count);
                    Console.WriteLine(string.Join(Environment.NewLine,
                        powerShellInstance.Streams.Error.Select(e => e.Exception.Message)));
                }
            }
        }
    }
}
