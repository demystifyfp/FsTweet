module Chessie

open Chessie.ErrorHandling

let mapFailure f result = 
  let mapFirstItem xs = 
    List.head xs |> f |> List.singleton 
  mapFailure mapFirstItem result

let onSuccess f (x, _) = f x

let onFailure f xs = 
  xs |> List.head |> f 

let either onSuccessF onFailureF = 
  either (onSuccess onSuccessF) (onFailure onFailureF)

[<RequireQualifiedAccess>]
module AR =
  let mapFailure f aResult =
    aResult
    |> Async.ofAsyncResult 
    |> Async.map (mapFailure f) |> AR

  let catch aComputation =
    aComputation 
    |> Async.Catch
    |> Async.map ofChoice
    |> AR 