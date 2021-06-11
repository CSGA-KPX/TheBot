module UserOptionTest

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Utils.Subcommands

open Expecto


type TestConfig() as x =
    inherit CommandOption()

    let privateCell = OptionCellSimple<int>(x, "private", 5)

    member val PublicCell = OptionCellSimple<int>(x, "public", 10)

    member x.PrivateCellValue = privateCell.Value

type ComplexCell(cb, key, def) =
    inherit OptionCell<int>(cb, key, def)

    override x.ConvertValue(input) = input |> int

type Test1CommandOption() as x =
    inherit OptionBase()

    let testBoolOpt = OptionCell(x, "isTest1")

    let testValueOpt =
        OptionCellSimple(x, "test1Val", 0, ArgIndex = Some 0)

    member x.BoolVal = testBoolOpt.IsDefined

    member x.ValueOpt = testValueOpt.Value


type TestSubcommands =
    | Test1 of Test1CommandOption
    | Test2
    | Test3

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            | Test1 _ -> "Test1"
            | Test2 -> "Test2"
            | Test3 -> "Test3"

[<Tests>]
let ConfigTests =
    testList
        "UserOptionTest"
        [ testCase "PredefinedClassTest"
          <| fun _ ->
              let cfg = TestConfig()
              cfg.Parse("asd asdasd private:9999 public:9999".Split(" "))

              printfn $"%A{cfg.NonOptionStrings}"

              Expect.equal cfg.NonOptionStrings.Count 2 "Has 2 non-option strings"
              Expect.equal cfg.NonOptionStrings.[0] "asd" "First non-option is asd"
              Expect.equal cfg.NonOptionStrings.[1] "asdasd" "Second non-option is asdasd"

              Expect.equal cfg.PrivateCellValue 9999 "Private cell value is 9999."
              Expect.equal cfg.PublicCell.Value 9999 "Public cell value is 9999"

          testCase "PredefinedRegister"
          <| fun _ ->
              let cfg = TestConfig()
              let def = cfg.RegisterOption("defined")
              let ndf = cfg.RegisterOption("ndefined")
              let defValue = cfg.RegisterOption("defValue", 55)
              let ndv = cfg.RegisterOption("ndefValue", 55)
              cfg.Parse("defined: defValue:100".Split(" "))

              printfn $"%A{cfg.NonOptionStrings}"

              Expect.equal def.IsDefined true "should defined"
              Expect.equal ndf.IsDefined false "should not defined"

              Expect.equal defValue.Value 100 "set to 100"
              Expect.equal ndv.Value 55 "default = 55"

          testCase "TestArray"
          <| fun _ ->
              let cfg = CommandOption()
              let arr = cfg.RegisterOption("array", 1)
              cfg.Parse("array: array:5 array:10 array:20".Split(" "))
              Expect.equal 1 arr.Value ""
              let a = arr.Values
              Expect.equal 1 a.[0] ""
              Expect.equal 5 a.[1] ""
              Expect.equal 10 a.[2] ""
              Expect.equal 20 a.[3] ""

          testCase "TestComplexCell_default"
          <| fun _ ->
              let cfg = CommandOption()

              let opt =
                  cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

              cfg.Parse("complex:".Split(" "))
              Expect.equal 10 opt.Value ""

          testCase "TestComplexCell_defined"
          <| fun _ ->
              let cfg = CommandOption()

              let opt =
                  cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

              cfg.Parse("complex:100".Split(" "))
              Expect.equal 100 opt.Value ""

          testCase "TestComplexCell_array"
          <| fun _ ->
              let cfg = CommandOption()

              let opt =
                  cfg.RegisterOption(ComplexCell(cfg, "complex", 10))

              cfg.Parse(
                  "complex: complex:0 complex:5 complex:10"
                      .Split(" ")
              )

              Expect.equal 10 opt.Value ""
              let a = opt.Values
              Expect.equal 10 a.[0] ""
              Expect.equal 0 a.[1] ""
              Expect.equal 5 a.[2] ""
              Expect.equal 10 a.[3] ""

          testCase "TestIndexed"
          <| fun _ ->
              let testArgs = [| "test1"; "isTest1:"; "12341234" |]

              match SubcommandParser.Parse<TestSubcommands>(testArgs) with
              | Some (Test1 ob) ->
                  Expect.isTrue ob.BoolVal ""
                  Expect.equal ob.ValueOpt 12341234 ""
              | Some Test2 -> failwith "Wrong match"
              | Some Test3 -> failwith "Wrong match"
              | None -> failwith "Wrong match"

          testCase "TestNonIndexed"
          <| fun _ ->
              let testArgs =
                  [| "test1"
                     "isTest1:"
                     "test1Val:12341234" |]

              match SubcommandParser.Parse<TestSubcommands>(testArgs) with
              | Some (Test1 ob) ->
                  Expect.isTrue ob.BoolVal ""
                  Expect.equal ob.ValueOpt 12341234 ""
              | Some Test2 -> failwith "Wrong match"
              | Some Test3 -> failwith "Wrong match"
              | None -> failwith "Wrong match"

          ]
