$(() => {
  var module = null;
  var reader = new FileReader();

  function onDocumentReady() {
    module = $("table#module-table")
    addPhraseTemplate();
  }

  $(document)
    .ready(onDocumentReady)
    .on("change", "#input", e => { startReadingFile(e); })
    .on("change", "[name='NativeLang']", () => { updateNativeLangInText(); })
    .on("change", "[name='ForeignLang']", () => { updateForeignLangInText(); })
    .on("input", '[contenteditable]', $.debounce(2000, () => { /*TODO: save edited line to backend*/ }))
    .on('paste', '[contenteditable]', e => {
      //strips html tags added to the editable tag when pasting
      var $self = $(e.target);
      setTimeout(() => { $self.html($self.text()); }, 0);
    })
    .on('keydown', '[contenteditable]', e => {
      var text = getTextAroundCursor();
      switch (e.which) {
        case 13: { //enter
          shiftRowsDown(e)
          jumpNextLine(e);
          return false; // prevent standart behaviour
        }
        case 38: { //up
          var prevRow = jumpPrevLine(e);
          if (prevRow) setCursorAtPos(prevRow, text.before.length);
          return false; // prevent standart behaviour
        }
        case 40: { //down
          var nextRow = jumpNextLine(e);
          setCursorAtPos(nextRow, text.before.length);
          return false; // prevent standart behaviour
        }
        case 8: { //backspace
          if (text.before == "") { // if cursor is at start of row, then concat with previous
            var prevRow = jumpPrevLine(e); //focus on prev line
            if (prevRow) {
              var prevRowLength = prevRow.children().last().text().length;
              appendTextToRow(prevRow, text.after);
              setCursorAtPos(prevRow, prevRowLength);
              shiftRowsUp(prevRow.next());
            }
            return false; // prevent standart behaviour
          }
          break;
        }
        case 46: { //del
          if (text.after == "") { // if cursor is at the end of row, then concat with next
            var curRow = $(e.target.parentNode);
            var nextRow = curRow.next();
            var nextRowText = nextRow.children().last().text();
            appendTextToRow(curRow, nextRowText);
            setCursorAtPos(curRow, text.before.length);
            shiftRowsUp(nextRow);
            return false; // prevent standart behaviour
          }
          break;
        }
      }
    })
    .on("click", "i.fa-play", e => {
      playSelected(e);
    })

  reader.onload = e => {
    var lines = e.target.result.split(/[\r\n]+/g); // tolerate both Windows and Unix linebreaks
    for (var i = 0; i < lines.length; i++) {
      var curRow = i % 4 ? curRow.next() : addPhraseTemplate();
      curRow.children().last().text(lines[i]);
    }
  };

  function startReadingFile(e) {
    debugger;
    var file = e.target.files[0];
    reader.readAsText(file);
  }

  function jumpPrevLine(e) {
    var curRow = $(e.target.parentNode);
    if (curRow.is(':first-child')) return null;
    var prevRow = curRow.prev();
    prevRow.children().last().focus();
    return prevRow;
  }

  function jumpNextLine(e) {
    var curRow = $(e.target.parentNode);
    var nextRow = curRow.is(':last-child') ? curRow : curRow.next();
    nextRow.children().last().focus();
    return nextRow;
  }

  // only for contenteditable elements
  function setCursorAtPos(row, pos) {
    var input = row.children().last();
    if (!input.text()) return;
    var textNode = input[0].firstChild;
    pos = Math.min(pos, textNode.length);
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
    addPhraseTemplate();
    var text = getTextAroundCursor();
    var prevText = text.before;
    var curText = text.after;
    var nextRow = $(e.target.parentNode).next();
    $(e.target).text(prevText);
    while (nextRow.length) {
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
    var rowCnt = module.find('tr').length;
    var res = "";
    // if last 4 rows are not all empty, then append new phrase
    for (i = 1; i <= 4; i++) {
      var row = module.find('tr').eq(rowCnt - i);
      res += row.children().last().text();
    }
    if (rowCnt == 0 || res != "") {
      module.append($("#phrase-template").html());
      rowCnt += 4;
    }
    return module.find('tr').eq(rowCnt - 4); // 1st row of the last phrase
  }

  function clearModuleTable() {
    module.html("");
  }

  function updateNativeLangInText() {
    var langCode = $("[name='NativeLang'] option:selected").val();
    module.find("tr:nth-child(2n+1)").each(function (i) {
      $(this).children().first().text(langCode);  // beware of this not working with =>
    });
  }

  function updateForeignLangInText() {
    var langCode = $("[name='ForeignLang'] option:selected").val();
    module.find("tr:nth-child(4n+2)").each(function (i) {
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
    var lastPhraseIndex = Math.floor(row[0].rowIndex / 4);
    if (!lastPhraseIndex) return; // alway leave one phrase, even empty
    var res = "";
    for (i = 0; i < 4; i++) {
      var row = module.find('tr').eq(lastPhraseIndex * 4 + i);
      res += row.children().last().text();
    }
    if (res == "")
      for (i = 3; i >= 0; i--) {
        var row = module.find('tr').eq(lastPhraseIndex * 4 + i);
        if ($(document.activeElement)[0] == row.children().last()[0]) // if the row about to be deleted has focus, then move focus to the last remaining row
        {
          var lastRow = module.find('tr').eq(lastPhraseIndex * 4 - 1);
          var lastElemTextLength = lastRow.children().last().text().length;
          setCursorAtPos(lastRow, lastElemTextLength)
          lastRow.children().last().focus();
        }
        row.remove();
      }
  }

});

