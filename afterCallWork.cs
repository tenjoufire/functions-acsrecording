// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.IO;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;

namespace Company.Function
{
    public static class afterCallWork
    {
        [FunctionName("afterCallWork")]
        public static async Task Run(
            [BlobTrigger("recordings/{name}", Source = BlobTriggerSource.EventGrid, Connection = "BLOB_CONNECTION_STRING")]Stream input, string name,
            ILogger log)
        {
            string recognizedText = "";
            try{
                if(input != null){
                    log.LogInformation($"Blob found: {name}");
                    log.LogInformation($"Blob size: {input.Length} bytes");
                    BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING"));
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAME"));

                    if(name.EndsWith(".wav")){
                        log.LogInformation("Blob is a wav file");

                        //音声認識
                        var speechConfig = SpeechConfig.FromSubscription(Environment.GetEnvironmentVariable("SPEECH_SUBSCRIPTION_KEY"), Environment.GetEnvironmentVariable("SPEECH_REGION"));
                        speechConfig.SpeechRecognitionLanguage = "ja-JP";
                        var reader = new BinaryReader(input);
                        using var audioConfigStream = AudioInputStream.CreatePushStream();
                        using var audioConfig = AudioConfig.FromStreamInput(audioConfigStream);
                        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

                        byte[] buffer;
                        do{
                            buffer = reader.ReadBytes(1024);
                            audioConfigStream.Write(buffer, buffer.Length);
                        }while(buffer.Length > 0);
                        
                        var stopRecognition = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                        //音声認識されたときの処理
                        recognizer.Recognized += (s, e) =>
                        {
                            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                            {
                                log.LogInformation($"RECOGNIZED: Text={e.Result.Text}");
                                recognizedText += e.Result.Text;
                            }
                            else if (e.Result.Reason == ResultReason.NoMatch)
                            {
                                log.LogInformation($"NOMATCH: Speech could not be recognized.");
                            }
                        };

                        //音声認識が終了したときの処理
                        recognizer.SessionStopped += (s, e) =>
                        {
                            log.LogInformation("Session Stopped");
                            stopRecognition.TrySetResult(0);
                        };

                        //音声認識を開始
                        await recognizer.StartContinuousRecognitionAsync();

                        // Waits for completion. Use Task.WaitAny to keep the task rooted.
                        Task.WaitAny(new[] { stopRecognition.Task });

                        //音声認識を終了
                        await recognizer.StopContinuousRecognitionAsync();

                        log.LogInformation($"Recognized: {recognizedText}");

                        //AOAI でサマリ作成
                        var openAiClient = new OpenAIClient(new Uri(Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")), new AzureKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));

                        ChatCompletionsOptions completionsOptions = new ChatCompletionsOptions(){
                            DeploymentName = "gpt4",
                            Messages = {
                                new ChatRequestSystemMessage("あなたはコールセンターで会話された内容を要約するエキスパートです。入力された文章を次のフォーマットに従って要約してください。 [応対概要] [お客様のご用件] [オペレーターの対応内容] [問題は解決しましたか]"),
                                new ChatRequestUserMessage(recognizedText)
                            }
                        };

                        Response<ChatCompletions> completionsResponse = openAiClient.GetChatCompletionsAsync(completionsOptions).Result;
                        string completion = completionsResponse.Value.Choices[0].Message.Content;
                        log.LogInformation($"completion: {completion}");

                        //要約をBLOBにアップロード
                        var blobClient = containerClient.GetBlobClient(name+".txt");
                        await blobClient.UploadAsync(BinaryData.FromString(completion), true);

                    }
                    else{
                        log.LogInformation("Blob is not a audio file");
                    }
                }
                else{
                    log.LogInformation("Blob not found");
                }
            }catch(Exception e){
                log.LogInformation(e.Message);
            }
        }
    }
}
