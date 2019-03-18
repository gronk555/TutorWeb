$(() => {
  var module = null;
  var reader = new FileReader();
  const MAX_FILE_SIZE = 2000000;

  function onDocumentReady() {
    module = $("table#module-table");
    if (!module.length) return false; // wrong page, dont set up other event handlers
    addPhraseTemplate();
    populateModule(moduleText); // moduleText defined in Edit.cshtml
    clearAllDirty();
    module
      .on("dragover", false)
      .on("dragenter", false)
      .on("drop", (e) => {
        e.stopPropagation();
        e.preventDefault();
        var file = e.originalEvent.dataTransfer.files[0];
        startReadingFile(file);
      });
    $("#editor_form").submit(event => {
      saveModuleText(); // on form submit, save editor text to cache just in case agent has not done it yet
    });
  }

  $(document)
    .ready(onDocumentReady)
    .on("change", "#tts", e => enableTTS(e))
    .on("click", "#save_as_file", () => downloadModuleText())
    .on('click', '#delete_all', () => clearModuleTable())
    .on("change", "#file-input", e => startReadingFile(e.target.files[0]))
    .on("change", "[name='NativeLang']", () => updateNativeLangInText())
    .on("change", "[name='ForeignLang']", () => updateForeignLangInText())
    .on('keydown', '[contenteditable]', e => {
      var text = getTextAroundCursor();
      var curRow = $(e.target.parentNode);
      switch (e.which) {
        case 13: { //enter
          shiftRowsDown(curRow)
          let nextRow = jumpNextLine(curRow);
          return false; // prevent standard behaviour
        }
        case 38: { //up
          var prevRow = jumpPrevLine(curRow);
          if (prevRow) setCursorAtPos(prevRow, text.before.length);
          return false; // prevent standard behaviour
        }
        case 40: { //down
          var nextRow = jumpNextLine(curRow);
          setCursorAtPos(nextRow, text.before.length);
          return false; // prevent standard behaviour
        }
        case 8: { //backspace
          if (text.before == "") { // if cursor is at start of row, then concat with previous
            var prevRow = jumpPrevLine(curRow); //focus on prev line
            if (prevRow) {
              appendTextToRow(prevRow, text.after);
              setCursorAtPos(prevRow, getText(prevRow).length);
              shiftRowsUp(prevRow.next());
            }
            return false; // prevent standard behaviour
          }
          else {
            document.execCommand("delete", false, null);
            return false;
            }
          break;
        }
        case 46: { //del
          if (text.after == "") { // if cursor is at the end of row, then concat with next
            var nextRow = curRow.next();
            appendTextToRow(curRow, getText(nextRow));
            setCursorAtPos(curRow, text.before.length);
            shiftRowsUp(nextRow);
            return false; // prevent standard behaviour
          }
          else {
            document.execCommand("forwardDelete", false, null);
            return false;
          }
          break;
        }
        case 17, 18: return true;
        default: {
          // TODO: bad attempt for undo/redo: selectAll is remembered in undo stack
          // see https://dev.to/chromiumdev/-native-undo--redo-for-the-web-3fl3
          onRowChanged(curRow); // mark as dirty
          if (e.ctrlKey || e.altKey) return true;
          let cleanTextBefore = cleanString(text.before);
          let cleanTextAfter = cleanString(text.after);
          curRow.children().last().focus();
          document.execCommand("selectAll");
          document.execCommand("insertText", false, cleanTextBefore + cleanTextAfter);
          setCursorAtPos(curRow, cleanTextBefore.length);
        }
      }
    })
    .on("click", "i.fa-play", e => {
      playSelected(e);
    });
  
  reader.onload = e => {
    populateModule(e.target.result);
    db();
  };

  var db = _.debounce(() => saveModuleText(), 3000);

  function onRowChanged(curRow) {
    curRow.addClass("dirty");
    db();
  }

  // save all edited rows to backend (after debounce interval on all events that modify module)
  function saveModuleText() {
    var totalRowCnt = module.find("tr").length;
    var dirtyRows = module
      .find("tr.dirty")
      .map((i, row) => {
        var text = getText($(row));
        return {
          iRow: row.rowIndex,
          value: text && text.trim(),
          langCode: $(row).children().first().text()
        };
      })
      .toArray();
    if (!dirtyRows.length) return;
    // TODO: if lang or any other module property is changed, pass it to param (only if we want to give up POST action on edit)
    var param = {
      ModuleId: moduleId,
      TotalRowCnt: totalRowCnt,
      DirtyRows: dirtyRows,
      EnableTTS: $("input#tts")[0].checked
    };
    //var strData = JSON.stringify(param);   //TODOTODO: if param object has strings with single quote, then stringify will not help, it will interfere with data: "{ 'param': '" + strData + "' }",
    //$.ajax({
    //  url: route,
    //  method: "POST",
    //  data: '{ "param": "' + strData + '" }',
    //  contentType: "application/json; charset=utf-8", //data param type
    //  dataType: "json" //return type
    //})
    //.done((data) => {      
    //  clearAllDirty();
    //})
    //.fail((err) => {});
    $.ajax({
      url: route, // hardcoded "/Module/SaveModuleText" will not work after publishing since it must be prepended with webAppName
      method: "POST",
      data: JSON.stringify(param),
      contentType: "application/json; charset=utf-8", //data param type
      dataType: "json" //return type
    })
    .done((data) => {
      clearAllDirty();
    })
    .fail((err) => {
      console.log(err)
    });
  }

  function populateModule(text) {
    debugger; 
    var lines = text.split(/\r\n|\n\r|\r|\n/g); // tolerate both Windows and Unix linebreaks

    for (var i = 0; i < lines.length; i++) {
      //append to the end of existing module text
      var curRow = i % 4 ? curRow.next() : addPhraseTemplate(); // by defaule all rows are Dirty
      setText(curRow, cleanString(lines[i]));
    }
  }

  function fileSize(b) {
    var u = 0, s=1024;
    while (b >= s || -b >= s) {
      b /= s;
      u++;
    }
    return (u ? b.toFixed(1) + ' ' : b) + ' KMGTPEZY'[u] + 'B';
  }

  function startReadingFile(file) {
    if (file.size > MAX_FILE_SIZE) {
      alert(`File is too big (${fileSize(file.size)}), max size is ${fileSize(MAX_FILE_SIZE)}.`);
      return;
    }
    if (file.type != "text/plain" && file.type != "text/html") {
      alert(`Invalid file format${file.type == "" ? "" : ` (${file.type})`}, must be text or html.`);
      return;
    }
    reader.readAsText(file);
  }

  function jumpPrevLine(curRow) {
    if (curRow.is(':first-child')) return null;
    var prevRow = curRow.prev();
    focusText(prevRow);
    return prevRow;
  }

  function jumpNextLine(curRow) {
    var nextRow = curRow.is(':last-child') ? curRow : curRow.next();
    focusText(nextRow);
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

  function shiftRowsDown(curRow) {
    addPhraseTemplate();
    var text = getTextAroundCursor();
    setText(curRow, text.before);
    var curText = text.after;
    var nextRow = curRow.next();
    while (nextRow.length) {
      var tmpText = getText(nextRow);
      setText(nextRow, curText);
      curText = tmpText;
      nextRow = nextRow.next();
    }
  }

  function shiftRowsUp(curRow) {
    if (!curRow.length) return;
    var nextRow = curRow.next();
    while (nextRow.length) {
      setText(curRow, getText(nextRow));
      curRow = nextRow;
      nextRow = nextRow.next();
    }
    setText(curRow, ""); // clean the last row
    removeIfEmpty(curRow);
  }

  function addPhraseTemplate() {
    var rowCnt = module.find('tr').length;
    var res = "";
    // if last 4 rows are not all empty, then append new phrase
    for (i = 1; i <= 4; i++) {
      var row = module.find('tr').eq(rowCnt - i);
      res += getText(row);
    }
    if (rowCnt == 0 || res != "") {
      var tmpl = $("#phrase-template").html();
      tmpl = tmpl.replace(/{{Model.NativeLang}}/g, nativeLang);
      tmpl = tmpl.replace(/{{Model.ForeignLang}}/g, foreignLang);
      module.append(tmpl);
      rowCnt += 4;
    }
    return module.find('tr').eq(rowCnt - 4); // 1st row of the last phrase
  }

  function clearModuleTable() {
    module.html("");
    addPhraseTemplate();
  }

  function updateNativeLangInText() {
    nativeLang = $("[name='NativeLang'] option:selected").val();
    module.find("tr:nth-child(2n+1)").each(function (i) {
      $(this).children().first().text(nativeLang);  // beware of this not working with =>
    });
    $("#phrase-template").find("tr:nth-child(2n+1)").each(function (i) {
      $(this).children().first().text(nativeLang);  // beware of this not working with =>
    });
  }

  function updateForeignLangInText() {
    foreignLang = $("[name='ForeignLang'] option:selected").val();
    module.find("tr:nth-child(4n+2)").each(function (i) {
      $(this).children().first().text(foreignLang);  // beware of this not working with =>
    });
    $("#phrase-template").find("tr:nth-child(4n+2)").each(function (i) {
      $(this).children().first().text(foreignLang);  // beware of this not working with =>
    });
  }

  function playSelected(e) {
    var text = e.target.parentElement.parentElement.lastElementChild.textContent;
    var lang = e.target.parentElement.parentElement.firstElementChild.textContent;
    if ($.trim(text).length == 0) return;
    var fname = "../../Content/TTS/" + lang + "/" + text + ".mp3";
    var audio = document.createElement('audio');
    audio.setAttribute('src', fname);
    audio.addEventListener("canplaythrough", () => {
      audio.play();
    });
  }

  function getTextAroundCursor() {
    var range = window.getSelection().getRangeAt(0);
    cursorIndex = range.startOffset;
    let text = range.startContainer.wholeText || range.startContainer.textContent;
    textBefore = text.substring(0, cursorIndex);
    textAfter = text.substring(cursorIndex);
    return { before: textBefore, after: textAfter, cursorPos: cursorIndex };
  }

  function removeIfEmpty(row) {
    var lastPhraseIndex = Math.floor(row[0].rowIndex / 4);
    if (!lastPhraseIndex) return; // alway leave one phrase, even empty
    var res = "";
    for (i = 0; i < 4; i++) {
      var row = module.find('tr').eq(lastPhraseIndex * 4 + i);
      res += getText(row);
    }
    if (res == "")
      for (i = 3; i >= 0; i--) {
        var row = module.find('tr').eq(lastPhraseIndex * 4 + i);
        if ($(document.activeElement)[0] == row.children().last()[0]) // if the row about to be deleted has focus, then move focus to the last remaining row
        {
          var lastRow = module.find('tr').eq(lastPhraseIndex * 4 - 1);
          setCursorAtPos(lastRow, getText(lastRow).length)
          focusText(lastRow);
        }
        row.remove();
      }
  }

  function enableTTS(e) {
    $(module[0].rows[0]).addClass("dirty"); // if there are no dirty rows we wont reach backend
    db();
  }

  function downloadModuleText() {
    if (!$("#Name").val()) {
      alert("Fill out the Name, please.");
      $("#Name").focus();
      return false;
    }
    var text = "";
    module.find("tr").each((i, row) => {
      text += getText($(row)) + "\r\n";
    });
    text = text.slice(0,-2); // remove last 2 chars
    var blob = new Blob([text], { "type": "text/plain" });
    $("#save_as_file")[0].download = $("#Name").val() + ".txt"; // get module name
    var url = URL.createObjectURL(blob);
    $("#save_as_file")[0].href = url;
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  function cleanString(s) {
    return !s ? "" : s.replace(/[~#%&*{}\\:<>?/+|\"”“,.…¿¡\-—_`!@$^()=\]\[;]/g, "");
  }

  function setText(row, txt, skipEvent) {
    row.children().last().text(txt);
    if (!skipEvent) onRowChanged(row);
  }

  function getText(row) {
    return row.children().last().text();
  }

  function focusText(row) {
    return row.children().last().focus();
  }

  function appendTextToRow(row, text) {
    var input = row.children().last();
    input.text(input.text() + text);
    onRowChanged(row);
  }

  function clearAllDirty(row) {
    module.find("tr.dirty").removeClass("dirty"); // clear dirty flag on all rows    
  }

});

