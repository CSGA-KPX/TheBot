namespace KPX.TheBot.Data.EveData.Process

open System

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Data.EveData.Group
open KPX.TheBot.Data.EveData.EveMarketGroup
open KPX.TheBot.Data.EveData.EveType

open LiteDB

type MetaGroup =
    | Tech1 = 1
    | Tech2 = 2
    | Faction = 4
    | Tech3 = 14
    | Tech1Struct = 54
    | Tech2Struct = 53

type NameSearchCond =
    | ByItemName of string
    | ByGroupName of string
    | ByMarketGroupName of string

    override x.ToString() = sprintf "%A" x

    member x.Contains(t : EveType) =
        let checkName (name : string) =
            if name.Length < 2 then failwith "关键词太短，至少2个字"
            if name.Contains("I", StringComparison.OrdinalIgnoreCase) then failwith "关键词不得含有'I'"

        match x with
        | ByItemName name ->
            checkName name
            t.Name.Contains(name)
        | ByGroupName name ->
            checkName name

            let group =
                EveGroupCollection.Instance.GetByGroupId(t.GroupId)

            group.Name.Contains(name)
        | ByMarketGroupName name ->
            checkName name
            /// 不是所有物品都有市场分类
            MarketGroupCollection.Instance.TryGetById(t.MarketGroupId)
            |> Option.map (fun g -> g.Name.Contains(name))
            |> Option.defaultValue false

/// EVE蓝图搜索条件
/// 如果指定了cacheResult则会将结果缓存
type ProcessSearchCond(pType : ProcessType, ?cacheName : string) =

    member val CacheName = defaultArg cacheName "" with get, set

    member x.CacheResult =
        not <| String.IsNullOrWhiteSpace(x.CacheName)

    member x.ProcessType = pType

    member val NameSearch : NameSearchCond [] = Array.empty with get, set

    member val NameExclude : NameSearchCond [] = Array.empty with get, set

    member val GroupIds : int [] = Array.empty with get, set

    member val CategoryIds : int [] = Array.empty with get, set

    member val MetaGroupIds : int [] = Array.empty with get, set

    member val ResultCountLimit = 100 with get, set

type ProcessSearchResult =
    | Result of EveProcess []
    | NoResult
    | TooManyResults

[<CLIMutable>]
type ProcessSearchCache =
    { [<BsonId(false)>]
      CacheName : string
      ProcessType : ProcessType
      Processes : EveProcess [] }

type EveProcessSearch private () =
    inherit CachedTableCollection<string, ProcessSearchCache>()

    static let instance = EveProcessSearch()

    static member Instance = instance

    override x.IsExpired = false

    override x.InitializeCollection() = ()

    override x.Depends =
        [| typeof<EveTypeCollection>
           typeof<EveGroupCollection>
           typeof<MarketGroupCollection> |]

    member private x.GetCollectionByType(t : ProcessType) =
        match t with
        | ProcessType.Reaction
        | ProcessType.Manufacturing -> BlueprintCollection.Instance :> EveProcessCollection
        | ProcessType.Planet -> PlanetProcessCollection.Instance :> EveProcessCollection
        | ProcessType.Refine -> RefineProcessCollection.Instance :> EveProcessCollection
        | _ -> invalidArg "ProcessType" (sprintf "无法为%A获取合适的数据表" t)

    member x.Search(cond : ProcessSearchCond) =
        let results =
            if cond.CacheResult then
                let ret = x.TryGetByKey(cond.CacheName)

                if ret.IsSome then ret.Value.Processes else x.SearchCore(cond)
            else
                x.SearchCore(cond)

        if results.Length = 0 then NoResult
        elif results.Length > cond.ResultCountLimit then TooManyResults
        else Result results

    member private x.SearchCore(cond : ProcessSearchCond) =
        let gids = cond.GroupIds |> set
        let cids = cond.CategoryIds |> set
        let mids = cond.MetaGroupIds |> set

        let db =
            x
                .GetCollectionByType(cond.ProcessType)
                .DbCollection

        let result =
            db.FindAll()
            |> Seq.choose
                (fun proc ->
                    let proc = proc.AsEveProcess()
                    let mutable ret = true

                    let product = proc.Process.GetFirstProduct()

                    if not gids.IsEmpty then ret <- ret && gids.Contains(product.Item.GroupId)
                    if not cids.IsEmpty then ret <- ret && cids.Contains(product.Item.CategoryId)
                    if not mids.IsEmpty then ret <- ret && mids.Contains(product.Item.MetaGroupId)

                    for criteria in cond.NameSearch do
                        ret <- ret && (criteria.Contains(product.Item))

                    for criteria in cond.NameExclude do
                        ret <- ret && (not <| criteria.Contains(product.Item))

                    if ret then Some proc else None)
            |> Seq.toArray



        if cond.CacheResult then
            x.DbCollection.Upsert(
                { CacheName = cond.CacheName
                  ProcessType = cond.ProcessType
                  Processes = result }
            )
            |> ignore

        result


[<RequireQualifiedAccess>]
module PredefinedSearchCond =
    /// 所有行星开发
    let Planet =
        ProcessSearchCond(ProcessType.Planet, "planet")

    let FuelBlocks =
        let FuelBlock = 1136
        ProcessSearchCond(ProcessType.Manufacturing, "fuelBlocks", GroupIds = [| FuelBlock |])

    let Components =
        let Tech2Component = 334
        let CapitalComponent = 873
        ProcessSearchCond(ProcessType.Manufacturing, "components", GroupIds = [| Tech2Component; CapitalComponent |])

    let T1Ships =
        let ships = 6

        ProcessSearchCond(
            ProcessType.Manufacturing,
            "T1Ships",
            GroupIds = [| ships |],
            MetaGroupIds =
                [| MetaGroup.Tech1 |> int
                   MetaGroup.Faction |> int |],
            NameExclude = [| ByMarketGroupName "特别" |]
        )

    let T2Ships =
        let ships = 6

        ProcessSearchCond(
            ProcessType.Manufacturing,
            "T2Ships",
            GroupIds = [| ships |],
            MetaGroupIds = [| MetaGroup.Tech2 |> int |],
            NameExclude = [| ByMarketGroupName "特别" |]
        )


    let T2ModulesOf (search : NameSearchCond) =
        let ammo = 8
        let drone = 18
        let modules = 7

        ProcessSearchCond(
            ProcessType.Manufacturing,
            "T2ModulesOf" + search.ToString(),
            NameSearch = Array.singleton search,
            CategoryIds = [| ammo; drone; modules |],
            MetaGroupIds = [| MetaGroup.Tech2 |> int |],
            NameExclude = [| ByMarketGroupName "特别" |]
        )
