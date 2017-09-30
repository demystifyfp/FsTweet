namespace Wall

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open User
  open Auth.Suave
  let renderWall (user : User) = 
    Successful.OK user.Username.Value
  
  let webpart () =
    path "/wall" >=> user renderWall