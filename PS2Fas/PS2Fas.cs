using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Digests;
using WebSocketSharp;
using Newtonsoft.Json;
using QRCoder;

namespace PS2Fas
{
    public class PS2FasInstance
    {
        public const String restapi = "https://api2.2fas.com";
        public const String wssapi = "wss://ws.2fas.com";
        public const String qruri = "twofas_c://";
        public static String configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".ps2fas");
        public static String configFilePath = Path.Combine(configPath, "id.conf");
        public static String pubPemFilePath = Path.Combine(configPath, "pub.pem");
        public static String rsaPemFilePath = Path.Combine(configPath, "rsa.pem");
        public String extensionId;
        private static readonly HttpClient client = new HttpClient();
        public int timeout;

        public PS2FasInstance(int timeout)
        {
            if (!CheckRegister())
            {
                //must call static Register method before construnction
                throw new Exception("Call Connect-2Fas to do inital config");
            }

            extensionId = File.ReadLines(configFilePath).First();
            this.timeout = timeout;
        }

        static PS2FasInstance()
        {
            client.BaseAddress = new Uri(restapi);
        }

        //Creates config files - id.conf, rsa.pem and pub.pem
        public static void Register(bool force)
        {
            if (File.Exists(configPath) && (force != true))
            {
                throw new Exception("Already Configured");
            }

            if (force)
            {
                Directory.Delete(configPath, true);
            }

            Directory.CreateDirectory(configPath);
            GenRSAKey();
            String modPem = File.ReadAllText(pubPemFilePath);
            modPem = modPem.Replace("-----BEGIN PUBLIC KEY-----", "");
            modPem = modPem.Replace("-----END PUBLIC KEY-----", "");
            modPem = modPem.Replace("\r", "");
            modPem = modPem.Replace("\n", "");
            BrowserInfo browserInfo = new BrowserInfo(modPem);
            var browserInfoJson = JsonConvert.SerializeObject(browserInfo);
            //createExtensionInstance
            var content = new StringContent(browserInfoJson, Encoding.UTF8, "application/json");
            var response = client.PostAsync("browser_extensions", content).Result;
            RegResponse resp = JsonConvert.DeserializeObject<RegResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            TextWriter tw = new StreamWriter(configFilePath);
            tw.WriteLine(resp.id);
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode((qruri + resp.id), QRCodeGenerator.ECCLevel.H);
            AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);
            string qrCodeAsAsciiArt = qrCode.GetGraphic(1);
            tw.WriteLine(qrCodeAsAsciiArt);
            tw.Flush();
            tw.Close();
            content.Dispose();
            response.Dispose();
        }

        public static bool CheckRegister()
        {
            if (Directory.Exists(configPath))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void GenRSAKey()
        {
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);

            AsymmetricCipherKeyPair keyPair;
            KeyGenerationParameters keyGenerationParameters = new KeyGenerationParameters(random, 2048);
            RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            keyPair = keyPairGenerator.GenerateKeyPair();
            var pubStringWriter = new StringWriter();
            var pubPemWriter = new PemWriter(pubStringWriter);
            pubPemWriter.WriteObject(keyPair.Public);
            pubStringWriter.Flush();
            pubStringWriter.Close();

            File.WriteAllText(pubPemFilePath, pubStringWriter.ToString());

            var stringWriter = new StringWriter();
            var pemWriter = new PemWriter(stringWriter);
            pemWriter.WriteObject(keyPair.Private);
            stringWriter.Flush();
            stringWriter.Close();

            File.WriteAllText(rsaPemFilePath, stringWriter.ToString());
        }

        //methods from browser extension and maybe future to dos
        //createExtensionInstance - in Register()
        //updateBrowserExtension - not implmented
        //getAllPairedDevices - not implmented
        //removePairedDevice - not implmented
        //request2FAToken - in GetOTP
        //close2FARequest - in GetOTP

        public String GetOTP(String domain)
        {
            //TO DO: Add better validation and parsing of domains
            
            String domainJson = "{\"domain\": \"" + domain + "\"}";
            var rtcontent = new StringContent(domainJson, Encoding.UTF8, "application/json");
            var rtresponse = client.PostAsync($"/browser_extensions/{extensionId}/commands/request_2fa_token", rtcontent).Result;
            var token_request = JsonConvert.DeserializeObject<OTPEvent>(rtresponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var token_request_id = token_request.token_request_id;
            rtcontent.Dispose();
            rtresponse.Dispose();
            OTPEvent token_event = null;

            bool abort = false;

            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                abort = true;
            };

            using (var ws = new WebSocket(wssapi + $"/browser_extensions/{extensionId}/2fa_requests/{token_request_id}"))
            {
                Action cleanup = () =>
                {
                    var errorjson = "{\"status\": \"terminated\"}";
                    var ercontent = new StringContent(errorjson, Encoding.UTF8, "application/json");
                    var erresponse = client.PostAsync($"browser_extensions/{extensionId}/2fa_requests/{token_request_id}/commands/close_2fa_request", ercontent).Result;
                    var errtext = erresponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    ercontent.Dispose();
                    erresponse.Dispose();
                    ws.Close();
                };

                ws.OnMessage += (sender, e) =>
                { 
                    token_event = JsonConvert.DeserializeObject<OTPEvent>(e.Data); 
                };

                ws.Connect();
                DateTime y = (DateTime.Now).AddMinutes(timeout);

                do
                {
                    if (abort)
                    {
                        cleanup();
                        break;
                    }
                    
                    if (y <= DateTime.Now)
                    {
                        cleanup();
                        throw new Exception("Get token timed out");
                    }
                    
                    Thread.Sleep(1000);

                } while (token_event == null || token_event.status == "pending");

                ws.Close();
            }

            if (abort)
            {
                throw new Exception("User aborted");
            }

            var closejson = "{\"status\": \"completed\"}";
            var clcontent = new StringContent(closejson, Encoding.UTF8, "application/json");
            var clresponse = client.PostAsync($"browser_extensions/{extensionId}/2fa_requests/{token_request_id}/commands/close_2fa_request", clcontent).Result;
            var text = clresponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clcontent.Dispose();
            clresponse.Dispose();

            byte[] data = Convert.FromBase64String(token_event.token);

            PemReader pr = new PemReader(File.OpenText(rsaPemFilePath));
            AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
            pr.Dispose();
            var cipher = new OaepEncoding(new RsaEngine(),new Sha512Digest());
            cipher.Init(false, KeyPair.Private);
            var decrypted = Encoding.UTF8.GetString(cipher.ProcessBlock(data, 0, data.Length));

            return decrypted;
        }
    }

    //Helper class for registering
    public class BrowserInfo
    {
        public string name { get; set; } = (Environment.MachineName + " PS2FAS");
        public string browser_name { get; set; } = "PS2FAS";
        public string browser_version { get; set; } = "1.0.0";
        public string public_key { get; set; }

        public BrowserInfo(String public_key)
        {
            this.public_key = public_key;
        }
    }
    //Helper class for registering
    public class RegResponse
    {
        public string name { get; set; }
        public string browser_name { get; set; }
        public string browser_version { get; set; }
        public string id { get; set; }
    }
    //Websocket helper class
    public class OTPEvent
    {
        public string Event { get; set; }
        public string token { get; set; }
        public string status { get; set; }
        public string token_request_id { get; set; }
    }
    
    //PowerShell Cmdlets

    [Cmdlet(VerbsCommunications.Connect, "2Fas")]
    [OutputType(typeof(String))]
    public class Connect2Fas : PSCmdlet
    {
        [Parameter(Position = 0)]
        public SwitchParameter Force
        {
            get { return force; }
            set { force = value; }
        }
        private bool force;

        // This method will be called for each input received from the pipeline to this cmdlet; if no input is received, this method is not called
        protected override void ProcessRecord()
        {
            if (PS2FasInstance.CheckRegister() && (Force != true))
            {
                ErrorRecord error = new ErrorRecord(new Exception("Only call Connect-2FAS to do inital config, to delete and replace config use -Force"), "Configured", ErrorCategory.ConnectionError, this);
                WriteError(error);
                return;
            }

            PS2FasInstance.Register(force);
            String regResult = "Registration Successful\n" + File.ReadAllText(PS2FasInstance.configFilePath) + "Scan above QRCode in 2FAS under Settings->Browser Extention->Add New\n";

            WriteObject(regResult);
        }
    }


    [Cmdlet(VerbsCommon.Get, "2FasOTP")]
    [OutputType(typeof(String))]
    public class Get2FasOTP : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public String Domain { get; set; }
        [Parameter(
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public int Timeout { get; set; } = 2;

        // This method will be called for each input received from the pipeline to this cmdlet; if no input is received, this method is not called
        protected override void ProcessRecord()
        {
            if (!PS2FasInstance.CheckRegister())
            {
                ErrorRecord error = new ErrorRecord(new Exception("Call Connect-2Fas to do inital config"), "NotConfigured", ErrorCategory.ConnectionError, this);
                WriteError(error);
                return;
            }

            WriteVerbose("Setting up client");
            PS2FasInstance client = new PS2FasInstance(Timeout);
            String OTP = null;

            WriteVerbose("Getting OTP");
            try
            {
                OTP = client.GetOTP(Domain);
            }
            catch (Exception e)
            {
                ErrorRecord error = new ErrorRecord(e, "TimeOut", ErrorCategory.ConnectionError, this);
                WriteError(error);
                return;
            }
            WriteObject(OTP);
        }
    }
}
