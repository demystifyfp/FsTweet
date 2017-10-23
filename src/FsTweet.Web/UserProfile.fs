namespace UserProfile

module Suave =
  open Database
  open Suave.Filters
  open Auth.Suave
  open Suave

  let renderUserProfile username loggedInUser  ctx = async {
    match loggedInUser with
    | None -> 
      return! Successful.OK username ctx
    | Some _  -> 
      return! Successful.OK "TODO" ctx
  }


  let webpart (_ : GetDataContext) = 
    pathScan "/%s" (fun username -> mayRequiresAuth (renderUserProfile username))
    