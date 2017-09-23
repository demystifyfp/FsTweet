namespace Auth

module Suave =
  open Suave.Filters
  open Suave.Operators
  open Suave.DotLiquid

  type LoginViewModel = {
    Username : string
    Password : string
    Error : string option
  }

  let emptyLoginViewModel = {
    Username = ""
    Password = ""
    Error = None
  }

  let renderLoginPage viewModel = 
    page "guest/login.liquid" viewModel
  let webpart () =
    path "/login" >=> renderLoginPage emptyLoginViewModel