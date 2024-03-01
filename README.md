# 概要
ACS で行われるすべての会議音声を録音し、その内容を AOAI を利用しサマリーを作成するデモです。

処理の流れ
 - ACS で開始された通話を Event Grid で Functions に通知し、レコーディングを開始する
 - レコーディングが完了した際にも Event Grid で Functions に通知し、レコーディングファイルを取得し Blob へアップロードする
 - Blob へレコーディングがアップロードされたことを検知し、音声認識と会話内容要約を実施し、TXT ファイルとして Blob へアップロードする

# 考慮事項
Speech To Text の処理を Functions のコード afterCallWork.cs で行っていますが、これは短い録音でのみ動作します。10分以上の長い録音の場合は、Durable Functions や Container Apps を用いた非同期処理を用いた音声認識の実装が必要です。

# 参考ドキュメント
https://learn.microsoft.com/ja-jp/azure/communication-services/concepts/voice-video-calling/call-recording
https://learn.microsoft.com/ja-jp/azure/event-grid/communication-services-voice-video-events
https://learn.microsoft.com/ja-jp/azure/ai-services/speech-service/how-to-recognize-speech?pivots=programming-language-csharp
https://learn.microsoft.com/ja-jp/azure/azure-functions/functions-event-grid-blob-trigger?tabs=in-process%2Cnodejs-v4&pivots=programming-language-csharp
https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme?view=azure-dotnet-preview