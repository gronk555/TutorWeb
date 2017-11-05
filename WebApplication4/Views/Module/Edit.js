$(() => {
  addPhraseTemplate();

  $(document)
    .on("change", "[name='NativeLang']", () => { updateNativeLangInText(); })
    .on("change", "[name='ForeignLang']", () => { updateForeignLangInText(); })
    .on("input", '[contenteditable]', $.debounce(2000, () => { /*TODO: save edited line to backend*/ }))
    .on('paste', '[contenteditable]', e => {
      //strips html tags added to the editable tag when pasting
      var $self = $(e.target);
      setTimeout(() => { $self.html($self.text()); }, 0);
    })
    .on('keydown', '[contenteditable]', e => {
      switch (e.which) {
        case 13: { //enter
          //if ($(e.target.parentNode).is(":last-child"))
          //  addPhraseTemplate();
          // move all following lines of text down 1 line, if necessary add 4 new lines to the end for a new phrase
          shiftRowsDown(e)
          jumpNextLine(e);
          //form ignores enter key
          return false;
        }
        case 38: { //up
          jumpPrevLine(e);
          break;
        }
        case 40: { //down
          jumpNextLine(e);
          break;
        }
        case 8: { //backspace
          debugger;
          var text = getTextAroundCursor();
          if (text.before == "") { // concat with previous
            var prevRow = jumpPrevLine(e); //focus on prev line
            var prevRowLength = prevRow.children().last()[0].firstChild.length;
            appendTextToRow(prevRow, text.after);
            setCursorAtPos(prevRow, prevRowLength);

            shiftRowsUp(prevRow.next());

            return false; // prevent standard behaviour from deleting a char
          }
          break;
        }
        case 46: { //del
          // TODO: concat with next if at the end
          break;
        }
      }
    })
    .on("click", "i.fa-play", e => {
      playSelected(e);
    })

});

function jumpPrevLine(e) {
  var prevRow = $(e.target.parentNode).prev();
  prevRow.children().last().focus();
  return prevRow;
}

function jumpNextLine(e)
{
  var nextRow = $(e.target.parentNode).next();
  nextRow.children().last().focus();
  return nextRow;
}

// only for contenteditable elements
function setCursorAtPos(row, pos) {
  var input = row.children().last();
  var textNode = input[0].firstChild;
  var range = document.createRange();
  range.setStart(textNode, pos);
  range.setEnd(textNode, pos);
  var sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
}

function appendTextToRow(row, text) {
  var input = row.children().last();
  input.text(input.text() + text);
}

function shiftRowsDown(e) {
  debugger;
  //TODO: if last 4 rows are not all empty
  addPhraseTemplate();
  var text = getTextAroundCursor();
  var prevText = text.before;
  var curText = text.after;
  var nextRow = $(e.target.parentNode).next();
  $(e.target).text(prevText);
  while (nextRow.length == 1) { // TODO: == 1?
    var tmpText = nextRow.children().last().text();
    nextRow.children().last().text(curText);
    curText = tmpText;
    nextRow = nextRow.next();
  }
}

function shiftRowsUp(curRow) {
  if (!curRow.length) return;
  debugger;
  var nextRow = curRow.next();
  while (nextRow.length) {
    curRow.children().last().text(nextRow.children().last().text());
    curRow = nextRow;
    nextRow = nextRow.next();
  }
  curRow.children().last().text(""); // clean the last row
  removeIfEmpty(curRow);
}

function addPhraseTemplate() {
  var rowCnt = $('table#module-table tr').length;
  var res = "";
  for (i = 1; i <= 4; i++) {
    var row = $("table#module-table").find('tr').eq(rowCnt - i);
    res += row.children().last().text();
  }
  if (rowCnt == 0 || res != "")
    $("table#module-table").append($("#phrase-template").html());
}

function clearModuleTable() {
  $("table#module-table").html("");
}

function updateNativeLangInText() {
  var langCode = $("[name='NativeLang'] option:selected").val();
  $("table#module-table tr:nth-child(2n+1)").each(function(i)  {
    $(this).children().first().text(langCode);  // beware of this not working with =>
  });
}

function updateForeignLangInText() {
  var langCode = $("[name='ForeignLang'] option:selected").val();
  $("table#module-table tr:nth-child(4n+2)").each(function (i) {
    $(this).children().first().text(langCode);  // beware of this not working with =>
  });
}

function playSelected(e) {
  alert(e);
  var sel = window.getSelection();
  var range = sel.getRangeAt(0);
  var pointedTag = range.startContainer.parentNode;

  var fname = "../../Sounds/" + e.target.parentElement.innerText + ".mp3";
  var audio = document.createElement('audio');
  audio.setAttribute('src', fname);

  audio.addEventListener("canplaythrough", () => {
    audio.play();
  });
}

function getTextAroundCursor() {
  var range = window.getSelection().getRangeAt(0);
  cursorIndex = range.startOffset;
  textBefore = range.startContainer.textContent.substring(0, cursorIndex);
  textAfter = range.startContainer.textContent.substring(cursorIndex);
  return { before: textBefore, after: textAfter };
}

function removeIfEmpty(row) {
  var phraseIndex = Math.floor(row[0].rowIndex / 4);
  var res = "";
  for (i = 0; i < 4; i++) {
    var row = $("table#module-table").find('tr').eq(phraseIndex * 4 + i);
    res += row.children().last().text();
  }
  if (res == "")
    for (i = 3; i >= 0; i--) {
      var row = $("table#module-table").find('tr').eq(phraseIndex * 4 + i);
      row.remove();
    }
}