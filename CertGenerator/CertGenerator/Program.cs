using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string blazorAppCertificatePath = @"..\..\..\..\..\BlazorCamPortal\BlazorCamPortal\server.pfx";
const string blazorAppSettingsRelativePath = @"..\..\..\..\..\BlazorCamPortal\BlazorCamPortal\appsettings.json";
const string esp32SecretsRelativePath = @"..\..\..\..\..\ESP_32_Cam_Firmware\include\secrets.h";

if (!IsOpenSslAvailable())
{
    Console.WriteLine("\nOpenSSL was not found on your system.");
    Console.WriteLine("Please install OpenSSL and add it to your PATH environment variable.");
    Console.WriteLine("Instructions:");
    Console.WriteLine("1. Download Windows OpenSSL (scroll down and download the latested installer): https://slproweb.com/products/Win32OpenSSL.html");
    Console.WriteLine("2. Install it in the default location (C:\\Program Files\\OpenSSL-Win64).");
    Console.WriteLine("3. Select \"Copy OpenSSL DLLs to The OpenSSL binaries (/bin) directory\"");
    Console.WriteLine("4. Search for \"edit environment variables\" in Windows -> Environment Variables -> Select \"Path\" in User Variables for <windows user name> -> Edit -> New -> Put \"C:\\Program Files\\OpenSSL-Win64\\bin\" -> Ok");
    Console.WriteLine("5. Restart the app");

    Console.ReadLine();
    return;
}

Console.WriteLine("OpenSSL is available.");

List<string> localIPs = GetLocalIPAddresses();
Console.WriteLine($"Local IPs detected: {string.Join(", ", localIPs)}");

string caKey = "ca.key";
string caCert = "ca.crt";
string serverKey = "server.key";
string serverCsr = "server.csr";
string serverCert = "server.crt";
string serverExt = "server.ext";
string pfxFile = "server.pfx";

string password = GenerateRandomPassword(16);

WriteServerExtensionsFile(serverExt, localIPs);

Console.WriteLine("Generating CA key and certificate...");
RunOpenSsl($"genrsa -out {caKey} 2048");
RunOpenSsl($"req -new -x509 -sha256 -days 3650 -key {caKey} -out {caCert} -subj \"/CN=MyLocalCA\"");

Console.WriteLine("Generating server key and CSR...");
RunOpenSsl($"genrsa -out {serverKey} 2048");
RunOpenSsl($"req -new -sha256 -key {serverKey} -out {serverCsr} -subj \"/CN=localhost\"");

Console.WriteLine("Signing server certificate with CA...");
RunOpenSsl($"x509 -req -sha256 -in {serverCsr} -CA {caCert} -CAkey {caKey} -CAcreateserial -out {serverCert} -days 825 -extfile {serverExt}");

Console.WriteLine("Exporting server certificate and key to PFX...");
RunOpenSsl($"pkcs12 -export -out {pfxFile} -inkey {serverKey} -in {serverCert} -certfile {caCert} -password pass:{password}");

Console.WriteLine($"PFX file generated: {pfxFile} with password: {password}");

CopyPfxToBlazorApp(pfxFile, blazorAppCertificatePath);

UpdateAppSettingsWithPassword(blazorAppSettingsRelativePath, password);

Console.WriteLine("Copied PFX to blazor app");

string caContent = File.ReadAllText(caCert);
string cString = ConvertToCString(caContent);

Console.WriteLine("Generated CA string for esp32:");
Console.WriteLine(cString);

CopyCaToSecretsFile(esp32SecretsRelativePath, cString);

ImportCaToTrustedRoot(caCert);

Console.WriteLine($"\n\n====================================");

Console.WriteLine("Operation completed!");

Console.ReadLine();
bool IsOpenSslAvailable()
{
    try
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "openssl",
                Arguments = "version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

List<string> GetLocalIPAddresses()
{
    var addresses = new List<string>();
    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        {
            var props = ni.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    string ip = addr.Address.ToString();
                    if (!addresses.Contains(ip))
                        addresses.Add(ip);
                }
            }
        }
    }
    return addresses;
}

void RunOpenSsl(string arguments)
{
    Console.WriteLine($"> openssl {arguments}");
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "openssl",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }
    };
    process.Start();
    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
    if (!string.IsNullOrEmpty(error))
        Console.WriteLine(error);

    if (process.ExitCode != 0)
        throw new Exception($"OpenSSL command failed: {arguments}");
}

void WriteServerExtensionsFile(string path, List<string> localIps)
{
    var sb = new StringBuilder();
    sb.Append("basicConstraints=CA:FALSE\n");
    sb.Append("keyUsage=digitalSignature,keyEncipherment\n");
    sb.Append("extendedKeyUsage=serverAuth\n");
    sb.Append("subjectAltName=@alt_names\n");
    sb.Append("\n");
    sb.Append("[alt_names]\n");

    sb.Append("DNS.1=localhost\n");
    int dnsIndex = 2;
    foreach (var ip in localIps)
    {
        sb.Append($"DNS.{dnsIndex}={ip}\n");
        dnsIndex++;
    }

    sb.Append("IP.1=127.0.0.1\n");
    sb.Append("IP.2=::1\n");
    int ipIndex = 3;
    foreach (var ip in localIps)
    {
        sb.Append($"IP.{ipIndex}={ip}\n");
        ipIndex++;
    }

    File.WriteAllText(path, sb.ToString());
}

