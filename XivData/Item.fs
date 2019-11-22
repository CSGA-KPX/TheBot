module XivData.Item

open System
open LibFFXIV.GameData.Raw

[<CLIMutable>]
type ItemRecord =
    { [<LiteDB.BsonIdAttribute(false)>]
      Id : int
      Name : string }

    override x.ToString() = sprintf "%s(%i)" x.Name x.Id

    static member GetUnknown(lodeId) =
        { Id = -1
          Name = "δ֪" }

///�������������б�
let internal itemOverriding =
    [| { Id = 14928
         Name = "�����ո�����" },
       { Id = 14928
         Name = "�����ո�����ʱװ" }
       { Id = 16604
         Name = "ӥ������" },
       { Id = 16604
         Name = "ӥ������ʱװ" }
       { Id = 13741
         Name = "�ܼ�֮��֤��" },
       { Id = 13741
         Name = "���ڵĹܼ�֮��֤��" }
       { Id = 18491
         Name = "��������ͷ��" },
       { Id = 18491
         Name = "��������ͷ��ʱװ" }
       { Id = 17915
         Name = "�����ɹ���Ĵ�Ƥ" },
       { Id = 17915
         Name = "�������ɹ���Ĵ�Ƥ" }
       { Id = 20561
         Name = "����װ��" },
       { Id = 20561
         Name = "����װ��" }
       { Id = 24187
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮" },
       { Id = 24187
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮24187" }
       { Id = 24188
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮" },
       { Id = 24188
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮24188" }
       { Id = 24189
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮" },
       { Id = 24189
         Name = "2018���Ⱥ��ʢ������������ھ�֤֮24189" } |]
    |> dict

type ItemCollection private () =
    inherit Utils.XivDataSource<int, ItemRecord>()

    static let instance = new ItemCollection()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        printfn "Building ItemCollection"
        db.EnsureIndex("_id", true) |> ignore
        db.EnsureIndex("Name") |> ignore
        let col = new LibFFXIV.GameData.Raw.XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
        let sht = col.GetSheet("Item", [| "Name" |])
        seq {
            for row in sht do
                yield { Id = row.Key.Main
                        Name = row.As<string>("Name") }
        }
        |> db.InsertBulk
        |> ignore
        GC.Collect()


    member x.TryLookupByName(name : string) =
        let ret = x.Collection.FindOne(LiteDB.Query.EQ("Name", new LiteDB.BsonValue(name)))
        if isNull (box ret) then None
        else Some ret

    member x.SearchByName(str) = x.Collection.Find(LiteDB.Query.Contains("Name", str)) |> Seq.toArray

    member x.AllItems() = x.Collection.FindAll() |> Seq.toArray
