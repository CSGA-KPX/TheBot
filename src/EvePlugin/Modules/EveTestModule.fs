namespace KPX.EvePlugin.Modules

open System
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.RecipeRPN
open KPX.TheBot.Host.Utils.HandlerUtils

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils
open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions
open KPX.EvePlugin.Utils.UserInventory


type EveRecipeTestModule() =
    inherit CommandHandlerBase()

    let data = DataBundle.Instance

    [<CommandHandlerMethod("#evetest", "", "", IsHidden = true)>]
    member x.HandleME(cmdArg: CommandEventArgs) =
        cmdArg.EnsureSenderOwner()

        let cfg = EveConfigParser()
        cfg.Parse(cmdArg.HeaderArgs)

        let item = data.TryGetItem(cfg.GetNonOptionString())


        let pm2 = EveProcessManager2(cfg)

        let ret = pm2.GetRecipeContext(item.Value)

        for kv in ret do
            printfn $"%A{kv.Key.Name}"
