namespace KPX.TheBot.Module.DataCacheModule

open KPX.FsCqHttp.Handler

open KPX.TheBot.Host
open KPX.TheBot.Host.Utils.HandlerUtils
open KPX.TheBot.Host.DataCache


type DataCacheModule(discover : HostedModuleDiscover) =
    inherit CommandHandlerBase()
    
    [<CommandHandlerMethod("##rebuilddatacache", "(超管) 重建数据缓存", "", IsHidden = true)>]
    member x.HandleRebuildDataCache(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        BotDataInitializer.rebuildAllCache(discover)
        cmdArg.Reply("重建数据缓存完成")

    [<CommandHandlerMethod("##testdatacache", "(超管) 测试数据缓存", "", IsHidden = true)>]
    member x.HandleTestDataCache(cmdArg : CommandEventArgs) =
        cmdArg.EnsureSenderOwner()
        BotDataInitializer.runDataTests(discover)
        cmdArg.Reply("测试数据缓存完成")