string GenerateRandomPassword(int length)
{
    const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    var sb = new StringBuilder();
    var rnd = new Random();
    for (int i = 0; i < length; i++)
        sb.Append(chars[rnd.Next(chars.Length)]);
    return sb.ToString();
}

string ConvertToCString(string text)
{
    var sb = new StringBuilder();
    sb.Append("#define ROOT_CA_CERT \\\n");
    using (var reader = new StringReader(text))
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            sb.Append($"\"{line}\\n\" \\\n");
        }
    }
    return sb.ToString();
}

void CopyPfxToBlazorApp(string sourcePfx, string targetRelativePath)
{
    try
    {
        string currentDir = Directory.GetCurrentDirectory();
        string targetFullPath = Path.Combine(currentDir, targetRelativePath);

        Console.WriteLine($"\nCopying PFX to: {targetFullPath}");

        string? targetDir = Path.GetDirectoryName(targetFullPath);
        if (!Directory.Exists(targetDir) && targetDir != null)
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Copy(sourcePfx, targetFullPath, overwrite: true);

        Console.WriteLine("PFX file successfully copied.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error copying PFX file: {ex.Message}");
    }
}

void UpdateAppSettingsWithPassword(string appSettingsRelativePath, string password)
{
    try
    {
        string currentDir = Directory.GetCurrentDirectory();
        string appSettingsFullPath = Path.Combine(currentDir, appSettingsRelativePath);

        Console.WriteLine($"\nUpdating appsettings.json at: {appSettingsFullPath}");

        if (!File.Exists(appSettingsFullPath))
        {
            Console.WriteLine("appsettings.json not found. Creating a new one.");
            var newJson = new JsonObject
            {
                ["CertificatesConfig"] = new JsonObject
                {
                    ["CertificateAutoGeneratedPassword"] = password
                }
            };

            string newJsonString = JsonSerializer.Serialize(newJson, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(appSettingsFullPath, newJsonString);
            Console.WriteLine("appsettings.json created and updated successfully.");
            return;
        }

        string jsonText = File.ReadAllText(appSettingsFullPath);
        var jsonNode = JsonNode.Parse(jsonText) ?? new JsonObject();

        if (jsonNode["CertificatesConfig"] == null)
        {
            jsonNode["CertificatesConfig"] = new JsonObject();
        }

        jsonNode["CertificatesConfig"]!["CertificateAutoGeneratedPassword"] = password;

        string updatedJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(appSettingsFullPath, updatedJson);

        Console.WriteLine("appsettings.json updated successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating appsettings.json: {ex.Message}");
    }
}

void CopyCaToSecretsFile(string secretsRelativePath, string caCString)
{
    try
    {
        string currentDir = Directory.GetCurrentDirectory();
        string secretsFullPath = Path.Combine(currentDir, secretsRelativePath);

        Console.WriteLine($"\nUpdating secrets.h at: {secretsFullPath}");

        if (!File.Exists(secretsFullPath))
        {
            Console.WriteLine("secrets.h not found. Creating a new one.");
            File.WriteAllText(secretsFullPath, caCString + Environment.NewLine);
            Console.WriteLine("secrets.h created with CA certificate.");
            return;
        }

        var lines = File.ReadAllLines(secretsFullPath).ToList();

        int index = lines.FindLastIndex(line => line.TrimStart().StartsWith("#define ROOT_CA_CERT"));
        if (index != -1)
        {
            lines.RemoveRange(index, lines.Count - index);
        }

        lines.AddRange(caCString.Split('\n'));

        File.WriteAllLines(secretsFullPath, lines);

        Console.WriteLine("secrets.h updated successfully with new CA certificate.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating secrets.h: {ex.Message}");
    }
}

void ImportCaToTrustedRoot(string caCertFile)
{
    try
    {
        string caCertFullPath = Path.Combine(Directory.GetCurrentDirectory(), caCertFile);

        Console.WriteLine($"\nImporting CA certificate into Trusted Root store: {caCertFullPath}");

        RemoveExistingCaFromTrustedRoot("MyLocalCA");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Import-Certificate -FilePath '{caCertFullPath}' -CertStoreLocation Cert:\\LocalMachine\\Root\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(output))
            Console.WriteLine(output);
        if (!string.IsNullOrEmpty(error))
            Console.WriteLine(error);

        if (process.ExitCode == 0)
        {
            Console.WriteLine("CA certificate imported into Trusted Root store successfully.");
        }
        else
        {
            Console.WriteLine("Failed to import CA certificate. Run this app as Administrator to import into the Local Machine Trusted Root store.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error importing CA certificate: {ex.Message}");
    }
}

void RemoveExistingCaFromTrustedRoot(string commonName)
{
    try
    {
        Console.WriteLine($"Removing existing '{commonName}' certificates from Trusted Root store...");

        string command = $"Get-ChildItem Cert:\\LocalMachine\\Root | Where-Object {{ $_.Subject -eq 'CN={commonName}' }} | Remove-Item";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(output))
            Console.WriteLine(output);
        if (!string.IsNullOrEmpty(error))
            Console.WriteLine(error);

        if (process.ExitCode == 0)
        {
            Console.WriteLine($"Existing '{commonName}' certificates removed.");
        }
        else
        {
            Console.WriteLine($"Could not remove existing '{commonName}' certificates. Run this app as Administrator.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error removing existing CA certificates: {ex.Message}");
    }
}

