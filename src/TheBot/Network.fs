[<AutoOpen>]
module KPX.TheBot.Host.Network

open System.Net.Http


[<RequireQualifiedAccess>]
module TheBotWebFetcher =
    open System.Collections.Generic

    let private maxParallel = 10

    let private logger = NLog.LogManager.GetLogger("BatchWebFetcher")

    let private httpClient =
        let flags = System.Net.DecompressionMethods.GZip ||| System.Net.DecompressionMethods.Deflate
        let hch = new HttpClientHandler(AutomaticDecompression = flags)
        let hc = new HttpClient(hch)

        hc.DefaultRequestHeaders.Connection.Add("keep-alive")

        hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot")
        |> ignore

        hc

    type private TaskSchedulerMessage =
        | Fetch of string * AsyncReplyChannel<HttpResponseMessage>
        | Finished

    let private fetchInfo (url: string) =
        logger.Info $"正在访问 :  %s{url}"

        httpClient
            .GetAsync(url)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()

    let private agent =
        MailboxProcessor.Start (fun inbox ->
            async {
                let queue = Queue<_>()
                let mutable count = 0

                while true do
                    let! msg = inbox.Receive()

                    match msg with
                    | Fetch (info, reply) -> queue.Enqueue(info, reply)
                    | Finished -> count <- count - 1

                    if count < maxParallel && queue.Count > 0 then
                        count <- count + 1
                        let (info, reply) = queue.Dequeue()

                        async {
                            let resp = fetchInfo info
                            reply.Reply(resp)
                            inbox.Post(Finished)
                        }
                        |> Async.Start

                    if count >= maxParallel && queue.Count > 0 then
                        logger.Warn("队列已满，当前并发：{0}，队列数：{1}。", count, queue.Count)
            })

    /// 发送请求并等待结果
    let fetch (url: string) =
        agent.PostAndReply(fun reply -> Fetch(url, reply))
