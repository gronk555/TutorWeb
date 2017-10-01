$(() => {
  $("i.fa-play").on("click", e => {
    var fname = "../../Sounds/" + e.target.parentElement.innerText + ".mp3";
    var audio = document.createElement('audio');
    audio.setAttribute('src', fname);

    audio.addEventListener("canplaythrough", () => {
      audio.play();
    });

  })
});
