module Json 

open System
open System.Text.Json

  type JsonParseError =
  | Unknown
  | TypeError of string * JsonElement
  | PropNotFound of string * JsonElement
  | DateParseError of JsonElement

  with
    override this.ToString() =
      match this with
      | Unknown -> "Unknown Parse Error"
      | TypeError(desiredType,_) -> desiredType
      | PropNotFound(pname, _) -> sprintf "Property %s not found" pname
      | DateParseError _ -> "Value could not be parsed as a date/time" 

  exception JsonParseException of JsonParseError

  let private _typeErr jvk desiredType je =
    let msg = sprintf "Count not parse (%s) as %s" (string jvk) desiredType
    TypeError(msg, je)

  let getInt (je:JsonElement) =
    match je.TryGetInt32 () with
    | (true, n) ->  n
    | (false, _) -> 
      raise <| JsonParseException(_typeErr je.ValueKind "Integer" je)

  let getStr (je:JsonElement) =
    match je.ValueKind with 
    | JsonValueKind.String -> je.GetString()
    | jvk -> 
      raise <| JsonParseException(_typeErr jvk "String" je)

  let getStrOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.String -> je.GetString() |> Some
    | JsonValueKind.Null -> None
    | jvk -> 
      raise <| JsonParseException(_typeErr jvk "String Option" je)

  let getFloatOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.Number -> je.GetDouble() |> Some
    | JsonValueKind.Null -> None
    | jvk -> raise <| JsonParseException(_typeErr jvk "float option" je)

  let getFloat je =
    match getFloatOpt je with
    | Some f -> f
    | None -> raise <| JsonParseException(_typeErr je.ValueKind "float" je)

  let getDTOOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.String -> 
      match je.GetString() |> DateTimeOffset.TryParse with 
      | (true, dto) -> Some dto
      | (false, _) -> raise <| JsonParseException(JsonParseError.DateParseError je) 
    | JsonValueKind.Null -> None 
    | jvk -> raise <| JsonParseException(_typeErr jvk "date" je)

  let getDateTimeOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.String -> 
      match je.GetString() |> DateTime.TryParse with 
      | (true, dto) -> Some dto
      | (false, _) -> raise <| JsonParseException(JsonParseError.DateParseError je) 
    | JsonValueKind.Null -> None 
    | jvk -> raise <| JsonParseException(_typeErr jvk "date" je)

  let getDTO je =
    match getDTOOpt je with
    | Some d -> d
    | None -> raise <| JsonParseException(_typeErr je.ValueKind "date" je)

  let getDateTime je =
    match getDateTimeOpt je with
    | Some d -> d
    | None -> raise <| JsonParseException(_typeErr je.ValueKind "date" je)

  let getPropOpt (n:string) (je:JsonElement) =
    match je.TryGetProperty n with
    | (true, je') when je'.ValueKind <> JsonValueKind.Null -> Some je'
    | _ -> None 

  let getProp n je =
    match getPropOpt n je with 
    | Some je' -> je'
    | None  -> raise <| JsonParseException(PropNotFound(n, je))

  let getArray (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.Array -> je.EnumerateArray() |> Seq.cast<JsonElement>
    | JsonValueKind.Null -> Seq.empty
    | jvk -> raise <| JsonParseException(_typeErr jvk "array" je)

  let getPropInt n            = getProp n >> getInt
  let getPropStr n            = getProp n >> getStr
  let getPropStrOpt n je      = getPropOpt n je |> Option.bind getStrOpt
  let getPropFloatOpt n je    = getPropOpt n je |> Option.bind getFloatOpt
  let getPropDTO n            = getProp n >> getDTO
  let getPropDTOOpt n je      = getPropOpt n je |> Option.bind getDTOOpt
  let getPropDateTime n       = getProp n >> getDateTime
  let getPropDateTimeOpt n je = getPropOpt n je |> Option.bind getDateTimeOpt