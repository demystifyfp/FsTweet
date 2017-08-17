module FsTweet.View

open System.IO
open System.Reflection

let viewsDir = 
  let currentPath =
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
  Path.Combine(currentPath, "views")