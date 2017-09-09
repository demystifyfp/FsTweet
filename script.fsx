#r "./packages/email/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
open Newtonsoft.Json

let placeHolders = 
  Map.empty
    .Add("verification_code", "12323")

JsonConvert.SerializeObject(placeHolders)
