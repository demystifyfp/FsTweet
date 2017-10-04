namespace Wall

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open User
  open Auth.Suave

  let renderWall (user : User) ctx = async {
    return! Successful.OK user.Username.Value ctx
  }
  
  let webpart () =
    path "/wall" >=> requiresAuth renderWall