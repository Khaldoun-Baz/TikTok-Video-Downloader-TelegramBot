Imports System.IO
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports HtmlAgilityPack
Imports Newtonsoft.Json.Linq

Module Module1

    Private Const Token As String = "5871781364:AAHCaPdBNuW6AtNlFUUbjLEO2F2Z0z5_zOQ"
    Private Const ApiUrl As String = "https://api.telegram.org/bot" & Token & "/"
    Private WithEvents client As New HttpClient()

    Sub Main()
        Console.WriteLine("Bot is running... Press [enter] to exit.")
        RunBot().Wait() ' Since Main is non-async, we call Wait() here.
    End Sub

    Async Function RunBot() As Task
        Dim offset As Integer = 0
        While True
            Try
                Dim updates = Await GetUpdatesAsync(offset)
                For Each update In updates
                    Dim message = update("message")
                    If message IsNot Nothing Then
                        Await ProcessMessageAsync(message)
                        offset = update("update_id").ToObject(Of Integer)() + 1
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine("Error: " & ex.Message)
            End Try
            Await Task.Delay(1000)
        End While
    End Function

    Async Function ProcessMessageAsync(ByVal message As JObject) As Task
        Dim chatId = message("chat")("id").ToString()
        Await SendMessageAsync(chatId, "Received your message! Sending video...")

        Dim tiktokLink As String = ExtractTikTokLink(message("text"))
        If String.IsNullOrEmpty(tiktokLink) Then Return

        Dim videoId As String = Await ExtractVideoIdAsync(tiktokLink)
        If String.IsNullOrEmpty(videoId) Then Return

        Dim videoPath As String = Await GetVideoAsync(videoId)
        If String.IsNullOrEmpty(videoPath) Then Return

        Await SendVideoAsync(chatId, videoPath)
    End Function

    Async Function GetUpdatesAsync(ByVal offset As Integer) As Task(Of JArray)
        Dim response = Await client.GetStringAsync(ApiUrl & "getUpdates?offset=" & offset)
        Dim updates = JObject.Parse(response)("result").ToObject(Of JArray)()
        Return updates
    End Function

    Async Function SendMessageAsync(ByVal chatId As String, ByVal text As String) As Task
        Dim parameters = New Dictionary(Of String, String) From {
            {"chat_id", chatId},
            {"text", text}
        }
        Await client.PostAsync(ApiUrl & "sendMessage", New FormUrlEncodedContent(parameters))
    End Function

    Async Function SendVideoAsync(ByVal chatId As String, ByVal videoPath As String) As Task
        Using content As New MultipartFormDataContent()
            content.Add(New StringContent(chatId), "chat_id")
            content.Add(New StreamContent(New FileStream(videoPath, FileMode.Open)), "video", "video.mp4")
            Await client.PostAsync(ApiUrl & "sendVideo", content)
        End Using
    End Function

    Function ExtractTikTokLink(ByVal text As String) As String
        Dim pattern As String = "https://vt\.tiktok\.com/[a-zA-Z0-9]+/"
        Dim match As Match = Regex.Match(text, pattern)
        If match.Success Then Return match.Value
        Return Nothing
    End Function

    Async Function ExtractVideoIdAsync(ByVal tiktokLink As String) As Task(Of String)
        Using client As New HttpClient()
            Dim tempHtml As String = Await client.GetStringAsync(tiktokLink)
            Dim doc As New HtmlDocument()
            doc.LoadHtml(tempHtml)
            Dim ogUrlNode As HtmlNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:url']")
            If ogUrlNode IsNot Nothing Then
                Dim content As String = ogUrlNode.GetAttributeValue("content", String.Empty)
                Dim match As Match = Regex.Match(content, "/video/(\d+)")
                If match.Success Then Return match.Groups(1).Value
            End If
        End Using
        Return Nothing
    End Function
    Async Function GetVideoAsync(ByVal videoId As String) As Task(Of String)
        Dim tempJson As String = Await client.GetStringAsync($"https://api16-normal-c-useast1a.tiktokv.com/aweme/v1/feed/?aweme_id={videoId}")
        Dim vObject As JObject = JObject.Parse(tempJson)
        Dim desiredUrl As String = vObject("aweme_list")(0)("video")("download_addr")("url_list")(1).ToString()
        Return Await DownloadVideoAsync(desiredUrl)
    End Function
    Async Function DownloadVideoAsync(ByVal url As String) As Task(Of String)
        Using response As HttpResponseMessage = Await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            response.EnsureSuccessStatusCode()
            Using videoStream As Stream = Await response.Content.ReadAsStreamAsync()
                Dim fileName As String = "tiktokvid_" & TimeSpan.TicksPerMillisecond & ".mp4"
                Dim filePath As String = Path.Combine(Path.GetTempPath(), fileName)
                Using fileStream As New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
                    Await videoStream.CopyToAsync(fileStream)
                    Return filePath
                End Using
            End Using
        End Using
    End Function

End Module
