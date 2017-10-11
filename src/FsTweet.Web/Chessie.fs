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

let (|Success|Failure|) result = 
  match result with
  | Ok (x,_) -> Success x
  | Bad errs -> Failure (List.head errs)

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

  let fail x =
    x 
    |> fail 
    |> Async.singleton 
    |> AR

  let either onSuccess onFailure aResult = 
    aResult
    |> Async.ofAsyncResult
    |> Async.map (either onSuccess onFailure)