// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using System.Collections.Generic;
using Azure.Communication.CallAutomation;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Company.Function
{
    public static class GetRecording
    {
        [FunctionName("GetRecording")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            if (eventGridEvent.EventType == "Microsoft.Communication.RecordingFileStatusUpdated")
            {
                CallAutomationClient callAutomationClient = new CallAutomationClient(Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING"));
                BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING"));
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAME"));
                log.LogInformation("Call Recording Event Received");

                //レコーディングイベントから録画をダウンロードする
                var callEvent = eventGridEvent.Data.ToObjectFromJson<CallRecordingEvent>();
                var recordingLocation = callEvent.recordingStorageInfo.recordingChunks[0].contentLocation;
                var recordingTime = callEvent.recordingStartTime;
                var recordingTimeString = recordingTime.ToString("yyyyMMddHHmmss");
                var recordingDownloadUri = new Uri(recordingLocation);
                var recordingStream = callAutomationClient.GetCallRecording().DownloadStreaming(recordingDownloadUri).Value;

                log.LogInformation("Recording downloaded");

                //録画をBLOBにアップロードする
                var blobClient = containerClient.GetBlobClient("video" + recordingTimeString + ".wav");
                try{
                    await blobClient.UploadAsync(recordingStream, true);
                }
                catch (Exception e)
                {
                    log.LogInformation($"error: {e.Message}");
                }
                log.LogInformation("Recording uploaded to BLOB");
            }
        }
    }
public class CallRecordingEvent
{
    public Recordingstorageinfo recordingStorageInfo { get; set; }
    public DateTime recordingStartTime { get; set; }
    public int recordingDurationMs { get; set; }
    public string sessionEndReason { get; set; }
}

public class Recordingstorageinfo
{
    public Recordingchunk[] recordingChunks { get; set; }
}

public class Recordingchunk
{
    public string documentId { get; set; }
    public int index { get; set; }
    public string endReason { get; set; }
    public string contentLocation { get; set; }
    public string metadataLocation { get; set; }
    public string deleteLocation { get; set; }
}

}
