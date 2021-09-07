namespace KPX.TheBot.Data.Common.Database

open System
open System.Collections.Generic


/// 缓存生成相关信息
type IInitializationInfo =
    /// 指示该生成过程依赖的数据类
    abstract Depends : Type []

[<AbstractClass>]
type BotDataCollection<'Key, 'Item>(dbName) as x =

    let colName = x.GetType().Name

    /// 调用InitializeCollection时的依赖项，
    /// 对在TheBotData外定义的项目无效
    abstract Depends : Type []
    
    member val Logger = NLog.LogManager.GetLogger $"%s{dbName}:%s{colName}"

    /// 获取数据库集合供复杂操作
    member x.DbCollection =
        getLiteDB(dbName).GetCollection<'Item>(colName)

    /// 清空当前集合，不释放空间
    member x.Clear() = x.DbCollection.DeleteAll() |> ignore

    member x.Count() = x.DbCollection.Count()

    /// 辅助方法：如果input为Some，返回值。如果为None，根据fmt和args生成KeyNotFoundException
    member internal x.PassOrRaise(input : option<'T>, fmt : string, [<ParamArray>] args : obj []) =
        if input.IsNone then
            raise
            <| KeyNotFoundException(String.Format(fmt, args))

        input.Value

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            x.DbCollection.FindAll().GetEnumerator() :> Collections.IEnumerator


    interface IEnumerable<'Item> with
        member x.GetEnumerator() =
            x.DbCollection.FindAll().GetEnumerator()

    interface IInitializationInfo with
        member x.Depends = x.Depends