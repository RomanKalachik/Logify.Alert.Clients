using Google.Apis.Auth.OAuth2;
using Google.Apis.Clouderrorreporting.v1beta1;
using Google.Apis.Clouderrorreporting.v1beta1.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Google.Apis.Clouderrorreporting.v1beta1.ProjectsResource.EventsResource;

namespace DevExpress.Logify.Core.Internal
{
    public class ErrorReporter
    {
        public static string CredentailsFileName { get; set; }
        public static string ProjectID { get; set; }
        public static string ProjectVersion { get; set; }

        private static ClouderrorreportingService CreateErrorReportingClient()
        {
            GoogleCredential credential = GoogleCredential.FromFile(CredentailsFileName);
            credential = credential.CreateScoped(ClouderrorreportingService.Scope.CloudPlatform);
            ClouderrorreportingService service = new ClouderrorreportingService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
            });
            return service;
        }

        static ClouderrorreportingService service;
        /// <summary>
        /// Creates a <seealso cref="ReportRequest"/> from a given exception.
        /// </summary>
        private static ReportRequest CreateReportRequest(LogifyClientExceptionReport report)
        {
            if (service == null)
                service = CreateErrorReportingClient();
            string projectId = ProjectID;

            string formattedProjectId = $"projects/{projectId}";

            ServiceContext serviceContext = new ServiceContext()
            {
                Service = "service_account",
                Version = ProjectVersion,
            };
            ReportedErrorEvent errorEvent = new ReportedErrorEvent()
            {
                Message = report.ReportString,
                ServiceContext = serviceContext, Context = new ErrorContext() { ReportLocation = new SourceLocation(  ) { FilePath="test", LineNumber=1,  FunctionName="test"} }
            };
            return new ReportRequest(service, errorEvent, formattedProjectId);
        }

        public static void SendReport(LogifyClientExceptionReport report)
        {
            ReportRequest request = CreateReportRequest(report);
            request.Execute();
        }
    }


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
                //if (!Directory.Exists(DirectoryName))
                //    Directory.CreateDirectory(DirectoryName);

                //if (!Directory.Exists(DirectoryName))
                //    return false;

                lock (typeof(OfflineDirectoryExceptionReportSender))
                {
                    //EnsureHaveSpace();
                    //string fileName = CreateTempFileName(DirectoryName);
                    //if (String.IsNullOrEmpty(fileName))
                    //    return false;

                    //Encoding encoding = Encoding;
                    //if (encoding == null)
                    //    encoding = Encoding.UTF8;
                    ErrorReporter.SendReport(report);
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