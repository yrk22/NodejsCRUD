using S3Explorer.com.amazon.s3;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploader
{
    class FileUploadS3
    {
        public void FileUpload(string Bucket,string fileName,string AWSAccessKeyID, string SecretAccessKeyID,byte[] data,string contentType)
        {
            AWSAuthConnection conn = new AWSAuthConnection(AWSAccessKeyID, SecretAccessKeyID);
            SortedList metadata = new SortedList();

            S3Object titledObject =
                 new S3Object(data, metadata);

            SortedList headers = new SortedList();
            headers.Add("Content-Type", contentType);
            headers.Add("x-amz-acl", "public-read");

            using (Response response = conn.put(Bucket, fileName, titledObject, headers))
            {
                HttpStatusCode status = response.Status;

                if (status == HttpStatusCode.OK)
                {
                    return;
                }
            }
        }
        //private void OnEncodeFinished()
        //{
        //    byte[] data;

        //    if (File.Exists(ConvertedPath))
        //    {
        //        ////Fix the metadata, move from end to beginning of file so it streams
        //        //FastStartWrapper fastStart = new FastStartWrapper("qt-faststart.exe");
        //        //fastStart.MoveMetaData(ConvertedPath, FinalPath);

        //        ////Remove the raw version
        //        //File.Delete(ConvertedPath);

        //        using (Stream fin = File.OpenRead(FinalPath))
        //        {
        //            data = new byte[fin.Length];
        //            fin.Read(data, 0, data.Length);
        //            //data = fin.ReadToEnd().ToCharArray().Select(c => (byte)c).ToArray();
        //        }
        //        RestPutFile(data, "text/plain", "wedding/" + txtPlanID.Text + ".mp4");
        //    }
        //    else
        //    {
        //        MessageBox.Show("Error converting file to mp4.");
        //        return;
        //    }
        //}
    }
}
