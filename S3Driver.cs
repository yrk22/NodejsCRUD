// This software code is made available "AS IS" without warranties of any        
// kind.  You may copy, display, modify and redistribute the software            
// code either by itself or as incorporated into your code; provided that        
// you do not remove any proprietary notices.  Your use of this software         
// code is at your own risk and you waive any claim against Amazon               
// Digital Services, Inc. or its affiliates with respect to your use of          
// this software code. (c) 2006 Amazon Digital Services, Inc. or its             
// affiliates.          



using System;
using System.Collections;
using S3Explorer.com.amazon.s3;
using System.Text;

namespace S3Sample
{
    class S3Driver
    {
        private static readonly string awsAccessKeyId = "<INSERT YOUR AWS ACCESS KEY ID HERE>";
        private static readonly string awsSecretAccessKey = "<INSERT YOUR AWS SECRET ACCESS KEY HERE>";

        // Convert the bucket name to lowercase for vanity domains.
        // the bucket must be lower case since DNS is case-insensitive.
        private static readonly string bucketName = awsAccessKeyId.ToLower() + "-test-bucket";
        private static readonly string keyName = "test-key";

        //static int Main(string[] args)
        //{
        //    try {
        //        if ( awsAccessKeyId.StartsWith( "<INSERT" ) ) {
        //            System.Console.WriteLine( "Please examine S3Driver.cs and update it with your credentials" );
        //            return 1;
        //        }

        //        AWSAuthConnection conn = new AWSAuthConnection( awsAccessKeyId, awsSecretAccessKey );

        //        System.Console.WriteLine( "----- creating bucket -----" );
        //        System.Console.WriteLine(
        //                                 conn.createBucket( bucketName, null ).getResponseMessage()
        //                                );

        //        System.Console.WriteLine( "----- listing bucket -----" );
        //        dumpBucketListing(conn.listBucket(bucketName, null, null, 0, null));

        //        System.Console.WriteLine( "----- putting object -----" );
        //        S3Object obj = new S3Object( "This is a test", null );
        //        SortedList headers = new SortedList();
        //        headers.Add( "Content-Type", "text/plain" );
        //        System.Console.WriteLine( conn.put( bucketName, keyName, obj, headers ).getResponseMessage() );

        //        System.Console.WriteLine( "----- listing bucket -----" );
        //        dumpBucketListing(conn.listBucket(bucketName, null, null, 0, null));

        //        System.Console.WriteLine( "----- query string auth example -----" );
        //        QueryStringAuthGenerator generator =
        //                  new QueryStringAuthGenerator( awsAccessKeyId, awsSecretAccessKey, true );
        //        generator.ExpiresIn = 60 * 1000;

        //        System.Console.WriteLine( "Try this url in your web browser (it will only work for 60 seconds)\n" );
        //        string url = generator.get( bucketName, keyName, null );
        //        System.Console.WriteLine( url );
        //        System.Console.WriteLine( "\npress enter >" );
        //        System.Console.ReadLine();

        //        System.Console.WriteLine( "\nNow try just the url without the query string arguments.  It should fail.\n" );
        //        System.Console.WriteLine( generator.makeBaseURL(bucketName, keyName) );
        //        System.Console.WriteLine( "\npress enter >" );
        //        System.Console.ReadLine();

        //        System.Console.WriteLine( "----- putting object with metadata and public read acl -----" );
        //        SortedList metadata = new SortedList();
        //        metadata.Add( "blah", "foo" );
        //        obj = new S3Object( "this is a publicly readable test", metadata );

        //        headers = new SortedList();
        //        headers.Add( "x-amz-acl", "public-read" );
        //        headers.Add( "Content-Type", "text/plain" );
        //        System.Console.WriteLine( conn.put( bucketName, keyName + "-public", obj, headers ).getResponseMessage() );

        //        System.Console.WriteLine( "----- anonymous read test -----" );
        //        System.Console.WriteLine( "\nYou should be able to try this in your browser\n" );
        //        string publicURL = generator.get(bucketName, keyName + "-public", null);
        //        System.Console.WriteLine(publicURL);
        //        System.Console.WriteLine( "\npress enter >" );
        //        System.Console.ReadLine();

        //        System.Console.WriteLine("----- vanity domain example -----");
        //        System.Console.WriteLine("\nThe bucket can also be specified as part of the domain. Any vanity domain that is CNAME'd to s3.amazon.com is also valid.");
        //        System.Console.WriteLine("\nTry this url out in your browser (it will only be valid for 60 seconds)\n");
        //        generator = new QueryStringAuthGenerator( awsAccessKeyId, awsSecretAccessKey, false, CallingFormat.SUBDOMAIN );
        //        generator.Expires = 60 * 1000;
        //        System.Console.WriteLine( generator.get(bucketName, keyName, null ) );
        //        System.Console.WriteLine( "\npress enter >" );
        //        System.Console.ReadLine();

        //        System.Console.WriteLine( "----- getting object's acl -----" );
        //        System.Console.WriteLine( conn.getACL( bucketName, keyName, null ).Object.Data );

        //        System.Console.WriteLine( "----- deleting objects -----" );
        //        System.Console.WriteLine( conn.delete( bucketName, keyName, null ).getResponseMessage() );
        //        System.Console.WriteLine( conn.delete( bucketName, keyName + "-public", null ).getResponseMessage() );

        //        System.Console.WriteLine( "----- listing bucket -----" );
        //        dumpBucketListing(conn.listBucket(bucketName, null, null, 0, null));

        //        System.Console.WriteLine( "----- listing all my buckets -----" );
        //        dumpAllMyBucketListing( conn.listAllMyBuckets( null ) );

        //        System.Console.WriteLine( "----- deleting bucket -----" );
        //        System.Console.WriteLine( conn.deleteBucket( bucketName, null ).getResponseMessage() );
        //        return 0;
        //    } catch ( Exception e ) {
        //        System.Console.WriteLine( e.Message );
        //        System.Console.WriteLine( e.StackTrace );
        //        System.Console.ReadLine();
        //        return 1;
        //    }
        //}

        private static void dumpBucketListing(ListBucketResponse list)
        {
            foreach (ListEntry entry in list.Entries)
            {
                Owner o = entry.Owner;
                if (o == null)
                {
                    o = new Owner("", "");
                }
                System.Console.WriteLine( entry.Key.PadRight( 20 ) + 
                                          entry.ETag.PadRight( 20 ) +
                                          entry.LastModified.ToString().PadRight( 20 ) +
                                          o.Id.PadRight( 10 ) +
                                          o.DisplayName.PadRight( 20 ) +
                                          entry.Size.ToString().PadRight( 11 ) +
                                          entry.StorageClass.PadRight( 10 ) );
            }
        }

        private static void dumpAllMyBucketListing(ListAllMyBucketsResponse list)
        {
            foreach (Bucket entry in list.Buckets)
            {
                System.Console.WriteLine( entry.Name.PadRight(20) +
                                          entry.CreationDate.ToString().PadRight(20) );
            }
        }
    }
}
