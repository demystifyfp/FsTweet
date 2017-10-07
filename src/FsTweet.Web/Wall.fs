namespace Wall

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open User
  open Auth.Suave
  open Suave.DotLiquid

  type WallViewModel = {
    Username :  string
  }

  let renderWall (user : User) ctx = async {
    let vm = {Username = user.Username.Value }
    return! page "user/wall.liquid" vm ctx
  }
  
  let webpart () =
    path "/wall" >=> requiresAuth renderWall