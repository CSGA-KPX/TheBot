namespace TheBotData.Common.Testing

/// 指示数据生成后测试
type IDataTest =
    /// 测试内容
    abstract RunTest : unit -> unit
    
[<AbstractClass>]
/// 对于不使用LiteDB的数据集进行测试的辅助类
type DataTest() =
    abstract RunTest : unit -> unit
    
    interface IDataTest with
        member x.RunTest() = x.RunTest()