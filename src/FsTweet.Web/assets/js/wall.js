$(function(){
  $("#tweetForm").submit(function(event){
    event.preventDefault();

    $.ajax({
      url : "/tweets",
      type: "post",
      data: JSON.stringify({post : $("#tweet").val()}),
      contentType: "application/json"
    }).done(function(){
      alert("successfully posted")
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });

  });

  $("textarea[maxlength]").on("propertychange input", function() {
    if (this.value.length > this.maxlength) {
        this.value = this.value.substring(0, this.maxlength);
    }  
  });

});