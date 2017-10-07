$(function(){
  $("#tweetForm").submit(function(event){
    event.preventDefault();

    $.ajax({
      url : "/tweets",
      type: "post",
      data: JSON.stringify({post : $("#tweet").val()}),
      dataType: "json",
      contentType: "application/json",
      success: function(){
        alert("successfully posted")
      }
    }).fail(function(data){
      console.log(data)
    })

  });

  $("textarea[maxlength]").on("propertychange input", function() {
    if (this.value.length > this.maxlength) {
        this.value = this.value.substring(0, this.maxlength);
    }  
  });

});