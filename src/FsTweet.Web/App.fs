module FsTweet.SuaveApp

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.DotLiquid

let guestHomePage = "guest/home.liquid"

let init () = 
  choose [
    path "/" >=> page guestHomePage ""
  ]
     