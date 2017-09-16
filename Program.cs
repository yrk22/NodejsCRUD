using System.IO;
using System;
using System.Linq;
using LittleChapel;
using Amazon.S3;
using log4net;
using Amazon.S3.Transfer;
using System.Reflection;
using System.Collections.Generic;
using PhotoAPI;
using System.Diagnostics;
using Amazon.S3.Model;
using Amazon.S3.IO;
using System;
using System.Timers;

namespace VideoDownloadGenerator
{
    class Program
    {
        static ILog Log4net = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        static WMS wms = new WMS();
        static FileSystemWatcher watcher;
        public static Plan Plan { get; set; }
        public static string FileSize { get; set; }
        public static decimal UploadFileSize;
        public static string UploadingFileSize;
        public static string UploadStartDateTime;
        public static string UploadEndDateTime;
        public static int PreviousPlanID;
        static void Main(string[] args)
        {
            try
            {
                //actual time:7500000 
                Console.Title = "S3 Video Uploader";
                Log4net.Info("----------------Ceremony S3 Video Uploader------------------------");
                Log4net.Info("\n");
                Log4net.Info("\n");
                string strVideoChapelLocation = string.Empty;
#if DEBUG
                strVideoChapelLocation = Convert.ToString(Settings.OriginalLocal);
#else
                strVideoChapelLocation = Convert.ToString(Settings.Original);
#endif
                watcher = new FileSystemWatcher();
                watcher.Path = strVideoChapelLocation;
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                Log4net.Info("Monitoring Chapel Folder.");
                watcher.Changed += OnChanged;
                watcher.Error += OnErrorWatch;
                watcher.Filter = "*.*";
                watcher.EnableRaisingEvents = true;
                Log4net.Info("\n");
                Log4net.Info("Folders being Monitored are : " + strVideoChapelLocation);
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log4net.Info("Error : " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
            }
            Console.ReadLine();
        }
        public static void timer_Elapsed(Timer timer)
        {
            try
            {
                timer.Stop();
                timer.Dispose();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log4net.Info("Error in Timer Elapsed:102," + ex.Message);
                Environment.Exit(0);
            }
        }
        static readonly string[] SizeSuffixes =
                  { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }

            int i = 0;
            decimal dValue = (decimal)value;
            while (Math.Round(dValue, decimalPlaces) >= 1000)
            {
                dValue /= 1024;
                i++;
            }
            return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
        }
        public static void HighLevelApi(string FileName, string filePath, int DownloadRequestID, int PlanID)
        {
            TransferUtility fileTransferUtility = new
                        TransferUtility(new AmazonS3Client(Settings.AWSAccessKey, Settings.AWSSecretKey, Amazon.RegionEndpoint.USEast1));
            var timer = new Timer();
            try
            {
                // 1.Specify advanced settings/options.
                UploadStartDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log4net.Info("\tStarted Uploading file to AmazonS3Client. Upload Start Time : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                TransferUtilityUploadRequest fileTransferUtilityRequest = new TransferUtilityUploadRequest
                {
                    BucketName = Settings.VideoBucketWedding,
                    FilePath = filePath,
                    StorageClass = S3StorageClass.ReducedRedundancy,
                    PartSize = 6291456, // 6 MB.
                    Key = "wedding/" + FileName,
                    CannedACL = S3CannedACL.PublicRead
                };
                fileTransferUtilityRequest.UploadProgressEvent += new EventHandler<UploadProgressArgs>
                    (uploadRequest_UploadPartProgressEvent);

                timer.Interval = 7500000;
                timer.Start();
                timer.Elapsed += (sender, e) => timer_Elapsed(timer);
                fileTransferUtility.Upload(fileTransferUtilityRequest);
                timer.Stop();
                timer.Dispose();
                UploadEndDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                //if ((UploadFileSize > 500 && UploadingFileSize.Contains("MB")) || (UploadFileSize > 1 && UploadingFileSize.Contains("GB")))
                //{
                //    SendUploadCompletedEmail(Plan.PlanID);
                //}
                Log4net.Info("\tFinished Uploading file to AmazonS3Client. Upload End Time : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                UpdateDB(DownloadRequestID, PlanID);
            }
            catch (Exception ex)
            {
                timer.Dispose();
                UpdateFailedStatus();
                Log4net.Info("Line:118,Exception occurred: " + ex.Message);
                Log4net.Info("Line:119,Exception occurred for PlanID: " + FileName);
                AbortingMultipartUpload(fileTransferUtility);
                SendErrorEmail(ex, Plan.PlanID);
            }
        }
        static void uploadRequest_UploadPartProgressEvent(object sender, UploadProgressArgs e)
        {
            // Process event.
            Log4net.Info(e.TransferredBytes / e.TotalBytes + "Progress:" + e.PercentDone);
        }
        public static void NormalApi(string strVideoFileName, string strDestinationFolder, int DownloadRequestID, int PlanID)
        {
            var timer = new Timer();
            try
            {
                using (var client = new AmazonS3Client(Settings.AWSAccessKey, Settings.AWSSecretKey, Amazon.RegionEndpoint.USEast1))
                {
                    TransferUtility fileTransferUtility = new TransferUtility(client);
                    TransferUtilityUploadRequest requests = new TransferUtilityUploadRequest()
                    {
                        BucketName = Settings.VideoBucketWedding,
                        Key = "wedding/" + strVideoFileName,
                        FilePath = strDestinationFolder,
                        CannedACL = S3CannedACL.PublicRead,
                        StorageClass = S3StorageClass.ReducedRedundancy
                    };
                    timer.Interval = 7500000;
                    timer.Start();
                    timer.Elapsed += (sender, e) => timer_Elapsed(timer);
                    fileTransferUtility.Upload(requests);
                    timer.Stop();
                    timer.Dispose();
                }
                UpdateDB(DownloadRequestID, PlanID);
            }
            catch (Exception ex)
            {
                UpdateFailedStatus();
                Log4net.Info("Line:140,Exception occurred: " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
                timer.Dispose();
            }
        }
        public static void UpdateDB(int DownloadRequestID, int PlanID)
        {
            try
            {
                Log4net.Info("\tUpdating DownloadRequests Table.");
                string Query = "update [WMS].[dbo].[DownloadRequest] set StatusID=2,CompleteDate='" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "' where DownloadRequestID=" + DownloadRequestID + " and planid =" + PlanID;
                wms.ExecuteCommand(Query);
                Log4net.Info("\tUpdated DownloadRequests Table.");
            }
            catch (Exception ex)
            {
                Log4net.Info("Line:157,Exception occurred: " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
            }
        }
        public static void AbortingMultipartUpload(TransferUtility fileTransferUtility)
        {
            try
            {
                fileTransferUtility.AbortMultipartUploads(
                Settings.VideoBucketWedding, DateTime.Now.AddDays(-2));
            }
            catch (Exception e)
            {
                Log4net.Info("Upload Aborting failed: " + e.Message);
            }
        }

        public static string RawFolderStructure
        {
            get
            {
                return Plan.WeddingDateTime.Value.ToString("MM_yy") + "\\" + Plan.WeddingDateTime.Value.ToString("MMddyy") + "\\" + Plan.Customer.FamilyName.Trim() + "_" + Plan.Customer.GroomNameFirst.Trim()[0] + "&" + Plan.Customer.BrideNameFirst.Trim()[0] + "_" + Plan.WeddingDateTime.Value.ToString("MMddyy") + "\\CeremonyVideo\\";
            }
        }
        public static void UpdateFailedStatus()
        {
            try
            {
                Log4net.Info("Line:185,Entered to update failed status");
                var DownloadRequest = wms.DownloadRequests.Where(c => c.PlanID == Plan.PlanID && c.DownloadType == DownloadType.Video).FirstOrDefault();
                DownloadRequest.Status = DownloadStatus.Failed;
                wms.SubmitChanges();
                Log4net.Info("Line:189,Updated failed status.DownloadRequest:" + DownloadRequest.DownloadRequestID);
            }
            catch (Exception ex)
            {
                Log4net.Info("Error:192,Failed to Update Download Requests: " + ex.Message);
            }

        }
        public static void OnErrorWatch(object source, ErrorEventArgs e)
        {
            Log4net.Info("Error : " + e.GetException().Message);
        }
        public static void OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                Log4net.Info("Entered OnChanged Event");
                Chapel(watcher.Path);
                Log4net.Info("Leaving OnChanged Event");
            }
        }
        public static void Chapel(string Path)
        {
            try
            {
                //System.Threading.Thread.Sleep(10000);
                Log4net.Info("Entered Chapel Function.");
                string[] strFileNames = Directory.GetFiles(Path, "*.mp4", SearchOption.AllDirectories);
                int PlanID;
                if (strFileNames.Length > 0)
                {
                    Log4net.Info("Number of Files are :" + strFileNames.Length);
                    for (int intR = 0; intR < strFileNames.Length; intR++)
                    {
                        FileInfo objFileInfo = new FileInfo(strFileNames[intR]);
                        string strFileName = objFileInfo.Name;
                        string strFinalPathName = string.Empty;
#if DEBUG
                        strFinalPathName = Settings.FinalPathLocal + strFileName;
#else
                        strFinalPathName = Settings.FinalPath + strFileName;
#endif
                        List<string> List = new List<string>();
                        string result = String.Empty;
                        Process ffmpeg = new Process();
                        Log4net.Info("\t--------- Initialising ffmpeg process ----------");
                        ffmpeg.StartInfo.UseShellExecute = false;
                        ffmpeg.StartInfo.CreateNoWindow = true;
                        ffmpeg.StartInfo.RedirectStandardOutput = true;
                        ffmpeg.StartInfo.Arguments = "-i " + Convert.ToString(strFileNames[intR]) + " -c:a copy -c:v copy -movflags faststart " + strFinalPathName;
                        //ffmpeg.StartInfo.Arguments = "-i " + Spegpaths + " -metadata major_brand=qt  -metadata minor_version=0  -metadata compatible_brands=qt " +
                        //    " -metadata com.apple.quicktime.title=\"Our Wedding\"  -metadata com.apple.quicktime.keywords=5-5-16 -metadata com.apple.quicktime.description=\"This video is of Our Wedding\"  -metadata com.apple.quicktime.author=\"Videographer\" -metadata encoder=\"Lavf57.62.101\"  -codec copy " + @"D:\ffmpeg\Mpeg\Mpeg\ffmpegS\11553061.mp4";
#if DEBUG
                        ffmpeg.StartInfo.FileName = Settings.FFMPEGLocal;
#else
                        ffmpeg.StartInfo.FileName = Settings.FFMPEG;
#endif
                        string strPlanFilePathName = System.IO.Path.GetFileName(strFinalPathName);
                        bool blnSuccess = int.TryParse(strPlanFilePathName.Replace(".mp4", ""), out PlanID);
                        if(PlanID==PreviousPlanID)
                        {
                            Environment.Exit(0);
                        }
                        PreviousPlanID = PlanID;
                        ffmpeg.StartInfo.RedirectStandardOutput = true;
                        ffmpeg.StartInfo.RedirectStandardError = true;
                        ffmpeg.OutputDataReceived += (s, es) =>
                        {
                            lock (result)
                            {
                                List.Add(es.Data);
                            }
                        };
                        ffmpeg.ErrorDataReceived += (s, et) =>
                        {
                            lock (result)
                            {
                                List.Add("!> " + et.Data);
                            }
                        };
                        Log4net.Info("\t--------- Starting ffmpeg process ----------");
                        System.Threading.Thread.Sleep(10000);
                        ffmpeg.Start();
                        System.Threading.Thread.Sleep(10000);
                        //ffmpeg.PriorityClass = ProcessPriorityClass.High;
                        ffmpeg.BeginErrorReadLine();
                        ffmpeg.BeginOutputReadLine();
                        ffmpeg.WaitForExit();
                        Log4net.Info("\t--------- Finished ffmpeg process ----------");
                        Log4net.Info("\t--------- Metadata ----------");
                        foreach (string sline in List)
                        {
                            //sw.WriteLine(sline);
                            Log4net.Info("\t " + sline);
                            //Console.WriteLine(sline);
                        }
                        Log4net.Info("\t--------- ******** ----------");
                        ffmpeg.Close();

                        
                        if (blnSuccess)
                        {
                            Log4net.Info("\t OnChanged Working on PlanID :" + PlanID);
                            List<Plan> objPlan = API.GetPlan(PlanID, wms);
                            if (objPlan != null && objPlan.Count > 0)
                            {
                                Plan = objPlan[0];
                                if (objPlan[0].CustomerID.HasValue)
                                {
                                    Log4net.Info("\t OnChanged Working on CustomerID :" + objPlan[0].CustomerID);
                                    List<Customer> objCust = API.GetCustomer(objPlan[0].CustomerID.Value, wms);
                                    Plan.Customer = objCust[0];
                                    if (Plan.Customer != null)
                                    {
                                        Log4net.Info("\t Checking if directory Exists :" + DestinationDirectory);
                                        if (!Directory.Exists(DestinationDirectory))
                                        {
                                            Log4net.Info("\t Creating directory :" + DestinationDirectory);
                                            Directory.CreateDirectory(DestinationDirectory);
                                        }
                                        if (Directory.Exists(DestinationDirectory))
                                        {
                                            DeleteFileOnS3(PlanID);
                                            try
                                            {
                                                if (File.Exists(DestinationDirectory + strPlanFilePathName))
                                                {
                                                    File.Delete(DestinationDirectory + strPlanFilePathName);
                                                }
                                                if (File.Exists(strFinalPathName))
                                                {
                                                    File.Move(strFinalPathName, DestinationDirectory + strPlanFilePathName);
                                                }
                                                else
                                                {
                                                    File.Move(Path + strPlanFilePathName, DestinationDirectory + strPlanFilePathName);
                                                }
                                                //if (File.Exists(Path + strPlanFilePathName))
                                                //{
                                                //    File.Delete(Path + strPlanFilePathName);
                                                //}
                                            }
                                            catch (Exception ex)
                                            {
                                                Log4net.Info("Line:320,Error has occurred" + ex.Message);
                                            }
                                            Log4net.Info("\t Moving File From Folder :" + (strFinalPathName) + " to DestinationFolder :" + DestinationDirectory);
                                            if (File.Exists(DestinationDirectory + strPlanFilePathName))
                                            {
                                                string strPath = DestinationDirectory + strPlanFilePathName;
                                                Upload(strPath, Plan.PlanID);
                                            }
                                            else
                                            {
                                                Log4net.Info("\t File Already Exists.");
                                            }
                                        }
                                        Log4net.Info("\tEntered Onchanged After Upload Function");
                                    }
                                }
                            }
                            Log4net.Info("\t OnChanged Finished Working on PlanID :" + PlanID);
                        }
                        else
                        {
                            Log4net.Error("\tFailed to parse the PlanID");
                        }
                        if (File.Exists(DestinationDirectory + strPlanFilePathName))
                        {
                            var DownloadRequests = wms.DownloadRequests.Where(x => x.PlanID == Plan.PlanID && x.DownloadType == DownloadType.Video && x.Status == DownloadStatus.Complete).FirstOrDefault();
                            if (DownloadRequests != null)
                            {
                                objFileInfo.Delete();
                            }
                        }
                    }
                }
                else
                {
                    Log4net.Info("No Files Found.");
                }
                Log4net.Info("Leaving ChapelorTrop Function.");

            }
            catch (Exception ex)
            {
                UpdateFailedStatus();
                Log4net.Info("Error 007: " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
            }
        }

        public static void DeleteFileOnS3(int PlanID)
        {
            try
            {
                Log4net.Info("Entered Delete File on S3 Function");
                using (var client = new AmazonS3Client(Settings.AWSAccessKey, Settings.AWSSecretKey, Amazon.RegionEndpoint.USEast1))
                {
                    S3FileInfo s3FileInfo = new Amazon.S3.IO.S3FileInfo(client, Settings.VideoBucketWedding, "wedding/" + PlanID + ".mp4");
                    if (s3FileInfo.Exists)
                    {
                        Log4net.Info("File exists on Amazon s3.");
                        client.DeleteObject(new Amazon.S3.Model.DeleteObjectRequest() { BucketName = Settings.VideoBucketWedding, Key = "wedding/" + PlanID + ".mp4" });
                        Log4net.Info("Deleted File on S3.");
                    }
                    else
                    {
                        Log4net.Info("File does not exist on Amazon s3.");
                    }
                }
                Log4net.Info("Leaving Delete File on S3 Function");
            }
            catch (Exception ex)
            {
                Log4net.Info("Line:355,Error : " + ex.Message);
            }
        }
        public static string DestinationDirectory
        {
            get
            {
                return Settings.DestinationDirectory + RawFolderStructure;
            }
        }
        public static void Upload(string strDestinationFolder, int PlanID)
        {
            DownloadRequest objDownloadRequestTable = null;
            try
            {
                Log4net.Info("\t Entered Upload Function");
                DateTime? VideoUploadDateTime = null;
                int DownloadRequestID = 0;
                var objDownloadRequestComplete = wms.DownloadRequests.Where(c => c.PlanID == PlanID && c.DownloadType == DownloadType.Video && c.Status == DownloadStatus.Complete).FirstOrDefault();
                dynamic objDownloadRequest = null;
                if (objDownloadRequestComplete == null)
                {
                    objDownloadRequest = wms.DownloadRequests.Where(x => x.PlanID == PlanID && x.DownloadType == DownloadType.Video && x.Status == DownloadStatus.Pending).FirstOrDefault();
                    if (objDownloadRequest != null)
                    {
                        wms.DownloadRequests.DeleteOnSubmit(objDownloadRequest);
                        wms.SubmitChanges();
                    }
                    Log4net.Info("\t Making entry into DownloadRequests Table");
                    if (objDownloadRequestTable == null)
                    {
                        objDownloadRequestTable = new DownloadRequest();
                        objDownloadRequestTable.PlanID = PlanID;
                        objDownloadRequestTable.Status = DownloadStatus.Pending;
                        objDownloadRequestTable.CreateDate = DateTime.Now;
                        objDownloadRequestTable.DownloadType = DownloadType.Video;
                        objDownloadRequestTable.CompleteDate = null;
                        wms.DownloadRequests.InsertOnSubmit(objDownloadRequestTable);
                        wms.SubmitChanges();
                    }
                    VideoUploadDateTime = objDownloadRequestTable.CreateDate;
                    DownloadRequestID = objDownloadRequestTable.DownloadRequestID;
                    Log4net.Info("\t Completed Making Entry to DownloadRequests Table");
                }
                else
                {
                    VideoUploadDateTime = Convert.ToDateTime(objDownloadRequestComplete.CreateDate);
                    DownloadRequestID = objDownloadRequestComplete.DownloadRequestID;
                }
                Log4net.Info("\t Video Upload DateTime :" + VideoUploadDateTime);
                Log4net.Info("\t Download Request ID:" + DownloadRequestID);
                Log4net.Info("\t Upload, Working on PlanID :" + PlanID);
                var downloadsToCreate = wms.DownloadRequests.Where(c => c.DownloadType == DownloadType.Video && c.PlanID == PlanID && (c.DownloadRequestID == DownloadRequestID)).ToArray();
                Log4net.Info("\t Count of DownloadsToCreate : " + downloadsToCreate.Count());
                foreach (var downloadToCreate in downloadsToCreate)
                {
                    string strVideoFileName = Path.GetFileName(strDestinationFolder);
                    Log4net.Info("\n\tStarting Upload of Video FileName:" + strVideoFileName + " DateTimeStamp:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss tt"));
                    try
                    {
                        long contentLength = new FileInfo(strDestinationFolder).Length;
                        UploadingFileSize = SizeSuffix(contentLength, 0);
                        Log4net.Info("\tFile size is :" + UploadingFileSize);
                        FileSize = UploadingFileSize.Replace("MB", "").Replace("GB", "");
                        decimal.TryParse(FileSize, out UploadFileSize);
                        if ((UploadFileSize < 100 && UploadingFileSize.Contains("MB")))
                        {
                            NormalApi(strVideoFileName, strDestinationFolder, DownloadRequestID, PlanID);
                        }
                        else
                        {
                            HighLevelApi(strVideoFileName, strDestinationFolder, DownloadRequestID, PlanID);
                        }
                        SendEmailToGuest(PlanID, VideoUploadDateTime);
                    }
                    catch (Exception e)
                    {
                        var DownloadRequest = wms.DownloadRequests.Where(c => c.PlanID == PlanID && c.DownloadType == DownloadType.Video).FirstOrDefault();
                        DownloadRequest.Status = DownloadStatus.Failed;
                        wms.SubmitChanges();
                        SendErrorEmail(e, PlanID);
                        Log4net.Info("\n\tLine:468,Error occurred FileName:" + strVideoFileName + " DateTimeStamp:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss tt") + " Message " + e.Message);
                    }
                }
                Log4net.Info("\t------End of PlanID :" + PlanID);
                Log4net.Info("\tLeaving Upload Function");
            }
            catch (Exception ex)
            {
                UpdateFailedStatus();
                Log4net.Info("Line:431, Error : " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
            }
        }

        private static void SendEmailToGuest(int PlanID, DateTime? VideoUploadDateTime)
        {
            try
            {
                Log4net.Info("\n\tEntered Guest Email Function.");
                var cmpRequest = wms.Plans.Where(p => p.PlanID == PlanID).ToList();
                var cmpdetails = wms.Companies.Where(c => c.CompanyID == cmpRequest[0].CompanyID).ToList();
                var GuestsRequest = from K in wms.SalesOrders
                                    join U in wms.Customers on K.CustomerID equals U.CustomerID
                                    join Si in wms.SalesItems on K.SalesOrderID equals Si.SalesOrderID
                                    where K.PlanID == PlanID && U.CustomerType == 2 && Si.ProductID == 2341 && K.CreateDate <= VideoUploadDateTime
                                    select new
                                    {
                                        Email = U.Email,
                                        SalesItemID = Si.SalesItemID,
                                        CreatedDate = K.CreateDate
                                    };
                var GuestRequestList = GuestsRequest.ToList();
                if (GuestRequestList != null && GuestRequestList.Count() > 0 && cmpdetails != null && cmpdetails.Count() > 0)
                {
                    for (int intI = 1; intI <= GuestRequestList.Count(); intI++)
                    {
                        Log4net.Info("\n\tPlease wait while Email is being sent to guests.");
                        Email email = new Email(cmpdetails[0].CompanyID);
                        string body = LittleChapel.Resources.Emails.GuestVideoEmailDownload;
                        body = body.Replace("[CompanyName]", cmpdetails[0].Name);
                        if (cmpdetails[0].CompanyID == 1)
                        {
                            body = body.Replace("[URL]", "https://www.littlechapel.com/our-wedding/" + PlanID);
                            body = body.Replace("[DownloadCode]", "Download Code :" + GuestRequestList[intI - 1].SalesItemID);
                        }
                        else if (cmpdetails[0].CompanyID == 2)
                        {
                            body = body.Replace("[URL]", "https://www.tropicanalvweddings.com/our-wedding/" + PlanID);
                            body = body.Replace("[DownloadCode]", "Download Code :" + GuestRequestList[intI - 1].SalesItemID);
                        }
                        email.To = GuestRequestList[intI - 1].Email;
                        //email.To = "ramakrishna.murthy@people10.com";
                        email.Subject = "Wedding Video Available";
                        email.Body = body;
                        email.LoadTemplate();
                        if (!email.Send())
                        {
                            Log4net.Info("\n\t Guest's Email failed.");
                        }
                        else
                        {
                            Log4net.Info("\n\t Guest's Email Sent.");
                        }
                    }
                }
                else
                {
                    Log4net.Info("\n\t Guest's Email -Found no guest to send email.");
                }
                Log4net.Info("\n\t Leaving Guest Email Function.");

            }
            catch (Exception ex)
            {
                Log4net.Info("Line:510,Error : " + ex.Message);
                SendErrorEmail(ex, Plan.PlanID);
            }
        }
        private static void SendErrorEmail(Exception e, int planid)
        {
            Log4net.Info("\n\tSending Error Mail.");
            Email email = new Email();
            email.From = "info@littlechapel.com";
            email.To = "exceptions@everafter.com";
            email.CC = "devteam@everafter.com;product@everafter.com";
            email.Subject = "Video Download Generator Exception for plan id " + planid;
            email.Body = "<b>DateTime:</b>" + DateTime.Now + "<br /><b>Error Message :</b>" + e.Message + Environment.NewLine + "<br /><b>StackTrace:</b>" + e.StackTrace;
            if (!email.Send())
            {
                Log4net.Info("\n\tError Email failed.");
            }
            else
            {
                Log4net.Info("\n\ttError Email Sent.");
            }
        }
        private static void SendUploadCompletedEmail(int PlanID)
        {
            Log4net.Info("\n\tSending Upload Completed Email.");
            Email email = new Email();
            email.From = "info@littlechapel.com";
            email.To = "yrk0265@gmail.com";
            email.CC = "ramakrishna.murthy@people10.com";
            //email.BCC = "deepti.vij@people10.com;Sanjay.Joshi@allegiantair.com";
            email.Subject = "Video Uploaded Successfully for Plan ID -" + PlanID;
            email.Body = "<b>Upload Start DateTime:</b>" + UploadStartDateTime +
                "<br /><b>Upload End DateTime:</b>" + UploadEndDateTime + Environment.NewLine + "<br /><b>FileSize:</b>" + UploadingFileSize;
            if (!email.Send())
            {
                Log4net.Info("\n\tUpload Email failed to send.");
            }
            else
            {
                Log4net.Info("\n\ttUpload Email Sent.");
            }
        }
        //public static void MultiPartUploads(string FileName, string FilePath)
        //{
        //    IAmazonS3 s3Client = new AmazonS3Client(Settings.AWSAccessKey, Settings.AWSSecretKey, Amazon.RegionEndpoint.USEast1);
        //    List<Amazon.S3.Model.UploadPartResponse> uploadResponses = new List<UploadPartResponse>();
        //    List<CopyPartResponse> copyResponses = new List<CopyPartResponse>();
        //    InitiateMultipartUploadRequest initiateRequest =
        //            new InitiateMultipartUploadRequest
        //            {
        //                BucketName = Settings.VideoBucketWedding,
        //                Key = "wedding/" + FileName
        //            };

        //    // Step 2. Initialize.
        //    Amazon.S3.Model.InitiateMultipartUploadResponse initResponse = s3Client.InitiateMultipartUpload(initiateRequest);

        //    // 2. Upload Parts.
        //    long contentLength = new FileInfo(FilePath).Length;
        //    long partSizeinMB = 10 * (long)Math.Pow(2, 20); // 5 MB
        //    try
        //    {
        //        long filePosition = 0;
        //        for (int i = 1; filePosition < contentLength; i++)
        //        {
        //            UploadPartRequest uploadRequest = new UploadPartRequest
        //            {
        //                BucketName = Settings.VideoBucketWedding,
        //                Key = "wedding/" + FileName,
        //                UploadId = initResponse.UploadId,
        //                PartNumber = i,
        //                PartSize = partSizeinMB,
        //                FilePosition = filePosition,
        //                FilePath = FilePath
        //            };
        //            // Upload part and add response to our list.
        //            uploadResponses.Add(s3Client.UploadPart(uploadRequest));
        //            filePosition += partSizeinMB;
        //        }
        //        // Step 3: complete.
        //        CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
        //        {
        //            BucketName = Settings.VideoBucketWedding,
        //            Key = "wedding/" + FileName,
        //            UploadId = initResponse.UploadId,
        //            //PartETags = new List<PartETag>(uploadResponses)
        //        };
        //        completeRequest.AddPartETags(uploadResponses);
        //        CompleteMultipartUploadResponse completeUploadResponse =
        //            s3Client.CompleteMultipartUpload(completeRequest);
        //    }
        //    catch (Exception e)
        //    {
        //        Log4net.Info("Exception occurred: " + e.Message);
        //        Log4net.Info("Exception occurred for PlanID: " + FileName);
        //        AbortMultipartUploadRequest abortMPURequest = new AbortMultipartUploadRequest
        //        {
        //            BucketName = Settings.VideoBucketWedding,
        //            Key = "wedding/" + FileName,
        //            UploadId = initResponse.UploadId
        //        };
        //        s3Client.AbortMultipartUpload(abortMPURequest);
        //        Log4net.Info("Aborted Upload for PlanID: " + FileName);
        //    }
        //}
        // 1. Upload a file, file name is used as the object key name.
        //fileTransferUtility.Upload(filePath, Settings.VideoBucketWedding);
        //Console.WriteLine("Upload 1 completed");

        // 2. Specify object key name explicitly.
        //fileTransferUtility.Upload(filePath,
        //                          Settings.VideoBucketWedding, FileName);
        //Console.WriteLine("Upload 2 completed");

        // 3. Upload data from a type of System.IO.Stream.
        //using (FileStream fileToUpload =
        //    new FileStream(filePath, FileMode.Open, FileAccess.Read))
        //{
        //    fileTransferUtility.Upload(fileToUpload,
        //                               Settings.VideoBucketWedding, FileName);
        //}
        //Console.WriteLine("Upload 3 completed");
    }
}
