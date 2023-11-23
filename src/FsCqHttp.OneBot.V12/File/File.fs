namespace KPX.FsCqHttp.OneBot.V12.File

open System

open KPX.FsCqHttp.OneBot.V12


/// 表示储存在实现端的文件
type RemoteData(id: FileId) =

    member x.Id = id

    member x.Download() = raise <| NotImplementedException()

/// 表示已经保存在本地的数据
type LocalData(name: string, data: byte[]) =
    member x.Name = name

    member x.Data = data

    member x.AsBase64() =
        Convert.ToBase64String(data, Base64FormattingOptions.None)

    member x.Upload() = raise <| NotImplementedException()

type UploadFile(data: LocalData) =
    inherit Request<RemoteData>("upload_file")

    new(name: string, data: byte[]) = UploadFile(LocalData(name, data))

    override x.GetRequestObj() =
        {| ``type`` = "data"
           data = data.AsBase64() |}

    override x.ProcessResponse(obj) =
        let fileId = obj.["file_id"].Value<string>()

        RemoteData(FileId fileId)


type GetFile(data: RemoteData) =
    inherit Request<LocalData>("get_file")

    override x.GetRequestObj() =
        {| file_id = data.Id.Value
           ``type`` = "data" |}

    override x.ProcessResponse(obj) =
        let name = obj.["name"].Value<string>()
        let data = Convert.FromBase64String(obj.["data"].Value<string>())
        LocalData(name, data)
