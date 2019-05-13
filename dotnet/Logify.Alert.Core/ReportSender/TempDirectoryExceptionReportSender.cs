//using Google.Apis.Auth.OAuth2;
//using Google.Apis.Gmail.v1;
//using Google.Apis.Gmail.v1.Data;
//using Google.Apis.Services;
//using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevExpress.Logify.Core.Internal
{
    //public class GmailSender
    //{
    //    static string ApplicationName = "error reporter";
    //    static GmailSender instance;
    //    static string[] Scopes = { GmailService.Scope.GmailSend };
    //    UserCredential credential;
    //    GmailService service;

    //    public void Init()
    //    {
    //        using (var stream =
    //            new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
    //        {
    //            string credPath = "token.json";
    //            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
    //                GoogleClientSecrets.Load(stream).Secrets,
    //                Scopes,
    //                "user",
    //                CancellationToken.None,
    //                new FileDataStore(credPath, true)).Result;
    //            Console.WriteLine("Credential file saved to: " + credPath);
    //            service = new GmailService(new BaseClientService.Initializer()
    //            {
    //                HttpClientInitializer = credential,
    //                ApplicationName = ApplicationName,
    //            });
    //        }
    //    }

    //    public void SendIt(string message)
    //    {
    //        var mailMessage = new System.Net.Mail.MailMessage();
    //        mailMessage.From = new System.Net.Mail.MailAddress("apportodriveerrors@e.e");
    //        mailMessage.To.Add("romankalachik@gmail.com");
    //        mailMessage.Subject = "error report";
    //        mailMessage.Body = message;
    //        mailMessage.IsBodyHtml = false;
    //        MemoryStream ms = new MemoryStream();
    //        ToEMLStream(mailMessage, ms);
    //        ms.Flush();
    //        ms.Position = 0;
    //        StreamReader reader = new StreamReader(ms);
    //        string text = reader.ReadToEnd();
    //        ms.Dispose();
    //        var result = service.Users.Messages.Send(new Message
    //        { 
    //            Raw = text,
    //        }, "me").Execute();
    //        Console.WriteLine($"Message ID {result.Id} sent.");
    //    }
    //    public static void ToEMLStream(MailMessage msg, Stream str)
    //    {
    //        using (var client = new SmtpClient())
    //        {
    //            var id = Guid.NewGuid();

    //            var tempFolder = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);

    //            tempFolder = Path.Combine(tempFolder, "MailMessageToEMLTemp");

    //            tempFolder = Path.Combine(tempFolder, id.ToString());

    //            if (!Directory.Exists(tempFolder))
    //            {
    //                Directory.CreateDirectory(tempFolder);
    //            }

    //            client.UseDefaultCredentials = true;
    //            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
    //            client.PickupDirectoryLocation = tempFolder;
    //            client.Send(msg);

    //            var filePath = Directory.GetFiles(tempFolder)[0];

    //            using (var fs = new FileStream(filePath, FileMode.Open))
    //            {
    //                fs.CopyTo(str);
    //            }

    //            if (Directory.Exists(tempFolder))
    //            {
    //                Directory.Delete(tempFolder, true);
    //            }
    //        }
    //    }

    //    public static GmailSender Default {
    //        get {
    //            if (instance == null) { instance = new GmailSender(); instance.Init(); }
    //            return instance;
    //        }
    //    }
    //}
    public class OfflineDirectoryExceptionReportSender : ExceptionReportSenderSkeleton, IOfflineDirectoryExceptionReportSender
    {
        internal const string TempFileNameExtension = "alert";
        internal const string TempFileNamePrefix = "LogifyR_";

        public OfflineDirectoryExceptionReportSender()
        {
            ReportCount = 100;
            DirectoryName = "offline_reports";
        }

        string CreateTempFileName(string directoryName)
        {
            DateTime now = DateTime.Now;
            string fileNameTemplate = String.Format(TempFileNamePrefix + "{0}_{1}_{2}_{3}_{4}_{5}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            for (int i = 0; i < 100; i++)
            {
                string fileName = fileNameTemplate;
                if (i != 0)
                    fileName += "_" + i.ToString();
                fileName += "." + TempFileNameExtension;
                string fullPath = Path.Combine(directoryName, fileName);
                if (!File.Exists(fullPath))
                    return fullPath;
            }
            return String.Empty;
        }
        void EnsureHaveSpace()
        {
            try
            {
                int reportCount = Math.Max(0, ReportCount - 1);
                string[] fileNames = Directory.GetFiles(DirectoryName, TempFileNamePrefix + "*." + TempFileNameExtension);
                if (fileNames == null || fileNames.Length <= reportCount)
                    return;

                List<FileInfo> files = new List<FileInfo>();
                foreach (string fileName in fileNames)
                    files.Add(new FileInfo(fileName));

                files.Sort(new FileInfoDateTimeComparer());

                int limit = files.Count - reportCount;
                for (int i = 0; i < limit; i++)
                {
                    try
                    {
                        files[i].Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        protected override bool SendExceptionReportCore(LogifyClientExceptionReport report)
        {
            try
            {
                if (!Directory.Exists(DirectoryName))
                    Directory.CreateDirectory(DirectoryName);

                if (!Directory.Exists(DirectoryName))
                    return false;

                lock (typeof(OfflineDirectoryExceptionReportSender))
                {
                    EnsureHaveSpace();
                    string fileName = CreateTempFileName(DirectoryName);
                    if (String.IsNullOrEmpty(fileName))
                        return false;

                    Encoding encoding = Encoding;
                    if (encoding == null)
                        encoding = Encoding.UTF8;
                    //GmailSender.Default.SendIt(report.ReportString);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public override bool CanSendExceptionReport()
        {
            return IsEnabled && !String.IsNullOrEmpty(DirectoryName) && base.CanSendExceptionReport();
        }
        public override void CopyFrom(IExceptionReportSender instance)
        {
            base.CopyFrom(instance);
            OfflineDirectoryExceptionReportSender other = instance as OfflineDirectoryExceptionReportSender;
            if (other == null)
                return;

            DirectoryName = other.DirectoryName;
            Encoding = other.Encoding;
            ReportCount = other.ReportCount;
            IsEnabled = other.IsEnabled;
        }

        public override IExceptionReportSender CreateEmptyClone()
        {
            return new OfflineDirectoryExceptionReportSender();
        }

        public string DirectoryName { get; set; }
        public Encoding Encoding { get; set; }
        public bool IsEnabled { get; set; }
        public int ReportCount { get; set; }

        class FileInfoDateTimeComparer : IComparer<FileInfo>
        {
            public int Compare(FileInfo x, FileInfo y)
            {
                return Comparer<DateTime>.Default.Compare(x.LastWriteTime, y.LastWriteTime);
            }
        }

#if ALLOW_ASYNC
        protected override Task<bool> SendExceptionReportCoreAsync(LogifyClientExceptionReport report)
        {
            return Task.FromResult(SendExceptionReportCore(report));
        }
#endif
    }
}