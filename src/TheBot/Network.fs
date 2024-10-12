[<AutoOpen>]
module KPX.TheBot.Host.Network

open System.Net
open System.Net.Http

open KPX.FsCqHttp.Handler


[<RequireQualifiedAccess>]
module TheBotWebFetcher =
    open System.Collections.Generic

    let private maxParallel = 10

    let private maxQueueItem = maxParallel * 5

    let private logger = NLog.LogManager.GetLogger("BatchWebFetcher")

    let mutable private httpClient : HttpClient option = None

    let mutable private httpClientUseProxy = false

    let initHttpClient (proxy : WebProxy option) =

        if httpClient.IsSome then httpClient.Value.Dispose()

        let hch = new HttpClientHandler()
        hch.AutomaticDecompression <- System.Net.DecompressionMethods.GZip ||| System.Net.DecompressionMethods.Deflate

        if proxy.IsSome then
            hch.Proxy <- proxy.Value
            httpClientUseProxy <- true
        else
            httpClientUseProxy <- false

        let hc = new HttpClient(hch)

        hc.DefaultRequestHeaders.Connection.Add("keep-alive")

        hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot")
        |> ignore

        httpClient <- Some hc

    type private TaskSchedulerMessage =
        | Fetch of string * AsyncReplyChannel<HttpResponseMessage>
        | Finished

    let private fetchInfo (url: string) =
        if httpClient.IsNone then initHttpClient(None)

        logger.Info $"正在访问 {httpClientUseProxy} :  %s{url}"

        httpClient
            .Value
            .GetAsync(url)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()

    let queue = Queue<_>()
    let mutable private concurrentCount = 0

    let private agent =
        MailboxProcessor.Start (fun inbox ->
            async {
                while true do
                    let! msg = inbox.Receive()

                    match msg with
                    | Fetch (info, reply) -> queue.Enqueue(info, reply)
                    | Finished -> concurrentCount <- concurrentCount - 1

                    if concurrentCount < maxParallel && queue.Count > 0 then
                        concurrentCount <- concurrentCount + 1
                        let (info, reply) = queue.Dequeue()

                        async {
                            let resp = fetchInfo info
                            reply.Reply(resp)
                            inbox.Post(Finished)
                        }
                        |> Async.Start

                    if concurrentCount >= maxParallel && queue.Count > 0 then
                        logger.Warn("队列已满，当前并发：{0}，队列数：{1}。", concurrentCount, queue.Count)
            })

    /// 发送请求并等待结果
    let fetch (url: string) =
        if queue.Count >= maxQueueItem then
            logger.Error("达到网络队列上限 当前并发：{0}，队列数：{1}。", concurrentCount, queue.Count)
            let ex = ModuleException(SystemError, "网络访问队列超上限")
            raise ex

        agent.PostAndReply(fun reply -> Fetch(url, reply))
        