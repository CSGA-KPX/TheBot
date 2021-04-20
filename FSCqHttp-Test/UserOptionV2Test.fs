module UserOptionV2Test

open NUnit.Framework

open KPX.FsCqHttp.Utils.UserOptionV2


[<SetUp>]
let Setup () =
    ()

type TestConfig() as x = 
    inherit OptionBase()

    let privateCell = OptionCellSimple<int>(x, "private", 5)

    member val PublicCell = OptionCellSimple<int>(x, "public", 10)

    member x.PrivateCellValue = privateCell.Value

type ComplexCell(cb, key, def) = 
    inherit OptionCell<int>(cb, key, def)

    override x.ConvertValue(input) = 
        input |> int

[<Test>]
let ``PredefinedClassTest`` () = 
    let cfg = TestConfig()
    cfg.Parse("asd asdasd private:9999 public:9999".Split(" "))

    printfn "%A" cfg.NonOptionStrings

    Assert.AreEqual(cfg.NonOptionStrings.Count, 2)
    Assert.AreEqual(cfg.NonOptionStrings.[0], "asd")
    Assert.AreEqual(cfg.NonOptionStrings.[1], "asdasd")

    Assert.AreEqual(cfg.PrivateCellValue, 9999)
    Assert.AreEqual(cfg.PublicCell.Value, 9999)

[<Test>]
let ``PredefinedRegister`` () = 
    let cfg = TestConfig()
    let def = cfg.RegisterOption("defined")
    let ndf = cfg.RegisterOption("ndefined")
    let defValue = cfg.RegisterOption("defValue", 55)
    let ndv = cfg.RegisterOption("ndefValue", 55)
    cfg.Parse("defined: defValue:100".Split(" "))

    printfn "%A" cfg.NonOptionStrings

    Assert.AreEqual(def.IsDefined, true)
    Assert.AreEqual(ndf.IsDefined, false)

    Assert.AreEqual(defValue.Value, 100)
    Assert.AreEqual(ndv.Value, 55)

[<Test>]
let ``TestArray`` () = 
    let cfg = OptionBase()
    let arr = cfg.RegisterOption("array", 1)
    cfg.Parse("array: array:5 array:10 array:20".Split(" "))
    
    Assert.AreEqual(1, arr.Value)
    let a = arr.Values
    Assert.AreEqual(1, a.[0])
    Assert.AreEqual(5, a.[1])
    Assert.AreEqual(10, a.[2])
    Assert.AreEqual(20, a.[3])

[<Test>]
let ``TestComplexCell_default`` () = 
    let cfg = OptionBase()
    let opt = cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

    cfg.Parse("complex:".Split(" "))

    Assert.AreEqual(10, opt.Value)

[<Test>]
let ``TestComplexCell_defined`` () = 
    let cfg = OptionBase()
    let opt = cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

    cfg.Parse("complex:100".Split(" "))

    Assert.AreEqual(100, opt.Value)

[<Test>]
let ``TestComplexCell_array`` () = 
    let cfg = OptionBase()
    let opt = cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

    cfg.Parse("complex: complex:0 complex:5 complex:10".Split(" "))

    Assert.AreEqual(10, opt.Value)
    let a = opt.Values
    Assert.AreEqual(10, a.[0])
    Assert.AreEqual(0, a.[1])
    Assert.AreEqual(5, a.[2])
    Assert.AreEqual(10, a.[3])