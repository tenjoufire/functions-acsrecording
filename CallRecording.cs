// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using System.Threading.Tasks;
using Azure.Communication.CallAutomation;

namespace Company.Function
{
    public static class CallRecording
    {
        /// <summary>
        /// Azure Function triggered by an Event Grid event.
        /// </summary>
        /// <param name="eventGridEvent">The Event Grid event.</param>
        /// <param name="log">The logger instance.</param>
        [FunctionName("CallRecording")]
        public static async Task Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            if (eventGridEvent.EventType == "Microsoft.Communication.CallStarted")
            {
                log.LogInformation("Call started");
                var callEvent = eventGridEvent.Data.ToObjectFromJson<CallStartedEvent>();
                await startRecordingAsync(callEvent.serverCallId, log);
            }
        }

        /// <summary>
        /// Starts the recording of a call asynchronously.
        /// </summary>
        /// <param name="serverCallId">The server call ID.</param>
        /// <param name="log">The logger instance.</param>
        public static async Task startRecordingAsync(String serverCallId, ILogger log)
        {
            CallAutomationClient callAutomationClient = new CallAutomationClient(Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING"));
            StartRecordingOptions recordingOptions = new StartRecordingOptions(new ServerCallLocator(serverCallId));
            recordingOptions.RecordingChannel = RecordingChannel.Mixed;
            recordingOptions.RecordingContent = RecordingContent.Audio;
            recordingOptions.RecordingFormat = RecordingFormat.Wav;
            var startRecordingResponse = await callAutomationClient.GetCallRecording()
                .StartAsync(recordingOptions).ConfigureAwait(false);

            log.LogInformation($"Recording started with recording id: {startRecordingResponse.Value.RecordingId}");
        }
    }

    public class CallStartedEvent
    {
        public StartedBy startedBy { get; set; }
        public string serverCallId { get; set; }
        public Group group { get; set; }
        public bool isTwoParty { get; set; }
        public string correlationId { get; set; }
        public bool isRoomsCall { get; set; }
    }
    public class Group
    {
        public string id { get; set; }
    }
    public class StartedBy
    {
        public CommunicationIdentifier communicationIdentifier { get; set; }
        public string role { get; set; }
    }
    public class CommunicationIdentifier
    {
        public string rawId { get; set; }
        public CommunicationUser communicationUser { get; set; }
    }
    public class CommunicationUser
    {
        public string id { get; set; }
    }
}
