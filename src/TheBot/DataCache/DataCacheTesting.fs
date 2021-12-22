namespace KPX.TheBot.Host.DataCache

/// 指示数据生成后测试
type IDataTest =
    /// 测试内容
    abstract RunTest: unit -> unit

[<AbstractClass>]
/// 对于不方便使用IDataTest情况下的辅助测试基类
type DataTest() =
    abstract RunTest: unit -> unit

    interface IDataTest with
        member x.RunTest() = x.RunTest()


[<RequireQualifiedAccess>]
module Expect =
    let equal a b =
        if a <> b then
            failwithf $"%A{a} <> %A{b}"

    let notEqual a b =
        if a = b then failwithf $"%A{a} = %A{b}"

    let isSome (v: 'a option) = if v.IsNone then failwithf "Is None"

    let isNone (v: 'a option) = if v.IsSome then failwithf "Is Some"

    let isTrue v = if v = false then failwithf "Expect true"

    let isFalse v = if v = true then failwithf "Expect false"
