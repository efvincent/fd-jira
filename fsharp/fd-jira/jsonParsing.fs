module efvJson 
open System
open System.Text.Json

  type JsonParseError =
  | Unknown
  | TypeError of string * JsonElement
  | PropNotFound of string * JsonElement

  with
    override this.ToString() =
      match this with
      | Unknown -> "Unknown Parse Error"
      | TypeError(desiredType,_) -> desiredType
      | PropNotFound(pname, _) -> sprintf "Property %s not found" pname

  exception JsonParseExn of JsonParseError

  let private _typeErr jvk desiredType je =
    let msg = sprintf "Count not parse (%s) as %s" (string jvk) desiredType
    TypeError(msg, je)

  let getInt (je:JsonElement) =
    match je.TryGetInt32 () with
    | (true, n) ->  n
    | (false, _) -> 
      raise <| JsonParseExn(_typeErr je.ValueKind "Integer" je)

  let getStr (je:JsonElement) =
    match je.ValueKind with 
    | JsonValueKind.String -> je.GetString()
    | jvk -> 
      raise <| JsonParseExn(_typeErr jvk "String" je)

  let getStrOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.String -> je.GetString() |> Some
    | JsonValueKind.Null -> None
    | jvk -> 
      raise <| JsonParseExn(_typeErr jvk "String Option" je)

  let getFloatOpt (je:JsonElement) =
    match je.ValueKind with
    | JsonValueKind.Number -> je.GetDouble() |> Some
    | JsonValueKind.Null -> None
    | jvk -> raise <| JsonParseExn(_typeErr jvk "float option" je)

  let getFloat (je:JsonElement) =
    match getFloatOpt je with
    | Some f -> f
    | None -> raise <| JsonParseExn(_typeErr je.ValueKind "float" je)

  let getProp (n:string) (je:JsonElement) =
    match je.TryGetProperty n with
    | (true, je') -> je'
    | (false, _)  -> raise <| JsonParseExn(PropNotFound(n, je))

  let getPropInt n      = (getProp n) >> getInt
  let getPropStr n      = (getProp n) >> getStr
  let getPropStrOpt n   = (getProp n) >> getStrOpt
  let getPropFloatOpt n = (getProp n) >> getFloatOpt
  