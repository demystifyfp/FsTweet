$(function(){
  $("#follow").on('click', function(){
    var $this = $(this);
    var userId = $this.data('user-id');
    $this.prop('disabled', true);
    $.ajax({
      url : "/follow",
      type: "post",
      data: JSON.stringify({userId : userId}),
      contentType: "application/json"
    }).done(function(){
      alert("successfully followed");
      $this.prop('disabled', false);
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });
  });
});