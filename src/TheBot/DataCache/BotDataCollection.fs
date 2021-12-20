namespace KPX.TheBot.Host.DataCache

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data


/// 缓存生成相关信息
type IInitializationInfo =
    /// 指示该生成过程依赖的数据类
    abstract Depends: Type []

[<AbstractClass>]
type BotDataCollection<'Item>(colName: string) =

    member internal x.CollectionName = colName

    /// 获取LiteCollection
    member val internal LiteCollection =
        DataAgent
            .GetCacheDatabase()
            .GetCollection<'Item>(colName)

    /// 调用InitializeCollection时的依赖项
    /// 仅在使用BotDataInitializer.buildAllCache时使用
    abstract Depends: Type []

    member val Logger = NLog.LogManager.GetLogger $"DataCache:%s{colName}"

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            x.LiteCollection.FindAll().GetEnumerator() :> Collections.IEnumerator

    interface IEnumerable<'Item> with
        member x.GetEnumerator() =
            x.LiteCollection.FindAll().GetEnumerator()

    interface IInitializationInfo with
        member x.Depends = x.Depends


namespace KPX.TheBot.Host.DataCache.LiteDb

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache


[<AutoOpen>]
module BotDataCollectionExtension =

    type BotDataCollection<'Item> with

        member x.DbCollection = x.LiteCollection

        /// 清空当前集合，不释放空间
        member x.Clear() = x.LiteCollection.DeleteAll() |> ignore

        member x.Count() = x.LiteCollection.Count()

        /// 辅助方法：如果input为Some，返回值。如果为None，根据fmt和args生成KeyNotFoundException
        member x.PassOrRaise(input: option<'T>, fmt: string, [<ParamArray>] args: obj []) =
            if input.IsNone then
                raise <| KeyNotFoundException(String.Format(fmt, args))

            input.Value
